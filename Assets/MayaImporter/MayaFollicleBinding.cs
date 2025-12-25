using UnityEngine;

namespace MayaImporter.Hair
{
    /// <summary>
    /// Maya follicle ‚Ì Unity ‘¤•Û{‰ğŒˆÏ‚İQÆB
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MayaFollicleBinding : MonoBehaviour
    {
        [Header("Source")]
        public string SourceNodeName;

        [Header("Attach Target (best-effort)")]
        public string MeshNodeName;
        public Transform MeshTransform;

        [Header("Params")]
        public float ParameterU = 0.5f;
        public float ParameterV = 0.5f;

        [Header("Resolved Root")]
        public Transform RootTransform;

        [Header("Notes")]
        public string Notes;
    }
}
