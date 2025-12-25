// NodeType: cloth (Phase C: non-empty decode)

using UnityEngine;
using MayaImporter;
using MayaImporter.Core;

namespace MayaImporter.Generated
{
    [DisallowMultipleComponent]
    [MayaNodeType("cloth")]
    public sealed class MayaGenerated_ClothNode : MayaPhaseCNodeBase
    {
        [Header("Decoded (cloth)")]
        [SerializeField] private bool enabled = true;

        [SerializeField] private float mass = 1f;
        [SerializeField] private float drag;
        [SerializeField] private float damp;
        [SerializeField] private float friction;

        [SerializeField] private int subSteps = 1;

        [SerializeField] private string incomingTime;
        [SerializeField] private string incomingInputMesh;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            bool muted = ReadBool(false, ".mute", "mute", ".disabled", "disabled");
            bool explicitEnabled = ReadBool(true, ".enabled", "enabled", ".enable", "enable");
            enabled = !muted && explicitEnabled;

            mass = ReadFloat(1f, ".mass", "mass", ".m", "m");
            drag = ReadFloat(0f, ".drag", "drag");
            damp = ReadFloat(0f, ".damp", "damp", ".damping", "damping");
            friction = ReadFloat(0f, ".friction", "friction", ".mu", "mu");
            subSteps = ReadInt(1, ".subSteps", "subSteps", ".ss", "ss");

            incomingTime = FindLastIncomingTo("time", "t");
            incomingInputMesh = FindLastIncomingTo("inputMesh", "inMesh", "input", "in");

            SetNotes($"{NodeType} '{NodeName}' decoded: enabled={enabled}, mass={mass}, drag={drag}, damp={damp}, friction={friction}, subSteps={subSteps}, incomingTime={(string.IsNullOrEmpty(incomingTime) ? "none" : incomingTime)}, incomingMesh={(string.IsNullOrEmpty(incomingInputMesh) ? "none" : incomingInputMesh)} (no runtime sim; attrs+connections preserved)");
        }
    }
}
