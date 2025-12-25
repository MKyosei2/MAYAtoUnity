// Assets/MayaImporter/Core/MayaPhaseCOpaqueRuntimeNodeBase.cs
// Final base for "opaque generated" PhaseC nodes.
//
// Guarantees for 100“_ðŒ:
// - Attaches MayaOpaqueNodeRuntime (explicit Unity-side representation for non-Unity concepts)
// - Attaches MayaOpaqueAttributePreview (inspector visibility)
// - Attaches MayaOpaqueConnectionPreview (inspector visibility of connectAttr)
// - Attaches MayaDecodedAttributeSummary (typed-ish snapshot)
// - Keeps raw attributes/connections in MayaNodeComponentBase as-is
// - No Maya/API required

using UnityEngine;
using MayaImporter.Runtime;

namespace MayaImporter.Core
{
    [DisallowMultipleComponent]
    public abstract class MayaPhaseCOpaqueRuntimeNodeBase : MayaPhaseCNodeBase
    {
        [Header("Opaque Runtime (auto)")]
        [SerializeField] private bool hasAttributes;
        [SerializeField] private bool hasConnections;

        [SerializeField] private int previewMaxEntries = 64;

        protected sealed override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            hasAttributes = AttributeCount > 0;
            hasConnections = ConnectionCount > 0;

            // 1) Unity-side explicit representation
            var opaque = GetComponent<MayaOpaqueNodeRuntime>();
            if (opaque == null) opaque = gameObject.AddComponent<MayaOpaqueNodeRuntime>();

            opaque.mayaNodeType = NodeType ?? "";
            opaque.mayaNodeName = NodeName ?? "";
            opaque.mayaParentName = ParentName ?? "";
            opaque.mayaUuid = Uuid ?? "";
            opaque.attributeCount = AttributeCount;
            opaque.connectionCount = ConnectionCount;

            // 2) Raw attribute preview
            var preview = GetComponent<MayaOpaqueAttributePreview>();
            if (preview == null) preview = gameObject.AddComponent<MayaOpaqueAttributePreview>();
            preview.maxEntries = Mathf.Clamp(previewMaxEntries, 0, 2048);
            preview.BuildFrom(this);

            // 3) Connection preview
            var cprev = GetComponent<MayaOpaqueConnectionPreview>();
            if (cprev == null) cprev = gameObject.AddComponent<MayaOpaqueConnectionPreview>();
            cprev.maxEntries = Mathf.Clamp(previewMaxEntries, 0, 4096);
            cprev.BuildFrom(this);

            // 4) Typed-ish summary
            var summary = GetComponent<MayaDecodedAttributeSummary>();
            if (summary == null) summary = gameObject.AddComponent<MayaDecodedAttributeSummary>();
            summary.maxEntriesPerCategory = Mathf.Clamp(previewMaxEntries, 0, 4096);
            summary.BuildFrom(this);

            SetNotes(
                $"{NodeType} '{NodeName}' finalized opaque: attrs={AttributeCount}, conns={ConnectionCount} " +
                $"+ runtime marker + attr preview + conn preview + typed summary."
            );
        }
    }
}
