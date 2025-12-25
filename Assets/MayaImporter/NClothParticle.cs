using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Dynamics
{
    [MayaNodeType("nClothParticle")]
    [DisallowMultipleComponent]
    public sealed class NClothParticle : MayaPhaseCNodeBase
    {
        [Header("Decoded (nClothParticle)")]
        [SerializeField] private float mass = 1f;
        [SerializeField] private float drag;
        [SerializeField] private float lift;
        [SerializeField] private float damp;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            mass = ReadFloat(1f, ".mass", "mass", ".m", "m");
            drag = ReadFloat(0f, ".drag", "drag");
            lift = ReadFloat(0f, ".lift", "lift");
            damp = ReadFloat(0f, ".damp", "damp", ".damping", "damping");

            SetNotes($"nClothParticle decoded: mass={mass}, drag={drag}, lift={lift}, damp={damp} (no runtime sim; attrs+connections preserved)");
        }
    }
}
