using UnityEngine;
using static Game.Client.Scenery.SceneryKit;

namespace Game.Client.Scenery
{
    // 1장 저잣거리: the fighters meet on a rutted dirt road. Behind the curb are market stalls,
    // barrels and a cart in front of a row of shops (a two-storey 객잔 in the middle) with shop
    // banners and red lanterns; farther back, village roofs and a pagoda fade into the haze over
    // low painted hills.
    //
    // Local frame: origin between the fighters on the ground, -Z away from the camera; the
    // camera looks down -Z, so local -X is screen right.
    public static class StreetArena
    {
        private const float ShopFrontZ = -7.4f;

        public static GameObject Create(Transform parent, out ArenaLook look)
        {
            var root = new GameObject("StreetArena");
            root.transform.SetParent(parent, false);
            Transform t = root.transform;

            Material earth = Lit(Earth(new Color(0.56f, 0.46f, 0.35f), 3), 0.05f, new Vector2(60f, 48f));
            Material road = Lit(Road(), 0.05f, new Vector2(15f, 1f));
            Material stone = Lit(Mottled(new Color(0.5f, 0.48f, 0.44f), 7), 0.1f, Vector2.one);
            Material wood = Lit(Mottled(new Color(0.42f, 0.29f, 0.18f), 3), 0.2f, Vector2.one);
            Material darkWood = Lit(Mottled(new Color(0.24f, 0.16f, 0.1f), 9), 0.2f, Vector2.one);
            Texture2D tileTexture = RoofTiles();
            Material tiles = Lit(tileTexture, 0.25f, Vector2.one);
            Texture2D front = ShopFront();
            Material plaster = Lit(Plaster(), 0.05f, new Vector2(2f, 1f));
            Material lantern = UnlitFog(null, new Color(0.95f, 0.36f, 0.2f));

            Prim(PrimitiveType.Quad, "Ground", t, earth, new Vector3(0f, -0.01f, -14f), Quaternion.Euler(90f, 0f, 0f), new Vector3(90f, 72f, 1f));
            Prim(PrimitiveType.Quad, "Road", t, road, new Vector3(0f, 0f, 0.2f), Quaternion.Euler(90f, 0f, 0f), new Vector3(60f, 5.2f, 1f));

            // Curb stones along the back edge of the road.
            for (int i = -26; i <= 26; i++)
            {
                float w = 0.45f + Hash(i, 1) * 0.2f;
                Prim(PrimitiveType.Cube, "Curb", t, stone, new Vector3(i * 0.62f, 0.05f, -2.45f + Hash(i, 2) * 0.06f), Quaternion.Euler(0f, Hash(i, 3) * 8f - 4f, 0f), new Vector3(w, 0.12f + Hash(i, 4) * 0.05f, 0.32f));
            }

            // Shops, screen left (+X) to right.
            Shop(t, front, tiles, darkWood, lantern, 11.5f, 4.2f, 3.2f, false, new Color(0.22f, 0.3f, 0.45f), 1);
            Shop(t, front, tiles, darkWood, lantern, 6.8f, 4.6f, 3.4f, false, new Color(0.62f, 0.18f, 0.12f), 2);
            Shop(t, front, tiles, darkWood, lantern, 1.2f, 6f, 3f, true, new Color(0.15f, 0.15f, 0.14f), 3);
            Shop(t, front, tiles, darkWood, lantern, -4.6f, 4.8f, 3.3f, false, new Color(0.55f, 0.42f, 0.16f), 4);
            Shop(t, front, tiles, darkWood, lantern, -9.6f, 4.4f, 3.1f, false, new Color(0.62f, 0.18f, 0.12f), 5);

            Material awningRed = UnlitFog(Stripes(new Color(0.75f, 0.28f, 0.2f), new Color(0.88f, 0.8f, 0.66f)), Color.white);
            Material awningBlue = UnlitFog(Stripes(new Color(0.25f, 0.33f, 0.48f), new Color(0.85f, 0.8f, 0.68f)), Color.white);
            Stall(t, wood, awningRed, new Vector3(3.4f, 0f, -4.6f), 6f, 11);
            Stall(t, wood, awningBlue, new Vector3(-3.1f, 0f, -5f), -8f, 12);

            Material barrel = Lit(Mottled(new Color(0.36f, 0.24f, 0.14f), 13), 0.25f, new Vector2(2f, 1f));
            Prim(PrimitiveType.Cylinder, "Barrel", t, barrel, new Vector3(-5.6f, 0.4f, -5.9f), Quaternion.identity, new Vector3(0.6f, 0.4f, 0.6f));
            Prim(PrimitiveType.Cylinder, "Barrel", t, barrel, new Vector3(-6.2f, 0.4f, -6.4f), Quaternion.identity, new Vector3(0.6f, 0.4f, 0.6f));
            Prim(PrimitiveType.Cylinder, "Barrel", t, barrel, new Vector3(-5.85f, 1.12f, -6.15f), Quaternion.identity, new Vector3(0.6f, 0.32f, 0.6f));
            Prim(PrimitiveType.Cube, "Crate", t, wood, new Vector3(0.4f, 0.25f, -6.4f), Quaternion.Euler(0f, 12f, 0f), new Vector3(0.5f, 0.5f, 0.5f));
            Prim(PrimitiveType.Cube, "Crate", t, wood, new Vector3(0.95f, 0.25f, -6.5f), Quaternion.Euler(0f, -6f, 0f), new Vector3(0.5f, 0.5f, 0.5f));
            Prim(PrimitiveType.Cube, "Crate", t, wood, new Vector3(0.65f, 0.72f, -6.45f), Quaternion.Euler(0f, 30f, 0f), new Vector3(0.44f, 0.44f, 0.44f));
            Cart(t, wood, darkWood, new Vector3(6.6f, 0f, -4.4f), -20f);

            Material bark = Lit(Mottled(new Color(0.3f, 0.23f, 0.17f), 11), 0.1f, new Vector2(1f, 3f));
            Material leaves = Lit(Mottled(new Color(0.33f, 0.42f, 0.25f), 6), 0.05f, Vector2.one);
            Prim(PrimitiveType.Cylinder, "Trunk", t, bark, new Vector3(-7.8f, 1.4f, -5.2f), Quaternion.Euler(0f, 0f, 6f), new Vector3(0.3f, 1.4f, 0.3f));
            Prim(PrimitiveType.Sphere, "Crown", t, leaves, new Vector3(-8f, 3.6f, -5.2f), Quaternion.identity, new Vector3(2.8f, 2f, 2.4f));
            Prim(PrimitiveType.Sphere, "Crown", t, leaves, new Vector3(-6.9f, 3.1f, -5f), Quaternion.identity, new Vector3(1.8f, 1.4f, 1.6f));

            // Village roofs and a pagoda beyond the shops, sinking into the haze.
            for (int i = 0; i < 7; i++)
            {
                float x = -18f + i * 6f + Hash(i, 21) * 2f;
                float z = -18f - Hash(i, 22) * 8f;
                float w = 4f + Hash(i, 23) * 2f;
                float h = 2.6f + Hash(i, 24) * 1.4f;
                Prim(PrimitiveType.Cube, "House", t, plaster, new Vector3(x, h * 0.5f, z), Quaternion.identity, new Vector3(w, h, 4f));
                HipRoof(t, "HouseRoof", tiles, new Vector3(x, h, z), w + 1.2f, 5.2f, 1.6f, 0.3f);
            }
            Pagoda(t, plaster, tiles, new Vector3(-6f, 0f, -36f));

            look = new ArenaLook
            {
                SkyTop = new Color(0.88f, 0.88f, 0.85f),
                SkyHorizon = new Color(0.96f, 0.9f, 0.8f),
                Fog = new Color(0.93f, 0.89f, 0.81f),
                FogStart = 14f,
                FogEnd = 110f,
                Backdrop = PaintBackdrop("StreetBackdrop", 1024, 512, new Color(0.84f, 0.86f, 0.86f), new Color(0.96f, 0.9f, 0.8f), 0.36f,
                    new Vector3(0.72f, 0.72f, 0.04f), new Color(0.95f, 0.7f, 0.5f), new[]
                    {
                        new BackdropLayer { Crest = u => Ridge(u, 0.47f, 0.06f, 1.2f), Tone = new Color(0.66f, 0.68f, 0.66f), Mist = 0.8f },
                        new BackdropLayer { Crest = u => Ridge(u, 0.42f, 0.04f, 4f, 22f), Tone = new Color(0.5f, 0.55f, 0.47f), Mist = 0.7f },
                    }),
            };
            return root;
        }

