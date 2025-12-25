using System;
using System.Collections.Generic;
using UnityEngine;
using MayaImporter.Animation;

namespace MayaImporter.Constraints
{
    public enum MayaConstraintKind
    {
        Point,
        Orient,
        Parent,
        Aim,
        Scale
    }

    [DisallowMultipleComponent]
    public sealed class MayaConstraintDriver : MonoBehaviour
    {
        [Serializable]
        public sealed class Target
        {
            public Transform Transform;
            public float Weight = 1f;

            public Matrix4x4 Offset = Matrix4x4.identity;
            public bool OffsetAuthored = false;

            public Vector3 ScaleOffset = Vector3.one;
            public bool ScaleOffsetAuthored = false;
        }

        [Header("Wiring")]
        public Transform Constrained;
        public MayaConstraintKind Kind = MayaConstraintKind.Point;

        [Header("Evaluation")]
        public bool DriveLocalChannels = true;
        public bool MaintainOffset = false;

        [Tooltip("Lower runs first when multiple constraints affect the same object.")]
        public int Priority = 0;

        [Header("Axis Masks (Skip)")]
        public bool DrivePosX = true, DrivePosY = true, DrivePosZ = true;
        public bool DriveRotX = true, DriveRotY = true, DriveRotZ = true;
        public bool DriveScaleX = true, DriveScaleY = true, DriveScaleZ = true;

        [Header("Offsets (Constraint-level)")]
        public Vector3 OffsetTranslate = Vector3.zero;
        public Vector3 OffsetRotateEuler = Vector3.zero;
        public Vector3 OffsetScale = Vector3.one;

        [Header("Rest Position")]
        public bool EnableRestPosition = false;
        public Vector3 RestTranslateWorld = Vector3.zero;
        public Vector3 RestRotateWorldEuler = Vector3.zero;
        public Vector3 RestScale = Vector3.one;

        [Header("Rotation Interp (orient/parent)")]
        // Maya: 0=NoFlip 1=Average 2=Shortest 3=Longest 4=Cache
        public int RotationInterpType = 1;
        public int RotationInterpCache = 0;

        [Header("Aim Settings (Aim Only)")]
        public Vector3 AimAxis = Vector3.forward;
        public Vector3 UpAxis = Vector3.up;
        public Vector3 WorldUpVector = Vector3.up;
        public Transform WorldUpObject = null;
        public int WorldUpType = 0;

        [Header("Targets")]
        public List<Target> Targets = new List<Target>();

        private MayaEulerRotationApplier _rotApplier;

        private bool _hasLastRotation = false;
        private Quaternion _lastWorldRotation = Quaternion.identity;

        private bool _initialized = false;

        private void OnEnable()
        {
            MayaConstraintManager.EnsureExists();
            MayaConstraintManager.Register(this);
            EnsureRotationApplierIfNeeded();
        }

        private void OnDisable()
        {
            MayaConstraintManager.Unregister(this);

            if (_rotApplier != null)
                _rotApplier.ClearOverride();
        }

        public void ForceReinitializeOffsets()
        {
            _initialized = false;
            _hasLastRotation = false;
        }

        internal void ApplyConstraintInternal()
        {
            if (Constrained == null) return;

            InitializeOffsetsIfNeeded();
            ApplyConstraintInternalImpl();
        }

        private void EnsureRotationApplierIfNeeded()
        {
            if (Constrained == null) return;
            if (_rotApplier == null) _rotApplier = Constrained.GetComponent<MayaEulerRotationApplier>();
        }

        private void InitializeOffsetsIfNeeded()
        {
            if (_initialized) return;
            _initialized = true;

            if (!MaintainOffset) return;

            // MaintainOffset auto init: only parentConstraint best-effort
            if (Kind == MayaConstraintKind.Parent)
            {
                for (int i = 0; i < Targets.Count; i++)
                {
                    var t = Targets[i];
                    if (t == null || t.Transform == null) continue;
                    if (t.OffsetAuthored) continue;

                    var targetWorld = t.Transform.localToWorldMatrix;
                    var constrainedWorld = Constrained.localToWorldMatrix;

                    t.Offset = targetWorld.inverse * constrainedWorld;
                    t.OffsetAuthored = true;
                }
            }
        }

