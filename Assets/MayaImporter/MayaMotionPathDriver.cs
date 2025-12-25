using System.Collections.Generic;
using UnityEngine;
using MayaImporter.Geometry;

namespace MayaImporter.Animation
{
    /// <summary>
    /// Runtime evaluator for Maya motionPath.
    /// - Supports multiple driven transforms
    /// - fractionMode (best-effort)
    /// - frontAxis/upAxis with negative directions (0..5)
    /// - worldUpType (best-effort)
    /// - bank + twists (best-effort)
    ///
    /// Notes:
    /// - Without Maya API we cannot perfectly reproduce NURBS parameter domain when fractionMode=false.
    ///   We normalize using uMin/uMax if available, else heuristics from polyline segment count.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MayaMotionPathDriver : MonoBehaviour
    {
        public int Priority = 0;

        [Header("Binding")]
        public MotionPathNode Source;
        public Transform CurveTransform;

        [Tooltip("Multiple driven transforms (recommended).")]
        public List<Transform> ConstrainedTargets = new List<Transform>(4);

        [Header("WorldUp (cached from node; node fields may be animated too)")]
        public Transform WorldUpObject;
        public Vector3 WorldUpVector = Vector3.up;

        private MayaCurvePolylineComponent _curve;

        // for stable up (reduce sudden flips)
        private bool _hasLastUp;
        private Vector3 _lastUp;

        private void Awake()
        {
            if (Source == null) Source = GetComponent<MotionPathNode>();
            ResolveCurveComponent();
        }

        private void OnEnable()
        {
            if (Source == null) Source = GetComponent<MotionPathNode>();
            ResolveCurveComponent();

            MayaMotionPathManager.EnsureExists();
            MayaMotionPathManager.Register(this);
        }

        private void OnDisable()
        {
            MayaMotionPathManager.Unregister(this);
        }

        private void ResolveCurveComponent()
        {
            _curve = CurveTransform != null ? CurveTransform.GetComponent<MayaCurvePolylineComponent>() : null;
        }

        internal void ApplyInternal()
        {
            if (Source == null || CurveTransform == null) return;

            if (_curve == null) ResolveCurveComponent();
            if (_curve == null || !_curve.HasValid) return;

            if (ConstrainedTargets == null || ConstrainedTargets.Count == 0) return;

            // Keep in sync if node updated bindings
            WorldUpVector = Source.worldUpVector;

            float t01 = ComputeT01(Source.uValue, Source.fractionMode, Source.uMin, Source.uMax, _curve);

            // Position
            Vector3 pos = _curve.EvaluateWorldPosition(CurveTransform, t01);

            // Orientation
            bool doFollow = Source.follow;

            Quaternion worldRot = Quaternion.identity;
            if (doFollow)
            {
                Vector3 tan = _curve.EvaluateWorldTangent(CurveTransform, t01);
                if (Source.inverseFront) tan = -tan;

                Vector3 up = ComputeWorldUp(t01, tan);

                // Make orthonormal frame + stabilize vs previous frame
                Orthonormalize(tan, ref up, out var side);

                if (_hasLastUp)
                {
                    // If flipped, flip side/up to keep continuity
                    if (Vector3.Dot(up, _lastUp) < 0f)
                    {
                        up = -up;
                        side = -side;
                    }
                }
                _lastUp = up;
                _hasLastUp = true;

                // Base look rotation (tangent forward, up)
                var look = Quaternion.LookRotation(tan, up);

                // Bank (best-effort)
                if (Source.bank)
                {
                    float roll = ComputeBankRollDeg(t01, tan, up, Source.bankScale, Source.bankThreshold);
                    if (Mathf.Abs(roll) > 1e-6f)
                        look = Quaternion.AngleAxis(roll, tan) * look;
                }

                // Twists (best-effort)
                if (Mathf.Abs(Source.sideTwist) > 1e-6f)
                    look = Quaternion.AngleAxis(Source.sideTwist, side) * look;

                if (Mathf.Abs(Source.upTwist) > 1e-6f)
                    look = Quaternion.AngleAxis(Source.upTwist, up) * look;

                if (Mathf.Abs(Source.frontTwist) > 1e-6f)
                    look = Quaternion.AngleAxis(Source.frontTwist, tan) * look;

                // Axis remap: Unity LookRotation assumes local +Z forward and +Y up.
                Vector3 localF = AxisFromMayaIndex(Source.followAxis);
                Vector3 localU = AxisFromMayaIndex(Source.upAxis);

                // extra inverseUp toggle
                if (Source.inverseUp) localU = -localU;

                // Ensure valid basis
                if (localF.sqrMagnitude < 1e-12f) localF = Vector3.forward;
                if (localU.sqrMagnitude < 1e-12f) localU = Vector3.up;
                if (Vector3.Cross(localF, localU).sqrMagnitude < 1e-8f) localU = Vector3.up;

                var qAxis = Quaternion.LookRotation(localF.normalized, localU.normalized);

                worldRot = look * Quaternion.Inverse(qAxis);
            }

            // Apply to all driven transforms
            for (int i = 0; i < ConstrainedTargets.Count; i++)
            {
                var tf = ConstrainedTargets[i];
                if (tf == null) continue;

                tf.position = pos;
                if (doFollow) tf.rotation = worldRot;
            }
        }

        private static float ComputeT01(float uValue, bool fractionMode, float uMin, float uMax, MayaCurvePolylineComponent curve)
        {
            if (fractionMode)
                return Mathf.Clamp01(uValue);

            // If uMin/uMax are usable, normalize from them
            if (IsFinite(uMin) && IsFinite(uMax) && Mathf.Abs(uMax - uMin) > 1e-6f)
                return Mathf.Clamp01((uValue - uMin) / (uMax - uMin));

            // Heuristic: if uValue seems already 0..1
            if (uValue >= -0.0001f && uValue <= 1.0001f)
                return Mathf.Clamp01(uValue);

            // Heuristic: treat uValue as segment index domain
            int segCount = 1;
            if (curve != null && curve.localPoints != null && curve.localPoints.Length >= 2)
                segCount = curve.closed ? curve.localPoints.Length : (curve.localPoints.Length - 1);

            if (segCount <= 0) segCount = 1;

            return Mathf.Clamp01(uValue / segCount);
        }

        private Vector3 ComputeWorldUp(float t01, Vector3 tan)
        {
            // Maya worldUpType best-effort:
            // 0: scene (vector), 1/2: object, 3: normal (from curve), otherwise vector
            int wut = Source != null ? Source.worldUpType : 0;

            Vector3 up;

            if (wut == 1 || wut == 2)
            {
                if (WorldUpObject != null && WorldUpObject.up.sqrMagnitude > 1e-12f)
                    up = WorldUpObject.up.normalized;
                else
                    up = (WorldUpVector.sqrMagnitude > 1e-12f ? WorldUpVector.normalized : Vector3.up);
            }
            else if (wut == 3)
            {
                // Curve normal (best-effort) from tangent derivative
                up = ComputeCurveNormalBestEffort(t01, tan);
            }
            else
            {
                up = (WorldUpVector.sqrMagnitude > 1e-12f ? WorldUpVector.normalized : Vector3.up);
                if (WorldUpObject != null && WorldUpObject.up.sqrMagnitude > 1e-12f)
                    up = WorldUpObject.up.normalized;
            }

            // Avoid parallel to tan
            if (Vector3.Cross(up, tan).sqrMagnitude < 1e-8f)
            {
                up = Vector3.Cross(tan, Vector3.right);
                if (up.sqrMagnitude < 1e-8f) up = Vector3.Cross(tan, Vector3.up);
                if (up.sqrMagnitude < 1e-12f) up = Vector3.up;
                up.Normalize();
            }

            return up;
        }

        private Vector3 ComputeCurveNormalBestEffort(float t01, Vector3 tan)
        {
            // Sample slightly ahead to get derivative of tangent
            const float dt = 0.0025f;
            float t2 = Mathf.Clamp01(t01 + dt);

            Vector3 tan2 = _curve.EvaluateWorldTangent(CurveTransform, t2);
            if (Source != null && Source.inverseFront) tan2 = -tan2;

            Vector3 d = (tan2 - tan);
            // remove parallel component
            d -= tan * Vector3.Dot(d, tan);

            if (d.sqrMagnitude < 1e-10f)
            {
                var up = (WorldUpVector.sqrMagnitude > 1e-12f ? WorldUpVector.normalized : Vector3.up);
                if (WorldUpObject != null && WorldUpObject.up.sqrMagnitude > 1e-12f)
                    up = WorldUpObject.up.normalized;
                return up;
            }

            return d.normalized;
        }

        private float ComputeBankRollDeg(float t01, Vector3 tan, Vector3 up, float bankScale, float bankThresholdDeg)
        {
            // Bank based on tangent change
            const float dt = 0.0025f;
            float t2 = Mathf.Clamp01(t01 + dt);

            Vector3 tan2 = _curve.EvaluateWorldTangent(CurveTransform, t2);
            if (Source != null && Source.inverseFront) tan2 = -tan2;

            float dot = Mathf.Clamp(Vector3.Dot(tan, tan2), -1f, 1f);
            float angleDeg = Mathf.Acos(dot) * Mathf.Rad2Deg;

            if (angleDeg < Mathf.Max(0f, bankThresholdDeg))
                return 0f;

            Vector3 cr = Vector3.Cross(tan, tan2);
            float sign = Mathf.Sign(Vector3.Dot(cr, up));
            if (sign == 0f) sign = 1f;

            return angleDeg * bankScale * sign;
        }

        private static void Orthonormalize(Vector3 forward, ref Vector3 up, out Vector3 side)
        {
            if (forward.sqrMagnitude < 1e-12f) forward = Vector3.forward;
            forward.Normalize();

            // side = up x forward
            side = Vector3.Cross(up, forward);
            if (side.sqrMagnitude < 1e-12f)
            {
                // pick any up not parallel
                up = Vector3.Cross(forward, Vector3.right);
                if (up.sqrMagnitude < 1e-8f) up = Vector3.Cross(forward, Vector3.up);
                if (up.sqrMagnitude < 1e-12f) up = Vector3.up;
                up.Normalize();
                side = Vector3.Cross(up, forward);
            }

            side.Normalize();
            up = Vector3.Cross(forward, side).normalized;
        }

        private static Vector3 AxisFromMayaIndex(int axis)
        {
            // Maya motionPath frontAxis/upAxis:
            // 0:X 1:Y 2:Z 3:-X 4:-Y 5:-Z
            switch (axis)
            {
                case 0: return Vector3.right;
                case 1: return Vector3.up;
                case 2: return Vector3.forward;
                case 3: return -Vector3.right;
                case 4: return -Vector3.up;
                case 5: return -Vector3.forward;
                default: return Vector3.forward;
            }
        }

        private static bool IsFinite(float v)
        {
            // Unity environments differ; keep it explicit.
            return !(float.IsNaN(v) || float.IsInfinity(v));
        }
    }
}
