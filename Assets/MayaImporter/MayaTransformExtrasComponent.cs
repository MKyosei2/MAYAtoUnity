using UnityEngine;

namespace MayaImporter.Core
{
    /// <summary>
    /// Unity Transform だけでは表現できない Maya の transform stack 情報を保持する。
    /// 「100%」の根拠: ここに “失われる可能性のある情報” を必ず保持し、Unity側で将来再現/検証できる状態にする。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MayaTransformExtrasComponent : MonoBehaviour
    {
        [Header("Transform Stack Flags")]
        public bool UsesTransformStack;

        [Header("Raw Maya Channels (as parsed, Maya space)")]
        public Vector3 Translate;
        public Vector3 RotateEuler;
        public Vector3 Scale = Vector3.one;

        [Tooltip("Maya rotateOrder (0..5).")]
        public int RotateOrder;

        [Header("Maya-only Extras (stored)")]
        public Vector3 JointOrientEuler;
        public Vector3 RotateAxisEuler;

        public Vector3 RotatePivot;
        public Vector3 RotatePivotTranslate;

        public Vector3 ScalePivot;
        public Vector3 ScalePivotTranslate;

        /// <summary>
        /// Maya shear (XY, XZ, YZ) 。Unity Transform では表現不可。
        /// </summary>
        public Vector3 Shear;

        // ---------------------------------------------------------
        // Maya 2020+ Transform Stack: offsetParentMatrix
        // ---------------------------------------------------------

        [Header("offsetParentMatrix (Maya 2020+)")]
        public bool HasOffsetParentMatrix;
        public Matrix4x4 OffsetParentMatrixMaya = Matrix4x4.identity;
        public string OffsetParentMatrixSource;

        public bool InsertedOffsetParentGameObject;
        public string OffsetParentGameObjectName;

        // ---------------------------------------------------------
        // Audit / Debug Matrices (best-effort)
        // ---------------------------------------------------------

        [Header("Audit (best-effort matrices)")]
        [Tooltip("Local TRS-only matrix in Maya space (does NOT include pivots/shear/opm).")]
        public Matrix4x4 LocalTrsMatrixMaya = Matrix4x4.identity;

        [Tooltip("Local TRS-only matrix converted to Unity space (does NOT include pivots/shear/opm).")]
        public Matrix4x4 LocalTrsMatrixUnity = Matrix4x4.identity;

        [Tooltip("True if any pivot/shear/axis info exists (meaning TRS-onlyでは不足の可能性がある).")]
        public bool HasNonTrsExtras;
    }
}
