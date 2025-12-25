using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Core
{
    /// <summary>
    /// Placeholder component (áAèÛë‘)
    /// - Keeps Maya node as a Unity component
    /// - Lossless attributes/connections are handled by MayaNodeComponentBase.InitializeFromRecord
    /// </summary>
    public sealed class MayaPlaceholderNode : MayaNodeComponentBase
    {
        [TextArea]
        public string Note = "Placeholder Maya node (kept losslessly via raw attributes & connections).";

        // IMPORTANT:
        // Do NOT override non-existent methods (OnCreateFromSceneData).
        // Base class already provides:
        // - InitializeFromRecord(NodeRecord rec, List<ConnectionRecord> allConnections)
        // - ApplyToUnity(MayaImportOptions options, MayaImportLog log) (default no-op)
    }
}
