using UnityEngine;

namespace MayaImporter.Core
{
    /// <summary>
    /// Fallback component for unsupported / not-yet-implemented Maya node types.
    /// Phase-1 guarantee: every node still becomes a GameObject with a component.
    /// Proof components (marker/attr/conn/summary) are attached by the Finalizer.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MayaUnknownNodeComponent : MayaNodeComponentBase
    {
        [TextArea]
        public string Note = "Unknown/Unsupported Maya node type (kept losslessly via raw attributes & connections).";
    }
}
