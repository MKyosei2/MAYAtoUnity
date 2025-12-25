using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Dynamics
{
    [MayaNodeType("nClothCollisionConstraint")]
    [DisallowMultipleComponent]
    public sealed class NClothCollisionConstraint : MayaPhaseCNodeBase
    {
        [Header("Decoded (nClothCollisionConstraint)")]
        [SerializeField] private bool enabled = true;
        [SerializeField] private float friction;
        [SerializeField] private float stickiness;
        [SerializeField] private float thickness;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            bool muted = ReadBool(false, ".mute", "mute", ".disabled", "disabled");
            bool explicitEnabled = ReadBool(true, ".enabled", "enabled", ".enable", "enable");
            enabled = !muted && explicitEnabled;

            friction = ReadFloat(0f, ".friction", "friction", ".mu", "mu");
            stickiness = ReadFloat(0f, ".stickiness", "stickiness", ".stk", "stk");
            thickness = ReadFloat(0f, ".thickness", "thickness", ".th", "th");

            SetNotes($"nClothCollisionConstraint decoded: enabled={enabled}, friction={friction}, stickiness={stickiness}, thickness={thickness} (no runtime sim; attrs+connections preserved)");
        }
    }
}
