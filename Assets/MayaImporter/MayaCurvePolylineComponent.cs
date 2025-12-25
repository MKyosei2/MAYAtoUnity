using System;
using UnityEngine;

namespace MayaImporter.Geometry
{
    /// <summary>
    /// Polyline approximation of a Maya curve for runtime evaluation (motionPath etc).
    /// Points are stored in local space of the curve node GameObject.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MayaCurvePolylineComponent : MonoBehaviour
    {
        public Vector3[] localPoints;
        public bool closed;

        private float[] _cumLen; // cumulative length
        private float _totalLen;

        public bool HasValid => localPoints != null && localPoints.Length >= 2;

        public void Set(Vector3[] points, bool isClosed)
        {
            localPoints = points;
            closed = isClosed;
            RebuildCache();
        }

        public void RebuildCache()
        {
            if (!HasValid)
            {
                _cumLen = null;
                _totalLen = 0f;
                return;
            }

            int n = localPoints.Length;
            int segCount = closed ? n : (n - 1);
            _cumLen = new float[segCount + 1];
            _cumLen[0] = 0f;

            float acc = 0f;
            for (int i = 0; i < segCount; i++)
            {
                int i0 = i;
                int i1 = (i + 1) % n;
                acc += Vector3.Distance(localPoints[i0], localPoints[i1]);
                _cumLen[i + 1] = acc;
            }

            _totalLen = Mathf.Max(acc, 1e-6f);
        }

        public Vector3 EvaluateWorldPosition(Transform curveTransform, float t01)
        {
            if (!HasValid) return curveTransform != null ? curveTransform.position : Vector3.zero;
            if (_cumLen == null || _cumLen.Length == 0) RebuildCache();

            t01 = Mathf.Clamp01(t01);
            float targetLen = t01 * _totalLen;

            int seg = FindSegment(targetLen);
            float segStart = _cumLen[seg];
            float segEnd = _cumLen[seg + 1];
            float u = (segEnd - segStart) > 1e-8f ? (targetLen - segStart) / (segEnd - segStart) : 0f;

            int n = localPoints.Length;
            int i0 = seg;
            int i1 = (seg + 1) % n;

            Vector3 lp = Vector3.Lerp(localPoints[i0], localPoints[i1], u);
            return curveTransform != null ? curveTransform.TransformPoint(lp) : lp;
        }

        public Vector3 EvaluateWorldTangent(Transform curveTransform, float t01)
        {
            if (!HasValid) return curveTransform != null ? curveTransform.forward : Vector3.forward;
            if (_cumLen == null || _cumLen.Length == 0) RebuildCache();

            t01 = Mathf.Clamp01(t01);
            float targetLen = t01 * _totalLen;

            int seg = FindSegment(targetLen);

            int n = localPoints.Length;
            int i0 = seg;
            int i1 = (seg + 1) % n;

            Vector3 lt = (localPoints[i1] - localPoints[i0]);
            if (lt.sqrMagnitude < 1e-12f) lt = Vector3.right;

            Vector3 wt = curveTransform != null ? curveTransform.TransformDirection(lt) : lt;
            if (wt.sqrMagnitude < 1e-12f) wt = Vector3.forward;

            return wt.normalized;
        }

        private int FindSegment(float targetLen)
        {
            // _cumLen length = segCount+1
            int segCount = _cumLen.Length - 1;

            int lo = 0, hi = segCount;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (_cumLen[mid + 1] < targetLen) lo = mid + 1;
                else hi = mid;
            }

            return Mathf.Clamp(lo, 0, segCount - 1);
        }
    }
}
