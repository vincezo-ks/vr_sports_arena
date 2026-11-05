using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Video;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SportsTour
{
    // The whole tour: 360 video on the skybox (only moves while the user holds the trigger), floating section menu, "YOU ARE AT" label,
    // hotspot arrows inside each video, look-down-to-open menu, and background music.
    public class TourManager : MonoBehaviour
    {
        [Header("Tour")]
        public string tourTitle = "College Sports Complex";
        public List<TourSection> sections = TourSection.Defaults();

        [Header("References (filled by Sports Tour > Build Scene)")]
        public VRRig rig;
        public VideoPlayer video;
        public AudioSource music;
        public Material skybox;
        public Material uiMaterial;
        public Font font;

        [Header("Menu")]
        [Tooltip("Look this far down (degrees) to start opening the menu")]
        public float lookDownAngle = 40f;
        public float lookDownSeconds = 0.8f;

        [Header("Walking")]
        [Tooltip("Video speed with a light trigger press (full press = 1)")]
        public float slowestWalk = 0.35f;
        [Tooltip("How dark the edges get while walking (reduces motion sickness). 0 = off")]
        [Range(0, 1)] public float comfortVignette = 0.85f;

        static readonly Color RowColor = new Color32(0x1d, 0x17, 0x40, 0xf2);
        static readonly Color RowCurrent = new Color32(0x3a, 0x2f, 0x80, 0xf2);
        static readonly Color PanelColor = new Color32(0x0d, 0x0a, 0x1f, 0xd1);
        static readonly Color Accent = new Color32(0x7c, 0x5c, 0xff, 0xe6);
        static readonly Color Lilac = new Color32(0xa9, 0x9c, 0xff, 0xff);

        int current = -1;
        bool menuOpen, lookDownArmed = true;
        float downSince = -1;
        Transform menu, pad, hotspotRoot, label, padProgress;
        TextMesh padText, labelText, musicText, hintText;
        MeshRenderer vignette;
        float vignetteAlpha;
        bool reachedEnd;
        MeshRenderer labelBar;
        readonly List<MeshRenderer> rows = new List<MeshRenderer>();
        readonly List<Transform> rings = new List<Transform>();
        float labelPop = 1f;

        void Start()
        {
            UIFactory.baseMaterial = uiMaterial;
            UIFactory.font = font;
            RenderSettings.skybox = skybox;

            // the video is a walk: it only plays while the user holds the trigger, and stops at the end
            video.isLooping = false;
            video.prepareCompleted += v => v.Play(); // shows the first frame; Update pauses it right away
            video.loopPointReached += v =>
            {
                reachedEnd = true;
                v.Pause();
                hintText.text = "END - PICK AN ARROW";
            };
            video.errorReceived += (v, msg) => labelText.text = "Video missing: " + sections[current].videoFile;

            BuildMenu();
            BuildPad();
            BuildLabel();
            BuildVignette();
            hotspotRoot = new GameObject("Hotspots").transform;
            rig.MenuButtonPressed += () => SetMenu(!menuOpen);

            if (music != null && music.clip != null) { music.loop = true; music.Play(); }

            GoTo(0);
            SetMenu(false);
            StartCoroutine(OpenMenuSoon());
        }

        // wait for the headset pose so the menu appears in front of the user, not at the floor origin
        System.Collections.IEnumerator OpenMenuSoon()
        {
            yield return new WaitForSeconds(0.6f);
            SetMenu(true);
        }

        // ---------- menu panel ----------
        const float RowH = 0.16f, Gap = 0.03f, W = 1.3f;

        void BuildMenu()
        {
            menu = new GameObject("Menu").transform;
            float totalH = (sections.Count + 1) * (RowH + Gap) + 0.36f;
            float RowY(int i) => totalH / 2 - 0.26f - i * (RowH + Gap);

            UIFactory.Shape("Panel", menu, UIFactory.Quad(W + 0.12f, totalH), PanelColor, Vector3.zero, 0);
            UIFactory.Text(menu, tourTitle.ToUpper(), 0.05f, Lilac, new Vector3(0, totalH / 2 - 0.1f, 0));

            for (int i = 0; i < sections.Count; i++)
            {
                int idx = i;
                var s = sections[i];
                var btn = UIFactory.Button("Row " + s.name, menu, W, RowH, new Vector3(0, RowY(i), -0.01f));
                btn.hoverScale = 1.04f;
                var bg = UIFactory.Shape("Bg", btn.transform, UIFactory.Quad(W, RowH), RowColor, Vector3.zero, 1);
                UIFactory.Shape("Dot", btn.transform, UIFactory.Circle(0.025f), s.color, new Vector3(-W / 2 + 0.07f, 0, -0.001f), 2);
                UIFactory.Text(btn.transform, $"{i + 1}.  {s.name}", 0.065f, Color.white, new Vector3(-W / 2 + 0.13f, 0, 0), TextAnchor.MiddleLeft);
                btn.onHover = on => UIFactory.SetColor(bg, on ? s.color : idx == current ? RowCurrent : RowColor);
                btn.onClick = () => { GoTo(idx); SetMenu(false); };
                rows.Add(bg);
            }

            float y = RowY(sections.Count) - 0.02f;
            musicText = MenuButton("MUSIC: ON", new Vector3(-W / 4, y, -0.01f), new Color32(0x2a, 0x22, 0x57, 0xf2), ToggleMusic);
            MenuButton("CLOSE", new Vector3(W / 4, y, -0.01f), new Color32(0x5a, 0x3f, 0xd6, 0xf2), () => SetMenu(false));
        }

        TextMesh MenuButton(string text, Vector3 pos, Color color, System.Action onClick)
        {
            float w = W / 2 - 0.02f;
            var btn = UIFactory.Button(text, menu, w, RowH, pos);
            var bg = UIFactory.Shape("Bg", btn.transform, UIFactory.Quad(w, RowH), color, Vector3.zero, 1);
            btn.onHover = on => UIFactory.SetColor(bg, on ? Color.Lerp(color, Color.white, 0.25f) : color);
            btn.onClick = onClick;
            return UIFactory.Text(btn.transform, text, 0.06f, Color.white, Vector3.zero);
        }

        // ---------- MENU pad at your feet: look down at it to open ----------
        void BuildPad()
        {
            var btn = UIFactory.Button("Menu Pad", null, 0.56f, 0.2f, Vector3.zero);
            btn.hoverScale = 1.1f;
            btn.onClick = () => SetMenu(!menuOpen);
            pad = btn.transform;
            UIFactory.Shape("Bg", pad, UIFactory.Quad(0.56f, 0.2f), Accent, Vector3.zero, 1);
            padText = UIFactory.Text(pad, "MENU", 0.07f, Color.white, new Vector3(0, 0.02f, 0));
            padProgress = new GameObject("Progress").transform; // pivot on the left so it fills left→right
            padProgress.SetParent(pad, false);
            padProgress.localPosition = new Vector3(-0.25f, -0.065f, -0.002f);
            UIFactory.Shape("Fill", padProgress, UIFactory.Quad(0.5f, 0.025f), Color.white, new Vector3(0.25f, 0, 0), 3);
            padProgress.localScale = new Vector3(0.001f, 1, 1);
        }

        // ---------- head-locked "YOU ARE AT" label ----------
        void BuildLabel()
        {
            label = new GameObject("Section Label").transform;
            label.SetParent(rig.head.transform, false);
            label.localPosition = new Vector3(0, 0.36f, 1.3f);
            UIFactory.Shape("Bg", label, UIFactory.Quad(1.0f, 0.14f), new Color32(0x0d, 0x0a, 0x1f, 0xc7), Vector3.zero, 10);
            labelBar = UIFactory.Shape("Bar", label, UIFactory.Quad(0.012f, 0.1f), Accent, new Vector3(-0.47f, 0, -0.002f), 11);
            UIFactory.Text(label, "YOU ARE AT", 0.025f, Lilac, new Vector3(-0.44f, 0.035f, 0), TextAnchor.MiddleLeft, 12);
            labelText = UIFactory.Text(label, "", 0.055f, Color.white, new Vector3(-0.44f, -0.018f, 0), TextAnchor.MiddleLeft, 12);
            hintText = UIFactory.Text(label, "", 0.025f, Lilac, new Vector3(0.47f, 0.035f, 0), TextAnchor.MiddleRight, 12);
        }

        // dark edges while walking: a head-locked quad with a clear circle in the middle
        void BuildVignette()
        {
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float r = new Vector2((x + 0.5f) / n - 0.5f, (y + 0.5f) / n - 0.5f).magnitude;
                px[y * n + x] = new Color32(255, 255, 255, (byte)(255 * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.18f, 0.36f, r))));
            }
            tex.SetPixels32(px);
            tex.Apply();
            vignette = UIFactory.Shape("Comfort Vignette", rig.head.transform, UIFactory.Quad(1.4f, 1.4f), Color.clear, new Vector3(0, 0, 0.3f), 50);
            vignette.sharedMaterial.mainTexture = tex;
            vignette.enabled = false;
        }

        void SetMenu(bool open)
        {
            menuOpen = open;
            menu.gameObject.SetActive(open);
            hotspotRoot.gameObject.SetActive(!open);
            label.gameObject.SetActive(!open); // head-locked label would cover the first menu row
            padText.text = open ? "CLOSE" : "MENU";
            if (open)
            {
                lookDownArmed = false;
                PlaceInFront(menu, 2.0f, -0.2f);
            }
        }

        // put something `dist` metres ahead (ignoring head pitch), `dy` above eye level, facing the viewer
        void PlaceInFront(Transform t, float dist, float dy)
        {
            var eye = rig.HeadPosition;
            var flat = Quaternion.Euler(0, rig.HeadYaw, 0) * Vector3.forward;
            t.position = eye + flat * dist + Vector3.up * dy;
            t.rotation = Quaternion.LookRotation(t.position - eye);
        }

        // ---------- hotspot arrows ----------
        void BuildHotspots(TourSection s)
        {
            foreach (Transform c in hotspotRoot) Destroy(c.gameObject);
            rings.Clear();

            foreach (var link in s.links)
            {
                int ti = sections.FindIndex(x => x.id == link.to);
                if (ti < 0) { Debug.LogWarning($"Hotspot in '{s.id}' points to unknown section '{link.to}'"); continue; }
                var target = sections[ti];
                var dir = Quaternion.Euler(-link.pitch, link.yaw, 0) * Vector3.forward;

                var spot = new GameObject("Hotspot → " + target.name).transform;
                spot.SetParent(hotspotRoot, false);
                spot.localPosition = dir * 5f;
                spot.rotation = Quaternion.LookRotation(dir);

                var btn = UIFactory.Button("Body", spot, 1.0f, 1.0f, Vector3.zero);
                btn.hoverScale = 1.18f;
                btn.onClick = () => GoTo(ti);
                var body = btn.transform;
                var disc = target.color; disc.a = 0.45f;
                UIFactory.Shape("Disc", body, UIFactory.Circle(0.48f), disc, Vector3.zero, 1);
                rings.Add(UIFactory.Shape("Ring", body, UIFactory.Ring(0.44f, 0.52f), new Color(1, 1, 1, 0.9f), new Vector3(0, 0, -0.005f), 2).transform);
                UIFactory.Shape("Arrow", body, UIFactory.Triangle(new Vector3(0, 0.24f), new Vector3(0.2f, -0.1f), new Vector3(-0.2f, -0.1f)),
                    Color.white, new Vector3(0, 0.02f, -0.01f), 3);
                UIFactory.Shape("NameBg", body, UIFactory.Quad(2.1f, 0.34f), new Color32(0x0d, 0x0a, 0x1f, 0xcc), new Vector3(0, -0.78f, 0), 1);
                UIFactory.Text(body, target.name, 0.2f, Color.white, new Vector3(0, -0.78f, 0));
            }
        }

        // ---------- switch section ----------
        public void GoTo(int i)
        {
            if (i < 0 || i >= sections.Count) return;
            var s = sections[i];
            current = i;
            for (int j = 0; j < rows.Count; j++) UIFactory.SetColor(rows[j], j == i ? RowCurrent : RowColor);

            labelText.text = s.name;
            UIFactory.SetColor(labelBar, s.color);
            labelPop = 0.6f;
            reachedEnd = false;
            hintText.text = "HOLD TRIGGER TO WALK";

            BuildHotspots(s);

            video.Stop();
            video.source = VideoSource.Url;
            video.url = VideoPath(s.videoFile);
            video.Prepare();
        }

        // Videos ship inside the app (Assets/StreamingAssets/Videos, compressed copies of ../videos),
        // so the APK runs on any Quest without copying anything onto it.
        static string VideoPath(string file) => Path.Combine(Application.streamingAssetsPath, "Videos", file);

        void ToggleMusic()
        {
            if (music == null) return;
            if (music.isPlaying) music.Pause(); else music.UnPause();
            musicText.text = music.isPlaying ? "MUSIC: ON" : "MUSIC: OFF";
        }

        void Update()
        {
            // keep the MENU pad near your feet, in the direction you're facing
            PlaceInFront(pad, 1.1f, -0.85f);
            // arrows move with the head like the 360 video does, so they stay glued to the same spot in it
            hotspotRoot.position = rig.HeadPosition;

            // look down for a moment → open menu (must look up again before it can re-open)
            float pitch = rig.HeadPitch;
            if (pitch > -lookDownAngle / 2) lookDownArmed = true;
            float k = 0;
            if (!menuOpen && lookDownArmed && pitch < -lookDownAngle)
            {
                if (downSince < 0) downSince = Time.time;
                k = Mathf.Clamp01((Time.time - downSince) / lookDownSeconds);
                if (k >= 1) { SetMenu(true); k = 0; }
            }
            else downSince = -1;
            padProgress.localScale = new Vector3(Mathf.Max(k, 0.001f), 1, 1);

            // pulsing hotspot rings + label pop
            float pulse = 1f + 0.125f * (1 - Mathf.Cos(Time.time * Mathf.PI / 0.9f));
            foreach (var r in rings) r.localScale = new Vector3(pulse, pulse, 1);
            labelPop = Mathf.MoveTowards(labelPop, 1f, Time.deltaTime * 1.2f);
            label.localScale = Vector3.one * EaseOutBack(labelPop);

            UpdateWalk();
            HandleKeys();
        }

        // hold trigger = walk forward through the video (pressure = speed), let go = stand still
        void UpdateWalk()
        {
            float input = menuOpen ? 0 : rig.WalkInput;
            bool walking = input > 0.05f && !reachedEnd && video.isPrepared;
            if (walking)
            {
                if (video.canSetPlaybackSpeed) video.playbackSpeed = Mathf.Lerp(slowestWalk, 1f, input);
                if (!video.isPlaying) video.Play();
            }
            else if (video.isPlaying && video.frame > 0) video.Pause();

            vignetteAlpha = Mathf.MoveTowards(vignetteAlpha, walking ? comfortVignette : 0, Time.deltaTime * 3f);
            vignette.enabled = vignetteAlpha > 0.01f;
            UIFactory.SetColor(vignette, new Color(0, 0, 0, vignetteAlpha));
        }

        static float EaseOutBack(float x)
        {
            float t = Mathf.InverseLerp(0.6f, 1f, x);
            const float c1 = 1.70158f, c3 = c1 + 1;
            return Mathf.Lerp(0.6f, 1f, 1 + c3 * Mathf.Pow(t - 1, 3) + c1 * Mathf.Pow(t - 1, 2));
        }

        // Editor / keyboard shortcuts: M = menu, 1–9 = jump, H = print look direction for hotspot links
        void HandleKeys()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.mKey.wasPressedThisFrame) SetMenu(!menuOpen);
            if (kb.hKey.wasPressedThisFrame) LogDirection();
            for (int n = 1; n <= 9; n++)
                if (kb[(Key)((int)Key.Digit1 + n - 1)].wasPressedThisFrame) GoTo(n - 1);
#else
            if (Input.GetKeyDown(KeyCode.M)) SetMenu(!menuOpen);
            if (Input.GetKeyDown(KeyCode.H)) LogDirection();
            for (int n = 1; n <= 9; n++)
                if (Input.GetKeyDown(KeyCode.Alpha0 + n)) GoTo(n - 1);
#endif
        }

        void LogDirection()
        {
            float yaw = Mathf.Round(rig.HeadYaw), pitch = Mathf.Round(rig.HeadPitch);
            string txt = $"yaw: {yaw}, pitch: {pitch}";
            Debug.Log($"[{sections[current].id}] {txt}");
            GUIUtility.systemCopyBuffer = txt;
        }
    }
}
