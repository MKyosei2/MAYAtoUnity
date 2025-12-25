using System;
using System.Collections.Generic;
using UnityEngine;

namespace MayaImporter.Constraints
{
    public enum MayaSurfaceConstraintKind
    {
        Geometry,
        Normal,
        Tangent,
        PointOnPoly,
    }

    /// <summary>
    /// Unity-only evaluator for Maya surface-related constraints:
    /// - geometryConstraint: position to closest point
    /// - normalConstraint: rotation to align to normal
    /// - tangentConstraint: rotation to align to tangent (with normal as up)
    /// - pointOnPolyConstraint: position (and optional rotation) by UV (best-effort)
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MayaSurfaceConstraintDriver : MonoBehaviour
    {
        [Serializable]
        public sealed class Target
        {
            public Transform Transform;
            public float Weight = 1f;
        }

        [Header("Constraint")]
        public MayaSurfaceConstraintKind Kind;
        public Transform Constrained;
        public List<Target> Targets = new List<Target>(8);

        [Header("Evaluation")]
        public int Priority = 0;
        public bool MaintainOffset = true;

        [Header("PointOnPoly (best-effort)")]
        public bool UseUV = false;
        public float U = 0f;
        public float V = 0f;

        [Header("Orientation Axes (Normal/Tangent)")]
        public Vector3 LocalAimAxis = Vector3.forward; // which local axis should aim to normal/tangent
        public Vector3 LocalUpAxis = Vector3.up;       // which local axis should align to up

        [Header("WorldUp (optional)")]
        public Transform WorldUpObject;
        public Vector3 WorldUpVector = Vector3.up;

        private bool _offsetReady;
        private Vector3 _posOffsetWS;
        private Quaternion _rotOffset = Quaternion.identity;

        private void Awake()
        {
            if (Constrained == null) Constrained = transform;
        }

        private void OnEnable()
        {
            if (Constrained == null) Constrained = transform;
            MayaSurfaceConstraintManager.EnsureExists();
            MayaSurfaceConstraintManager.Register(this);
        }

        private void OnDisable()
        {
            MayaSurfaceConstraintManager.Unregister(this);
        }

        internal void ApplyConstraintInternal()
        {
            if (Constrained == null) return;
            if (Targets == null || Targets.Count == 0) return;

            // Blend samples
            Vector3 sumP = Vector3.zero;
            Vector3 sumN = Vector3.zero;
            Vector3 sumT = Vector3.zero;
            float sumW = 0f;

            Vector3 query = Constrained.position;

            for (int i = 0; i < Targets.Count; i++)
            {
                var t = Targets[i];
                if (t == null || t.Transform == null) continue;

                float w = t.Weight;
                if (w <= 0f) continue;

                if (!MayaMeshSamplingUtil.TryGetMesh(t.Transform, out var mesh, out var meshTf, out _))
                    continue;

                MayaMeshSamplingUtil.Sample s;
                if (Kind == MayaSurfaceConstraintKind.PointOnPoly && UseUV)
                {
                    s = MayaMeshSamplingUtil.SampleByUVOnMeshWS(mesh, meshTf, U, V);
                    if (!s.Valid)
                        s = MayaMeshSamplingUtil.ClosestPointOnMeshWS(mesh, meshTf, query);
                }
                else
                {
                    s = MayaMeshSamplingUtil.ClosestPointOnMeshWS(mesh, meshTf, query);
                }

                if (!s.Valid) continue;

                sumP += s.PositionWS * w;
                sumN += s.NormalWS * w;
                sumT += s.TangentWS * w;
                sumW += w;
            }

            if (sumW <= 1e-8f) return;

            Vector3 basePos = sumP / sumW;

            Vector3 baseN = sumN.sqrMagnitude > 1e-12f ? (sumN / sumW).normalized : Vector3.up;
            Vector3 baseT = sumT.sqrMagnitude > 1e-12f ? (sumT / sumW).normalized : Vector3.forward;

            // Determine base rotation (if used)
            Quaternion baseRot = Constrained.rotation;

            if (Kind == MayaSurfaceConstraintKind.Normal)
            {
                Vector3 up = GetWorldUp(baseN);
                baseRot = RotationFromAxes(baseN, up);
            }
            else if (Kind == MayaSurfaceConstraintKind.Tangent)
            {
                // Use tangent as forward, normal as up (stable), fallback to worldUp if needed
                Vector3 up = baseN.sqrMagnitude > 1e-12f ? baseN : GetWorldUp(baseT);
                baseRot = RotationFromAxes(baseT, up);
            }
            else
            {
                // Geometry / PointOnPoly: rotation unchanged by default
            }

            // Maintain offset (captured at first valid evaluation)
            if (MaintainOffset && !_offsetReady)
            {
                _posOffsetWS = Constrained.position - basePos;
                _rotOffset = Constrained.rotation * Quaternion.Inverse(baseRot);
                _offsetReady = true;
            }

            // Apply
            if (Kind == MayaSurfaceConstraintKind.Geometry || Kind == MayaSurfaceConstraintKind.PointOnPoly)
            {
                Constrained.position = MaintainOffset ? (basePos + _posOffsetWS) : basePos;
            }
            else
            {
                // Normal/Tangent often also pins position in Maya setups; but we keep it conservative.
                // If you want position too, make GeometryConstraint alongside.
            }

            if (Kind == MayaSurfaceConstraintKind.Normal || Kind == MayaSurfaceConstraintKind.Tangent)
            {
                Constrained.rotation = MaintainOffset ? (_rotOffset * baseRot) : baseRot;
            }
        }

        private Vector3 GetWorldUp(Vector3 fallbackAxis)
        {
            Vector3 up = WorldUpVector.sqrMagnitude > 1e-12f ? WorldUpVector.normalized : Vector3.up;
            if (WorldUpObject != null && WorldUpObject.up.sqrMagnitude > 1e-12f)
                up = WorldUpObject.up.normalized;

            // Avoid parallel with forward axis
            if (Vector3.Cross(up, fallbackAxis).sqrMagnitude < 1e-8f)
            {
                up = Vector3.Cross(fallbackAxis, Vector3.right);
                if (up.sqrMagnitude < 1e-8f) up = Vector3.Cross(fallbackAxis, Vector3.up);
                if (up.sqrMagnitude < 1e-12f) up = Vector3.up;
                up.Normalize();
            }

            return up;
        }

        private Quaternion RotationFromAxes(Vector3 desiredForward, Vector3 desiredUp)
        {
            if (desiredForward.sqrMagnitude < 1e-12f) desiredForward = Vector3.forward;
            if (desiredUp.sqrMagnitude < 1e-12f) desiredUp = Vector3.up;

            desiredForward.Normalize();
            desiredUp.Normalize();

            var target = Quaternion.LookRotation(desiredForward, desiredUp);

            var localF = LocalAimAxis.sqrMagnitude > 1e-12f ? LocalAimAxis.normalized : Vector3.forward;
            var localU = LocalUpAxis.sqrMagnitude > 1e-12f ? LocalUpAxis.normalized : Vector3.up;

            // ensure not parallel
            if (Vector3.Cross(localF, localU).sqrMagnitude < 1e-8f)
                localU = Vector3.up;

            var axis = Quaternion.LookRotation(localF, localU);
            return target * Quaternion.Inverse(axis);
        }
    }
}
