using UnityEngine;
using static Game.Client.Scenery.SceneryKit;

namespace Game.Client.Scenery
{
    // 3장 화산 단련장: a granite cliff top above a sea of clouds, where the 화산파 Taoists train.
    // The fighters stand on an octagonal stone platform carved with a plum blossom (매화, the
    // sect's emblem). Plum trees in bloom, an open pavilion (정자), an incense burner and a
    // stele stand on the plateau; past the boulder-lined edge, sheer granite spires rise out of
    // the clouds toward painted peaks.
    //
    // Local frame: origin between the fighters on the platform, -Z away from the camera; local
    // -X is screen right.
    public static class MountHuaArena
    {
        private const float PlateauY = -0.18f;
        private const float EdgeZ = -8f;
        private static readonly Color GraniteColor = new Color(0.74f, 0.73f, 0.7f);

        public static GameObject Create(Transform parent, out ArenaLook look)
        {
            var root = new GameObject("MountHuaArena");
            root.transform.SetParent(parent, false);
            Transform t = root.transform;

            Texture2D graniteTexture = Granite(GraniteColor, 4);
            Material plateau = Lit(graniteTexture, 0.12f, new Vector2(26f, 10f));
            Material granite = Lit(graniteTexture, 0.12f, Vector2.one);
            Material cliff = Lit(graniteTexture, 0.1f, new Vector2(10f, 6f));
            Material peakRock = Lit(Granite(new Color(0.46f, 0.47f, 0.48f), 8), 0.1f, new Vector2(1f, 3f));
            Material red = Lit(Mottled(new Color(0.52f, 0.16f, 0.11f), 15), 0.25f, Vector2.one);
            Texture2D tileTexture = RoofTiles();
            Material tiles = Lit(tileTexture, 0.25f, Vector2.one);

            // Plateau (ends at the cliff edge) and the drop below it.
            Prim(PrimitiveType.Quad, "Plateau", t, plateau, new Vector3(0f, PlateauY, (EdgeZ + 12f) * 0.5f), Quaternion.Euler(90f, 0f, 0f), new Vector3(52f, 12f - EdgeZ, 1f));
            Prim(PrimitiveType.Quad, "Cliff", t, cliff, new Vector3(0f, PlateauY - 15f, EdgeZ - 0.4f), Quaternion.Euler(0f, 180f, 0f), new Vector3(52f, 30f, 1f));
            for (int i = -14; i <= 14; i++)
            {
                float x = i * 1.75f + Hash(i, 1) * 0.6f;
                var size = new Vector3(1.7f + Hash(i, 2) * 0.9f, 0.7f + Hash(i, 3) * 0.8f, 1.3f + Hash(i, 4) * 0.6f);
                Rock(t, granite, new Vector3(x, PlateauY + size.y * 0.2f, EdgeZ - 0.2f + Hash(i, 5) * 0.5f), size, Hash(i, 6) * 360f);
            }

            // Sea of clouds below the edge, drifting into the fog.
            Material clouds = UnlitFog(CloudSea(new Color(0.97f, 0.97f, 0.96f), new Color(0.8f, 0.83f, 0.86f)), Color.white);
            clouds.SetTextureScale("_BaseMap", new Vector2(14f, 6f));
            Prim(PrimitiveType.Quad, "CloudSea", t, clouds, new Vector3(0f, -7f, -29f), Quaternion.Euler(90f, 0f, 0f), new Vector3(220f, 44f, 1f));

            // Granite spires rising out of the clouds, nearest first.
            Vector4[] spires =
            {
                new Vector4(-7.5f, -16f, 30f, 5f), new Vector4(-9.8f, -18f, 27f, 4f), new Vector4(8f, -20f, 33f, 6f),
                new Vector4(-15f, -26f, 38f, 7.5f), new Vector4(15f, -31f, 41f, 8.5f), new Vector4(1.5f, -38f, 44f, 9f),
                new Vector4(-25f, -42f, 47f, 10f), new Vector4(26f, -44f, 48f, 10f),
            };
            for (int i = 0; i < spires.Length; i++)
            {
                Vector4 s = spires[i];
                Spire(t, "Spire", peakRock, new Vector3(s.x, -26f, s.y), s.z, s.w, i * 17 + 3);
            }

            // The octagonal platform, a flat face toward the camera.
            Material emblem = Lit(PlumPlatformTop(), 0.12f, Vector2.one);
            Drum(t, "PlatformStep", new Vector3(0f, PlateauY - 0.02f, 0f), 3.75f, 0.1f, granite, granite, 8).transform.localRotation = Quaternion.Euler(0f, 22.5f, 0f);
            Drum(t, "Platform", new Vector3(0f, PlateauY + 0.06f, 0f), 3.4f, 0.12f, emblem, granite, 8).transform.localRotation = Quaternion.Euler(0f, 22.5f, 0f);

            Material bark = Lit(Mottled(new Color(0.2f, 0.15f, 0.13f), 21), 0.1f, new Vector2(1f, 2f));
            Material blossom = Lit(Blossom(), 0.05f, Vector2.one);
            PlumTree(t, bark, blossom, new Vector3(2.9f, PlateauY, -4.6f), 1f, 1);
            PlumTree(t, bark, blossom, new Vector3(5.3f, PlateauY, -6.6f), 0.85f, 2);
            PlumTree(t, bark, blossom, new Vector3(-6.2f, PlateauY, -6.9f), 0.9f, 3);

            Pavilion(t, granite, red, tiles, new Vector3(-3.6f, PlateauY, -5.6f));
            IncenseBurner(t, new Vector3(0.6f, PlateauY, -5.6f));
            Stele(t, tiles, new Vector3(-0.9f, PlateauY, -6.6f));
            Pine(t, new Vector3(7.2f, PlateauY, -7.6f));
            Rock(t, granite, new Vector3(-2f, PlateauY, -4.6f), new Vector3(0.9f, 0.45f, 0.7f), 40f);
            Rock(t, granite, new Vector3(4.4f, PlateauY, -3.6f), new Vector3(0.7f, 0.35f, 0.6f), 110f);

            look = new ArenaLook
            {
                SkyTop = new Color(0.8f, 0.85f, 0.88f),
                SkyHorizon = new Color(0.93f, 0.94f, 0.93f),
                Fog = new Color(0.9f, 0.92f, 0.93f),
                FogStart = 10f,
                FogEnd = 120f,
                Backdrop = PaintBackdrop("MountHuaBackdrop", 1024, 512, new Color(0.72f, 0.79f, 0.84f), new Color(0.93f, 0.94f, 0.93f), 0.38f,
                    new Vector3(0.68f, 0.78f, 0.035f), new Color(0.96f, 0.86f, 0.7f), new[]
                    {
                        new BackdropLayer { Crest = u => Peaks(u, 0.4f, 0.3f, 11, 5), Tone = new Color(0.66f, 0.7f, 0.74f), Mist = 0.95f },
                        new BackdropLayer { Crest = u => Peaks(u, 0.38f, 0.22f, 7, 9), Tone = new Color(0.46f, 0.5f, 0.54f), Mist = 0.9f },
                        new BackdropLayer { Crest = u => 0.4f + Mathf.PerlinNoise(u * 18f, 3f) * 0.025f, Tone = new Color(0.96f, 0.96f, 0.95f), Mist = 0.2f },
                    }),
            };
            return root;
        }

