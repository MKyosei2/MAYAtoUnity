// NodeType: curveFromMeshCoM (Phase C: non-empty decode)

using UnityEngine;
using MayaImporter;
using MayaImporter.Core;

namespace MayaImporter.Generated
{
    [DisallowMultipleComponent]
    [MayaNodeType("curveFromMeshCoM")]
    public sealed class MayaGenerated_CurveFromMeshCoMNode : MayaPhaseCNodeBase
    {
        [Header("Decoded (curveFromMeshCoM)")]
        [SerializeField] private bool enabled = true;
        [SerializeField] private int samples = 16;
        [SerializeField] private float smoothing;

        [SerializeField] private string incomingMesh;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            bool muted = ReadBool(false, ".mute", "mute", ".disabled", "disabled");
            bool explicitEnabled = ReadBool(true, ".enabled", "enabled", ".enable", "enable");
            enabled = !muted && explicitEnabled;

            samples = ReadInt(16, ".samples", "samples", ".numSamples", "numSamples", ".n", "n");
            smoothing = ReadFloat(0f, ".smoothing", "smoothing", ".smooth", "smooth");

            incomingMesh = FindLastIncomingTo("inMesh", "inputMesh", "mesh", "worldMesh", "input", "in");
            string im = string.IsNullOrEmpty(incomingMesh) ? "none" : incomingMesh;

            SetNotes($"{NodeType} '{NodeName}' decoded: enabled={enabled}, samples={samples}, smoothing={smoothing}, incomingMesh={im} (curve not generated; connections preserved)");
        }
    }
}
