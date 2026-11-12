using UnityEngine;

namespace Game.Client.Scenery
{
    // Building blocks for the code-built 3D sets (the 단련 stage and the battle arenas): primitives
    // without colliders, a few small generated meshes, URP materials and procedural textures, so
    // no set needs imported assets. Sets lay themselves out with local -Z pointing away from their
    // camera, so a prop's +Z face is the one seen on screen.
    public static class SceneryKit
    {
        public static readonly Color Stone = new Color(0.47f, 0.45f, 0.41f);
        public static readonly Color TileDark = new Color(0.17f, 0.17f, 0.18f);

        // ---- props -------------------------------------------------------------------------

        public static void Pine(Transform parent, Vector3 position)
        {
            var root = new GameObject("Pine").transform;
            root.SetParent(parent, false);
            root.localPosition = position;
            Material bark = Lit(Mottled(new Color(0.27f, 0.2f, 0.15f), 11), 0.1f, new Vector2(1f, 3f));
            Material needles = Lit(Mottled(new Color(0.16f, 0.22f, 0.15f), 5), 0.05f, Vector2.one);

            // A leaning, kinked trunk with flat needle pads stacked along it, 소나무 style.
            Prim(PrimitiveType.Cylinder, "Trunk", root, bark, new Vector3(0.1f, 0.9f, 0f), Quaternion.Euler(0f, 0f, -8f), new Vector3(0.22f, 0.95f, 0.22f));
            Prim(PrimitiveType.Cylinder, "Trunk", root, bark, new Vector3(-0.1f, 2.5f, 0f), Quaternion.Euler(0f, 0f, 18f), new Vector3(0.16f, 0.75f, 0.16f));
            Prim(PrimitiveType.Cylinder, "Branch", root, bark, new Vector3(-0.6f, 2.2f, 0.1f), Quaternion.Euler(0f, 0f, 62f), new Vector3(0.07f, 0.55f, 0.07f));
            Prim(PrimitiveType.Sphere, "Pad", root, needles, new Vector3(-1.1f, 2.5f, 0.15f), Quaternion.Euler(0f, 20f, 0f), new Vector3(1.4f, 0.32f, 0.9f));
            Prim(PrimitiveType.Sphere, "Pad", root, needles, new Vector3(0.3f, 2.1f, -0.1f), Quaternion.Euler(0f, -10f, 0f), new Vector3(1.1f, 0.28f, 0.8f));
            Prim(PrimitiveType.Sphere, "Pad", root, needles, new Vector3(-0.3f, 3.15f, 0f), Quaternion.Euler(0f, 35f, 0f), new Vector3(1.6f, 0.36f, 1.1f));
            Prim(PrimitiveType.Sphere, "Pad", root, needles, new Vector3(-0.45f, 3.55f, 0.05f), Quaternion.Euler(0f, 5f, 0f), new Vector3(0.9f, 0.26f, 0.7f));
        }

        // 석등: footing, post, platform, a lit firebox and a little hip roof.
        public static void StoneLantern(Transform parent, Material stone, Material roof, Vector3 position)
        {
            var root = new GameObject("StoneLantern").transform;
            root.SetParent(parent, false);
            root.localPosition = position;

            Prim(PrimitiveType.Cube, "Base", root, stone, new Vector3(0f, 0.08f, 0f), Quaternion.identity, new Vector3(0.5f, 0.16f, 0.5f));
            Prim(PrimitiveType.Cylinder, "Post", root, stone, new Vector3(0f, 0.5f, 0f), Quaternion.identity, new Vector3(0.16f, 0.27f, 0.16f));
            Prim(PrimitiveType.Cube, "Platform", root, stone, new Vector3(0f, 0.8f, 0f), Quaternion.identity, new Vector3(0.46f, 0.07f, 0.46f));
            Prim(PrimitiveType.Cube, "Firebox", root, stone, new Vector3(0f, 0.99f, 0f), Quaternion.identity, new Vector3(0.32f, 0.3f, 0.32f));
            HipRoof(root, "Roof", roof, new Vector3(0f, 1.14f, 0f), 0.62f, 0.62f, 0.2f, 0.04f);
            Prim(PrimitiveType.Sphere, "Finial", root, stone, new Vector3(0f, 1.38f, 0f), Quaternion.identity, Vector3.one * 0.09f);

            // The window glows toward the camera; a small warm light spills onto the paving.
            var glow = new Material(Shader.Find("Sprites/Default")) { name = "LanternGlow", color = new Color(1f, 0.72f, 0.35f) };
            Prim(PrimitiveType.Quad, "Window", root, glow, new Vector3(0f, 0.99f, 0.161f), Quaternion.Euler(0f, 180f, 0f), new Vector3(0.16f, 0.17f, 1f));
            var lightGo = new GameObject("LanternLight");
            lightGo.transform.SetParent(root, false);
            lightGo.transform.localPosition = new Vector3(0f, 1f, 0.35f);
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.7f, 0.4f);
            light.intensity = 0.35f;
            light.range = 2.5f;
            light.shadows = LightShadows.None;
        }