        // Needle-sharp peak line: count peaks of varied height and width over a flat base.
        private static float Peaks(float u, float baseV, float height, int count, int seed)
        {
            float best = baseV;
            for (int i = 0; i < count; i++)
            {
                float c = (i + 0.2f + Hash(seed, i) * 0.6f) / count;
                float w = 0.025f + Hash(seed, i + 50) * 0.04f;
                float h = height * (0.35f + Hash(seed, i + 100) * 0.65f);
                float d = Mathf.Abs(u - c) / w;
                if (d < 1f)
                    best = Mathf.Max(best, baseV + h * Mathf.Pow(1f - d, 1.6f) + Mathf.Sin(u * 400f + i) * 0.002f);
            }
            return best;
        }

        // 매화나무: a dark, crooked trunk splitting into angular branches, with clusters of
        // pink-white blossom at the branch ends.
        private static void PlumTree(Transform parent, Material bark, Material blossom, Vector3 position, float scale, int seed)
        {
            var root = new GameObject("PlumTree").transform;
            root.SetParent(parent, false);
            root.localPosition = position;
            root.localScale = Vector3.one * scale;
            root.localRotation = Quaternion.Euler(0f, Hash(seed, 1) * 360f, 0f);

            Prim(PrimitiveType.Cylinder, "Trunk", root, bark, new Vector3(0f, 0.7f, 0f), Quaternion.Euler(0f, 0f, 10f), new Vector3(0.24f, 0.72f, 0.24f));
            Prim(PrimitiveType.Cylinder, "Trunk", root, bark, new Vector3(-0.25f, 1.8f, 0f), Quaternion.Euler(0f, 0f, -25f), new Vector3(0.17f, 0.55f, 0.17f));
            Vector3[] tips =
            {
                new Vector3(0.9f, 2.6f, 0.2f), new Vector3(-1.1f, 2.9f, -0.3f), new Vector3(-0.2f, 3.3f, 0.4f),
                new Vector3(0.5f, 2.1f, -0.6f), new Vector3(-0.9f, 2.2f, 0.6f),
            };
            var fork = new Vector3(-0.45f, 2.25f, 0f);
            for (int i = 0; i < tips.Length; i++)
            {
                Vector3 tip = tips[i] + new Vector3(Hash(seed, i + 10) - 0.5f, Hash(seed, i + 20) * 0.4f, Hash(seed, i + 30) - 0.5f) * 0.4f;
                Vector3 from = i < 3 ? fork : new Vector3(0.05f, 1.35f, 0f);
                Vector3 mid = (from + tip) * 0.5f;
                Prim(PrimitiveType.Cylinder, "Branch", root, bark, mid, Quaternion.FromToRotation(Vector3.up, tip - from), new Vector3(0.07f, (tip - from).magnitude * 0.5f, 0.07f));
                for (int k = 0; k < 3; k++)
                {
                    Vector3 offset = new Vector3(Hash(seed * 7 + i, k) - 0.5f, Hash(seed * 7 + i, k + 5) - 0.5f, Hash(seed * 7 + i, k + 9) - 0.5f) * 0.55f;
                    float s = 0.42f + Hash(seed + i, k + 13) * 0.3f;
                    Prim(PrimitiveType.Sphere, "Blossom", root, blossom, Vector3.Lerp(mid, tip, 0.55f + k * 0.22f) + offset, Quaternion.Euler(0f, k * 40f, 0f), new Vector3(s, s * 0.75f, s));
                }
            }
        }

