// NodeType: closestPointOnMesh (Phase C: non-empty decode)

using UnityEngine;
using MayaImporter;
using MayaImporter.Core;

namespace MayaImporter.Generated
{
    [DisallowMultipleComponent]
    [MayaNodeType("closestPointOnMesh")]
    public sealed class MayaGenerated_ClosestPointOnMeshNode : MayaPhaseCNodeBase
    {
        [Header("Decoded (closestPointOnMesh)")]
        [SerializeField] private Vector3 inPosition;
        [SerializeField] private int faceIndex;
        [SerializeField] private int triangleIndex;

        [SerializeField] private string incomingMesh;
        [SerializeField] private string incomingPosition;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            float x = ReadFloat(0f, ".inPositionX", "inPositionX", ".ipx", "ipx");
            float y = ReadFloat(0f, ".inPositionY", "inPositionY", ".ipy", "ipy");
            float z = ReadFloat(0f, ".inPositionZ", "inPositionZ", ".ipz", "ipz");
            inPosition = new Vector3(x, y, z);

            faceIndex = ReadInt(-1, ".faceIndex", "faceIndex", ".fi", "fi");
            triangleIndex = ReadInt(-1, ".triangleIndex", "triangleIndex", ".ti", "ti");

            incomingMesh = FindLastIncomingTo("inMesh", "inputMesh", "mesh");
            incomingPosition = FindLastIncomingTo("inPositionX", "inPositionY", "inPositionZ", "inPosition");

            SetNotes($"{NodeType} '{NodeName}' decoded: inPos={inPosition}, faceIndex={faceIndex}, triIndex={triangleIndex}, incomingMesh={(string.IsNullOrEmpty(incomingMesh) ? "none" : incomingMesh)}, incomingPos={(string.IsNullOrEmpty(incomingPosition) ? "none" : incomingPosition)}");
        }
    }
}