        // 병기대: a wooden frame with four spears leaning in it.
        public static void WeaponRack(Transform parent, Material wood, Vector3 position, float yaw)
        {
            var root = new GameObject("WeaponRack").transform;
            root.SetParent(parent, false);
            root.localPosition = position;
            root.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Material steel = Lit(null, 0.75f, Vector2.one, new Color(0.7f, 0.72f, 0.74f));
            steel.SetFloat("_Metallic", 0.8f);
            Material tassel = Lit(null, 0.2f, Vector2.one, new Color(0.6f, 0.12f, 0.08f));
            Material shaft = Lit(null, 0.3f, Vector2.one, new Color(0.22f, 0.13f, 0.08f));

            Prim(PrimitiveType.Cube, "Post", root, wood, new Vector3(-0.6f, 0.7f, 0f), Quaternion.identity, new Vector3(0.09f, 1.4f, 0.09f));
            Prim(PrimitiveType.Cube, "Post", root, wood, new Vector3(0.6f, 0.7f, 0f), Quaternion.identity, new Vector3(0.09f, 1.4f, 0.09f));
            Prim(PrimitiveType.Cube, "Bar", root, wood, new Vector3(0f, 1.25f, 0f), Quaternion.identity, new Vector3(1.4f, 0.07f, 0.07f));
            Prim(PrimitiveType.Cube, "Bar", root, wood, new Vector3(0f, 0.3f, 0f), Quaternion.identity, new Vector3(1.3f, 0.07f, 0.07f));
            Prim(PrimitiveType.Cube, "Foot", root, wood, new Vector3(-0.6f, 0.03f, 0f), Quaternion.identity, new Vector3(0.12f, 0.06f, 0.5f));
            Prim(PrimitiveType.Cube, "Foot", root, wood, new Vector3(0.6f, 0.03f, 0f), Quaternion.identity, new Vector3(0.12f, 0.06f, 0.5f));

            for (int i = 0; i < 4; i++)
            {
                var spear = new GameObject("Spear").transform;
                spear.SetParent(root, false);
                spear.localPosition = new Vector3(-0.39f + i * 0.26f, 0f, 0.1f);
                spear.localRotation = Quaternion.Euler(-7f, 0f, 0f);
                float length = 2.1f + (i % 2) * 0.15f;
                Prim(PrimitiveType.Cylinder, "Shaft", spear, shaft, new Vector3(0f, length * 0.5f, 0f), Quaternion.identity, new Vector3(0.035f, length * 0.5f, 0.035f));
                Prim(PrimitiveType.Sphere, "Tassel", spear, tassel, new Vector3(0f, length - 0.02f, 0f), Quaternion.identity, new Vector3(0.09f, 0.12f, 0.09f));
                Prim(PrimitiveType.Cube, "Blade", spear, steel, new Vector3(0f, length + 0.12f, 0f), Quaternion.Euler(0f, 45f, 0f), new Vector3(0.05f, 0.24f, 0.012f));
            }
        }

        // ---- meshes ------------------------------------------------------------------------