        // 정자: a square pavilion on a stone base, four red pillars, a red beam frame and a
        // pyramid roof with lifted corners.
        private static void Pavilion(Transform parent, Material stone, Material red, Material tiles, Vector3 position)
        {
            var root = new GameObject("Pavilion").transform;
            root.SetParent(parent, false);
            root.localPosition = position;
            root.localRotation = Quaternion.Euler(0f, 12f, 0f);
            const float half = 1.15f, height = 2.3f, baseHeight = 0.35f;
            Prim(PrimitiveType.Cube, "Base", root, stone, new Vector3(0f, baseHeight * 0.5f, 0f), Quaternion.identity, new Vector3(half * 2f + 0.8f, baseHeight, half * 2f + 0.8f));
            for (int i = 0; i < 4; i++)
            {
                float px = i % 2 == 0 ? -half : half, pz = i < 2 ? -half : half;
                Prim(PrimitiveType.Cylinder, "Pillar", root, red, new Vector3(px, baseHeight + height * 0.5f, pz), Quaternion.identity, new Vector3(0.2f, height * 0.5f, 0.2f));
            }
            float beamY = baseHeight + height - 0.1f;
            Prim(PrimitiveType.Cube, "Beam", root, red, new Vector3(0f, beamY, half), Quaternion.identity, new Vector3(half * 2f + 0.3f, 0.22f, 0.16f));
            Prim(PrimitiveType.Cube, "Beam", root, red, new Vector3(0f, beamY, -half), Quaternion.identity, new Vector3(half * 2f + 0.3f, 0.22f, 0.16f));
            Prim(PrimitiveType.Cube, "Beam", root, red, new Vector3(half, beamY, 0f), Quaternion.identity, new Vector3(0.16f, 0.22f, half * 2f + 0.3f));
            Prim(PrimitiveType.Cube, "Beam", root, red, new Vector3(-half, beamY, 0f), Quaternion.identity, new Vector3(0.16f, 0.22f, half * 2f + 0.3f));
            HipRoof(root, "Roof", tiles, new Vector3(0f, beamY + 0.1f, 0f), half * 2f + 1.6f, half * 2f + 1.6f, 1.3f, 0.35f);
            Prim(PrimitiveType.Sphere, "Finial", root, Lit(null, 0.5f, Vector2.one, new Color(0.55f, 0.42f, 0.2f)), new Vector3(0f, beamY + 1.5f, 0f), Quaternion.identity, Vector3.one * 0.22f);
        }

