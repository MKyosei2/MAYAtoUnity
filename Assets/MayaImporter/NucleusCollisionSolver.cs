using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Dynamics
{
    [MayaNodeType("nucleusCollisionSolver")]
    [DisallowMultipleComponent]
    public sealed class NucleusCollisionSolver : MayaPhaseCNodeBase
    {
        [Header("Decoded (nucleusCollisionSolver)")]
        [SerializeField] private int subSteps = 1;
        [SerializeField] private int maxCollisionIterations = 4;
        [SerializeField] private float collisionTolerance;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            subSteps = ReadInt(1, ".subSteps", "subSteps", ".ss", "ss");
            maxCollisionIterations = ReadInt(4, ".maxCollisionIterations", "maxCollisionIterations", ".mci", "mci");
            collisionTolerance = ReadFloat(0f, ".collisionTolerance", "collisionTolerance", ".ct", "ct");

            SetNotes($"nucleusCollisionSolver decoded: subSteps={subSteps}, maxCollIter={maxCollisionIterations}, tolerance={collisionTolerance} (no runtime nucleus sim; attrs+connections preserved)");
        }
    }
}