        // A shop of the given width with its front on ShopFrontZ: stone footing, a timber front,
        // a tiled roof, a banner pole and two red lanterns. twoStorey adds a pent roof and an
        // upper floor (the 객잔).
        private static void Shop(Transform parent, Texture2D front, Material tiles, Material darkWood, Material lantern,
            float x, float width, float height, bool twoStorey, Color bannerColor, int seed)
        {
            const float depth = 4f;
            float z = ShopFrontZ - depth * 0.5f;
            Material facade = Lit(front, 0.15f, new Vector2(Mathf.Round(width / 2f), 1f));
            Prim(PrimitiveType.Cube, "Footing", parent, darkWood, new Vector3(x, 0.1f, z), Quaternion.identity, new Vector3(width + 0.2f, 0.2f, depth + 0.2f));
            Prim(PrimitiveType.Cube, "Shop", parent, facade, new Vector3(x, 0.2f + height * 0.5f, z), Quaternion.identity, new Vector3(width, height, depth));

            float top = 0.2f + height;
            if (twoStorey)
            {
                Prim(PrimitiveType.Cube, "PentRoof", parent, tiles, new Vector3(x, top + 0.05f, ShopFrontZ + 0.35f), Quaternion.Euler(20f, 0f, 0f), new Vector3(width + 0.8f, 0.1f, 1.3f));
                Prim(PrimitiveType.Cube, "Upper", parent, facade, new Vector3(x, top + 1.2f, z - 0.4f), Quaternion.identity, new Vector3(width - 0.6f, 2.4f, depth - 0.8f));
                Prim(PrimitiveType.Cube, "Balcony", parent, darkWood, new Vector3(x, top + 0.45f, ShopFrontZ - 0.25f), Quaternion.identity, new Vector3(width - 0.4f, 0.12f, 0.08f));
                top += 2.4f;
                HipRoof(parent, "Roof", tiles, new Vector3(x, top, z - 0.4f), width + 0.8f, depth + 0.4f, 1.6f, 0.4f);
            }
            else
            {
                HipRoof(parent, "Roof", tiles, new Vector3(x, top, z), width + 1.2f, depth + 1.2f, 1.5f, 0.3f);
            }

            Material cloth = UnlitFog(Banner(bannerColor, seed), Color.white);
            BannerPole(parent, darkWood, cloth, new Vector3(x + width * 0.5f - 0.2f, 0f, ShopFrontZ + 0.35f), 3.6f, 0.55f, 1.7f);

            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 p = new Vector3(x + side * (width * 0.5f - 0.5f), 0.2f + height - 0.55f, ShopFrontZ + 0.35f);
                Prim(PrimitiveType.Sphere, "Lantern", parent, lantern, p, Quaternion.identity, new Vector3(0.32f, 0.42f, 0.32f));
                Prim(PrimitiveType.Cylinder, "LanternCap", parent, darkWood, p + Vector3.up * 0.22f, Quaternion.identity, new Vector3(0.16f, 0.03f, 0.16f));
                Prim(PrimitiveType.Cylinder, "LanternCap", parent, darkWood, p - Vector3.up * 0.22f, Quaternion.identity, new Vector3(0.16f, 0.03f, 0.16f));
            }
        }