        public static GameObject Prim(PrimitiveType type, string name, Transform parent, Material material, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = rotation;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        // A capped cylinder standing on position: submesh 0 is the top (planar UVs over the
        // disc, so a square texture maps onto it whole), submesh 1 the side.
        public static GameObject Drum(Transform parent, string name, Vector3 position, float radius, float height, Material top, Material side, int segments = 64)
        {
            var vertices = new Vector3[segments * 3 + 3];
            var uvs = new Vector2[vertices.Length];
            var topTris = new int[segments * 3];
            var sideTris = new int[segments * 6];

            int centre = segments * 3 + 2;
            vertices[centre] = new Vector3(0f, height, 0f);
            uvs[centre] = new Vector2(0.5f, 0.5f);
            for (int i = 0; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                float c = Mathf.Cos(a), s = Mathf.Sin(a);
                vertices[segments * 2 + i] = new Vector3(c * radius, 0f, s * radius);
                uvs[segments * 2 + i] = new Vector2(i / (float)segments, 0f);
                if (i == segments)
                    break;
                vertices[i] = new Vector3(c * radius, height, s * radius);
                uvs[i] = new Vector2(0.5f + c * 0.5f, 0.5f + s * 0.5f);
                vertices[segments + i] = vertices[i];
                uvs[segments + i] = new Vector2(i / (float)segments, height / (radius * Mathf.PI * 2f / 4f));
            }
            vertices[segments * 3 + 1] = vertices[0];
            uvs[segments * 3 + 1] = new Vector2(1f, uvs[segments].y);

            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                topTris[i * 3] = centre;
                topTris[i * 3 + 1] = next;
                topTris[i * 3 + 2] = i;

                int upper = segments + i;
                int upperNext = i + 1 == segments ? segments * 3 + 1 : segments + i + 1;
                int lower = segments * 2 + i;
                int lowerNext = segments * 2 + i + 1;
                sideTris[i * 6] = upper;
                sideTris[i * 6 + 1] = upperNext;
                sideTris[i * 6 + 2] = lower;
                sideTris[i * 6 + 3] = upperNext;
                sideTris[i * 6 + 4] = lowerNext;
                sideTris[i * 6 + 5] = lower;
            }

            var mesh = new Mesh { name = name, vertices = vertices, uv = uvs, subMeshCount = 2 };
            mesh.SetTriangles(topTris, 0);
            mesh.SetTriangles(sideTris, 1);
            // The top and side have their own rim vertices, so the edge stays crisp.
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterials = new[] { top, side };
            return go;
        }

