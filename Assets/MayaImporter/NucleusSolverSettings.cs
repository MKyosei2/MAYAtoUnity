using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Dynamics
{
    [MayaNodeType("nucleusSolverSettings")]
    [DisallowMultipleComponent]
    public sealed class NucleusSolverSettings : MayaPhaseCNodeBase
    {
        [Header("Decoded (nucleusSolverSettings)")]
        [SerializeField] private bool enabled = true;

        [SerializeField] private int subSteps = 1;
        [SerializeField] private float gravity = 9.8f;
        [SerializeField] private float startFrame;

        [SerializeField] private float timeScale = 1f;
        [SerializeField] private float spaceScale = 1f;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            bool muted = ReadBool(false, ".mute", "mute", ".disabled", "disabled");
            bool explicitEnabled = ReadBool(true, ".enabled", "enabled", ".enable", "enable");
            enabled = !muted && explicitEnabled;

            subSteps = ReadInt(1, ".subSteps", "subSteps", ".ss", "ss");
            gravity = ReadFloat(9.8f, ".gravity", "gravity", ".g", "g");
            startFrame = ReadFloat(0f, ".startFrame", "startFrame", ".sf", "sf");

            timeScale = ReadFloat(1f, ".timeScale", "timeScale", ".ts", "ts");
            spaceScale = ReadFloat(1f, ".spaceScale", "spaceScale", ".sp", "sp");

            SetNotes($"nucleusSolverSettings decoded: enabled={enabled}, subSteps={subSteps}, gravity={gravity}, startFrame={startFrame}, timeScale={timeScale}, spaceScale={spaceScale} (no runtime nucleus sim; attrs+connections preserved)");
        }
    }
}
