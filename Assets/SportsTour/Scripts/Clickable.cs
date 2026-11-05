using System;
using UnityEngine;

namespace SportsTour
{
    // Anything with a collider + this component can be pointed at (laser, gaze or mouse) and clicked.
    public class Clickable : MonoBehaviour
    {
        public Action onClick;
        public Action<bool> onHover;
        public float hoverScale = 1.06f;

        Vector3 baseScale;
        int hoverCount; // several pointers can hover the same thing

        void Start() => baseScale = transform.localScale;

        void Update()
        {
            var target = hoverCount > 0 ? baseScale * hoverScale : baseScale;
            transform.localScale = Vector3.Lerp(transform.localScale, target, Time.deltaTime * 14f);
        }

        public void SetHover(bool on)
        {
            bool was = hoverCount > 0;
            hoverCount = Mathf.Max(0, hoverCount + (on ? 1 : -1));
            if (was != hoverCount > 0) onHover?.Invoke(hoverCount > 0);
        }

        public void Click() => onClick?.Invoke();

        void OnDisable()
        {
            if (hoverCount > 0) onHover?.Invoke(false);
            hoverCount = 0;
            if (baseScale != Vector3.zero) transform.localScale = baseScale;
        }
    }
}
