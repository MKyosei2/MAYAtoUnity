using UnityEngine;

namespace MayaImporter.IK
{
    /// <summary>
    /// Maya IK handle stored settings (Unity-only evaluation; no Maya/API).
    /// Runtime solve is done by MayaIkRuntimeSolver.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MayaIkHandleComponent : MonoBehaviour
    {
        [Header("Maya IK Handle")]
        public string SolverType;      // ikRPsolver / ikSCsolver / ikSplineSolver etc.

        [Header("Chain")]
        public Transform StartJoint;   // joint
        public Transform EndEffector;  // ikEffector node (usually child of EndJoint)
        public Transform EndJoint;     // resolved best-effort

        [Header("Controls")]
        [Tooltip("Preferred pole control object. Usually set by poleVectorConstraint.")]
        public Transform PoleVector;

        [Tooltip("If PoleVector transform is not provided, we can use the authored poleVector (double3) from ikHandle.")]
        public Vector3 PoleVectorValue;

        [Tooltip("True when PoleVectorValue was authored/read from ikHandle attributes.")]
        public bool HasPoleVectorValue;

        [Tooltip("Twist in degrees (ikHandle.twist).")]
        public float Twist;

        [Header("Generated Helpers")]
        [Tooltip("Auto-created helper transform when only PoleVectorValue exists.")]
        public Transform AutoPoleVectorTransform;

        // ---------------- Spline IK ----------------

        [Header("Spline IK (ikSplineSolver)")]
        [Tooltip("Connected spline curve node name (best-effort).")]
        public string SplineCurveNode;

        [Tooltip("Curve transform (or shape GO) used for spline IK.")]
        public Transform SplineCurve;

        [Tooltip("rootOnCurve (roc). If false, keep StartJoint position and offset the solved positions.")]
        public bool SplineRootOnCurve = true;

        [Tooltip("offset (ofs). Best-effort normalized offset along curve (0..1 recommended).")]
        public float SplineOffset = 0f;
    }
}
