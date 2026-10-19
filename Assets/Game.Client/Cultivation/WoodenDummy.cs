using UnityEngine;

namespace Game.Client.Cultivation
{
    // Placeholder 목각인형 (wooden training dummy): a post with three short arms and a plain
    // procedural wood-grain texture. Built from primitives so it needs no assets.
    public static class WoodenDummy
    {
        public static GameObject Create(Transform parent, Vector3 localPosition)
        {
            var root = new GameObject("WoodenDummy");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPosition;

            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "WoodenDummy", mainTexture = WoodGrain() };
            material.SetFloat("_Smoothness", 0.15f);

            // Post (1.45 m) facing the character, who stands on the -Z side of it.
            Part(root.transform, material, new Vector3(0f, 0.725f, 0f), Quaternion.identity, new Vector3(0.26f, 0.725f, 0.26f));
            // Two upper arms angled forward, one lower arm straight out.
            Part(root.transform, material, new Vector3(-0.1f, 1.2f, -0.2f), Quaternion.Euler(75f, 20f, 0f), new Vector3(0.06f, 0.2f, 0.06f));
            Part(root.transform, material, new Vector3(0.1f, 1.2f, -0.2f), Quaternion.Euler(75f, -20f, 0f), new Vector3(0.06f, 0.2f, 0.06f));
            Part(root.transform, material, new Vector3(0f, 0.85f, -0.22f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.06f, 0.18f, 0.06f));
            // Base block.
            GameObject foot = GameObject.CreatePrimitive(PrimitiveType.Cube);
            foot.name = "Base";
            Object.Destroy(foot.GetComponent<Collider>());
            foot.transform.SetParent(root.transform, false);
            foot.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            foot.transform.localScale = new Vector3(0.5f, 0.1f, 0.5f);
            foot.GetComponent<Renderer>().sharedMaterial = material;
            return root;
        }

        private static void Part(Transform parent, Material material, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            part.name = "Wood";
            Object.Destroy(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localRotation = rotation;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
        }

        // Vertical grain: warm brown with wavy darker streaks.
        private static Texture2D WoodGrain()
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "WoodGrain", wrapMode = TextureWrapMode.Repeat };
            var light = new Color(0.55f, 0.36f, 0.2f);
            var dark = new Color(0.33f, 0.2f, 0.1f);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size * Mathf.PI * 2f;
                    float wave = Mathf.Sin(u * 7f + Mathf.Sin(y * 0.09f) * 1.6f + Mathf.PerlinNoise(x * 0.05f, y * 0.02f) * 3f);
                    float t = Mathf.Clamp01(wave * 0.5f + 0.5f);
                    pixels[y * size + x] = Color.Lerp(light, dark, t * t);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }
    }
}
