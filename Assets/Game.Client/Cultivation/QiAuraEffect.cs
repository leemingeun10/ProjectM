using System;
using System.Collections;
using UnityEngine;

namespace Game.Client.Cultivation
{
    // Rising/swirling qi (기) around a seated body, built entirely in code so it needs no assets.
    // One effect, three strengths: Play(tier) only changes size, density, swirl, colour and glow.
    //   1: a faint blue haze drifting upward.
    //   2: a distinct blue vortex circling the body, with a ground ring.
    //   3: blue shot through with gold, ending in an outward burst (onBurst lets the caller shake
    //      or flash the screen at that moment).
    // Attach with Create(parent, position) at the body's centre; the effect rises along the
    // parent's +Y, so use an upright parent (not a bone, whose local axes are usually rotated).
    // Reusable for other screens.
    public class QiAuraEffect : MonoBehaviour
    {
        private struct TierSettings
        {
            public float Duration;
            public float Rate;
            public float Radius;
            public Vector2 Size;
            public float Lifetime;
            public float RiseSpeed;
            public float Orbital;
            public Color ColorA;
            public Color ColorB;
            public float RingRate;
            public float LightIntensity;
            public Color LightColor;
            public int BurstCount;
        }

        private static readonly Color PaleBlue = new Color(0.62f, 0.8f, 1f, 0.32f);
        private static readonly Color Blue = new Color(0.35f, 0.66f, 1f, 0.7f);
        private static readonly Color DeepBlue = new Color(0.2f, 0.45f, 1f, 0.75f);
        private static readonly Color Gold = new Color(1f, 0.8f, 0.35f, 0.85f);

        private static readonly TierSettings[] Tiers =
        {
            new TierSettings
            {
                Duration = 3.0f, Rate = 16f, Radius = 0.32f, Size = new Vector2(0.12f, 0.24f), Lifetime = 1.8f,
                RiseSpeed = 0.28f, Orbital = 0.4f, ColorA = PaleBlue, ColorB = PaleBlue,
                RingRate = 0f, LightIntensity = 0.6f, LightColor = new Color(0.6f, 0.8f, 1f), BurstCount = 0,
            },
            new TierSettings
            {
                Duration = 3.5f, Rate = 48f, Radius = 0.45f, Size = new Vector2(0.14f, 0.34f), Lifetime = 1.8f,
                RiseSpeed = 0.45f, Orbital = 2.6f, ColorA = Blue, ColorB = DeepBlue,
                RingRate = 40f, LightIntensity = 1.6f, LightColor = new Color(0.4f, 0.65f, 1f), BurstCount = 0,
            },
            new TierSettings
            {
                Duration = 4.0f, Rate = 80f, Radius = 0.55f, Size = new Vector2(0.16f, 0.46f), Lifetime = 1.6f,
                RiseSpeed = 0.7f, Orbital = 3.6f, ColorA = Blue, ColorB = Gold,
                RingRate = 70f, LightIntensity = 2.0f, LightColor = new Color(1f, 0.82f, 0.45f), BurstCount = 160,
            },
        };

        private ParticleSystem body;
        private ParticleSystem ring;
        private ParticleSystem burst;
        private Light glow;
        private Coroutine running;
        private Material material;

        public bool IsPlaying => running != null;

        public static QiAuraEffect Create(Transform parent, Vector3 localPosition)
        {
            var go = new GameObject("QiAura");
            go.SetActive(false);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            var effect = go.AddComponent<QiAuraEffect>();
            effect.Build();
            go.SetActive(true);
            return effect;
        }

        // Returns the effect's length in seconds. onBurst (tier 3 only) fires at the outward burst.
        public float Play(int tier, Action onBurst = null)
        {
            Stop();
            TierSettings s = Tiers[Mathf.Clamp(tier, 1, Tiers.Length) - 1];
            running = StartCoroutine(Run(s, onBurst));
            return s.Duration;
        }

        public void Stop()
        {
            if (running != null)
                StopCoroutine(running);
            running = null;
            body.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ring.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            burst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            glow.intensity = 0f;
        }

        private IEnumerator Run(TierSettings s, Action onBurst)
        {
            Configure(s);
            body.Play();
            if (s.RingRate > 0f)
                ring.Play();

            // Swell in, hold, then either burst (tier 3) or fade out.
            float fadeOut = 0.6f;
            float hold = s.Duration - fadeOut;
            for (float t = 0f; t < hold; t += Time.deltaTime)
            {
                float swell = Mathf.SmoothStep(0f, 1f, t / 0.8f);
                glow.intensity = s.LightIntensity * swell * (0.85f + 0.15f * Mathf.Sin(t * 9f));
                yield return null;
            }

            body.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            ring.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (s.BurstCount > 0)
            {
                burst.Emit(s.BurstCount);
                onBurst?.Invoke();
            }

            for (float t = 0f; t < fadeOut; t += Time.deltaTime)
            {
                float k = s.BurstCount > 0 ? 1.6f - t / fadeOut * 1.6f : 1f - t / fadeOut;
                glow.intensity = s.LightIntensity * Mathf.Max(0f, k);
                yield return null;
            }
            glow.intensity = 0f;
            running = null;
        }

