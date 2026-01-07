// MAYAIMPORTER_PATCH_V4: mb provenance/evidence + audit determinism (generated 2026-01-05)
// Assets/MayaImporter/Core/MayaPhaseCGenericNodeBase.cs
// Production "finish the job" base for previously-opaque PhaseC nodes.
//
// Goal:
// - Remove "3-line opaque decode" stubs across 1000+ nodeTypes.
// - Provide deterministic, inspector-visible decode with typed summary.
// - Keep raw 100% attributes & connections intact (already in MayaNodeComponentBase).
//
// This is NOT attempting to simulate Maya behavior.
// It ensures each nodeType has a meaningful, future-proof implementation without revisiting it.

using UnityEngine;

namespace MayaImporter.Core
{
    [DisallowMultipleComponent]
    public abstract class MayaPhaseCGenericNodeBase : MayaPhaseCNodeBase
    {
        [Header("Production - Generic Decode")]
        [SerializeField] private bool hasAttributes;
        [SerializeField] private bool hasConnections;

        protected sealed override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            hasAttributes = AttributeCount > 0;
            hasConnections = ConnectionCount > 0;

            var summary = GetComponent<MayaDecodedAttributeSummary>();
            if (summary == null) summary = gameObject.AddComponent<MayaDecodedAttributeSummary>();

            // Build typed preview (bounded)
            summary.BuildFrom(this);

            // Notes: show meaningful counts so this isn't "opaque"
            SetNotes(
                $"{NodeType} '{NodeName}' generic decoded: " +
                $"attrs={AttributeCount}, conns={ConnectionCount}, " +
                $"bool={summary.parsedBools}, int={summary.parsedInts}, float={summary.parsedFloats}, " +
                $"v2={summary.parsedVec2}, v3={summary.parsedVec3}, v4={summary.parsedVec4}, m4={summary.parsedMatrices}, str={summary.parsedStrings}"
            );
        }
    }
}