        private void ApplyConstraintInternalImpl()
        {
            switch (Kind)
            {
                case MayaConstraintKind.Point:
                    {
                        if (TryComputeWeightedPositionWorld(out var wpos))
                            ApplyPosition(wpos);
                        else if (EnableRestPosition)
                            ApplyPosition(RestTranslateWorld);
                        break;
                    }
                case MayaConstraintKind.Orient:
                    {
                        if (TryComputeWeightedRotationWorld(out var wrot))
                            ApplyRotation(wrot);
                        else if (EnableRestPosition)
                            ApplyRotation(Quaternion.Euler(RestRotateWorldEuler));
                        break;
                    }
                case MayaConstraintKind.Parent:
                    {
                        bool okPos = TryComputeWeightedPositionWorld(out var wpos);
                        bool okRot = TryComputeWeightedRotationWorld(out var wrot);

                        if (!okPos && EnableRestPosition) wpos = RestTranslateWorld;
                        if (!okRot && EnableRestPosition) wrot = Quaternion.Euler(RestRotateWorldEuler);

                        if (okPos || EnableRestPosition) ApplyPosition(wpos);
                        if (okRot || EnableRestPosition) ApplyRotation(wrot);
                        break;
                    }
                case MayaConstraintKind.Aim:
                    {
                        if (TryComputeAimWorldRotation(out var aimRot))
                            ApplyRotation(aimRot);
                        else if (EnableRestPosition)
                            ApplyRotation(Quaternion.Euler(RestRotateWorldEuler));
                        break;
                    }
                case MayaConstraintKind.Scale:
                    {
                        if (TryComputeWeightedWorldScale(out var wscale))
                            ApplyScale(wscale);
                        else if (EnableRestPosition)
                            ApplyScale(RestScale);
                        break;
                    }
            }
        }

        // -------------------------
        // Position
        // -------------------------

        private bool TryComputeWeightedPositionWorld(out Vector3 worldPos)
        {
            float sumW = 0f;
            Vector3 sum = Vector3.zero;

            for (int i = 0; i < Targets.Count; i++)
            {
                var t = Targets[i];
                if (t == null || t.Transform == null) continue;

                float w = Mathf.Max(0f, t.Weight);
                if (w <= 0f) continue;

                var m = t.Transform.localToWorldMatrix * t.Offset;
                sum += (m.MultiplyPoint3x4(Vector3.zero) * w);
                sumW += w;
            }

            if (sumW <= 1e-8f)
            {
                worldPos = default;
                return false;
            }

            worldPos = sum / sumW;
            return IsFinite(worldPos);
        }

        private void ApplyPosition(Vector3 worldPos)
        {
            Vector3 localPos;

            if (DriveLocalChannels && Constrained.parent != null)
                localPos = Constrained.parent.InverseTransformPoint(worldPos);
            else
                localPos = worldPos;

            localPos += OffsetTranslate;

            var cur = Constrained.localPosition;
            if (DrivePosX) cur.x = localPos.x;
            if (DrivePosY) cur.y = localPos.y;
            if (DrivePosZ) cur.z = localPos.z;
            Constrained.localPosition = cur;
        }

        // -------------------------
        // Rotation
        // -------------------------

