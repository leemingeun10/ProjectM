using UnityEngine;
using static Game.Client.Scenery.SceneryKit;

namespace Game.Client.Scenery
{
    // 2장 연무장 (also the 무공 제작 and 비무 set): the fighters stand on a raised stone 연무대 with
    // a 태극 carved at its centre, flanked by two braziers. Behind it a 담장 opens through a red
    // gate onto a large hall; weapon racks, a war drum, blue banners, 석등 and pines fill the
    // courtyard. The painted ink mountains (InkWashBackground's image) stay as the backdrop.
    //
    // Local frame: origin between the fighters on the 연무대, -Z away from the camera; local -X is
    // screen right.
    public static class TrainingHallArena
    {
        private const float CourtY = -0.3f;
        private const float WallZ = -10f;
        private const float WallHeight = 2.2f;
        private const float GateHalfWidth = 2.8f;
        private static readonly Color Red = new Color(0.5f, 0.15f, 0.1f);

        public static GameObject Create(Transform parent, out ArenaLook look)
        {
            var root = new GameObject("TrainingHallArena");
            root.transform.SetParent(parent, false);
            Transform t = root.transform;

            Material paving = Lit(Paving(), 0.1f, new Vector2(40f, 32f));
            Material stone = Lit(Mottled(new Color(0.52f, 0.5f, 0.46f), 7), 0.1f, new Vector2(4f, 1f));
            Material wood = Lit(Mottled(new Color(0.3f, 0.19f, 0.11f), 3), 0.2f, Vector2.one);
            Texture2D tileTexture = RoofTiles();
            Material tiles = Lit(tileTexture, 0.25f, Vector2.one);
            Material red = Lit(Mottled(Red, 15), 0.25f, Vector2.one);

            Prim(PrimitiveType.Quad, "Courtyard", t, paving, new Vector3(0f, CourtY, -14f), Quaternion.Euler(90f, 0f, 0f), new Vector3(80f, 64f, 1f));

            // 연무대: a stone block whose carved top is a separate quad (the block's sides keep the
            // plain stone), with a low step at the front.
            const float platformWidth = 10f, platformDepth = 6.4f;
            Prim(PrimitiveType.Cube, "Platform", t, stone, new Vector3(0f, CourtY * 0.5f, 0f), Quaternion.identity, new Vector3(platformWidth, -CourtY, platformDepth));
            Prim(PrimitiveType.Quad, "PlatformTop", t, Lit(PlatformTop(), 0.12f, Vector2.one), new Vector3(0f, 0.002f, 0f), Quaternion.Euler(90f, 0f, 0f), new Vector3(platformWidth, platformDepth, 1f));
            Prim(PrimitiveType.Cube, "Step", t, stone, new Vector3(0f, CourtY * 0.75f, platformDepth * 0.5f + 0.3f), Quaternion.identity, new Vector3(4f, -CourtY * 0.5f, 0.6f));

            Brazier(t, new Vector3(2.7f, 0f, -2.7f));
            Brazier(t, new Vector3(-2.7f, 0f, -2.7f));

            // 담장 either side of the gate.
            Material plaster = Lit(Plaster(), 0.05f, new Vector2(9f, 1f));
            Material cap = Lit(tileTexture, 0.25f, new Vector2(18f, 1f));
            for (int side = -1; side <= 1; side += 2)
            {
                float cx = side * (GateHalfWidth + 9f);
                Prim(PrimitiveType.Cube, "Wall", t, plaster, new Vector3(cx, CourtY + WallHeight * 0.5f, WallZ), Quaternion.identity, new Vector3(18f, WallHeight, 0.4f));
                Prim(PrimitiveType.Cube, "WallCap", t, cap, new Vector3(cx, CourtY + WallHeight, WallZ), Quaternion.Euler(45f, 0f, 0f), new Vector3(18f, 0.46f, 0.46f));
            }
            Gate(t, red, tiles);

            Hall(t, stone, red, tiles);

            WeaponRack(t, wood, new Vector3(3.6f, CourtY, -6.2f), 15f);
            WeaponRack(t, wood, new Vector3(5.2f, CourtY, -7.4f), 25f);
            WarDrum(t, wood, red, new Vector3(-3.7f, CourtY, -6.4f));
            StoneLantern(t, stone, tiles, new Vector3(2.1f, CourtY, -8.9f));
            StoneLantern(t, stone, tiles, new Vector3(-2.1f, CourtY, -8.9f));
            Material bannerCloth = UnlitFog(Banner(new Color(0.16f, 0.24f, 0.4f), 7), Color.white);
            BannerPole(t, wood, bannerCloth, new Vector3(5.6f, CourtY, -9.2f), 5.2f, 0.8f, 3f);
            BannerPole(t, wood, bannerCloth, new Vector3(-6.4f, CourtY, -9.2f), 5.2f, 0.8f, 3f);
            Pine(t, new Vector3(8.5f, CourtY, -12.5f));
            Pine(t, new Vector3(-9f, CourtY, -13f));

            look = new ArenaLook
            {
                SkyTop = Hex(0xE8E4D9),
                SkyHorizon = Hex(0xF2DEC4),
                Fog = Hex(0xEEE2D0),
                FogStart = 15f,
                FogEnd = 160f,
                Backdrop = null,
            };
            return root;
        }

