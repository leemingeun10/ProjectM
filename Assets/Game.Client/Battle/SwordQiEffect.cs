using System.Collections;
using UnityEngine;

namespace Game.Client.Battle
{
    // Faint qi (기운) streaming off the sword tip while a hidden-technique clip plays: a few soft
    // particles left behind in world space, plus a slight tint of the sword trail. Begin starts it
    // in a colour (HiddenTechniqueQiColors); End fades it out over FadeOutSeconds; Stop cuts it.
    //
    // Built in code (no assets) on its own material instance, so it touches no other effect. The
    // shader is Sprites/Default, the same mobile-safe one QiAuraEffect uses.
    public class SwordQiEffect : MonoBehaviour
    {
        // ---- Strength: keep it faint ----
        private const float EmissionRate = 28f;           // particles per second
        private const float ParticleAlpha = 0.45f;
        private const float MinSize = 0.05f;
        private const float MaxSize = 0.10f;
        private const float MinLifetime = 0.3f;
        private const float MaxLifetime = 0.5f;
        private const float DriftSpeed = 0.12f;           // m/s, random direction
        private const float TrailTint = 0.35f;            // 0 = trail untouched, 1 = fully qi-coloured
        public const float FadeOutSeconds = 0.2f;
        private const int MaxParticles = 64;

        private ParticleSystem particles;
        private ParticleSystemRenderer particleRenderer;
        private Material material;
        private TrailRenderer trail;
        private Gradient trailBase;
        private Gradient tinted;
        private Coroutine fading;

        public bool IsActive { get; private set; }

        // Parents the effect to the sword tip (the trail's transform); trail may be null.
        public static SwordQiEffect Create(Transform tip, TrailRenderer trail)
        {
            var go = new GameObject("SwordQi");
            go.transform.SetParent(tip, false);
            var effect = go.AddComponent<SwordQiEffect>();
            effect.trail = trail;
            effect.Build();
            return effect;
        }

        public void Begin(Color color)
        {
            StopFade();
            IsActive = true;
            var main = particles.main;
            main.startColor = new Color(color.r, color.g, color.b, ParticleAlpha);
            material.color = Color.white;
            particles.Play();
            SetTrailTint(color, TrailTint);
        }

        public void End()
        {
            if (!IsActive)
                return;
            IsActive = false;
            particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            StopFade();
            fading = StartCoroutine(FadeOut());
        }

        public void Stop()
        {
            IsActive = false;
            StopFade();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            material.color = Color.white;
            RestoreTrail();
        }

        private void OnDisable() => Stop();

        private void OnDestroy()
        {
            if (material != null)
                Destroy(material);
        }

        private IEnumerator FadeOut()
        {
            for (float t = 0f; t < FadeOutSeconds; t += Time.deltaTime)
            {
                float k = 1f - t / FadeOutSeconds;
                material.color = new Color(1f, 1f, 1f, k);
                LerpTrailBack(1f - k);
                yield return null;
            }
            particles.Clear();
            material.color = Color.white;
            RestoreTrail();
            fading = null;
        }

        private void StopFade()
        {
            if (fading != null)
                StopCoroutine(fading);
            fading = null;
        }

        private void SetTrailTint(Color color, float amount)
        {
            if (trail == null)
                return;
            var keys = trailBase.colorKeys;
            for (int i = 0; i < keys.Length; i++)
                keys[i].color = Color.Lerp(keys[i].color, color, amount);
            tinted = new Gradient();
            tinted.SetKeys(keys, trailBase.alphaKeys);
            trail.colorGradient = tinted;
        }

        // Moves the trail from its tinted gradient back towards the base one (k: 0 tinted -> 1 base).
        private void LerpTrailBack(float k)
        {
            if (trail == null || tinted == null)
                return;
            var from = tinted.colorKeys;
            var to = trailBase.colorKeys;
            var keys = new GradientColorKey[to.Length];
            for (int i = 0; i < to.Length; i++)
                keys[i] = new GradientColorKey(Color.Lerp(from[Mathf.Min(i, from.Length - 1)].color, to[i].color, k), to[i].time);
            var g = new Gradient();
            g.SetKeys(keys, trailBase.alphaKeys);
            trail.colorGradient = g;
        }

        private void RestoreTrail()
        {
            if (trail != null && trailBase != null)
                trail.colorGradient = trailBase;
            tinted = null;
        }

        private void Build()
        {
            if (trail != null)
            {
                trailBase = new Gradient();
                trailBase.SetKeys(trail.colorGradient.colorKeys, trail.colorGradient.alphaKeys);
            }

            material = new Material(Shader.Find("Sprites/Default")) { mainTexture = SoftDot(), name = "SwordQi" };

            particles = gameObject.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.playOnAwake = false;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(MinLifetime, MaxLifetime);
            main.startSize = new ParticleSystem.MinMaxCurve(MinSize, MaxSize);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f, DriftSpeed);
            // World space: particles stay where the tip passed, so they stream off the swing.
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = MaxParticles;
            main.scalingMode = ParticleSystemScalingMode.Shape;

            var emission = particles.emission;
            emission.rateOverTime = EmissionRate;

            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.04f;

            var colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = fade;

            var sizeOverLifetime = particles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(1f, 1.3f)));

            particleRenderer = GetComponent<ParticleSystemRenderer>();
            particleRenderer.sharedMaterial = material;
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        }

        // Round soft-edged sprite.
        private static Texture2D SoftDot()
        {
            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "SwordQiDot" };
            float r = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - r) * (x - r) + (y - r) * (y - r)) / r;
                    float a = Mathf.Clamp01(1f - d);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            }
            tex.Apply();
            return tex;
        }
    }
}
