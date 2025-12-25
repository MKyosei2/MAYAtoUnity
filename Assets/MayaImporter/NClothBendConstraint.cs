using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Dynamics
{
    [MayaNodeType("nClothBendConstraint")]
    [DisallowMultipleComponent]
    public sealed class NClothBendConstraint : MayaPhaseCNodeBase
    {
        [Header("Decoded (nClothBendConstraint)")]
        [SerializeField] private bool enabled = true;
        [SerializeField] private float stiffness;
        [SerializeField] private float damping;
        [SerializeField] private float restAngle;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            bool muted = ReadBool(false, ".mute", "mute", ".disabled", "disabled");
            bool explicitEnabled = ReadBool(true, ".enabled", "enabled", ".enable", "enable");
            enabled = !muted && explicitEnabled;

            stiffness = ReadFloat(0f, ".stiffness", "stiffness", ".k", "k");
            damping = ReadFloat(0f, ".damping", "damping", ".dmp", "dmp");
            restAngle = ReadFloat(0f, ".restAngle", "restAngle", ".ra", "ra");

            SetNotes($"nClothBendConstraint decoded: enabled={enabled}, stiffness={stiffness}, damping={damping}, restAngle={restAngle} (no runtime sim; attrs+connections preserved)");
        }
    }
}
