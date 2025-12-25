using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Dynamics
{
    [MayaNodeType("nucleusConstraintBase")]
    [DisallowMultipleComponent]
    public sealed class NucleusConstraintBase : MayaPhaseCNodeBase
    {
        [Header("Decoded (nucleusConstraintBase)")]
        [SerializeField] private bool enabled = true;
        [SerializeField] private float strength;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            bool muted = ReadBool(false, ".mute", "mute", ".disabled", "disabled");
            bool explicitEnabled = ReadBool(true, ".enabled", "enabled", ".enable", "enable");
            enabled = !muted && explicitEnabled;

            strength = ReadFloat(0f, ".strength", "strength", ".s", "s");

            SetNotes($"nucleusConstraintBase decoded: enabled={enabled}, strength={strength} (no runtime nucleus sim; attrs+connections preserved)");
        }
    }
}