        // A bronze brazier on a stone post, with a flame that lights the platform.
        private static void Brazier(Transform parent, Vector3 position)
        {
            var root = new GameObject("Brazier").transform;
            root.SetParent(parent, false);
            root.localPosition = position;
            Material bronze = Lit(null, 0.55f, Vector2.one, new Color(0.3f, 0.24f, 0.16f));
            bronze.SetFloat("_Metallic", 0.6f);
            Prim(PrimitiveType.Cylinder, "Post", root, bronze, new Vector3(0f, 0.45f, 0f), Quaternion.identity, new Vector3(0.18f, 0.45f, 0.18f));
            Prim(PrimitiveType.Cylinder, "Bowl", root, bronze, new Vector3(0f, 0.95f, 0f), Quaternion.identity, new Vector3(0.62f, 0.08f, 0.62f));
            Prim(PrimitiveType.Sphere, "Flame", root, UnlitFog(null, new Color(1f, 0.62f, 0.25f)), new Vector3(0f, 1.18f, 0f), Quaternion.identity, new Vector3(0.38f, 0.5f, 0.38f));
            Prim(PrimitiveType.Sphere, "FlameCore", root, UnlitFog(null, new Color(1f, 0.88f, 0.55f)), new Vector3(0f, 1.12f, 0.08f), Quaternion.identity, new Vector3(0.2f, 0.3f, 0.2f));
            var lightGo = new GameObject("BrazierLight");
            lightGo.transform.SetParent(root, false);
            lightGo.transform.localPosition = new Vector3(0f, 1.5f, 0.3f);
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.66f, 0.38f);
            light.intensity = 1.2f;
            light.range = 4.5f;
            light.shadows = LightShadows.None;
        }

        // 일주문-style gate in the wall gap: two red pillars, a lintel with a name board and a
        // small tiled roof.
        private static void Gate(Transform parent, Material red, Material tiles)
        {
            float y = CourtY;
            for (int side = -1; side <= 1; side += 2)
            {
                Prim(PrimitiveType.Cylinder, "GatePillar", parent, red, new Vector3(side * GateHalfWidth, y + 1.75f, WallZ), Quaternion.identity, new Vector3(0.36f, 1.75f, 0.36f));
                Prim(PrimitiveType.Cube, "PillarBase", parent, Lit(null, 0.1f, Vector2.one, new Color(0.45f, 0.43f, 0.4f)), new Vector3(side * GateHalfWidth, y + 0.15f, WallZ), Quaternion.identity, new Vector3(0.6f, 0.3f, 0.6f));
            }
            Prim(PrimitiveType.Cube, "Lintel", parent, red, new Vector3(0f, y + 3.3f, WallZ), Quaternion.identity, new Vector3(GateHalfWidth * 2f + 0.8f, 0.32f, 0.4f));
            Prim(PrimitiveType.Cube, "LintelBand", parent, Lit(null, 0.2f, Vector2.one, new Color(0.2f, 0.38f, 0.33f)), new Vector3(0f, y + 3.05f, WallZ), Quaternion.identity, new Vector3(GateHalfWidth * 2f + 0.6f, 0.14f, 0.42f));
            NameBoard(parent, new Vector3(0f, y + 3.85f, WallZ + 0.22f), new Vector2(1.9f, 0.7f));
            HipRoof(parent, "GateRoof", tiles, new Vector3(0f, y + 3.46f, WallZ), GateHalfWidth * 2f + 2f, 1.8f, 0.9f, 0.3f);
        }

