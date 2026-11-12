using UnityEngine;
using static Game.Client.Scenery.SceneryKit;

namespace Game.Client.Cultivation
{
    // The 단련 stage's set: a stone-paved 수련장 courtyard with a raised round dais (팔괘 and 태극
    // carved into its top), a plastered 담장 with a tiled cap, a hall roof behind it, a pine, a pair
    // of 석등 and a 병기대, in front of a painted ink-mountain sky. Everything is primitives, small
    // generated meshes and procedural textures, so it needs no assets.
    //
    // The set is laid out in a frame turned to face the stage camera (yaw), so local -Z is "behind
    // the character" as seen on screen. The dais top is at the parent's y = 0, where the character
    // sits; the courtyard is one step below it.
    public static class TrainingGround
    {
        private const float GroundY = -0.16f;
        private const float DaisRadius = 1.45f;
        private const float WallZ = -7f;
        private const float WallHeight = 1.45f;

        public static GameObject Create(Transform parent, Quaternion yaw)
        {
            var root = new GameObject("TrainingGround");
            root.transform.SetParent(parent, false);
            root.transform.localRotation = yaw;
            Transform t = root.transform;

            Material paving = Lit(Paving(), 0.1f, new Vector2(12f, 12f));
            Material stone = Lit(Mottled(new Color(0.52f, 0.5f, 0.46f), 7), 0.1f, new Vector2(4f, 1f));
            Texture2D tileTexture = RoofTiles();
            Material tiles = Lit(tileTexture, 0.25f, Vector2.one);
            Material wood = Lit(Mottled(new Color(0.3f, 0.19f, 0.11f), 3), 0.2f, Vector2.one);

            // Courtyard paving, one step below the dais.
            GameObject ground = Prim(PrimitiveType.Quad, "Courtyard", t, paving, new Vector3(0f, GroundY, -2f), Quaternion.Euler(90f, 0f, 0f), new Vector3(24f, 24f, 1f));
            ground.GetComponent<Renderer>().receiveShadows = true;

            // Two-step round dais; the carved top is its own submesh.
            Material carved = Lit(DaisTop(), 0.12f, Vector2.one);
            Drum(t, "DaisStep", new Vector3(0f, GroundY, 0f), DaisRadius + 0.3f, 0.08f, stone, stone);
            Drum(t, "Dais", new Vector3(0f, GroundY + 0.08f, 0f), DaisRadius, 0.08f, carved, stone);

            // 담장: plaster over a stone footing, capped with a tiled ridge.
            Material plaster = Lit(Plaster(), 0.05f, new Vector2(12f, 1f));
            Prim(PrimitiveType.Cube, "Wall", t, plaster, new Vector3(0f, GroundY + WallHeight * 0.5f, WallZ), Quaternion.identity, new Vector3(24f, WallHeight, 0.35f));
            Prim(PrimitiveType.Cube, "WallCap", t, Lit(tileTexture, 0.25f, new Vector2(24f, 1f)), new Vector3(0f, GroundY + WallHeight, WallZ), Quaternion.Euler(45f, 0f, 0f), new Vector3(24f, 0.42f, 0.42f));

            // A hall behind the wall: lattice-and-pillar facade under a hip roof with lifted eaves.
            Material facade = Lit(HallFacade(), 0.1f, new Vector2(4f, 1f));
            Prim(PrimitiveType.Cube, "Hall", t, facade, new Vector3(-1.6f, GroundY + 1.3f, -10.5f), Quaternion.identity, new Vector3(7f, 2.6f, 3.5f));
            HipRoof(t, "HallRoof", tiles, new Vector3(-1.6f, GroundY + 2.6f, -10.5f), 8.6f, 5f, 1.6f, 0.35f);
            Prim(PrimitiveType.Cube, "HallRidge", t, Lit(null, 0.2f, Vector2.one, TileDark), new Vector3(-1.6f, GroundY + 4.22f, -10.5f), Quaternion.identity, new Vector3(3.9f, 0.14f, 0.18f));

            Pine(t, new Vector3(2.4f, GroundY, -5.4f));
            StoneLantern(t, stone, tiles, new Vector3(-1.6f, GroundY, -4f));
            StoneLantern(t, stone, tiles, new Vector3(1.2f, GroundY, -4.8f));
            WeaponRack(t, wood, new Vector3(1.85f, GroundY, -2.5f), -30f);

            // Painted sky with ink mountains, far behind everything (unlit).
            var skyMaterial = new Material(Shader.Find("Sprites/Default")) { name = "TrainingSky", mainTexture = Sky() };
            Prim(PrimitiveType.Quad, "Sky", t, skyMaterial, new Vector3(0f, 7f, -26f), Quaternion.identity, new Vector3(44f, 22f, 1f));

            return root;
        }

        // ---- textures ----------------------------------------------------------------------