        // A hip roof whose eave corners lift by `lift` (the curved 처마 of a hall roof): base
        // width x depth at y = 0, ridge along X at y = height.
        public static GameObject HipRoof(Transform parent, string name, Material material, Vector3 position, float width, float depth, float height, float lift)
        {
            float hw = width * 0.5f, hd = depth * 0.5f;
            float ridge = Mathf.Max(0.01f, hw - hd);
            var fl = new Vector3(-hw, lift, hd);
            var fr = new Vector3(hw, lift, hd);
            var bl = new Vector3(-hw, lift, -hd);
            var br = new Vector3(hw, lift, -hd);
            var fm = new Vector3(0f, 0f, hd);
            var bm = new Vector3(0f, 0f, -hd);
            var rl = new Vector3(-ridge, height, 0f);
            var rr = new Vector3(ridge, height, 0f);

            var vertices = new System.Collections.Generic.List<Vector3>();
            var uvs = new System.Collections.Generic.List<Vector2>();
            var tris = new System.Collections.Generic.List<int>();
            // Each face also gets a reversed copy on its own vertices, so the eaves have an
            // underside (shaded, facing down) when seen from below.
            void Tri(Vector3 a, Vector3 b, Vector3 c, bool alongX)
            {
                for (int side = 0; side < 2; side++)
                {
                    int i = vertices.Count;
                    foreach (Vector3 v in new[] { a, b, c })
                    {
                        vertices.Add(v);
                        uvs.Add(new Vector2((alongX ? v.x : v.z) / width * 6f, v.y + (alongX ? Mathf.Abs(v.z) : Mathf.Abs(v.x)) * -0.5f));
                    }
                    tris.Add(i);
                    tris.Add(side == 0 ? i + 2 : i + 1);
                    tris.Add(side == 0 ? i + 1 : i + 2);
                }
            }

            // Front and back slopes sag slightly at mid-eave, giving the corners their lift.
            Tri(fl, rl, fm, true); Tri(fm, rl, rr, true); Tri(fm, rr, fr, true);
            Tri(br, rr, bm, true); Tri(bm, rr, rl, true); Tri(bm, rl, bl, true);
            Tri(bl, rl, fl, false);
            Tri(fr, rr, br, false);

            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        // ---- materials and textures ----------------------------------------------------------

        public static Material Lit(Texture2D texture, float smoothness, Vector2 tiling, Color? tint = null)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = texture != null ? texture.name : "Scenery" };
            if (texture != null)
            {
                material.mainTexture = texture;
                material.mainTextureScale = tiling;
            }
            material.color = tint ?? Color.white;
            material.SetFloat("_Smoothness", smoothness);
            return material;
        }

        public static Texture2D NewTexture(string name, int width, int height)
            => new Texture2D(width, height, TextureFormat.RGBA32, true) { name = name, wrapMode = TextureWrapMode.Repeat, anisoLevel = 4 };

        public static Texture2D Finish(Texture2D texture, Color[] pixels)
        {
            texture.SetPixels(pixels);
            texture.Apply(true, true);
            return texture;
        }

        // Cheap deterministic per-cell variation in [0, 1).
        public static float Hash(int a, int b)
        {
            uint h = (uint)(a * 374761393 + b * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFF) / 65536f;
        }

        // 박석 paving (one 2 m tile): staggered rows of uneven slabs with worn edges and grout.
        public static Texture2D Paving()
        {
            const int size = 256, rowHeight = 64;
            int[][] rows =
            {
                new[] { 0, 96, 176 },
                new[] { 0, 72, 176 },
                new[] { 0, 88, 176 },
                new[] { 0, 112, 176 },
            };
            var grout = new Color(0.16f, 0.15f, 0.14f);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                int row = y / rowHeight;
                int ry = y % rowHeight;
                for (int x = 0; x < size; x++)
                {
                    int sx = (x + row * 41) % size;
                    int[] cuts = rows[row];
                    int slab = 0;
                    for (int c = 0; c < cuts.Length; c++)
                        if (sx >= cuts[c])
                            slab = c;
                    int left = cuts[slab];
                    int right = slab + 1 < cuts.Length ? cuts[slab + 1] : size;
                    float edge = Mathf.Min(Mathf.Min(sx - left, right - 1 - sx), Mathf.Min(ry, rowHeight - 1 - ry));
                    // Ragged grout line.
                    float ragged = Mathf.PerlinNoise(x * 0.31f, y * 0.29f) * 1.6f;
                    if (edge < 1.2f + ragged * 0.6f)
                    {
                        pixels[y * size + x] = grout;
                        continue;
                    }

                    float v = Hash(row, slab);
                    Color c0 = Color.Lerp(Stone, new Color(0.56f, 0.53f, 0.47f), v);
                    float mottle = Mathf.PerlinNoise(x * 0.06f + v * 50f, y * 0.06f) * 0.16f - 0.08f
                                 + Mathf.PerlinNoise(x * 0.4f, y * 0.4f) * 0.06f - 0.03f;
                    float wear = Mathf.Clamp01(1f - (edge - 1.5f) / 6f) * 0.12f;
                    float k = 1f + mottle - wear;
                    pixels[y * size + x] = new Color(c0.r * k, c0.g * k, c0.b * k);
                }
            }
            return Finish(NewTexture("Paving", size, size), pixels);
        }

        public static Texture2D Mottled(Color baseColor, int seed)
        {
            const int size = 64;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Sampled along circles, so the noise wraps at the texture edges and tiles.
                    float u = x / (float)size * Mathf.PI * 2f, w = y / (float)size * Mathf.PI * 2f;
                    float n = Mathf.PerlinNoise(seed * 10f + Mathf.Cos(u) * 2f + Mathf.Sin(w) * 0.7f, Mathf.Sin(u) * 2f + Mathf.Cos(w) * 2f);
                    float k = 0.85f + n * 0.3f;
                    pixels[y * size + x] = new Color(baseColor.r * k, baseColor.g * k, baseColor.b * k);
                }
            }
            return Finish(NewTexture("Mottled", size, size), pixels);
        }

        // 담장 face (one 2 m bay, full height): stone footing, a dark band, then lime plaster.
        public static Texture2D Plaster()
        {
            const int width = 128, height = 128;
            var plaster = new Color(0.78f, 0.74f, 0.66f);
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float n = Mathf.PerlinNoise(x * 0.05f, y * 0.05f);
                    Color c;
                    if (y < 34)
                    {
                        // Two courses of footing stones.
                        int course = y / 17;
                        int sx = (x + course * 23) % width;
                        int stone = sx / 32;
                        bool joint = sx % 32 < 2 || y % 17 < 2;
                        float v = Hash(course + 7, stone);
                        c = joint ? new Color(0.2f, 0.19f, 0.18f) : Color.Lerp(new Color(0.36f, 0.35f, 0.33f), new Color(0.48f, 0.46f, 0.42f), v) * (0.92f + n * 0.16f);
                    }
                    else if (y < 39)
                    {
                        c = new Color(0.22f, 0.2f, 0.18f);
                    }
                    else
                    {
                        // Rain streaks darken the plaster toward the footing.
                        float streak = Mathf.PerlinNoise(x * 0.35f, y * 0.012f) * 0.12f;
                        float grime = Mathf.Clamp01(1f - (y - 39) / 40f) * 0.12f;
                        c = plaster * (0.94f + n * 0.1f - streak - grime);
                    }
                    pixels[y * width + x] = c;
                }
            }
            return Finish(NewTexture("Plaster", width, height), pixels);
        }

        // 기와: rounded tile channels running down the slope.
        public static Texture2D RoofTiles()
        {
            const int size = 64;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float ridge = Mathf.Sin(x / (float)size * Mathf.PI * 2f * 4f);
                    float course = (y % 16) / 16f;
                    float k = 0.75f + ridge * 0.25f - (course > 0.85f ? 0.2f : 0f) + Mathf.PerlinNoise(x * 0.2f, y * 0.2f) * 0.1f;
                    pixels[y * size + x] = TileDark * (1.4f * k);
                }
            }
            return Finish(NewTexture("RoofTiles", size, size), pixels);
        }

        // One bay of a hall front: red pillars at both edges, a red lintel with a green 단청 band,
        // a paper-and-lattice door above a wooden panel.
        public static Texture2D HallFacade()
        {
            const int width = 128, height = 128;
            var red = new Color(0.48f, 0.15f, 0.1f);
            var teal = new Color(0.2f, 0.38f, 0.33f);
            var paper = new Color(0.82f, 0.77f, 0.64f);
            var lattice = new Color(0.3f, 0.18f, 0.11f);
            var panel = new Color(0.34f, 0.21f, 0.13f);
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float n = 0.93f + Mathf.PerlinNoise(x * 0.1f, y * 0.1f) * 0.12f;
                    Color c;
                    if (x < 9 || x >= width - 9)
                        c = red * (x < 3 || x >= width - 3 ? 0.75f : 1f);
                    else if (y >= height - 14)
                        c = y >= height - 9 && y < height - 4 ? teal : red;
                    else if (y < 30)
                        c = y > 26 || x < 12 || x > width - 13 ? lattice : panel;
                    else if (x < 12 || x > width - 13 || y > height - 18 || y < 33)
                        c = lattice;
                    else
                        c = (x - 12) % 10 < 2 || (y - 33) % 12 < 2 ? lattice : paper;
                    pixels[y * width + x] = c * n;
                }
            }
            return Finish(NewTexture("HallFacade", width, height), pixels);
        }

        // ---- arena additions -------------------------------------------------------------------

        // Flat colour/texture that ignores lighting but still sinks into the fog; double-sided,
        // so it suits cloth (banners, awnings seen from below) and glowing lanterns.
        public static Material UnlitFog(Texture2D texture, Color color)
        {
            var material = new Material(Shader.Find("Game/InkUnlitFog")) { name = texture != null ? texture.name : "SceneryUnlit" };
            material.SetColor("_BaseColor", color);
            if (texture != null)
                material.SetTexture("_BaseMap", texture);
            return material;
        }

        // A jagged rock spire (화산 granite peak): rings of jittered points narrowing to a crooked
        // tip, flat shaded. Base at position, height up +Y.
        public static GameObject Spire(Transform parent, string name, Material material, Vector3 position, float height, float radius, int seed)
        {
            const int sides = 7, rings = 8;
            var ring = new Vector3[rings + 1, sides];
            for (int r = 0; r <= rings; r++)
            {
                float t = r / (float)rings;
                // Steep sides that pinch in near the top, leaning a little toward a random side.
                float width = radius * (1f - Mathf.Pow(t, 1.4f)) * (0.7f + Hash(seed, r) * 0.6f) + 0.15f;
                Vector3 lean = new Vector3(Hash(seed, 31) - 0.5f, 0f, Hash(seed, 37) - 0.5f) * (radius * 0.6f * t * t);
                for (int k = 0; k < sides; k++)
                {
                    float a = (k + Hash(seed + r, k) * 0.5f) / sides * Mathf.PI * 2f;
                    float jitter = 0.55f + Hash(seed * 3 + r, k + 11) * 0.9f;
                    ring[r, k] = new Vector3(Mathf.Cos(a) * width * jitter, t * height, Mathf.Sin(a) * width * jitter) + lean;
                }
            }

            var vertices = new System.Collections.Generic.List<Vector3>();
            var uvs = new System.Collections.Generic.List<Vector2>();
            void Tri(Vector3 a, Vector3 b, Vector3 c)
            {
                foreach (Vector3 v in new[] { a, b, c })
                {
                    vertices.Add(v);
                    uvs.Add(new Vector2((Mathf.Atan2(v.z, v.x) / Mathf.PI + 1f) * 2f, v.y / radius * 0.5f));
                }
            }
            for (int r = 0; r < rings; r++)
            {
                for (int k = 0; k < sides; k++)
                {
                    int n = (k + 1) % sides;
                    Tri(ring[r, k], ring[r + 1, k], ring[r, n]);
                    Tri(ring[r, n], ring[r + 1, k], ring[r + 1, n]);
                }
            }

            // Every triangle has its own vertices, so the normals come out flat (faceted rock).
            var triangles = new int[vertices.Count];
            for (int i = 0; i < triangles.Length; i++)
                triangles[i] = i;
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        // A boulder: a squashed, tilted sphere.
        public static GameObject Rock(Transform parent, Material material, Vector3 position, Vector3 size, float yaw)
            => Prim(PrimitiveType.Sphere, "Rock", parent, material, position, Quaternion.Euler(Hash((int)(yaw * 7f), 3) * 20f - 10f, yaw, Hash((int)yaw, 5) * 20f - 10f), size);

        // A cloth banner on a pole: the pole stands at position, the cloth (width x length) hangs
        // from an arm on its +X side, facing +Z.
        public static void BannerPole(Transform parent, Material pole, Material cloth, Vector3 position, float poleHeight, float width, float length)
        {
            var root = new GameObject("Banner").transform;
            root.SetParent(parent, false);
            root.localPosition = position;
            Prim(PrimitiveType.Cylinder, "Pole", root, pole, new Vector3(0f, poleHeight * 0.5f, 0f), Quaternion.identity, new Vector3(0.07f, poleHeight * 0.5f, 0.07f));
            Prim(PrimitiveType.Cylinder, "Arm", root, pole, new Vector3(width * 0.55f, poleHeight - 0.08f, 0f), Quaternion.Euler(0f, 0f, 90f), new Vector3(0.04f, width * 0.6f, 0.04f));
            Prim(PrimitiveType.Quad, "Cloth", root, cloth, new Vector3(width * 0.55f, poleHeight - 0.1f - length * 0.5f, 0f), Quaternion.Euler(0f, 180f, 0f), new Vector3(width, length, 1f));
        }

        // Packed earth with scattered pebbles and broad damp patches (tiles).
        public static Texture2D Earth(Color baseColor, int seed)
        {
            const int size = 128;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size * Mathf.PI * 2f, w = y / (float)size * Mathf.PI * 2f;
                    float broad = Mathf.PerlinNoise(seed + Mathf.Cos(u) * 1.5f + Mathf.Sin(w) * 0.8f, Mathf.Sin(u) * 1.5f + Mathf.Cos(w) * 1.5f);
                    float fine = Mathf.PerlinNoise(seed + Mathf.Cos(u) * 9f + 40f, Mathf.Sin(w) * 9f + Mathf.Sin(u) * 4f);
                    Color c = baseColor * (0.84f + broad * 0.22f + fine * 0.1f);
                    // Pebbles: a pale dot with a darker lower edge.
                    if (Hash(x / 6 + seed, y / 6) > 0.92f)
                    {
                        float dx = x % 6 - 2.5f, dy = y % 6 - 2.5f;
                        if (dx * dx + dy * dy < 3.5f)
                            c = Color.Lerp(c, new Color(0.72f, 0.68f, 0.6f), 0.6f) * (dy < -1f ? 0.8f : 1f);
                    }
                    c.a = 1f;
                    pixels[y * size + x] = c;
                }
            }
            return Finish(NewTexture("Earth", size, size), pixels);
        }

        // Pale granite (화강암): speckled grain with a few dark hairline cracks (tiles).
        public static Texture2D Granite(Color baseColor, int seed)
        {
            const int size = 128;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size * Mathf.PI * 2f, w = y / (float)size * Mathf.PI * 2f;
                    float broad = Mathf.PerlinNoise(seed + Mathf.Cos(u) * 2f + Mathf.Sin(w), Mathf.Sin(u) * 2f + Mathf.Cos(w) * 2f);
                    float speck = Hash(x + seed * 131, y);
                    float k = 0.86f + broad * 0.18f + (speck > 0.93f ? -0.25f : speck < 0.05f ? 0.12f : 0f);
                    // Cracks: thin bands where a second noise crosses 0.5.
                    float crack = Mathf.PerlinNoise(seed + Mathf.Cos(u) * 3f + Mathf.Sin(w) * 2f + 70f, Mathf.Sin(u) * 3f + Mathf.Cos(w) * 3f);
                    if (Mathf.Abs(crack - 0.5f) < 0.012f)
                        k *= 0.62f;
                    Color c = baseColor * k;
                    c.a = 1f;
                    pixels[y * size + x] = c;
                }
            }
            return Finish(NewTexture("Granite", size, size), pixels);
        }

        // Two-colour cloth stripes running along V (awnings).
        public static Texture2D Stripes(Color a, Color b)
        {
            const int size = 32;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Color c = (x / 8 % 2 == 0 ? a : b) * (0.95f + Hash(x, y) * 0.08f);
                    c.a = 1f;
                    pixels[y * size + x] = c;
                }
            }
            return Finish(NewTexture("Stripes", size, size), pixels);
        }

        // A shop banner: coloured cloth with a pale border and a pale disc holding a few bold ink
        // strokes, which reads at a distance as a painted character.
        public static Texture2D Banner(Color cloth, int seed)
        {
            const int width = 64, height = 160;
            var pale = new Color(0.93f, 0.89f, 0.8f);
            var ink = new Color(0.1f, 0.09f, 0.08f);
            var pixels = new Color[width * height];
            var centre = new Vector2(width * 0.5f, height * 0.66f);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Color c = cloth * (0.92f + Mathf.PerlinNoise(x * 0.1f, y * 0.05f) * 0.12f);
                    if (x < 4 || x >= width - 4 || y >= height - 4 || y < 4)
                        c = pale;
                    float d = Vector2.Distance(new Vector2(x, y), centre);
                    if (d < 22f)
                        c = pale;
                    float sx = x - centre.x, sy = y - centre.y;
                    bool stroke = (Mathf.Abs(sy - 8f) < 2.4f && Mathf.Abs(sx) < 13f)
                               || (Mathf.Abs(sy + 6f + seed % 3 * 2f) < 2.4f && Mathf.Abs(sx) < 15f)
                               || (Mathf.Abs(sx - (seed % 2 == 0 ? 0f : 4f)) < 2.6f && Mathf.Abs(sy) < 16f)
                               || (Mathf.Abs(sx + sy * 0.6f + 6f) < 2.2f && sy < 0f && sy > -14f);
                    if (d < 20f && stroke)
                        c = ink;
                    // A second, smaller pale character lower on the cloth.
                    float lx = x - centre.x, ly = y - height * 0.3f;
                    if ((Mathf.Abs(lx) < 2.2f && Mathf.Abs(ly) < 13f) || (Mathf.Abs(ly - 4f + seed % 2 * 7f) < 2f && Mathf.Abs(lx) < 11f))
                        c = pale;
                    c.a = 1f;
                    pixels[y * width + x] = c;
                }
            }
            Texture2D texture = Finish(NewTexture("Banner", width, height), pixels);
            texture.wrapMode = TextureWrapMode.Clamp;
            return texture;
        }

        // Soft cloud sea seen from above (tiles).
        public static Texture2D CloudSea(Color light, Color shade)
        {
            const int size = 128;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size * Mathf.PI * 2f, w = y / (float)size * Mathf.PI * 2f;
                    float n = Mathf.PerlinNoise(Mathf.Cos(u) * 1.6f + Mathf.Sin(w) * 0.6f + 5f, Mathf.Sin(u) * 1.6f + Mathf.Cos(w) * 1.6f + 5f) * 0.7f
                            + Mathf.PerlinNoise(Mathf.Cos(u) * 4f + 20f, Mathf.Sin(u) * 2f + Mathf.Cos(w) * 4f + 20f) * 0.3f;
                    pixels[y * size + x] = Color.Lerp(shade, light, Mathf.SmoothStep(0.3f, 0.7f, n));
                }
            }
            return Finish(NewTexture("CloudSea", size, size), pixels);
        }

        // ---- painted backdrops ---------------------------------------------------------------

        // One silhouette band of a painted backdrop: Crest(u) is its top edge (fraction of the
        // image height) at horizontal position u. It is filled with Tone, washing toward the
        // horizon colour lower down by Mist (0 = solid ink, 1 = fades fully into mist).
        public struct BackdropLayer
        {
            public System.Func<float, float> Crest;
            public Color Tone;
            public float Mist;
        }

        // Paints a backdrop image for InkWashBackground: a vertical sky gradient (horizon colour at
        // horizonV and below, top colour at the top edge), an optional sun (x, y in image fractions,
        // z = radius as a fraction of the height; z <= 0 for none), then the layers back to front.
        public static Texture2D PaintBackdrop(string name, int width, int height, Color top, Color horizon, float horizonV,
            Vector3 sun, Color sunColor, BackdropLayer[] layers)
        {
            var pixels = new Color[width * height];
            var crests = new float[layers.Length, width];
            for (int l = 0; l < layers.Length; l++)
                for (int x = 0; x < width; x++)
                    crests[l, x] = layers[l].Crest(x / (float)width);

            var sunCentre = new Vector2(sun.x * width / height, sun.y);
            for (int y = 0; y < height; y++)
            {
                float v = y / (float)height;
                Color sky = Color.Lerp(horizon, top, Mathf.SmoothStep(horizonV, 1f, v));
                for (int x = 0; x < width; x++)
                {
                    Color c = sky;
                    if (sun.z > 0f)
                    {
                        float sd = Vector2.Distance(new Vector2(x / (float)height, v), sunCentre);
                        c = sd < sun.z ? Color.Lerp(sunColor, c, Mathf.Clamp01((sd - sun.z) * height + 1f))
                                       : Color.Lerp(c, sunColor, Mathf.Clamp01(1f - (sd - sun.z) / (sun.z * 3f)) * 0.15f);
                    }
                    for (int l = 0; l < layers.Length; l++)
                    {
                        float crest = crests[l, x];
                        if (v > crest)
                            continue;
                        float depth = (crest - v) / Mathf.Max(0.02f, crest);
                        Color tone = Color.Lerp(layers[l].Tone, horizon, Mathf.SmoothStep(0f, 1f, depth) * layers[l].Mist);
                        c = Color.Lerp(c, tone, Mathf.Clamp01((crest - v) * height / 2f));
                    }
                    c.a = 1f;
                    pixels[y * width + x] = c;
                }
            }
            Texture2D texture = Finish(NewTexture(name, width, height), pixels);
            texture.wrapMode = TextureWrapMode.Clamp;
            return texture;
        }

        // Sum-of-sines ridge line for backdrop layers, in the same shape as UIInkMountains.
        public static float Ridge(float u, float baseV, float amplitude, float phase, float frequency = 14f)
        {
            float z = u * frequency;
            return baseV + (Mathf.Sin(z * 0.55f + phase) * 0.55f + Mathf.Sin(z * 1.37f + phase * 2.3f) * 0.3f + Mathf.Sin(z * 3.1f + phase * 0.7f) * 0.15f) * amplitude;
        }
    }
}