        // The main hall: a stone terrace with steps, a red-pillared lattice front, a 현판 and a
        // big hip roof with strongly lifted eaves.
        private static void Hall(Transform parent, Material stone, Material red, Material tiles)
        {
            const float z = -17f, width = 15f, depth = 6.5f, terrace = 1.1f, body = 4.2f;
            float y = CourtY;
            Prim(PrimitiveType.Cube, "Terrace", parent, stone, new Vector3(0f, y + terrace * 0.5f, z), Quaternion.identity, new Vector3(width + 2f, terrace, depth + 2f));
            for (int i = 0; i < 4; i++)
                Prim(PrimitiveType.Cube, "HallStep", parent, stone, new Vector3(0f, y + terrace * (i + 0.5f) / 4f, z + depth * 0.5f + 1f + (3 - i) * 0.3f + 0.15f), Quaternion.identity, new Vector3(4f, terrace / 4f, 0.3f));

            Material facade = Lit(HallFacade(), 0.1f, new Vector2(6f, 1f));
            Prim(PrimitiveType.Cube, "Hall", parent, facade, new Vector3(0f, y + terrace + body * 0.5f, z), Quaternion.identity, new Vector3(width, body, depth));
            float roofY = y + terrace + body;
            HipRoof(parent, "HallRoof", tiles, new Vector3(0f, roofY, z), width + 3.4f, depth + 3f, 2.6f, 0.75f);
            Prim(PrimitiveType.Cube, "HallRidge", parent, Lit(null, 0.2f, Vector2.one, TileDark), new Vector3(0f, roofY + 2.62f, z), Quaternion.identity, new Vector3(width - depth + 0.2f, 0.22f, 0.26f));
            NameBoard(parent, new Vector3(0f, roofY - 0.55f, z + depth * 0.5f + 0.06f), new Vector2(2.6f, 0.9f));
        }

        // 현판: a dark board with a gold frame and pale strokes for its characters.
        private static void NameBoard(Transform parent, Vector3 position, Vector2 size)
        {
            Prim(PrimitiveType.Cube, "NameBoardFrame", parent, Lit(null, 0.4f, Vector2.one, new Color(0.62f, 0.48f, 0.2f)), position, Quaternion.identity, new Vector3(size.x + 0.12f, size.y + 0.12f, 0.06f));
            Prim(PrimitiveType.Quad, "NameBoard", parent, UnlitFog(BoardText(), Color.white), position + new Vector3(0f, 0f, 0.035f), Quaternion.Euler(0f, 180f, 0f), new Vector3(size.x, size.y, 1f));
        }

        // A war drum (북) on a wooden stand, its skin facing the platform.
        private static void WarDrum(Transform parent, Material wood, Material red, Vector3 position)
        {
            var root = new GameObject("WarDrum").transform;
            root.SetParent(parent, false);
            root.localPosition = position;
            root.localRotation = Quaternion.Euler(0f, -35f, 0f);
            Prim(PrimitiveType.Cube, "Leg", root, wood, new Vector3(-0.75f, 0.8f, 0f), Quaternion.identity, new Vector3(0.12f, 1.6f, 0.12f));
            Prim(PrimitiveType.Cube, "Leg", root, wood, new Vector3(0.75f, 0.8f, 0f), Quaternion.identity, new Vector3(0.12f, 1.6f, 0.12f));
            Prim(PrimitiveType.Cube, "Foot", root, wood, new Vector3(-0.75f, 0.04f, 0f), Quaternion.identity, new Vector3(0.16f, 0.08f, 0.8f));
            Prim(PrimitiveType.Cube, "Foot", root, wood, new Vector3(0.75f, 0.04f, 0f), Quaternion.identity, new Vector3(0.16f, 0.08f, 0.8f));
            Prim(PrimitiveType.Cylinder, "Drum", root, red, new Vector3(0f, 1.2f, 0f), Quaternion.Euler(90f, 0f, 0f), new Vector3(1.2f, 0.32f, 1.2f));
            Material skin = Lit(null, 0.15f, Vector2.one, new Color(0.82f, 0.74f, 0.58f));
            Prim(PrimitiveType.Cylinder, "Skin", root, skin, new Vector3(0f, 1.2f, 0.33f), Quaternion.Euler(90f, 0f, 0f), new Vector3(1.05f, 0.01f, 1.05f));
            Prim(PrimitiveType.Cylinder, "Skin", root, skin, new Vector3(0f, 1.2f, -0.33f), Quaternion.Euler(90f, 0f, 0f), new Vector3(1.05f, 0.01f, 1.05f));
        }