        // Dais top: worn stone with a double border ring, the eight trigrams and a 태극 in the middle,
        // all cut in as darker grooves.
        private static Texture2D DaisTop()
        {
            const int size = 512;
            // Trigram lines from the inside out, 1 = solid (양), 0 = broken (음).
            int[] trigrams = { 0b111, 0b110, 0b101, 0b100, 0b011, 0b010, 0b001, 0b000 };
            var light = new Color(0.6f, 0.57f, 0.52f);
            var groove = new Color(0.25f, 0.23f, 0.21f);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = (x + 0.5f) / size * 2f - 1f, py = (y + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(px * px + py * py);
                    float mottle = Mathf.PerlinNoise(x * 0.025f, y * 0.025f) * 0.14f - 0.07f + Mathf.PerlinNoise(x * 0.2f, y * 0.2f) * 0.05f;
                    Color c = light * (1f + mottle);
                    float cut = 0f;

                    if (Mathf.Abs(r - 0.94f) < 0.012f || Mathf.Abs(r - 0.885f) < 0.006f)
                        cut = 1f;

                    float angle = Mathf.Atan2(py, px) * Mathf.Rad2Deg;
                    for (int k = 0; k < 8; k++)
                    {
                        float d = Mathf.Abs(Mathf.DeltaAngle(angle, 90f + k * 45f));
                        if (d > 15f)
                            continue;
                        for (int line = 0; line < 3; line++)
                        {
                            float lr = 0.62f + line * 0.075f;
                            if (Mathf.Abs(r - lr) >= 0.022f)
                                continue;
                            bool solid = (trigrams[k] >> (2 - line) & 1) == 1;
                            if (solid || d > 3.5f)
                                cut = 0.85f;
                        }
                    }

                    const float tr = 0.3f;
                    if (Mathf.Abs(r - tr) < 0.008f)
                        cut = 1f;
                    else if (r < tr)
                    {
                        float top = Vector2.Distance(new Vector2(px, py), new Vector2(0f, tr * 0.5f));
                        float bottom = Vector2.Distance(new Vector2(px, py), new Vector2(0f, -tr * 0.5f));
                        bool dark = px > 0f;
                        if (top < tr * 0.5f) dark = false;
                        if (bottom < tr * 0.5f) dark = true;
                        if (top < tr * 0.12f) dark = true;
                        if (bottom < tr * 0.12f) dark = false;
                        if (dark)
                            cut = 0.55f;
                    }

                    // Weathered edge of the disc.
                    if (r > 0.97f)
                        c *= 0.85f;
                    pixels[y * size + x] = Color.Lerp(c, groove * (1f + mottle), cut);
                }
            }
            return Finish(NewTexture("DaisTop", size, size), pixels);
        }

        // Dusk sky over layered ink-wash ridges with a vermilion sun, matching the title screen.
        private static Texture2D Sky()
        {
            const int width = 1024, height = 512;
            var top = new Color(0.17f, 0.17f, 0.18f);
            var horizon = new Color(0.72f, 0.66f, 0.55f);
            var sun = new Color(0.78f, 0.26f, 0.16f);
            var sunCentre = new Vector2(0.24f * width, 0.6f * height);
            float sunRadius = 0.035f * width;
            // Ridges back to front: base height (fraction of texture), amplitude, phase, ink tone.
            Vector4[] ridges =
            {
                new Vector4(0.47f, 0.06f, 3.9f, 0.6f),
                new Vector4(0.41f, 0.05f, 1.7f, 0.42f),
                new Vector4(0.35f, 0.04f, 0.4f, 0.24f),
            };
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                float v = y / (float)height;
                Color sky = Color.Lerp(horizon, top, Mathf.SmoothStep(0.3f, 1f, v));
                for (int x = 0; x < width; x++)
                {
                    float u = x / (float)width;
                    Color c = sky;
                    float sd = Vector2.Distance(new Vector2(x, y), sunCentre);
                    if (sd < sunRadius + 1.5f)
                        c = Color.Lerp(sun, c, Mathf.Clamp01(sd - sunRadius + 0.5f));
                    else
                        c = Color.Lerp(c, sun, Mathf.Clamp01(1f - (sd - sunRadius) / (sunRadius * 3f)) * 0.18f);

                    foreach (Vector4 ridge in ridges)
                    {
                        float z = u * 14f;
                        float wave = Mathf.Sin(z * 0.55f + ridge.z) * 0.55f + Mathf.Sin(z * 1.37f + ridge.z * 2.3f) * 0.3f
                                   + Mathf.Sin(z * 3.1f + ridge.z * 0.7f) * 0.15f + (Mathf.PerlinNoise(x * 0.02f, ridge.z) - 0.5f) * 0.15f;
                        float crest = ridge.x + wave * ridge.y;
                        if (v > crest)
                            continue;
                        // Ink pooled at the crest, washing out downward into mist.
                        float depth = (crest - v) / crest;
                        float ink = Mathf.Lerp(1f, 0.55f, Mathf.SmoothStep(0f, 0.6f, depth));
                        float soft = Mathf.Clamp01((crest - v) * height / 3f);
                        Color tone = Color.Lerp(horizon, new Color(ridge.w, ridge.w, ridge.w * 1.02f), ink);
                        c = Color.Lerp(c, tone, soft);
                    }
                    pixels[y * width + x] = c;
                }
            }
            Texture2D texture = Finish(NewTexture("TrainingSky", width, height), pixels);
            texture.wrapMode = TextureWrapMode.Clamp;
            return texture;
        }
    }
}
