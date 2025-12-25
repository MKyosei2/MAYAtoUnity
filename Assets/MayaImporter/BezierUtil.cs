using UnityEngine;

namespace MayaImporter.Utils
{
    public static class BezierUtil
    {
        public static Vector3 EvaluateCubic(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            t = Mathf.Clamp01(t);
            float u = 1f - t;
            float tt = t * t;
            float uu = u * u;
            float uuu = uu * u;
            float ttt = tt * t;

            // (1-t)^3 p0 + 3(1-t)^2 t p1 + 3(1-t) t^2 p2 + t^3 p3
            return (uuu * p0) + (3f * uu * t * p1) + (3f * u * tt * p2) + (ttt * p3);
        }

        public static Vector3 DerivativeCubic(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            t = Mathf.Clamp01(t);
            float u = 1f - t;

            // 3(1-t)^2(p1-p0) + 6(1-t)t(p2-p1) + 3t^2(p3-p2)
            return (3f * u * u * (p1 - p0)) + (6f * u * t * (p2 - p1)) + (3f * t * t * (p3 - p2));
        }

        public static float ApproximateLength(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, int segments = 32)
        {
            if (segments < 2) segments = 2;
            float len = 0f;
            Vector3 prev = p0;
            for (int i = 1; i <= segments; i++)
            {
                float t = i / (float)segments;
                Vector3 pt = EvaluateCubic(p0, p1, p2, p3, t);
                len += Vector3.Distance(prev, pt);
                prev = pt;
            }
            return len;
        }
    }
}
