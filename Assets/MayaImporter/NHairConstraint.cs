using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Dynamics
{
    [MayaNodeType("nHairConstraint")]
    [DisallowMultipleComponent]
    public sealed class NHairConstraint : MayaPhaseCNodeBase
    {
        [Header("Decoded (nHairConstraint)")]
        [SerializeField] private bool enabled = true;
        [SerializeField] private float strength;
        [SerializeField] private float damping;

        [SerializeField] private string incomingStrengthPlug;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            bool muted = ReadBool(false, ".mute", "mute", ".disabled", "disabled");
            bool explicitEnabled = ReadBool(true, ".enabled", "enabled", ".enable", "enable");
            enabled = !muted && explicitEnabled;

            strength = ReadFloat(0f, ".strength", "strength", ".s", "s");
            damping = ReadFloat(0f, ".damping", "damping", ".dmp", "dmp");

            incomingStrengthPlug = FindLastIncomingTo("strength", "s");

            SetNotes($"nHairConstraint decoded: enabled={enabled}, strength={strength}, damping={damping}, incomingStrength={(string.IsNullOrEmpty(incomingStrengthPlug) ? "none" : incomingStrengthPlug)} (no runtime sim; attrs+connections preserved)");
        }
    }
}
