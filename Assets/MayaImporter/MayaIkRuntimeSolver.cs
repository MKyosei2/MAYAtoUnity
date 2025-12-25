using System;
using System.Collections.Generic;
using UnityEngine;
using MayaImporter.Geometry;

namespace MayaImporter.IK
{
    /// <summary>
    /// Runtime IK solver for Maya ikHandle.
    /// Target is the ikHandle GameObject transform (this.transform) for RP/SC.
    ///
    /// Supports:
    /// - ikRPsolver: TwoBone analytic when possible + pole vector + twist
    /// - ikSCsolver: CCD best-effort (no pole)
    /// - ikSplineSolver: Best-effort spline IK using MayaCurvePolylineComponent (arc-length parameterized)
    ///
    /// Pole Vector sources:
    /// - Data.PoleVector (preferred, usually from poleVectorConstraint)
    /// - Data.AutoPoleVectorTransform (generated from ikHandle.poleVector double3)
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MayaIkRuntimeSolver : MonoBehaviour
    {
        [Header("Source Data")]
        public MayaIkHandleComponent Data;

        [Header("Runtime Options")]
        public int Priority = 0;
        public int CcdIterations = 10;
        public float SolveEpsilon = 0.0005f;

        private readonly List<Transform> _chain = new List<Transform>(16);

        // cached rest pose for stable orientation
        private readonly List<Quaternion> _restRot = new List<Quaternion>(16);
        private readonly List<Vector3> _restDir = new List<Vector3>(16);
        private readonly List<float> _restLen = new List<float>(16);
        private float _restTotalLen = 0f;

        private void Awake()
        {
            if (Data == null) Data = GetComponent<MayaIkHandleComponent>();
            RebuildChain();
        }

        private void OnEnable()
        {
            if (Data == null) Data = GetComponent<MayaIkHandleComponent>();
            RebuildChain();

            MayaIkManager.EnsureExists();
            MayaIkManager.Register(this);
        }

        private void OnDisable()
        {
            MayaIkManager.Unregister(this);
        }

        public void RebuildChain()
        {
            _chain.Clear();
            _restRot.Clear();
            _restDir.Clear();
            _restLen.Clear();
            _restTotalLen = 0f;

            if (Data == null) return;
            if (Data.StartJoint == null) return;

            var endJoint = Data.EndJoint;
            if (endJoint == null)
            {
                if (Data.EndEffector != null && Data.EndEffector.parent != null)
                    endJoint = Data.EndEffector.parent;
            }

            if (endJoint == null) return;

            // Build chain by walking parents from end to start
            var tmp = new List<Transform>(16);
            var t = endJoint;
            while (t != null)
            {
                tmp.Add(t);
                if (t == Data.StartJoint) break;
                t = t.parent;
            }

            if (tmp.Count == 0 || tmp[tmp.Count - 1] != Data.StartJoint)
            {
                _chain.Add(Data.StartJoint);
                _chain.Add(endJoint);
            }
            else
            {
                for (int i = tmp.Count - 1; i >= 0; i--)
                    _chain.Add(tmp[i]);
            }

            // Cache rest pose for stable rotations / lengths
            for (int i = 0; i < _chain.Count; i++)
            {
                var j = _chain[i];
                _restRot.Add(j != null ? j.rotation : Quaternion.identity);

                if (i < _chain.Count - 1 && j != null && _chain[i + 1] != null)
                {
                    var d = (_chain[i + 1].position - j.position);
                    float len = d.magnitude;
                    if (len < 1e-6f) { d = j.forward; len = 0f; }
                    else d /= len;

                    _restDir.Add(d);
                    _restLen.Add(len);
                    _restTotalLen += len;
                }
                else
                {
                    _restDir.Add(Vector3.forward);
                }
            }

            if (_restTotalLen < 1e-6f) _restTotalLen = 1f;
        }

        internal void SolveInternal()
        {
            if (Data == null) return;
            if (_chain.Count < 2) return;

            var st = Data.SolverType ?? "";
            bool isSpline = st.IndexOf("ikSpline", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            st.IndexOf("Spline", StringComparison.OrdinalIgnoreCase) >= 0;

            if (isSpline)
            {
                SolveSplineIk();
                return;
            }

            var targetPos = transform.position;

            bool isRP = st.IndexOf("ikRP", StringComparison.OrdinalIgnoreCase) >= 0;
            bool isSC = st.IndexOf("ikSC", StringComparison.OrdinalIgnoreCase) >= 0;

            Transform pole = Data.PoleVector != null ? Data.PoleVector : Data.AutoPoleVectorTransform;

            if (isRP && _chain.Count >= 3)
            {
                if (_chain.Count == 3)
                {
                    SolveTwoBoneRP(_chain[0], _chain[1], _chain[2], targetPos, pole, Data.Twist);
                }
                else
                {
                    SolveCcd(_chain, targetPos, CcdIterations, SolveEpsilon);
                    ApplyPoleBestEffort(_chain, targetPos, pole);
                    ApplyTwistBestEffort(_chain, targetPos, Data.Twist);
                }
                return;
            }

            if (isSC)
            {
                SolveCcd(_chain, targetPos, CcdIterations, SolveEpsilon);
                return;
            }

            SolveCcd(_chain, targetPos, CcdIterations, SolveEpsilon);
        }

        // =========================================================
        // Spline IK (best-effort)
        // =========================================================

        private void SolveSplineIk()
        {
            if (Data == null) return;
            if (Data.SplineCurve == null) return;

            var curve = Data.SplineCurve.GetComponent<MayaCurvePolylineComponent>();
            if (curve == null || !curve.HasValid) return;

            if (_chain.Count < 2) return;

            // build normalized t for each joint based on rest lengths (arc-length distribution)
            float offset = Data.SplineOffset;
            // Best-effort normalize: if offset looks not normalized, clamp anyway.
            // Use repeat to mimic Maya-style wrapping along curve.
            float Wrap01(float v) => Mathf.Repeat(v, 1f);

            var tList = new float[_chain.Count];
            tList[0] = Wrap01(offset);

            float acc = 0f;
            for (int i = 1; i < _chain.Count; i++)
            {
                float len = (i - 1) < _restLen.Count ? _restLen[i - 1] : 0f;
                acc += len;
                float u = acc / _restTotalLen;
                tList[i] = Wrap01(offset + u);
            }

            // Evaluate positions
            var posList = new Vector3[_chain.Count];
            for (int i = 0; i < _chain.Count; i++)
            {
                posList[i] = curve.EvaluateWorldPosition(Data.SplineCurve, tList[i]);
            }

            // rootOnCurve false => keep start joint position, shift whole solution
            if (!Data.SplineRootOnCurve)
            {
                var start = _chain[0];
                if (start != null)
                {
                    Vector3 delta = start.position - posList[0];
                    for (int i = 0; i < posList.Length; i++)
                        posList[i] += delta;
                }
            }

            // Apply positions
            for (int i = 0; i < _chain.Count; i++)
            {
                var j = _chain[i];
                if (j == null) continue;
                j.position = posList[i];
            }

            // Apply rotations based on restDir -> newDir, plus distributed twist
            float twistTotal = Data.Twist; // degrees
            int last = _chain.Count - 1;

            for (int i = 0; i < _chain.Count; i++)
            {
                var j = _chain[i];
                if (j == null) continue;

                if (i >= last) continue; // last joint: keep rotation as-is (best-effort)

                Vector3 newDir = (posList[i + 1] - posList[i]);
                if (newDir.sqrMagnitude < 1e-12f) continue;
                newDir.Normalize();

                Vector3 restDir = (i < _restDir.Count) ? _restDir[i] : Vector3.forward;
                if (restDir.sqrMagnitude < 1e-12f) restDir = j.forward;

                Quaternion baseRot = (i < _restRot.Count) ? _restRot[i] : j.rotation;

                Quaternion align = Quaternion.FromToRotation(restDir, newDir);
                Quaternion rot = align * baseRot;

                // distribute twist along the chain (0..1)
                float a = last > 0 ? (float)i / (float)last : 0f;
                if (Mathf.Abs(twistTotal) > 1e-5f)
                    rot = Quaternion.AngleAxis(twistTotal * a, newDir) * rot;

                j.rotation = rot;
            }
        }

        // =========================================================
        // RP / SC Solvers
        // =========================================================

        private static void SolveTwoBoneRP(
            Transform start,
            Transform mid,
            Transform end,
            Vector3 targetPos,
            Transform pole,
            float twistDegrees)
        {
            if (start == null || mid == null || end == null) return;

            var p0 = start.position;
            var p1 = mid.position;
            var p2 = end.position;

            float L0 = Vector3.Distance(p0, p1);
            float L1 = Vector3.Distance(p1, p2);

            if (L0 < 1e-6f || L1 < 1e-6f) return;

            Vector3 d = targetPos - p0;
            float r = d.magnitude;
            if (r < 1e-8f) return;

            // Clamp distance to reachable range
            float minR = Mathf.Abs(L0 - L1) + 1e-6f;
            float maxR = (L0 + L1) - 1e-6f;
            r = Mathf.Clamp(r, minR, maxR);

            Vector3 u = d / d.magnitude; // start->target dir

            // Plane normal from pole or current bend
            Vector3 n;
            if (pole != null)
            {
                var pv = pole.position - p0;
                n = Vector3.Cross(u, pv);
                if (n.sqrMagnitude < 1e-10f)
                    n = Vector3.Cross(p1 - p0, p2 - p1);
            }
            else
            {
                n = Vector3.Cross(p1 - p0, p2 - p1);
            }

            if (n.sqrMagnitude < 1e-10f)
            {
                n = Vector3.Cross(u, Vector3.up);
                if (n.sqrMagnitude < 1e-10f)
                    n = Vector3.Cross(u, Vector3.right);
            }
            n.Normalize();

            Vector3 v = Vector3.Cross(n, u);
            if (v.sqrMagnitude < 1e-10f) return;
            v.Normalize();

            // Law of cosines (angle at start)
            float cosA = (L0 * L0 + r * r - L1 * L1) / (2f * L0 * r);
            cosA = Mathf.Clamp(cosA, -1f, 1f);
            float sinA = Mathf.Sqrt(Mathf.Max(0f, 1f - cosA * cosA));

            // Desired mid position
            Vector3 p1d = p0 + u * (cosA * L0) + v * (sinA * L0);

            // Rotate start so (start->mid) points to (start->p1d)
            Vector3 cur0 = (mid.position - start.position);
            Vector3 des0 = (p1d - start.position);

            if (cur0.sqrMagnitude > 1e-12f && des0.sqrMagnitude > 1e-12f)
            {
                var q0 = Quaternion.FromToRotation(cur0, des0);
                start.rotation = q0 * start.rotation;
            }

            // Rotate mid so (mid->end) points to (mid->target)
            Vector3 cur1 = (end.position - mid.position);
            Vector3 des1 = (targetPos - mid.position);

            if (cur1.sqrMagnitude > 1e-12f && des1.sqrMagnitude > 1e-12f)
            {
                var q1 = Quaternion.FromToRotation(cur1, des1);
                mid.rotation = q1 * mid.rotation;
            }

            // Twist around axis (start->target)
            ApplyTwistBestEffort(start, targetPos, twistDegrees);
        }

        private static void SolveCcd(
            List<Transform> chain,
            Vector3 targetPos,
            int iterations,
            float eps)
        {
            if (chain == null || chain.Count < 2) return;

            var end = chain[chain.Count - 1];
            if (end == null) return;

            iterations = Mathf.Clamp(iterations, 1, 64);

            for (int it = 0; it < iterations; it++)
            {
                float dist = Vector3.Distance(end.position, targetPos);
                if (dist <= eps) break;

                for (int i = chain.Count - 2; i >= 0; i--)
                {
                    var j = chain[i];
                    if (j == null) continue;

                    Vector3 toEnd = end.position - j.position;
                    Vector3 toTarget = targetPos - j.position;

                    if (toEnd.sqrMagnitude < 1e-12f || toTarget.sqrMagnitude < 1e-12f)
                        continue;

                    var q = Quaternion.FromToRotation(toEnd, toTarget);
                    j.rotation = q * j.rotation;
                }
            }
        }

        private static void ApplyPoleBestEffort(List<Transform> chain, Vector3 targetPos, Transform pole)
        {
            if (pole == null) return;
            if (chain == null || chain.Count < 3) return;

            var start = chain[0];
            var mid = chain[1];
            var end = chain[chain.Count - 1];
            if (start == null || mid == null || end == null) return;

            Vector3 axis = (targetPos - start.position);
            if (axis.sqrMagnitude < 1e-12f) return;
            axis.Normalize();

            Vector3 curN = Vector3.Cross(mid.position - start.position, end.position - mid.position);
            if (curN.sqrMagnitude < 1e-12f) return;
            curN.Normalize();

            Vector3 pv = pole.position - start.position;
            Vector3 desN = Vector3.Cross(axis, pv);
            if (desN.sqrMagnitude < 1e-12f) return;
            desN.Normalize();

            float angle = SignedAngleAroundAxis(curN, desN, axis);
            if (float.IsNaN(angle) || Mathf.Abs(angle) < 1e-5f) return;

            start.rotation = Quaternion.AngleAxis(angle, axis) * start.rotation;
        }

        private static void ApplyTwistBestEffort(List<Transform> chain, Vector3 targetPos, float twistDegrees)
        {
            if (chain == null || chain.Count == 0) return;
            ApplyTwistBestEffort(chain[0], targetPos, twistDegrees);
        }

        private static void ApplyTwistBestEffort(Transform start, Vector3 targetPos, float twistDegrees)
        {
            if (start == null) return;
            if (Mathf.Abs(twistDegrees) < 1e-5f) return;

            Vector3 axis = (targetPos - start.position);
            if (axis.sqrMagnitude < 1e-12f) return;
            axis.Normalize();

            start.rotation = Quaternion.AngleAxis(twistDegrees, axis) * start.rotation;
        }

        private static float SignedAngleAroundAxis(Vector3 fromN, Vector3 toN, Vector3 axis)
        {
            fromN = Vector3.ProjectOnPlane(fromN, axis).normalized;
            toN = Vector3.ProjectOnPlane(toN, axis).normalized;
            if (fromN.sqrMagnitude < 1e-12f || toN.sqrMagnitude < 1e-12f) return 0f;

            return Vector3.SignedAngle(fromN, toN, axis);
        }
    }
}
