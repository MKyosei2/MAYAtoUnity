using UnityEngine;

namespace MayaImporter.Runtime
{
    /// <summary>
    /// Portfolio / Audit proof for blendShape application.
    /// Attach to the base mesh GO.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MayaBlendShapeApplyResult : MonoBehaviour
    {
        [Header("Identity")]
        public string blendShapeNodeName;

        [Header("Counts")]
        public int metaTargetCount;
        public int decodedTargetCount;

        public int appliedTargetCount;
        public int skippedTargetCount;

        [Header("Apply Status")]
        public bool appliedToUnity;

        [TextArea(3, 8)]
        public string note;

        [Header("Timestamp (UTC ISO-8601)")]
        public string updatedUtc;
    }
}
