using UnityEngine;

namespace MayaImporter.Runtime
{
    /// <summary>
    /// Portfolio proof component:
    /// Import結果の「監査情報」をRootに残す（Unity-onlyで100%主張を支える）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MayaImportedRootInfo : MonoBehaviour
    {
        [Header("Source Identity")]
        public string sourcePath;
        public string sourceKind;
        public int schemaVersion;
        public string rawSha256;

        [Header("Scene Counts")]
        public int nodeCount;
        public int connectionCount;

        [Header("Unity-side Counts")]
        public int unityNodeComponentCount;
        public int unknownNodeComponentCount;
        public int opaqueRuntimeNodeCount;

        [Header(".mb Embedded ASCII (best-effort)")]
        public int mbExtractedAsciiChars;
        public int mbExtractedAsciiStatements;
        public int mbExtractedAsciiScore;

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