        // 향로: a three-legged bronze burner with a lid.
        private static void IncenseBurner(Transform parent, Vector3 position)
        {
            var root = new GameObject("IncenseBurner").transform;
            root.SetParent(parent, false);
            root.localPosition = position;
            Material bronze = Lit(null, 0.55f, Vector2.one, new Color(0.28f, 0.24f, 0.17f));
            bronze.SetFloat("_Metallic", 0.6f);
            for (int i = 0; i < 3; i++)
            {
                float a = i * 120f * Mathf.Deg2Rad;
                Prim(PrimitiveType.Cylinder, "Leg", root, bronze, new Vector3(Mathf.Cos(a) * 0.22f, 0.15f, Mathf.Sin(a) * 0.22f), Quaternion.identity, new Vector3(0.07f, 0.15f, 0.07f));
            }
            Prim(PrimitiveType.Sphere, "Bowl", root, bronze, new Vector3(0f, 0.45f, 0f), Quaternion.identity, new Vector3(0.7f, 0.42f, 0.7f));
            Prim(PrimitiveType.Cylinder, "Rim", root, bronze, new Vector3(0f, 0.64f, 0f), Quaternion.identity, new Vector3(0.6f, 0.03f, 0.6f));
            Prim(PrimitiveType.Sphere, "Lid", root, bronze, new Vector3(0f, 0.72f, 0f), Quaternion.identity, new Vector3(0.42f, 0.24f, 0.42f));
        }

        // A stone stele (비석) on a plinth, capped with a small tiled roof.
        private static void Stele(Transform parent, Material tiles, Vector3 position)
        {
            var root = new GameObject("Stele").transform;
            root.SetParent(parent, false);
            root.localPosition = position;
            root.localRotation = Quaternion.Euler(0f, -10f, 0f);
            Material dark = Lit(Granite(new Color(0.42f, 0.42f, 0.42f), 12), 0.2f, Vector2.one);
            Prim(PrimitiveType.Cube, "Plinth", root, dark, new Vector3(0f, 0.18f, 0f), Quaternion.identity, new Vector3(1.1f, 0.36f, 0.6f));
            Prim(PrimitiveType.Cube, "Slab", root, dark, new Vector3(0f, 1.2f, 0f), Quaternion.identity, new Vector3(0.75f, 1.7f, 0.22f));
            HipRoof(root, "Cap", tiles, new Vector3(0f, 2.05f, 0f), 1.1f, 0.5f, 0.3f, 0.08f);
        }

