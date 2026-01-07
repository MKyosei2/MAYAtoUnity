// PATCH: ProductionImpl v6 (Unity-only, retention-first)
// NodeType: closestPointOnCurve (Phase C: non-empty decode)

using UnityEngine;
using MayaImporter;
using MayaImporter.Core;

namespace MayaImporter.Generated
{
    [DisallowMultipleComponent]
    [MayaNodeType("closestPointOnCurve")]
    public sealed class MayaGenerated_ClosestPointOnCurveNode : MayaPhaseCNodeBase
    {
        [Header("Decoded (closestPointOnCurve)")]
        [SerializeField] private Vector3 inPosition;
        [SerializeField] private float parameter;
        [SerializeField] private bool useNormalizedParam;

        [SerializeField] private string incomingCurve;
        [SerializeField] private string incomingPosition;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            float x = ReadFloat(0f, ".inPositionX", "inPositionX", ".ipx", "ipx");
            float y = ReadFloat(0f, ".inPositionY", "inPositionY", ".ipy", "ipy");
            float z = ReadFloat(0f, ".inPositionZ", "inPositionZ", ".ipz", "ipz");
            inPosition = new Vector3(x, y, z);

            parameter = ReadFloat(0f, ".parameter", "parameter", ".u", "u");
            useNormalizedParam = ReadBool(false, ".useNormalizedParam", "useNormalizedParam", ".unp", "unp");

            incomingCurve = FindLastIncomingTo("inputCurve", "ic", "curve");
            incomingPosition = FindLastIncomingTo("inPositionX", "inPositionY", "inPositionZ", "inPosition");

            SetNotes($"{NodeType} '{NodeName}' decoded: inPos={inPosition}, param={parameter}, normalized={useNormalizedParam}, incomingCurve={(string.IsNullOrEmpty(incomingCurve) ? "none" : incomingCurve)}, incomingPos={(string.IsNullOrEmpty(incomingPosition) ? "none" : incomingPosition)}");
        }
    }
}
