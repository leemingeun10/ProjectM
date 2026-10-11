using UnityEngine;

namespace Game.Client.Battle
{
    // Builds the ink-wash (수묵화) battle backdrop at runtime: gradient sky, a distant backdrop,
    // soft linear fog and a plain earthen platform. The backdrop is a painted image on a far quad
    // that is resized every frame to cover the camera view; without an image it falls back to
    // procedural mountain silhouettes that fade with distance.
    public class InkWashBackground : MonoBehaviour
    {
        private struct RidgeLayer
        {
            public float Distance;
            public Color Color;
            public float BaseHeight;
            public float Amplitude;
            public float Phase;
        }

        private const float RidgeHalfWidth = 140f;
        private const int RidgeSegments = 180;
        private const float RidgeBottom = -20f;

        [SerializeField] private Shader skyShader;
        [SerializeField] private Shader unlitFogShader;
        [Tooltip("Point the backdrop is arranged around (between the fighters, on the ground).")]
        [SerializeField] private Vector3 stageCenter = new Vector3(0f, 0f, -1f);
        [Tooltip("Direction from the stage toward the backdrop (away from the camera).")]
        [SerializeField] private Vector3 backDirection = Vector3.left;

        [Header("Backdrop Image")]
        [Tooltip("Painted backdrop shown on a far quad. Leave empty to use procedural mountain ridges.")]
        [SerializeField] private Texture2D backdropTexture;
        [Tooltip("Distance from the stage centre to the backdrop plane; farther sinks deeper into the fog.")]
        [SerializeField] private float backdropDistance = 50f;
        [Tooltip("Horizontal image position (0 = left edge, 1 = right edge) kept at the view centre when the screen is narrower than the image.")]
        [Range(0f, 1f)]
        [SerializeField] private float backdropFocusU = 0.32f;
        [Tooltip("Vertical image position (0 = bottom, 1 = top) kept at the view centre. Below 0.5 lifts the painted mountains above the platform; the quad grows to stay covering.")]
        [Range(0.2f, 0.8f)]
        [SerializeField] private float backdropFocusV = 0.38f;
        [Tooltip("Extra coverage so camera moves never reveal the quad edges.")]
        [SerializeField] private float backdropMargin = 1.1f;

        [Header("Sky")]
        [SerializeField] private Color skyTopColor = Hex(0xE8E4D9);
        [SerializeField] private Color skyHorizonColor = Hex(0xF2DEC4);

        [Header("Fog")]
        [SerializeField] private Color fogColor = Hex(0xEEE2D0);
        [SerializeField] private float fogStart = 15f;
        [SerializeField] private float fogEnd = 160f;

        [Header("Platform")]
        [SerializeField] private Color platformColor = Hex(0x8B7355);
        [SerializeField] private float platformRadius = 3.5f;
        [SerializeField] private float platformThickness = 0.2f;

        private Transform backdrop;

        private void Awake()
        {
            ApplySky();
            ApplyFog();
            if (backdropTexture != null)
                BuildBackdrop();
            else
                BuildMountains();
            BuildPlatform();
        }

        private void LateUpdate()
        {
            if (backdrop != null)
                FitBackdrop();
        }

        private void ApplySky()
        {
            Shader shader = skyShader != null ? skyShader : Shader.Find("Game/InkSkyGradient");
            var material = new Material(shader) { name = "InkSky" };
            material.SetColor("_TopColor", skyTopColor);
            material.SetColor("_HorizonColor", skyHorizonColor);
            // Ambient lighting is deliberately not regenerated so the fighters keep their current look.
            RenderSettings.skybox = material;
        }

        private void ApplyFog()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogStartDistance = fogStart;
            RenderSettings.fogEndDistance = fogEnd;
        }

        private void BuildMountains()
        {
            // Nearest (darkest) to farthest (closest to the sky colour), like atmospheric perspective in ink painting.
            var layers = new[]
            {
                new RidgeLayer { Distance = 28f, Color = Hex(0x5C6670), BaseHeight = 3.5f, Amplitude = 2.2f, Phase = 0.0f },
                new RidgeLayer { Distance = 45f, Color = Hex(0x8A949D), BaseHeight = 6.0f, Amplitude = 3.5f, Phase = 1.7f },
                new RidgeLayer { Distance = 70f, Color = Hex(0xB8C2C9), BaseHeight = 15.0f, Amplitude = 6.0f, Phase = 3.9f },
            };

            Shader shader = unlitFogShader != null ? unlitFogShader : Shader.Find("Game/InkUnlitFog");
            Vector3 back = backDirection.normalized;
            Vector3 side = Vector3.Cross(Vector3.up, back).normalized;

            for (int i = 0; i < layers.Length; i++)
            {
                RidgeLayer layer = layers[i];
                var go = new GameObject("MountainLayer" + i);
                go.transform.SetParent(transform, false);
                go.transform.position = stageCenter + back * layer.Distance;
                go.transform.rotation = Quaternion.LookRotation(side, Vector3.up);

                var material = new Material(shader) { name = "InkMountain" + i };
                material.SetColor("_BaseColor", layer.Color);
                go.AddComponent<MeshFilter>().sharedMesh = BuildRidgeMesh(layer);
                go.AddComponent<MeshRenderer>().sharedMaterial = material;
            }
        }

