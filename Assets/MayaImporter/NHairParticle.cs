using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Dynamics
{
    [MayaNodeType("nHairParticle")]
    [DisallowMultipleComponent]
    public sealed class NHairParticle : MayaPhaseCNodeBase
    {
        [Header("Decoded (nHairParticle)")]
        [SerializeField] private float mass = 1f;
        [SerializeField] private float drag;
        [SerializeField] private float damp;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            mass = ReadFloat(1f, ".mass", "mass", ".m", "m");
            drag = ReadFloat(0f, ".drag", "drag");
            damp = ReadFloat(0f, ".damp", "damp", ".damping", "damping");

            SetNotes($"nHairParticle decoded: mass={mass}, drag={drag}, damp={damp} (no runtime sim; attrs+connections preserved)");
        }
    }
}