        // A market stall: a table of produce and pots under a striped awning on four poles.
        private static void Stall(Transform parent, Material wood, Material awning, Vector3 position, float yaw, int seed)
        {
            var root = new GameObject("Stall").transform;
            root.SetParent(parent, false);
            root.localPosition = position;
            root.localRotation = Quaternion.Euler(0f, yaw, 0f);

            Prim(PrimitiveType.Cube, "Table", root, wood, new Vector3(0f, 0.72f, 0f), Quaternion.identity, new Vector3(1.8f, 0.08f, 0.85f));
            Prim(PrimitiveType.Cube, "Front", root, wood, new Vector3(0f, 0.36f, 0.4f), Quaternion.identity, new Vector3(1.8f, 0.7f, 0.05f));
            for (int i = 0; i < 4; i++)
            {
                float px = i % 2 == 0 ? -1f : 1f, pz = i < 2 ? -0.55f : 0.55f;
                Prim(PrimitiveType.Cylinder, "Pole", root, wood, new Vector3(px, 1.05f + (pz > 0f ? -0.12f : 0.12f), pz), Quaternion.identity, new Vector3(0.05f, 1.05f + (pz > 0f ? -0.12f : 0.12f), 0.05f));
            }
            Prim(PrimitiveType.Cube, "Awning", root, awning, new Vector3(0f, 2.1f, 0f), Quaternion.Euler(12f, 0f, 0f), new Vector3(2.3f, 0.03f, 1.5f));

            // Produce: cabbages, persimmons, radishes, and two crocks.
            Color[] produce = { new Color(0.45f, 0.62f, 0.3f), new Color(0.9f, 0.5f, 0.15f), new Color(0.9f, 0.88f, 0.8f), new Color(0.7f, 0.2f, 0.15f) };
            for (int i = 0; i < 9; i++)
            {
                Color c = produce[(int)(Hash(seed, i) * produce.Length) % produce.Length];
                float s = 0.16f + Hash(seed, i + 20) * 0.08f;
                Prim(PrimitiveType.Sphere, "Produce", root, Lit(null, 0.3f, Vector2.one, c), new Vector3(-0.7f + (i % 5) * 0.28f + Hash(seed, i + 40) * 0.08f, 0.76f + s * 0.4f, i < 5 ? 0.1f : -0.2f), Quaternion.identity, new Vector3(s, s * 0.85f, s));
            }
            Material crock = Lit(null, 0.5f, Vector2.one, new Color(0.3f, 0.2f, 0.14f));
            Prim(PrimitiveType.Sphere, "Crock", root, crock, new Vector3(0.62f, 0.95f, -0.1f), Quaternion.identity, new Vector3(0.32f, 0.38f, 0.32f));
            Prim(PrimitiveType.Sphere, "Crock", root, crock, new Vector3(1.25f, 0.2f, 0.7f), Quaternion.identity, new Vector3(0.42f, 0.46f, 0.42f));
        }

