using UnityEngine;

namespace MayaImporter.Runtime
{
    /// <summary>
    /// Portfolio / Audit proof for skinCluster application.
    /// - attach to target mesh AND skinCluster node for traceability.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MayaSkinApplyResult : MonoBehaviour
    {
        [Header("Identity")]
        public string skinClusterNodeName;
        public string targetObjectName;

        [Header("Counts")]
        public int influenceCount;
        public int fullWeightCount;
        public int maxVertexIndex;
        public int maxInfluenceIndex;

        [Header("Apply Status")]
        public bool appliedToUnity;
        [TextArea(3, 8)]
        public string note;

        [Header("Unity limitation clamp stats")]
        public int unityClampedVertices;
        public int unityClampedInfluences;

        [Header("Bone resolution (best-effort estimate)")]
        public int missingBoneEstimate;

        [Header("Timestamp (UTC ISO-8601)")]
        public string updatedUtc;
    }
}
