using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Dynamics
{
    [MayaNodeType("solverStateCache")]
    [DisallowMultipleComponent]
    public sealed class SolverStateCache : MayaPhaseCNodeBase
    {
        [Header("Decoded (solverStateCache)")]
        [SerializeField] private string cachePath;
        [SerializeField] private float startFrame;
        [SerializeField] private float endFrame;
        [SerializeField] private bool enabled = true;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            cachePath = ReadString("",
                ".cachePath", "cachePath",
                ".path", "path",
                ".fileName", "fileName");

            startFrame = ReadFloat(0f, ".startFrame", "startFrame", ".sf", "sf");
            endFrame = ReadFloat(0f, ".endFrame", "endFrame", ".ef", "ef");

            bool muted = ReadBool(false, ".mute", "mute", ".disabled", "disabled");
            bool explicitEnabled = ReadBool(true, ".enabled", "enabled", ".enable", "enable");
            enabled = !muted && explicitEnabled;

            SetNotes($"solverStateCache decoded: enabled={enabled}, path='{cachePath}', start={startFrame}, end={endFrame} (no runtime cache playback; attrs+connections preserved)");
        }
    }
}