        // A two-wheeled hand cart.
        private static void Cart(Transform parent, Material wood, Material darkWood, Vector3 position, float yaw)
        {
            var root = new GameObject("Cart").transform;
            root.SetParent(parent, false);
            root.localPosition = position;
            root.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Prim(PrimitiveType.Cube, "Bed", root, wood, new Vector3(0f, 0.62f, 0f), Quaternion.Euler(0f, 0f, -6f), new Vector3(1.7f, 0.1f, 1f));
            Prim(PrimitiveType.Cube, "Side", root, wood, new Vector3(0f, 0.78f, 0.48f), Quaternion.Euler(0f, 0f, -6f), new Vector3(1.7f, 0.25f, 0.05f));
            Prim(PrimitiveType.Cube, "Side", root, wood, new Vector3(0f, 0.78f, -0.48f), Quaternion.Euler(0f, 0f, -6f), new Vector3(1.7f, 0.25f, 0.05f));
            Prim(PrimitiveType.Cylinder, "Wheel", root, darkWood, new Vector3(0.2f, 0.45f, 0.58f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.9f, 0.03f, 0.9f));
            Prim(PrimitiveType.Cylinder, "Wheel", root, darkWood, new Vector3(0.2f, 0.45f, -0.58f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.9f, 0.03f, 0.9f));
            Prim(PrimitiveType.Cube, "Handle", root, wood, new Vector3(-1.35f, 0.42f, 0.4f), Quaternion.Euler(0f, 0f, -18f), new Vector3(1.2f, 0.05f, 0.05f));
            Prim(PrimitiveType.Cube, "Handle", root, wood, new Vector3(-1.35f, 0.42f, -0.4f), Quaternion.Euler(0f, 0f, -18f), new Vector3(1.2f, 0.05f, 0.05f));
            Material sack = Lit(Mottled(new Color(0.72f, 0.64f, 0.48f), 17), 0.05f, Vector2.one);
            Prim(PrimitiveType.Sphere, "Sack", root, sack, new Vector3(0.3f, 0.86f, 0.15f), Quaternion.Euler(0f, 0f, 20f), new Vector3(0.7f, 0.42f, 0.5f));
            Prim(PrimitiveType.Sphere, "Sack", root, sack, new Vector3(-0.35f, 0.82f, -0.15f), Quaternion.Euler(0f, 30f, -10f), new Vector3(0.6f, 0.36f, 0.45f));
        }

