// MAYAIMPORTER_PATCH_V4: mb provenance/evidence + audit determinism (generated 2026-01-05)
using UnityEngine;
using MayaImporter.Utils;

namespace MayaImporter.Core
{
    /// <summary>
    /// Maya transform stack math (Unity-only).
    /// Builds the local matrix in Maya space from channels + pivots/shear/rotateAxis/jointOrient/offsetParentMatrix.
    /// This is the backbone for "strict" reconstruction without Autodesk/Maya API.
    /// </summary>
    public static class MayaTransformStackMath
    {
        /// <summary>
        /// Maya rotateOrder: 0=xyz 1=yzx 2=zxy 3=xzy 4=yxz 5=zyx
        /// Returns a quaternion that applies rotations in the specified order (intrinsic).
        /// </summary>
        public static Quaternion EulerToQuaternion(Vector3 eulerDeg, int rotateOrder)
        {
            float x = eulerDeg.x;
            float y = eulerDeg.y;
            float z = eulerDeg.z;

            Quaternion qx = Quaternion.AngleAxis(x, Vector3.right);
            Quaternion qy = Quaternion.AngleAxis(y, Vector3.up);
            Quaternion qz = Quaternion.AngleAxis(z, Vector3.forward);

            // Matches MayaEulerRotationApplier.ToQuaternion convention in this project.
            switch (rotateOrder)
            {
                case 0: return qx * qy * qz; // xyz
                case 1: return qy * qz * qx; // yzx
                case 2: return qz * qx * qy; // zxy
                case 3: return qx * qz * qy; // xzy
                case 4: return qy * qx * qz; // yxz
                case 5: return qz * qy * qx; // zyx
                default: return qx * qy * qz;
            }
        }

        public static Matrix4x4 ShearMatrix(Vector3 sh)
        {
            // Maya shear: (xy, xz, yz)
            var m = Matrix4x4.identity;
            m.m01 = sh.x;
            m.m02 = sh.y;
            m.m12 = sh.z;
            return m;
        }

        /// <summary>
        /// Build local matrix in Maya space (parent space) including:
        /// T, rotatePivot(+translate), rotate (with rotateOrder), rotateAxis, scalePivot(+translate), shear, scale, and optional jointOrient and offsetParentMatrix.
        /// </summary>
        public static Matrix4x4 BuildLocalMatrixMaya(
            Vector3 t,
            Vector3 rEulerDeg,
            Vector3 s,
            int rotateOrder,
            Vector3 rotateAxisEulerDeg,
            Vector3 rotatePivot,
            Vector3 rotatePivotTranslate,
            Vector3 scalePivot,
            Vector3 scalePivotTranslate,
            Vector3 shear,
            Quaternion jointOrientPreRotation,
            bool hasJointOrient,
            Matrix4x4 offsetParentMatrixMaya,
            bool hasOffsetParentMatrix)
        {
            // Rotation stack (Maya space)
            var qRot = EulerToQuaternion(rEulerDeg, rotateOrder);

            // rotateAxis is stored as Euler degrees in Maya (best-effort assume XYZ order)
            var qRa = Quaternion.Euler(rotateAxisEulerDeg);

            // Maya transform stack uses R then rotateAxis
            var q = qRot * qRa;

            if (hasJointOrient)
                q = jointOrientPreRotation * q;

            // Matrix assembly (Maya local)
            var M =
                Matrix4x4.Translate(t) *
                Matrix4x4.Translate(rotatePivotTranslate) *
                Matrix4x4.Translate(rotatePivot) *
                Matrix4x4.Rotate(q) *
                Matrix4x4.Translate(-rotatePivot) *
                Matrix4x4.Translate(scalePivotTranslate) *
                Matrix4x4.Translate(scalePivot) *
                ShearMatrix(shear) *
                Matrix4x4.Scale(s) *
                Matrix4x4.Translate(-scalePivot);

            if (hasOffsetParentMatrix)
                M = offsetParentMatrixMaya * M;

            return M;
        }

        public static void ApplyLocalMatrixToTransform(Transform tr, in Matrix4x4 localUnity)
        {
            MatrixUtil.DecomposeTRS(localUnity, out var t, out var r, out var s);
            tr.localPosition = t;
            tr.localRotation = r;
            tr.localScale = s;
        }
    }
}
