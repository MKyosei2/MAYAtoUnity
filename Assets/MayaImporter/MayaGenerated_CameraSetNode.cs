// PATCH: ProductionImpl v6 (Unity-only, retention-first)
// NodeType: cameraSet (Phase C: non-empty decode)

using UnityEngine;
using MayaImporter;
using MayaImporter.Core;

namespace MayaImporter.Generated
{
    [DisallowMultipleComponent]
    [MayaNodeType("cameraSet")]
    public sealed class MayaGenerated_CameraSetNode : MayaPhaseCNodeBase
    {
        [Header("Decoded (cameraSet)")]
        [SerializeField] private bool enabled = true;
        [SerializeField] private int activeIndex;
        [SerializeField] private string activeCamera;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            bool muted = ReadBool(false, ".mute", "mute", ".disabled", "disabled");
            bool explicitEnabled = ReadBool(true, ".enabled", "enabled", ".enable", "enable");
            enabled = !muted && explicitEnabled;

            activeIndex = ReadInt(0, ".active", "active", ".activeIndex", "activeIndex", ".index", "index");
            activeCamera = ReadString("", ".activeCamera", "activeCamera", ".camera", "camera", ".cam", "cam");

            SetNotes($"{NodeType} '{NodeName}' decoded: enabled={enabled}, activeIndex={activeIndex}, activeCamera='{activeCamera}' (membership via connections preserved)");
        }
    }
}