        private bool TryComputeWeightedRotationWorld(out Quaternion worldRot)
        {
            worldRot = Quaternion.identity;

            float sumW = 0f;
            int count = 0;

            Span<float> ws = stackalloc float[64];
            Span<Quaternion> qs = stackalloc Quaternion[64];

            for (int i = 0; i < Targets.Count; i++)
            {
                var t = Targets[i];
                if (t == null || t.Transform == null) continue;

                float w = Mathf.Max(0f, t.Weight);
                if (w <= 0f) continue;

                var m = t.Transform.localToWorldMatrix * t.Offset;
                var q = m.rotation;

                if (count < 64)
                {
                    ws[count] = w;
                    qs[count] = q;
                    count++;
                }
                sumW += w;
            }

            if (sumW <= 1e-8f || count == 0)
                return false;

            if (count == 1)
            {
                worldRot = qs[0];
                _hasLastRotation = true;
                _lastWorldRotation = worldRot;
                return true;
            }

            int tInterp = RotationInterpType;
            if (tInterp == 4) tInterp = 0; // Cache -> NoFlip best-effort

            if (tInterp == 2 || tInterp == 3)
            {
                Quaternion acc = qs[0];
                float accW = ws[0];

                for (int i = 1; i < count; i++)
                {
                    float w = ws[i];
                    float t = w / (accW + w);

                    Quaternion q2 = qs[i];
                    float dot = Quaternion.Dot(acc, q2);

                    if (tInterp == 2)
                    {
                        if (dot < 0f) q2 = Negate(q2);
                    }
                    else
                    {
                        if (dot > 0f) q2 = Negate(q2);
                    }

                    acc = Quaternion.Slerp(acc, q2, t);
                    accW += w;
                }

                worldRot = Normalize(acc);
                _hasLastRotation = true;
                _lastWorldRotation = worldRot;
                return true;
            }
            else
            {
                Quaternion refQ = qs[0];
                if (tInterp == 0 && _hasLastRotation) refQ = _lastWorldRotation;

                Vector4 sum = Vector4.zero;
                for (int i = 0; i < count; i++)
                {
                    Quaternion q = qs[i];
                    float w = ws[i];

                    if (Quaternion.Dot(q, refQ) < 0f)
                        q = Negate(q);

                    sum.x += q.x * w;
                    sum.y += q.y * w;
                    sum.z += q.z * w;
                    sum.w += q.w * w;
                }

                worldRot = Normalize(new Quaternion(sum.x, sum.y, sum.z, sum.w));
                _hasLastRotation = true;
                _lastWorldRotation = worldRot;
                return true;
            }
        }

        private void ApplyRotation(Quaternion worldRot)
        {
            EnsureRotationApplierIfNeeded();

            if (OffsetRotateEuler != Vector3.zero)
                worldRot = worldRot * Quaternion.Euler(OffsetRotateEuler);

            Quaternion localRot = worldRot;
            if (DriveLocalChannels && Constrained.parent != null)
                localRot = Quaternion.Inverse(Constrained.parent.rotation) * worldRot;

            if (_rotApplier != null && DriveLocalChannels)
            {
                bool all = DriveRotX && DriveRotY && DriveRotZ;

                if (all)
                {
                    _rotApplier.SetOverride(localRot);
                    _rotApplier.ApplyNow();
                }
                else
                {
                    _rotApplier.ClearOverride();

                    Vector3 desired = NormalizeEuler(localRot.eulerAngles);
                    Vector3 e = _rotApplier.eulerDeg;

                    if (DriveRotX) e.x = desired.x;
                    if (DriveRotY) e.y = desired.y;
                    if (DriveRotZ) e.z = desired.z;

                    _rotApplier.eulerDeg = e;
                    _rotApplier.ApplyNow();
                }
            }
            else
            {
                if (DriveLocalChannels) Constrained.localRotation = localRot;
                else Constrained.rotation = worldRot;
            }
        }

        // -------------------------
        // Aim (best-effort)
        // -------------------------

