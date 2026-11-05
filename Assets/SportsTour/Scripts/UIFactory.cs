using UnityEngine;

namespace SportsTour
{
    // Builds the flat VR UI out of simple meshes + 3D text (no Canvas / EventSystem needed).
    public static class UIFactory
    {
        public static Material baseMaterial; // Sprites/Default: unlit, transparent, double-sided
        public static Font font;

        public static Mesh Quad(float w, float h)
        {
            var m = new Mesh { name = "Quad" };
            float x = w / 2, y = h / 2;
            m.vertices = new[] { new Vector3(-x, -y), new Vector3(-x, y), new Vector3(x, y), new Vector3(x, -y) };
            m.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
            m.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            m.RecalculateBounds();
            return m;
        }

        public static Mesh Ring(float inner, float outer, int seg = 64)
        {
            var m = new Mesh { name = "Ring" };
            var v = new Vector3[seg * 2];
            var t = new int[seg * 6];
            for (int i = 0; i < seg; i++)
            {
                float a = i * Mathf.PI * 2 / seg;
                var d = new Vector3(Mathf.Cos(a), Mathf.Sin(a));
                v[i * 2] = d * inner;
                v[i * 2 + 1] = d * outer;
                int n = (i + 1) % seg;
                t[i * 6] = i * 2; t[i * 6 + 1] = n * 2; t[i * 6 + 2] = i * 2 + 1;
                t[i * 6 + 3] = i * 2 + 1; t[i * 6 + 4] = n * 2; t[i * 6 + 5] = n * 2 + 1;
            }
            m.vertices = v; m.triangles = t; m.RecalculateBounds();
            return m;
        }

        public static Mesh Circle(float r, int seg = 64) => Ring(0f, r, seg);

        public static Mesh Triangle(Vector3 a, Vector3 b, Vector3 c)
        {
            var m = new Mesh { name = "Triangle", vertices = new[] { a, b, c }, triangles = new[] { 0, 1, 2 } };
            m.RecalculateBounds();
            return m;
        }

        public static MeshRenderer Shape(string name, Transform parent, Mesh mesh, Color color, Vector3 pos, int order = 0)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = new Material(baseMaterial) { color = color };
            r.sortingOrder = order;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return r;
        }

        public static void SetColor(Renderer r, Color c) => r.sharedMaterial.color = c;

        // lineHeight is the height of one line of text in metres
        public static TextMesh Text(Transform parent, string value, float lineHeight, Color color, Vector3 pos,
            TextAnchor anchor = TextAnchor.MiddleCenter, int order = 5)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos + new Vector3(0, 0, -0.002f);
            var t = go.AddComponent<TextMesh>();
            t.font = font;
            t.fontSize = 96; // high res glyphs, then scaled down so it stays sharp
            t.characterSize = lineHeight / (t.fontSize * 0.1f);
            t.anchor = anchor;
            t.alignment = TextAlignment.Left;
            t.color = color;
            t.text = value;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = font.material;
            r.sortingOrder = order;
            return t;
        }

        public static Clickable Button(string name, Transform parent, float w, float h, Vector3 pos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.AddComponent<BoxCollider>().size = new Vector3(w, h, 0.02f);
            return go.AddComponent<Clickable>();
        }
    }
}
