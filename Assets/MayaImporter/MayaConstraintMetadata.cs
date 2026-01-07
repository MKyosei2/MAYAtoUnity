// MAYAIMPORTER_PATCH_V4: mb provenance/evidence + audit determinism (generated 2026-01-05)
using System.Collections.Generic;
using UnityEngine;

namespace MayaImporter.Components
{
    /// <summary>
    /// Generic constraint metadata container used by Maya constraint node components.
    /// Stored on the constraint node GameObject (same object as MayaNodeComponentBase).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MayaConstraintMetadata : MonoBehaviour
    {
        [Header("Type")]
        public string constraintType = ""; // "point","orient","parent","aim","scale"

        [Header("Common")]
        public bool maintainOffset = false;

        // Interpolation (orientConstraint / parentConstraint rotation blending)
        // Maya: 0=NoFlip 1=Average 2=Shortest 3=Longest 4=Cache
        public int interpType = 1;
        public int interpCache = 0;

        // Rest Position (general constraint attributes)
        public bool enableRestPosition = false;
        public Vector3 restTranslate = Vector3.zero;  // world space
        public Vector3 restRotate = Vector3.zero;     // world space euler (degrees)
        public Vector3 restScale = Vector3.one;       // multiplicative

        // parentConstraint decomposition target (best-effort metadata)
        public bool useDecompositionTarget = false;
        public Vector3 rotationDecompositionTarget = Vector3.zero;

        // Axis masks (skip)
        public bool drivePosX = true, drivePosY = true, drivePosZ = true;
        public bool driveRotX = true, driveRotY = true, driveRotZ = true;
        public bool driveScaleX = true, driveScaleY = true, driveScaleZ = true;

        [Header("Aim Only")]
        public Vector3 aimAxis = Vector3.forward;
        public Vector3 upAxis = Vector3.up;
        public Vector3 worldUpVector = Vector3.up;
        public string worldUpObjectNodeName = null;

        [Header("Targets")]
        public List<Target> targets = new List<Target>();

        [System.Serializable]
        public struct Target
        {
            public string targetNodeName;
            public float weight;

            // parentConstraint target offset (targetOffsetTranslate/Rotate)
            public Vector3 offsetTranslate;
            public Vector3 offsetRotate;

            // scaleConstraint has no per-target offset in Maya node; keep for future extensibility
            public Vector3 offsetScale;
        }
    }
}
