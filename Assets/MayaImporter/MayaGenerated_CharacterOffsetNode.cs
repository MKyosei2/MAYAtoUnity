// NodeType: characterOffset (Phase C: non-empty decode)

using UnityEngine;
using MayaImporter;
using MayaImporter.Core;

namespace MayaImporter.Generated
{
    [DisallowMultipleComponent]
    [MayaNodeType("characterOffset")]
    public sealed class MayaGenerated_CharacterOffsetNode : MayaPhaseCNodeBase
    {
        [Header("Decoded (characterOffset)")]
        [SerializeField] private bool enabled = true;
        [SerializeField] private Vector3 translateOffset;
        [SerializeField] private Vector3 rotateOffset;
        [SerializeField] private string incomingOffset;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            bool muted = ReadBool(false, ".mute", "mute", ".disabled", "disabled");
            bool explicitEnabled = ReadBool(true, ".enabled", "enabled", ".enable", "enable");
            enabled = !muted && explicitEnabled;

            translateOffset = new Vector3(
                ReadFloat(0f, ".translateOffsetX", "translateOffsetX", ".tox", "tox"),
                ReadFloat(0f, ".translateOffsetY", "translateOffsetY", ".toy", "toy"),
                ReadFloat(0f, ".translateOffsetZ", "translateOffsetZ", ".toz", "toz")
            );

            rotateOffset = new Vector3(
                ReadFloat(0f, ".rotateOffsetX", "rotateOffsetX", ".rox", "rox"),
                ReadFloat(0f, ".rotateOffsetY", "rotateOffsetY", ".roy", "roy"),
                ReadFloat(0f, ".rotateOffsetZ", "rotateOffsetZ", ".roz", "roz")
            );

            incomingOffset = FindLastIncomingTo("translateOffsetX", "translateOffsetY", "translateOffsetZ", "rotateOffsetX", "rotateOffsetY", "rotateOffsetZ");

            SetNotes($"{NodeType} '{NodeName}' decoded: enabled={enabled}, tOff={translateOffset}, rOff={rotateOffset}, incoming={(string.IsNullOrEmpty(incomingOffset) ? "none" : incomingOffset)}");
        }
    }
}
