using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using CommonUsages = UnityEngine.XR.CommonUsages; // InputSystem has its own CommonUsages too

namespace SportsTour
{
    // Head tracking + pointing for Quest 3 (OpenXR), with fallbacks:
    //  - controllers: laser + trigger on a button = select, hold trigger anywhere else = walk, A/X = menu button
    //  - no controllers (hand tracking): look at an item for 1.4s to select
    //  - Unity Editor without headset: drag mouse to look, click to select
    public class VRRig : MonoBehaviour
    {
        public Camera head;
        public Material laserMaterial;
        public float eyeHeightWithoutHeadset = 1.6f;

        public event Action MenuButtonPressed;
        // 0 = standing still, 1 = full speed. Trigger pressure (or W / Space / Up in the Editor).
        public float WalkInput { get; private set; }
        public bool XRActive => XRSettings.isDeviceActive;
        public Vector3 HeadPosition => head.transform.position;
        public float HeadYaw => Mathf.Atan2(head.transform.forward.x, head.transform.forward.z) * Mathf.Rad2Deg;
        public float HeadPitch => Mathf.Asin(Mathf.Clamp(head.transform.forward.y, -1f, 1f)) * Mathf.Rad2Deg;

        static readonly InputFeatureUsage<Vector3> PointerPosition = new InputFeatureUsage<Vector3>("PointerPosition");
        static readonly InputFeatureUsage<Quaternion> PointerRotation = new InputFeatureUsage<Quaternion>("PointerRotation");
        const float RayLength = 30f, GazeTime = 1.4f;

        class Pointer
        {
            public XRNode node;
            public LineRenderer line;
            public Transform dot;
            public Clickable hover;
            public bool prevTrigger, prevPrimary, walking;
        }

        Pointer[] hands;
        Clickable gazeHover, mouseHover;
        float gazeStart;
        Transform gazeRing;
        bool originSet;
        float mouseYaw, mousePitch;
        Vector2 mouseDownPos;
        bool mouseDragging;
        float walk;

        void Start()
        {
            hands = new[] { MakePointer(XRNode.RightHand), MakePointer(XRNode.LeftHand) };
            gazeRing = UIFactory.Shape("GazeRing", head.transform, UIFactory.Ring(0.012f, 0.018f), new Color(1, 1, 1, 0.85f),
                new Vector3(0, 0, 1f), 20).transform;
            Application.onBeforeRender += UpdateHead;
        }

        void OnDestroy() => Application.onBeforeRender -= UpdateHead;

        Pointer MakePointer(XRNode node)
        {
            var go = new GameObject(node + " Laser");
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.widthMultiplier = 0.004f;
            line.positionCount = 2;
            line.sharedMaterial = laserMaterial;
            line.startColor = new Color(0.66f, 0.61f, 1f, 0.9f);
            line.endColor = new Color(0.66f, 0.61f, 1f, 0.1f);
            line.enabled = false;
            var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(dot.GetComponent<Collider>());
            dot.transform.SetParent(transform, false);
            dot.transform.localScale = Vector3.one * 0.03f;
            dot.GetComponent<Renderer>().sharedMaterial = new Material(laserMaterial) { color = Color.white };
            dot.SetActive(false);
            return new Pointer { node = node, line = line, dot = dot.transform };
        }

        void Update()
        {
            if (XRActive && !originSet) TrySetFloorOrigin();
            UpdateHead();

            walk = 0;
            bool anyController = false;
            foreach (var p in hands) anyController |= UpdateController(p);

            bool useGaze = XRActive && !anyController;
            UpdateGaze(useGaze);
            if (!XRActive) UpdateMouse();
            if (KeyboardWalk()) walk = 1;
            WalkInput = walk;
        }

