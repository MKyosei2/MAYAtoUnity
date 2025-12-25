using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Dynamics
{
    [MayaNodeType("nHair")]
    [DisallowMultipleComponent]
    public sealed class NHairNode : MayaPhaseCNodeBase
    {
        [Header("Decoded (nHair)")]
        [SerializeField] private bool enabled = true;
        [SerializeField] private float drag;
        [SerializeField] private float damp;
        [SerializeField] private float lift;
        [SerializeField] private float friction;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            bool muted = ReadBool(false, ".mute", "mute", ".disabled", "disabled");
            bool explicitEnabled = ReadBool(true, ".enabled", "enabled", ".enable", "enable");
            enabled = !muted && explicitEnabled;

            drag = ReadFloat(0f, ".drag", "drag");
            damp = ReadFloat(0f, ".damp", "damp", ".damping", "damping");
            lift = ReadFloat(0f, ".lift", "lift");
            friction = ReadFloat(0f, ".friction", "friction", ".mu", "mu");

            SetNotes($"nHair decoded: enabled={enabled}, drag={drag}, damp={damp}, lift={lift}, friction={friction} (no runtime sim; attrs+connections preserved)");
        }
    }
}