        // A vertical strip along local Z whose top edge is a gentle sum-of-sines ridge line.
        private static Mesh BuildRidgeMesh(RidgeLayer layer)
        {
            var vertices = new Vector3[(RidgeSegments + 1) * 2];
            var triangles = new int[RidgeSegments * 6];

            for (int s = 0; s <= RidgeSegments; s++)
            {
                float z = Mathf.Lerp(-RidgeHalfWidth, RidgeHalfWidth, s / (float)RidgeSegments);
                float ridge = Mathf.Sin(z * 0.045f + layer.Phase) * 0.55f
                            + Mathf.Sin(z * 0.11f + layer.Phase * 2.3f) * 0.3f
                            + Mathf.Sin(z * 0.27f + layer.Phase * 0.7f) * 0.15f;
                float height = Mathf.Max(0.5f, layer.BaseHeight + ridge * layer.Amplitude);

                vertices[s * 2] = new Vector3(0f, RidgeBottom, z);
                vertices[s * 2 + 1] = new Vector3(0f, height, z);
            }

            for (int s = 0; s < RidgeSegments; s++)
            {
                int v = s * 2;
                int t = s * 6;
                triangles[t] = v;
                triangles[t + 1] = v + 1;
                triangles[t + 2] = v + 2;
                triangles[t + 3] = v + 1;
                triangles[t + 4] = v + 3;
                triangles[t + 5] = v + 2;
            }

            var mesh = new Mesh { name = "InkRidge" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        private void BuildBackdrop()
        {
            var go = new GameObject("Backdrop");
            go.transform.SetParent(transform, false);
            // Local +Z points away from the camera, so local +X is screen-right and the image is not mirrored.
            go.transform.rotation = Quaternion.LookRotation(backDirection.normalized, Vector3.up);

            Shader shader = unlitFogShader != null ? unlitFogShader : Shader.Find("Game/InkUnlitFog");
            var material = new Material(shader) { name = "InkBackdrop" };
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseMap", backdropTexture);
            go.AddComponent<MeshFilter>().sharedMesh = BuildUnitQuad();
            go.AddComponent<MeshRenderer>().sharedMaterial = material;

            backdrop = go.transform;
            FitBackdrop();
        }

        // Positions and scales the backdrop so it covers the camera view at the backdrop plane
        // ("cover" fit, preserving the image aspect), whatever the screen orientation.
        private void FitBackdrop()
        {
            Camera cam = Camera.main;
            if (cam == null)
                return;

            Vector3 back = backDirection.normalized;
            Vector3 side = Vector3.Cross(Vector3.up, back).normalized;
            Vector3 planePoint = stageCenter + back * backdropDistance;
            Vector3 camPos = cam.transform.position;
            float depth = Vector3.Dot(planePoint - camPos, back);
            float facing = Vector3.Dot(cam.transform.forward, back);
            if (depth <= 0f || facing <= 0.01f)
                return;

            Vector3 viewCentre = camPos + cam.transform.forward * (depth / facing);
            float viewHeight = 2f * depth * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float viewWidth = viewHeight * cam.aspect;
            float imageAspect = backdropTexture.width / (float)backdropTexture.height;

            // Tall enough that the vertical focus point can sit at the view centre with both edges covered.
            float coverHeight = viewHeight / (2f * Mathf.Min(backdropFocusV, 1f - backdropFocusV));
            float quadHeight = Mathf.Max(coverHeight, viewWidth / imageAspect) * backdropMargin;
            float quadWidth = quadHeight * imageAspect;

            // Slide the image sideways toward the focus point without exposing its edges.
            float maxShift = Mathf.Max(0f, (quadWidth - viewWidth * backdropMargin) * 0.5f);
            float shift = Mathf.Clamp((0.5f - backdropFocusU) * quadWidth, -maxShift, maxShift);
            float lift = (0.5f - backdropFocusV) * quadHeight;

            backdrop.position = viewCentre + side * shift + Vector3.up * lift;
            backdrop.localScale = new Vector3(quadWidth, quadHeight, 1f);
        }

        private static Mesh BuildUnitQuad()
        {
            var mesh = new Mesh { name = "InkBackdropQuad" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f),
            };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateBounds();
            return mesh;
        }

        private void BuildPlatform()
        {
            GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            platform.name = "Platform";
            Destroy(platform.GetComponent<Collider>());
            platform.transform.SetParent(transform, false);
            platform.transform.position = stageCenter + Vector3.down * (platformThickness * 0.5f);
            // Unity's cylinder is 1 unit wide and 2 units tall.
            platform.transform.localScale = new Vector3(platformRadius * 2f, platformThickness * 0.5f, platformRadius * 2f);

            var renderer = platform.GetComponent<MeshRenderer>();
            var material = new Material(renderer.sharedMaterial) { name = "InkPlatform" };
            material.SetColor("_BaseColor", platformColor);
            renderer.sharedMaterial = material;
        }

        private static Color Hex(int rgb)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
        }
    }
}