        static bool KeyboardWalk()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            return kb != null && (kb.wKey.isPressed || kb.spaceKey.isPressed || kb.upArrowKey.isPressed);
#else
            return Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.UpArrow);
#endif
        }

        void TrySetFloorOrigin()
        {
            var subs = new List<XRInputSubsystem>();
            SubsystemManager.GetSubsystems(subs);
            foreach (var s in subs)
                if (s.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor)) originSet = true;
        }

        void UpdateHead()
        {
            if (!XRActive)
            {
                head.transform.localPosition = new Vector3(0, eyeHeightWithoutHeadset, 0);
                head.transform.localRotation = Quaternion.Euler(mousePitch, mouseYaw, 0);
                return;
            }
            var d = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
            if (!d.isValid) d = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            if (d.TryGetFeatureValue(CommonUsages.devicePosition, out var pos)) head.transform.localPosition = pos;
            if (d.TryGetFeatureValue(CommonUsages.deviceRotation, out var rot)) head.transform.localRotation = rot;
        }

        bool UpdateController(Pointer p)
        {
            var d = InputDevices.GetDeviceAtXRNode(p.node);
            Vector3 pos = default;
            Quaternion rot = default;
            bool tracked = XRActive && d.isValid && d.TryGetFeatureValue(CommonUsages.isTracked, out var t) && t;
            // prefer the OpenXR "aim" pose (points where the controller points), fall back to the grip pose
            if (tracked && !(d.TryGetFeatureValue(PointerPosition, out pos) && d.TryGetFeatureValue(PointerRotation, out rot)))
                tracked = d.TryGetFeatureValue(CommonUsages.devicePosition, out pos) & d.TryGetFeatureValue(CommonUsages.deviceRotation, out rot);

            if (!tracked)
            {
                p.line.enabled = false;
                p.dot.gameObject.SetActive(false);
                SetHover(ref p.hover, null);
                p.walking = false;
                return false;
            }

            var origin = transform.TransformPoint(pos);
            var dir = transform.rotation * rot * Vector3.forward;
            var hit = Cast(new Ray(origin, dir), out var end);
            SetHover(ref p.hover, hit);
            p.line.enabled = true;
            p.line.SetPosition(0, origin);
            p.line.SetPosition(1, end);
            p.dot.gameObject.SetActive(hit != null);
            p.dot.position = end;

            d.TryGetFeatureValue(CommonUsages.triggerButton, out var trigger);
            d.TryGetFeatureValue(CommonUsages.trigger, out var pressure);
            d.TryGetFeatureValue(CommonUsages.primaryButton, out var primary);
            // trigger pressed on a button = click; pressed anywhere else = walk while held, pressure = speed
            if (trigger && !p.prevTrigger)
            {
                if (p.hover != null) p.hover.Click();
                else p.walking = true;
            }
            if (!trigger) p.walking = false;
            if (p.walking) walk = Mathf.Max(walk, pressure);
            if (primary && !p.prevPrimary) MenuButtonPressed?.Invoke();
            p.prevTrigger = trigger;
            p.prevPrimary = primary;
            return true;
        }

        void UpdateGaze(bool on)
        {
            gazeRing.gameObject.SetActive(on);
            if (!on) { SetHover(ref gazeHover, null); return; }
            var hit = Cast(new Ray(head.transform.position, head.transform.forward), out _);
            if (hit != gazeHover) gazeStart = Time.time;
            SetHover(ref gazeHover, hit);
            float k = hit == null ? 0 : Mathf.Clamp01((Time.time - gazeStart) / GazeTime);
            gazeRing.localScale = Vector3.one * Mathf.Lerp(1f, 0.3f, k);
            if (hit != null && k >= 1f)
            {
                hit.Click();
                gazeStart = float.MaxValue; // one click per look
            }
        }

        void UpdateMouse()
        {
#if ENABLE_INPUT_SYSTEM
            var m = Mouse.current;
            if (m == null) return;
            Vector2 mp = m.position.ReadValue();
            if (m.leftButton.wasPressedThisFrame) { mouseDownPos = mp; mouseDragging = false; }
            if (m.leftButton.isPressed)
            {
                var delta = m.delta.ReadValue();
                if ((mp - mouseDownPos).sqrMagnitude > 36) mouseDragging = true;
                if (mouseDragging)
                {
                    // drag the world, like the web version
                    mouseYaw -= delta.x * 0.15f;
                    mousePitch = Mathf.Clamp(mousePitch + delta.y * 0.15f, -85f, 85f);
                }
            }
            var hit = Cast(head.ScreenPointToRay(mp), out _);
            SetHover(ref mouseHover, hit);
            if (m.leftButton.wasReleasedThisFrame && !mouseDragging && hit != null) hit.Click();
#else
            Vector2 mp = Input.mousePosition;
            if (Input.GetMouseButtonDown(0)) { mouseDownPos = mp; mouseDragging = false; }
            if (Input.GetMouseButton(0))
            {
                if ((mp - mouseDownPos).sqrMagnitude > 36) mouseDragging = true;
                if (mouseDragging)
                {
                    mouseYaw -= Input.GetAxis("Mouse X") * 3f;
                    mousePitch = Mathf.Clamp(mousePitch + Input.GetAxis("Mouse Y") * 3f, -85f, 85f);
                }
            }
            var hit = Cast(head.ScreenPointToRay(mp), out _);
            SetHover(ref mouseHover, hit);
            if (Input.GetMouseButtonUp(0) && !mouseDragging && hit != null) hit.Click();
#endif
        }

        static Clickable Cast(Ray ray, out Vector3 end)
        {
            end = ray.origin + ray.direction * RayLength;
            if (!Physics.Raycast(ray, out var hit, RayLength)) return null;
            end = hit.point;
            var c = hit.collider.GetComponentInParent<Clickable>();
            return c != null && c.isActiveAndEnabled ? c : null;
        }

        static void SetHover(ref Clickable current, Clickable next)
        {
            if (current != null && !current.isActiveAndEnabled) current = null; // hidden since last frame
            if (current == next) return;
            if (current != null) current.SetHover(false);
            current = next;
            if (current != null) current.SetHover(true);
        }
    }
}