        private void Configure(TierSettings s)
        {
            var main = body.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(s.Lifetime * 0.7f, s.Lifetime);
            main.startSize = new ParticleSystem.MinMaxCurve(s.Size.x, s.Size.y);
            main.startColor = new ParticleSystem.MinMaxGradient(s.ColorA, s.ColorB);
            var emission = body.emission;
            emission.rateOverTime = s.Rate;
            var shape = body.shape;
            shape.radius = s.Radius;
            var velocity = body.velocityOverLifetime;
            velocity.y = new ParticleSystem.MinMaxCurve(s.RiseSpeed * 0.6f, s.RiseSpeed);
            velocity.orbitalY = new ParticleSystem.MinMaxCurve(s.Orbital * 0.8f, s.Orbital);
            float radial = s.Orbital > 1f ? -0.15f : 0f;
            velocity.radial = new ParticleSystem.MinMaxCurve(radial, radial);

            var ringMain = ring.main;
            ringMain.startColor = new ParticleSystem.MinMaxGradient(s.ColorA, s.ColorB);
            ringMain.startSize = new ParticleSystem.MinMaxCurve(s.Size.x * 0.6f, s.Size.y * 0.6f);
            var ringEmission = ring.emission;
            ringEmission.rateOverTime = s.RingRate;
            var ringShape = ring.shape;
            ringShape.radius = s.Radius + 0.25f;
            var ringVelocity = ring.velocityOverLifetime;
            ringVelocity.orbitalY = new ParticleSystem.MinMaxCurve(s.Orbital * 1.2f, s.Orbital * 1.2f);

            glow.color = s.LightColor;
            glow.range = 1.5f + s.Radius * 3f;
        }

        private void Build()
        {
            material = new Material(Shader.Find("Sprites/Default")) { mainTexture = SoftDot(), name = "QiAura" };

            body = MakeSystem("Body", Vector3.zero);
            var bodyShape = body.shape;
            bodyShape.shapeType = ParticleSystemShapeType.Circle;
            bodyShape.rotation = new Vector3(90f, 0f, 0f);
            var bodySize = body.sizeOverLifetime;
            bodySize.enabled = true;
            bodySize.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.4f), new Keyframe(0.3f, 1f), new Keyframe(1f, 1.4f)));

            // The ring hugs the floor (the effect sits at the seated hips, about half a metre up).
            ring = MakeSystem("Ring", new Vector3(0f, -0.45f, 0f));
            var ringShape = ring.shape;
            ringShape.shapeType = ParticleSystemShapeType.Circle;
            ringShape.radiusThickness = 0f;
            ringShape.rotation = new Vector3(90f, 0f, 0f);
            var ringMain = ring.main;
            ringMain.startLifetime = 1.0f;
            ringMain.startSpeed = 0f;

            burst = MakeSystem("Burst", Vector3.zero);
            var burstMain = burst.main;
            burstMain.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
            burstMain.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 5f);
            burstMain.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.28f);
            burstMain.startColor = new ParticleSystem.MinMaxGradient(Gold, new Color(1f, 0.95f, 0.8f, 0.9f));
            var burstEmission = burst.emission;
            burstEmission.rateOverTime = 0f;
            var burstShape = burst.shape;
            burstShape.shapeType = ParticleSystemShapeType.Sphere;
            burstShape.radius = 0.2f;
            var drag = burst.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.drag = 3f;

            var lightGo = new GameObject("Glow");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 0.2f, 0.3f);
            glow = lightGo.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.intensity = 0f;
            glow.shadows = LightShadows.None;
        }

        private ParticleSystem MakeSystem(string name, Vector3 localPosition)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPosition;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.startSpeed = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 600;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = fade;

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            // Axes of one velocity group must share a curve mode; every curve here is two constants.
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.orbitalX = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.orbitalY = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.orbitalZ = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.radial = new ParticleSystem.MinMaxCurve(0f, 0f);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            return ps;
        }

        // Round soft-edged sprite for every particle.
        private static Texture2D SoftDot()
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "QiDot" };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f;
                    float dy = (y + 0.5f) / size * 2f - 1f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * a * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        private void OnDestroy()
        {
            if (material != null)
            {
                Destroy(material.mainTexture);
                Destroy(material);
            }
        }
    }
}