        // Platform top: pale granite with a double border ring, eight radial grooves and a
        // five-petalled plum blossom carved at the centre.
        private static Texture2D PlumPlatformTop()
        {
            const int size = 512;
            var light = new Color(0.78f, 0.77f, 0.74f);
            var groove = new Color(0.42f, 0.41f, 0.4f);
            var petal = new Color(0.7f, 0.5f, 0.52f);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = (x + 0.5f) / size * 2f - 1f, py = (y + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(px * px + py * py);
                    float angle = Mathf.Atan2(py, px);
                    float mottle = Mathf.PerlinNoise(x * 0.03f, y * 0.03f) * 0.12f - 0.06f + (Hash(x, y) > 0.95f ? -0.08f : 0f);
                    Color c = light * (1f + mottle);

                    if (Mathf.Abs(r - 0.88f) < 0.01f || Mathf.Abs(r - 0.83f) < 0.005f)
                        c = groove;
                    // Radial joints between the eight slabs (through the octagon's corners).
                    float slabAngle = Mathf.Repeat(angle * Mathf.Rad2Deg + 22.5f, 45f);
                    if (r > 0.42f && r < 0.83f && Mathf.Min(slabAngle, 45f - slabAngle) * Mathf.Deg2Rad * r < 0.006f)
                        c = groove;

                    // Plum blossom: five round petals around a small centre, with a carved outline.
                    const float petalR = 0.1f, petalDist = 0.17f;
                    float best = 10f;
                    for (int k = 0; k < 5; k++)
                    {
                        float a = Mathf.PI * 0.5f + k * Mathf.PI * 2f / 5f;
                        best = Mathf.Min(best, Vector2.Distance(new Vector2(px, py), new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * petalDist));
                    }
                    if (best < petalR)
                        c = Color.Lerp(c, petal, 0.55f);
                    if (Mathf.Abs(best - petalR) < 0.008f && r > 0.06f)
                        c = groove;
                    if (r < 0.06f)
                        c = Color.Lerp(groove, new Color(0.75f, 0.6f, 0.3f), 0.5f);
                    if (Mathf.Abs(r - 0.4f) < 0.006f)
                        c = groove;
                    c.a = 1f;
                    pixels[y * size + x] = c;
                }
            }
            Texture2D texture = Finish(NewTexture("PlumPlatformTop", size, size), pixels);
            texture.wrapMode = TextureWrapMode.Clamp;
            return texture;
        }

        // Blossom clusters: soft drifts of pink and white with a few deeper-pink hearts (tiles).
        private static Texture2D Blossom()
        {
            const int size = 64;
            var pink = new Color(0.97f, 0.78f, 0.83f);
            var white = new Color(0.98f, 0.95f, 0.94f);
            var deep = new Color(0.86f, 0.5f, 0.58f);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size * Mathf.PI * 2f, w = y / (float)size * Mathf.PI * 2f;
                    float n = Mathf.PerlinNoise(Mathf.Cos(u) * 2.5f + Mathf.Sin(w) + 3f, Mathf.Sin(u) * 2.5f + Mathf.Cos(w) * 2.5f + 3f);
                    Color c = Color.Lerp(white, pink, Mathf.SmoothStep(0.35f, 0.65f, n));
                    if (Hash(x / 3, y / 3) > 0.9f)
                        c = Color.Lerp(c, deep, 0.6f);
                    pixels[y * size + x] = c;
                }
            }
            return Finish(NewTexture("Blossom", size, size), pixels);
        }
    }
}
