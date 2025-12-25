// NodeType: cameraView (Phase C: non-empty decode)

using UnityEngine;
using MayaImporter;
using MayaImporter.Core;

namespace MayaImporter.Generated
{
    [DisallowMultipleComponent]
    [MayaNodeType("cameraView")]
    public sealed class MayaGenerated_CameraViewNode : MayaPhaseCNodeBase
    {
        [Header("Decoded (cameraView)")]
        [SerializeField] private bool enabled = true;
        [SerializeField] private string camera;
        [SerializeField] private int viewMode;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            bool muted = ReadBool(false, ".mute", "mute", ".disabled", "disabled");
            bool explicitEnabled = ReadBool(true, ".enabled", "enabled", ".enable", "enable");
            enabled = !muted && explicitEnabled;

            camera = ReadString("", ".camera", "camera", ".cam", "cam");
            viewMode = ReadInt(0, ".viewMode", "viewMode", ".mode", "mode");

            SetNotes($"{NodeType} '{NodeName}' decoded: enabled={enabled}, camera='{camera}', viewMode={viewMode}");
        }
    }
}
