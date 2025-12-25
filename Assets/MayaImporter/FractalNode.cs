using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Nodes
{
    [MayaNodeType("fractal")]
    [DisallowMultipleComponent]
    public sealed class FractalNode : MayaPhaseCNodeBase
    {
        [Header("Decoded (fractal)")]
        [SerializeField] private float amplitude = 1f;
        [SerializeField] private float ratio = 0.707f;
        [SerializeField] private float frequencyRatio = 2f;
        [SerializeField] private int depthMax = 3;
        [SerializeField] private float bias;
        [SerializeField] private float threshold;

        // NOTE: This is not a procedural noise simulator; it is a deterministic decode of parameters.
        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            amplitude = ReadFloat(1f, ".amplitude", "amplitude", ".amp", "amp");
            ratio = ReadFloat(0.707f, ".ratio", "ratio");
            frequencyRatio = ReadFloat(2f, ".frequencyRatio", "frequencyRatio", ".fr", "fr");
            depthMax = ReadInt(3, ".depthMax", "depthMax", ".dm", "dm", ".octaves", "octaves");
            bias = ReadFloat(0f, ".bias", "bias");
            threshold = ReadFloat(0f, ".threshold", "threshold", ".th", "th");

            SetNotes(
                $"fractal decoded: amp={amplitude}, ratio={ratio}, freqRatio={frequencyRatio}, depthMax={depthMax}, bias={bias}, threshold={threshold}. " +
                $"(no runtime noise sim; raw attrs+connections preserved)"
            );
        }
    }
}
