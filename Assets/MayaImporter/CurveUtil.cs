using System.Collections.Generic;
using UnityEngine;

namespace MayaImporter.Utils
{
    /// <summary>
    /// “曲線をUnity側で再構築できる” ための最低限のカーブユーティリティ。
    /// Maya の curve / motionPath 等で、まずは「サンプリング点列を作れる」ことを重視。
    /// </summary>
    public static class CurveUtil
    {
        public static List<Vector3> SampleCubicBezierSegments(IReadOnlyList<Vector3> controlPoints, int samplesPerSegment = 16, bool includeLast = true)
        {
            var result = new List<Vector3>();
            if (controlPoints == null || controlPoints.Count < 4) return result;

            if (samplesPerSegment < 2) samplesPerSegment = 2;

            int segCount = (controlPoints.Count - 1) / 3; // p0 + 3n
            if (segCount <= 0) return result;

            for (int s = 0; s < segCount; s++)
            {
                int i0 = s * 3;
                Vector3 p0 = controlPoints[i0 + 0];
                Vector3 p1 = controlPoints[i0 + 1];
                Vector3 p2 = controlPoints[i0 + 2];
                Vector3 p3 = controlPoints[i0 + 3];

                int start = (s == 0) ? 0 : 1; // 連結点の重複を避ける
                int end = samplesPerSegment;
                for (int i = start; i <= end; i++)
                {
                    float t = i / (float)samplesPerSegment;
                    if (!includeLast && s == segCount - 1 && i == end) break;
                    result.Add(BezierUtil.EvaluateCubic(p0, p1, p2, p3, t));
                }
            }

            return result;
        }

        public static Vector3 EvaluatePolyline(IReadOnlyList<Vector3> points, float t01)
        {
            if (points == null || points.Count == 0) return Vector3.zero;
            if (points.Count == 1) return points[0];

            t01 = Mathf.Clamp01(t01);
            float scaled = t01 * (points.Count - 1);
            int i0 = Mathf.FloorToInt(scaled);
            int i1 = Mathf.Clamp(i0 + 1, 0, points.Count - 1);
            float lt = scaled - i0;

            return Vector3.Lerp(points[i0], points[i1], lt);
        }

        public static float ApproximatePolylineLength(IReadOnlyList<Vector3> points)
        {
            if (points == null || points.Count < 2) return 0f;
            float len = 0f;
            for (int i = 1; i < points.Count; i++)
                len += Vector3.Distance(points[i - 1], points[i]);
            return len;
        }
    }
}
