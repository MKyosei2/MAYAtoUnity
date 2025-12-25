using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Dynamics
{
    [MayaNodeType("nHairStretchConstraint")]
    [DisallowMultipleComponent]
    public sealed class NHairStretchConstraint : MayaPhaseCNodeBase
    {
        [Header("Decoded (nHairStretchConstraint)")]
        [SerializeField] private bool enabled = true;
        [SerializeField] private float stretchResistance;
        [SerializeField] private float compressionResistance;
        [SerializeField] private float damping;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            bool muted = ReadBool(false, ".mute", "mute", ".disabled", "disabled");
            bool explicitEnabled = ReadBool(true, ".enabled", "enabled", ".enable", "enable");
            enabled = !muted && explicitEnabled;

            stretchResistance = ReadFloat(0f, ".stretchResistance", "stretchResistance", ".sr", "sr");
            compressionResistance = ReadFloat(0f, ".compressionResistance", "compressionResistance", ".cr", "cr");
            damping = ReadFloat(0f, ".damping", "damping", ".dmp", "dmp");

            SetNotes($"nHairStretchConstraint decoded: enabled={enabled}, stretchRes={stretchResistance}, compRes={compressionResistance}, damping={damping} (no runtime sim; attrs+connections preserved)");
        }
    }
}
