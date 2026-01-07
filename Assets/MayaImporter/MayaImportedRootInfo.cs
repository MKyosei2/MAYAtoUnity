using UnityEngine;

namespace MayaImporter.Runtime
{
    /// <summary>
    /// Portfolio proof component (Unity-only):
    /// Stores import identity + preservation counters on the imported root.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MayaImportedRootInfo : MonoBehaviour
    {
        [Header("Source Identity")]
        public string sourcePath;
        public string sourceKind;
        public int schemaVersion;
        public string rawSha256;

        [Header("Source Size")]
        public int rawByteCount;

        [Header(".mb Chunk Index (best-effort)")]
        public string mbHeader4CC;
        public int mbChunkCount;
        public int mbExtractedStringCount;

        [Header(".mb Embedded ASCII (best-effort)")]
        public int mbExtractedAsciiChars;
        public int mbExtractedAsciiStatements;
        public int mbExtractedAsciiScore;
        public bool mbEmbeddedAsciiParsed;

        [Header(".mb Preservation Fallback (Production)")]
        public bool mbUsedChunkPlaceholderNodes;
        public string mbFallbackReason;

        [Header("Scene Counts")]
        public int nodeCount;
        public int connectionCount;

        [Header("Unity-side Counts")]
        public int unityNodeComponentCount;
        public int unknownNodeComponentCount;
        public int opaqueRuntimeNodeCount;

        [Header("Import Log")]
        public int warningCount;
        public int errorCount;

        [Header("Top Node Types")]
        public string[] topNodeTypes = new string[0];
        public int[] topNodeTypeCounts = new int[0];

        [Header("Timestamp (UTC ISO-8601)")]
        public string lastUpdatedUtc;
    }
}
