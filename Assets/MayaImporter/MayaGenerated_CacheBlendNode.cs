// NodeType: cacheBlend (Phase C: non-empty decode)

using UnityEngine;
using MayaImporter;
using MayaImporter.Core;

namespace MayaImporter.Generated
{
    [DisallowMultipleComponent]
    [MayaNodeType("cacheBlend")]
    public sealed class MayaGenerated_CacheBlendNode : MayaPhaseCNodeBase
    {
        [Header("Decoded (cacheBlend)")]
        [SerializeField] private float weight = 0.5f;
        [SerializeField] private bool enabled = true;

        [SerializeField] private string incomingWeight;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            bool muted = ReadBool(false, ".mute", "mute", ".disabled", "disabled");
            bool explicitEnabled = ReadBool(true, ".enabled", "enabled", ".enable", "enable");
            enabled = !muted && explicitEnabled;

            weight = ReadFloat(0.5f, ".weight", "weight", ".w", "w", ".blend", "blend");
            incomingWeight = FindLastIncomingTo("weight", "w", "blend");

            SetNotes($"{NodeType} '{NodeName}' decoded: enabled={enabled}, weight={weight}, incomingWeight={(string.IsNullOrEmpty(incomingWeight) ? "none" : incomingWeight)}");
        }
    }
}
