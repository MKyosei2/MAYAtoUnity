using System;
using UnityEngine;

namespace MayaImporter.Core
{
    /// <summary>
    /// Phase D:
    /// Importされた “このファイル” の coverage を Inspector で証明するためのコンポーネント。
    /// TextAsset(MayaCoverageReport) と併用して採用側の懸念を潰す。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MayaCoverageSummaryComponent : MonoBehaviour
    {
        [Header("Upgrade Unknown/Placeholder (inside imported root)")]
        public int foundUnknown;
        public int upgraded;
        public int noMapping;
        public int alreadyHadMapped;

        [Header("Coverage (inside imported root)")]
        public int nodeCount;
        public int uniqueNodeTypes;
        public int unknownRemaining;

        [Header("Missing nodeTypes (appeared in this file)")]
        [SerializeField] private string[] missingNodeTypes = Array.Empty<string>();

        public string[] MissingNodeTypes => missingNodeTypes;

        // PhaseD から明示的に渡す用
        public void Apply(MayaCoverageSnapshot s)
        {
            foundUnknown = s.foundUnknown;
            upgraded = s.upgraded;
            noMapping = s.noMapping;
            alreadyHadMapped = s.alreadyHadMapped;

            nodeCount = s.nodeCount;
            uniqueNodeTypes = s.uniqueNodeTypes;
            unknownRemaining = s.unknownRemaining;

            missingNodeTypes = (s.missingNodeTypes != null) ? s.missingNodeTypes : Array.Empty<string>();
        }
    }

    /// <summary>
    /// Phase D の結果を “型付きで” 渡すためのスナップショット。
    /// dynamic を使わず、Unity設定差でも確実にコンパイルする。
    /// </summary>
    [Serializable]
    public struct MayaCoverageSnapshot
    {
        public int foundUnknown;
        public int upgraded;
        public int noMapping;
        public int alreadyHadMapped;

        public int nodeCount;
        public int uniqueNodeTypes;
        public int unknownRemaining;

        public string[] missingNodeTypes;
    }
}
