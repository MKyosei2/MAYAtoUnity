// PATCH: ProductionImpl v6 (Unity-only, retention-first)
// NodeType: closestPointOnSurface (Phase C: non-empty decode)

using UnityEngine;
using MayaImporter;
using MayaImporter.Core;

namespace MayaImporter.Generated
{
    [DisallowMultipleComponent]
    [MayaNodeType("closestPointOnSurface")]
    public sealed class MayaGenerated_ClosestPointOnSurfaceNode : MayaPhaseCNodeBase
    {
        [Header("Decoded (closestPointOnSurface)")]
        [SerializeField] private Vector3 inPosition;
        [SerializeField] private float parameterU;
        [SerializeField] private float parameterV;

        [SerializeField] private string incomingSurface;
        [SerializeField] private string incomingPosition;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            float x = ReadFloat(0f, ".inPositionX", "inPositionX", ".ipx", "ipx");
            float y = ReadFloat(0f, ".inPositionY", "inPositionY", ".ipy", "ipy");
            float z = ReadFloat(0f, ".inPositionZ", "inPositionZ", ".ipz", "ipz");
            inPosition = new Vector3(x, y, z);

            parameterU = ReadFloat(0f, ".parameterU", "parameterU", ".u", "u");
            parameterV = ReadFloat(0f, ".parameterV", "parameterV", ".v", "v");

            incomingSurface = FindLastIncomingTo("inputSurface", "inSurface", "surface");
            incomingPosition = FindLastIncomingTo("inPositionX", "inPositionY", "inPositionZ", "inPosition");

            SetNotes($"{NodeType} '{NodeName}' decoded: inPos={inPosition}, (u,v)=({parameterU},{parameterV}), incomingSurface={(string.IsNullOrEmpty(incomingSurface) ? "none" : incomingSurface)}, incomingPos={(string.IsNullOrEmpty(incomingPosition) ? "none" : incomingPosition)}");
        }
    }
}
