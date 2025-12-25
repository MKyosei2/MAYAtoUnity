using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Dynamics
{
    [MayaNodeType("nClothRigidityConstraint")]
    [DisallowMultipleComponent]
    public sealed class NClothRigidityConstraint : MayaPhaseCNodeBase
    {
        [Header("Decoded (nClothRigidityConstraint)")]
        [SerializeField] private bool enabled = true;
        [SerializeField] private float rigidity;
        [SerializeField] private float damping;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            bool muted = ReadBool(false, ".mute", "mute", ".disabled", "disabled");
            bool explicitEnabled = ReadBool(true, ".enabled", "enabled", ".enable", "enable");
            enabled = !muted && explicitEnabled;

            rigidity = ReadFloat(0f, ".rigidity", "rigidity", ".rg", "rg");
            damping = ReadFloat(0f, ".damping", "damping", ".dmp", "dmp");

            SetNotes($"nClothRigidityConstraint decoded: enabled={enabled}, rigidity={rigidity}, damping={damping} (no runtime sim; attrs+connections preserved)");
        }
    }
}
