using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Dynamics
{
    [MayaNodeType("nClothSelfCollision")]
    [DisallowMultipleComponent]
    public sealed class NClothSelfCollision : MayaPhaseCNodeBase
    {
        [Header("Decoded (nClothSelfCollision)")]
        [SerializeField] private bool enabled = true;
        [SerializeField] private float selfCollideWidth;
        [SerializeField] private float selfCollideStrength;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            bool muted = ReadBool(false, ".mute", "mute", ".disabled", "disabled");
            bool explicitEnabled = ReadBool(true, ".enabled", "enabled", ".enable", "enable");
            enabled = !muted && explicitEnabled;

            selfCollideWidth = ReadFloat(0f, ".selfCollideWidth", "selfCollideWidth", ".scw", "scw");
            selfCollideStrength = ReadFloat(0f, ".selfCollideStrength", "selfCollideStrength", ".scs", "scs", ".strength", "strength");

            SetNotes($"nClothSelfCollision decoded: enabled={enabled}, width={selfCollideWidth}, strength={selfCollideStrength} (no runtime sim; attrs+connections preserved)");
        }
    }
}