        // A five-storey pagoda (탑), each storey narrower, with its own little roof.
        private static void Pagoda(Transform parent, Material body, Material roof, Vector3 position)
        {
            float y = position.y;
            for (int i = 0; i < 5; i++)
            {
                float w = 3.2f - i * 0.42f;
                float h = i == 0 ? 2.4f : 1.5f;
                Prim(PrimitiveType.Cube, "Storey", parent, body, new Vector3(position.x, y + h * 0.5f, position.z), Quaternion.identity, new Vector3(w, h, w));
                y += h;
                HipRoof(parent, "PagodaRoof", roof, new Vector3(position.x, y, position.z), w + 1.4f, w + 1.4f, 0.5f, 0.25f);
                y += 0.35f;
            }
            Prim(PrimitiveType.Cylinder, "Spire", parent, roof, new Vector3(position.x, y + 0.9f, position.z), Quaternion.identity, new Vector3(0.12f, 1f, 0.12f));
        }

        // Dirt road (one 4 m stretch across its full width): lighter packed earth with two dark
        // wheel ruts running along U.
        private static Texture2D Road()
        {
            const int width = 128, height = 64;
            var baseColor = new Color(0.66f, 0.56f, 0.43f);
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float u = x / (float)width * Mathf.PI * 2f;
                    float n = Mathf.PerlinNoise(Mathf.Cos(u) * 2f + y * 0.08f, Mathf.Sin(u) * 2f);
                    float wobble = Mathf.Sin(u * 2f) * 1.5f;
                    float rut = Mathf.Min(Mathf.Abs(y - 20f - wobble), Mathf.Abs(y - 44f - wobble));
                    float k = 0.86f + n * 0.2f - Mathf.Clamp01(1f - rut / 3.5f) * 0.18f;
                    // Edges blend toward the plain ground colour.
                    float edge = Mathf.Min(y, height - 1 - y) / 8f;
                    Color c = Color.Lerp(new Color(0.56f, 0.46f, 0.35f), baseColor, Mathf.Clamp01(edge)) * k;
                    c.a = 1f;
                    pixels[y * width + x] = c;
                }
            }
            return Finish(NewTexture("Road", width, height), pixels);
        }

        // One 2 m bay of a shop front, full height: dark posts at the edges, an open counter
        // with shadowed goods behind on the lower half, lattice windows above and a beam on top.
        private static Texture2D ShopFront()
        {
            const int size = 128;
            var post = new Color(0.22f, 0.14f, 0.09f);
            var board = new Color(0.45f, 0.32f, 0.2f);
            var paper = new Color(0.84f, 0.79f, 0.66f);
            var interior = new Color(0.1f, 0.08f, 0.07f);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float n = 0.92f + Mathf.PerlinNoise(x * 0.12f, y * 0.04f) * 0.14f;
                    Color c;
                    if (x < 8 || x >= size - 8 || y >= size - 14)
                        c = post;
                    else if (y < 26)
                        c = (y % 8 < 1) ? post : board; // counter boards
                    else if (y < 66)
                    {
                        // Dim interior with shelves of jars.
                        c = interior;
                        if ((y - 26) % 14 < 2)
                            c = post * 1.3f;
                        else if ((y - 26) % 14 > 6 && Hash(x / 9, (y - 26) / 14) > 0.45f && x % 9 > 2)
                            c = new Color(0.32f, 0.22f, 0.15f);
                    }
                    else if (y < 70)
                        c = post;
                    else
                        c = (x - 8) % 12 < 2 || (y - 70) % 11 < 2 ? post : paper;
                    pixels[y * size + x] = c * n;
                }
            }
            return Finish(NewTexture("ShopFront", size, size), pixels);
        }
    }
}
