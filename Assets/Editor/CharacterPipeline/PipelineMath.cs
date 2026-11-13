using UnityEngine;

namespace CharacterPipeline
{
    // Small, engine-light helpers shared by the pipeline tools (and covered by EditMode tests).
    public static class PipelineMath
    {
        // Rotates `bone` in world space so the bone -> child direction points along `target`.
        // Returns the angle (degrees) the direction was off before the turn.
        public static float AlignTowards(Transform bone, Transform child, Vector3 target)
        {
            if (bone == null || child == null)
                return 0f;
            Vector3 current = child.position - bone.position;
            if (current.sqrMagnitude < 1e-12f)
                return 0f;
            float before = Vector3.Angle(current, target);
            bone.rotation = Quaternion.FromToRotation(current, target) * bone.rotation;
            return before;
        }

        // Angle (degrees) between bone -> child and the wanted direction; 0 means exact.
        public static float AngleOff(Transform bone, Transform child, Vector3 target)
        {
            if (bone == null || child == null)
                return float.NaN;
            return Vector3.Angle(child.position - bone.position, target);
        }

        // How far a model has to be raised (metres, may be negative) so its lower sole meets y = 0.
        public static float GroundOffset(float leftSoleY, float rightSoleY)
        {
            return -Mathf.Min(leftSoleY, rightSoleY);
        }

        // Uniform scale that turns a measured height into the wanted one.
        public static float ScaleForHeight(float measuredHeight, float targetHeight)
        {
            if (measuredHeight <= 0f)
                throw new System.ArgumentOutOfRangeException(nameof(measuredHeight), "Measured height must be positive.");
            return targetHeight / measuredHeight;
        }

        public static float Luminance(Color c)
        {
            return 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
        }

        // Fraction of the leg weight moved to the hips for a hem vertex at height h: `topShare` at
        // the hem top (belt), `bottomShare` at the hem bottom, linear in between.
        public static float HemHipShare(float h, float hemBottom, float hemTop, float bottomShare, float topShare = 1f)
        {
            if (hemTop <= hemBottom)
                return topShare;
            float t = Mathf.Clamp01((hemTop - h) / (hemTop - hemBottom));
            return Mathf.Lerp(topShare, bottomShare, t);
        }

        // Möller-Trumbore; returns the hit distance along `dir` or -1.
        public static float RayTriangle(Vector3 origin, Vector3 dir, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 e1 = b - a, e2 = c - a;
            Vector3 p = Vector3.Cross(dir, e2);
            float det = Vector3.Dot(e1, p);
            if (Mathf.Abs(det) < 1e-12f)
                return -1f;
            float inv = 1f / det;
            Vector3 s = origin - a;
            float u = Vector3.Dot(s, p) * inv;
            if (u < 0f || u > 1f)
                return -1f;
            Vector3 q = Vector3.Cross(s, e1);
            float v = Vector3.Dot(dir, q) * inv;
            if (v < 0f || u + v > 1f)
                return -1f;
            float t = Vector3.Dot(e2, q) * inv;
            return t >= 0f ? t : -1f;
        }
    }
}
