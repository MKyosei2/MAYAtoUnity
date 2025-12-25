using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Runtime
{
    /// <summary>
    /// Phase-2:
    /// Base class for nodeTypes that have no direct Unity equivalent yet.
    /// This still counts as "reconstructed in Unity" because:
    /// - The Maya node exists as a GameObject + 1 component (nodeType-mapped)
    /// - Attributes/Connections are preserved (MayaNodeComponentBase)
    /// - A Unity runtime component is attached to represent the concept (MayaOpaqueNodeRuntime)
    /// </summary>
    public abstract class MayaAutoOpaqueNodeBase : MayaNodeComponentBase
    {
        public override void ApplyToUnity(MayaImportOptions options, MayaImportLog log)
        {
            // Attach an explicit Unity-side representation (Unity has no concept -> create component)
            var opaque = GetComponent<MayaOpaqueNodeRuntime>();
            if (opaque == null) opaque = gameObject.AddComponent<MayaOpaqueNodeRuntime>();

            opaque.mayaNodeType = NodeType ?? "";
            opaque.mayaNodeName = NodeName ?? "";
            opaque.mayaParentName = ParentName ?? "";
            opaque.mayaUuid = Uuid ?? "";

            // Optional: store a small summary for quick view (full data remains on MayaNodeComponentBase)
            opaque.attributeCount = Attributes != null ? Attributes.Count : 0;
            opaque.connectionCount = Connections != null ? Connections.Count : 0;

            // (No destructive behavior; pure reconstruction marker)
            log?.Info($"[OpaqueNode] {opaque.mayaNodeType} '{opaque.mayaNodeName}' attrs={opaque.attributeCount} conns={opaque.connectionCount}");
        }
    }
}