        // Top of the 연무대 (10 x 6.4 m): a dark granite border band, large pale slabs in a grid
        // and a carved circle with a 태극 at the centre.
        private static Texture2D PlatformTop()
        {
            const int width = 500, height = 320;
            const float border = 22f;
            var slab = new Color(0.62f, 0.59f, 0.54f);
            var rim = new Color(0.36f, 0.34f, 0.32f);
            var groove = new Color(0.27f, 0.25f, 0.23f);
            var pixels = new Color[width * height];
            var centre = new Vector2(width * 0.5f, height * 0.5f);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float mottle = Mathf.PerlinNoise(x * 0.03f, y * 0.03f) * 0.12f - 0.06f + Mathf.PerlinNoise(x * 0.25f, y * 0.25f) * 0.05f;
                    float edge = Mathf.Min(Mathf.Min(x, width - 1 - x), Mathf.Min(y, height - 1 - y));
                    Color c;
                    if (edge < border)
                    {
                        c = rim * (1f + mottle);
                        // Joints across the border band every 50 px.
                        if ((edge > 3f) && ((x % 50 < 2 && (y < border || y >= height - border)) || (y % 40 < 2 && (x < border || x >= width - border))))
                            c = groove;
                    }
                    else
                    {
                        float ix = x - border, iy = y - border;
                        float cellW = (width - border * 2f) / 5f, cellH = (height - border * 2f) / 3f;
                        bool joint = ix % cellW < 1.6f || iy % cellH < 1.6f || edge < border + 3f;
                        float v = Hash((int)(ix / cellW), (int)(iy / cellH));
                        c = joint ? groove : Color.Lerp(slab, slab * 0.9f, v) * (1f + mottle);
                    }

                    float r = Vector2.Distance(new Vector2(x, y), centre);
                    const float tr = 62f;
                    if (Mathf.Abs(r - tr - 14f) < 2f || Mathf.Abs(r - tr) < 1.6f)
                        c = groove;
                    else if (r < tr)
                    {
                        float px = (x - centre.x) / tr, py = (y - centre.y) / tr;
                        float top = Vector2.Distance(new Vector2(px, py), new Vector2(0f, 0.5f));
                        float bottom = Vector2.Distance(new Vector2(px, py), new Vector2(0f, -0.5f));
                        bool dark = px > 0f;
                        if (top < 0.5f) dark = false;
                        if (bottom < 0.5f) dark = true;
                        if (top < 0.13f) dark = true;
                        if (bottom < 0.13f) dark = false;
                        c = dark ? Color.Lerp(c, groove, 0.65f) : slab * (1.05f + mottle);
                    }
                    c.a = 1f;
                    pixels[y * width + x] = c;
                }
            }
            Texture2D texture = Finish(NewTexture("PlatformTop", width, height), pixels);
            texture.wrapMode = TextureWrapMode.Clamp;
            return texture;
        }

        // The name board's face: dark lacquer with three pale characters made of bold strokes.
        private static Texture2D BoardText()
        {
            const int width = 192, height = 64;
            var lacquer = new Color(0.12f, 0.1f, 0.09f);
            var gold = new Color(0.86f, 0.74f, 0.42f);
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int glyph = x / 64;
                    float gx = x % 64 - 32f, gy = y - 32f;
                    bool stroke = (Mathf.Abs(gy - 12f) < 3f && Mathf.Abs(gx) < 18f)
                               || (Mathf.Abs(gy + 2f * glyph - 1f) < 3f && Mathf.Abs(gx) < 14f + glyph * 2f)
                               || (Mathf.Abs(gx + (glyph - 1) * 6f) < 3f && Mathf.Abs(gy) < 20f)
                               || (Mathf.Abs(gy + 16f) < 3f && Mathf.Abs(gx - glyph * 3f) < 16f);
                    pixels[y * width + x] = stroke ? gold : lacquer;
                }
            }
            Texture2D texture = Finish(NewTexture("BoardText", width, height), pixels);
            texture.wrapMode = TextureWrapMode.Clamp;
            return texture;
        }

        private static Color Hex(int rgb) => new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);
    }
}