        private bool TryComputeAimWorldRotation(out Quaternion worldRot)
        {
            worldRot = Quaternion.identity;

            if (!TryComputeWeightedPositionWorld(out var targetWorldPos))
                return false;

            Vector3 dir = (targetWorldPos - Constrained.position);
            if (dir.sqrMagnitude <= 1e-10f) return false;
            dir.Normalize();

            Vector3 up = WorldUpVector;

            if (WorldUpType == 1 && WorldUpObject != null) up = WorldUpObject.up;
            else if (WorldUpType == 2 && WorldUpObject != null) up = WorldUpObject.up;
            else if (WorldUpType == 3) up = WorldUpVector;

            Quaternion aimQ = Quaternion.FromToRotation(AimAxis.normalized, dir);

            Vector3 rotatedUp = aimQ * UpAxis.normalized;
            if (rotatedUp.sqrMagnitude < 1e-8f) rotatedUp = Vector3.up;

            Quaternion upQ = Quaternion.FromToRotation(rotatedUp, up.normalized);
            worldRot = Normalize(upQ * aimQ);
            return true;
        }

        // -------------------------
        // Scale
        // -------------------------

        private bool TryComputeWeightedWorldScale(out Vector3 worldScale)
        {
            worldScale = Vector3.one;

            float sumW = 0f;
            bool allPositive = true;

            double lnX = 0, lnY = 0, lnZ = 0;
            Vector3 arith = Vector3.zero;

            int count = 0;

            for (int i = 0; i < Targets.Count; i++)
            {
                var t = Targets[i];
                if (t == null || t.Transform == null) continue;

                float w = Mathf.Max(0f, t.Weight);
                if (w <= 0f) continue;

                Vector3 s = t.Transform.lossyScale;
                if (t.ScaleOffsetAuthored)
                    s = new Vector3(s.x * t.ScaleOffset.x, s.y * t.ScaleOffset.y, s.z * t.ScaleOffset.z);

                arith += s * w;

                if (s.x <= 0f || s.y <= 0f || s.z <= 0f) allPositive = false;

                if (allPositive)
                {
                    lnX += (double)w * Math.Log(s.x);
                    lnY += (double)w * Math.Log(s.y);
                    lnZ += (double)w * Math.Log(s.z);
                }

                sumW += w;
                count++;
            }

            if (sumW <= 1e-8f || count == 0)
                return false;

            if (allPositive)
            {
                worldScale = new Vector3(
                    (float)Math.Exp(lnX / sumW),
                    (float)Math.Exp(lnY / sumW),
                    (float)Math.Exp(lnZ / sumW));
            }
            else
            {
                worldScale = arith / sumW;
            }

            return IsFinite(worldScale);
        }

        private void ApplyScale(Vector3 worldScale)
        {
            Vector3 parentLossy = Constrained.parent ? Constrained.parent.lossyScale : Vector3.one;
            Vector3 local = new Vector3(
                SafeDiv(worldScale.x, parentLossy.x),
                SafeDiv(worldScale.y, parentLossy.y),
                SafeDiv(worldScale.z, parentLossy.z));

            local = new Vector3(local.x * OffsetScale.x, local.y * OffsetScale.y, local.z * OffsetScale.z);

            var cur = Constrained.localScale;
            if (DriveScaleX) cur.x = local.x;
            if (DriveScaleY) cur.y = local.y;
            if (DriveScaleZ) cur.z = local.z;
            Constrained.localScale = cur;
        }

        private static bool IsFinite(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);

        private static Quaternion Normalize(Quaternion q)
        {
            float m = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            if (m <= 1e-12f) return Quaternion.identity;
            float inv = 1f / m;
            return new Quaternion(q.x * inv, q.y * inv, q.z * inv, q.w * inv);
        }

        private static Quaternion Negate(Quaternion q) => new Quaternion(-q.x, -q.y, -q.z, -q.w);

        private static float SafeDiv(float a, float b)
        {
            if (Mathf.Abs(b) <= 1e-12f) return 0f;
            return a / b;
        }

        private static Vector3 NormalizeEuler(Vector3 euler0to360)
            => new Vector3(NormDeg(euler0to360.x), NormDeg(euler0to360.y), NormDeg(euler0to360.z));

        private static float NormDeg(float d)
        {
            d %= 360f;
            if (d > 180f) d -= 360f;
            if (d < -180f) d += 360f;
            return d;
        }
    }
}
