// MAYAIMPORTER_PATCH_V4: mb provenance/evidence + audit determinism (generated 2026-01-05)
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace MayaImporter.Core
{
    /// <summary>
    /// Phase D (完成):
    /// - Imported root の中だけで Unknown/Placeholder を mapped component に自動置換（可能な限り）
    /// - 未マッピング nodeType を検出して “欠損ゼロで保持” しつつ、証明用レポートを生成
    /// - 結果は MayaCoverageSummaryComponent に保存（Inspectorで確認可）
    /// </summary>
    public static class MayaPhaseDAutoCoverageOnImport
    {
        public static string Run_BestEffort(GameObject root, MayaSceneData scene, MayaImportOptions options, MayaImportLog log)
        {
            options ??= new MayaImportOptions();
            log ??= new MayaImportLog();
            if (root == null) return "";

            var report = new StringBuilder(16 * 1024);

            try
            {
                // 1) Upgrade Unknown/Placeholder inside this root
                var upgrade = UpgradeUnknownNodesUnderRoot(root, log);

                // 2) Collect coverage info
                var summary = BuildCoverageSummary(root);

                // 3) Attach component for Inspector proof (typed snapshot)
                var comp = root.GetComponent<MayaCoverageSummaryComponent>();
                if (comp == null) comp = root.AddComponent<MayaCoverageSummaryComponent>();

                var snap = new MayaCoverageSnapshot
                {
                    foundUnknown = upgrade.foundUnknown,
                    upgraded = upgrade.upgraded,
                    noMapping = upgrade.noMapping,
                    alreadyHadMapped = upgrade.alreadyHadMapped,

                    nodeCount = summary.nodeCount,
                    uniqueNodeTypes = summary.uniqueNodeTypes,
                    unknownRemaining = summary.unknownRemaining,

                    missingNodeTypes = summary.missingMappings.ToArray()
                };

                comp.Apply(snap);

                // 4) Build human-readable report (TextAsset subasset)
                report.AppendLine("# MayaImporter Coverage Report (Phase D / OnImport)");
                report.AppendLine();
                report.AppendLine($"- Unity: `{Application.unityVersion}`");
                report.AppendLine($"- Root: `{root.name}`");
                report.AppendLine($"- Timestamp(Local): `{DateTime.Now:yyyy-MM-dd HH:mm:ss}`");
                report.AppendLine();

                report.AppendLine("## 1) Upgrade Unknown/Placeholder (inside imported root)");
                report.AppendLine();
                report.AppendLine($"- FoundUnknown: **{upgrade.foundUnknown}**");
                report.AppendLine($"- Upgraded: **{upgrade.upgraded}**");
                report.AppendLine($"- NoMapping: **{upgrade.noMapping}**");
                report.AppendLine($"- AlreadyHadMapped: **{upgrade.alreadyHadMapped}**");
                report.AppendLine();

                report.AppendLine("## 2) Coverage Summary (inside imported root)");
                report.AppendLine();
                report.AppendLine($"- MayaNodeComponentBase count: **{summary.nodeCount}**");
                report.AppendLine($"- Unique nodeTypes: **{summary.uniqueNodeTypes}**");
                report.AppendLine($"- Unknown/Placeholder remaining: **{summary.unknownRemaining}**");
                report.AppendLine($"- Missing mappings (nodeTypes): **{summary.missingMappings.Count}**");
                report.AppendLine();

                if (summary.missingMappings.Count > 0)
                {
                    report.AppendLine("### 2.1 Missing nodeTypes (appeared in this file)");
                    foreach (var nt in summary.missingMappings.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                        report.AppendLine($"- `{nt}`");
                    report.AppendLine();
                }

                // Standard list cross-check (optional)
                if (MayaStandardNodeTypes.TryGet(out var std) && std != null && std.Count > 0)
                {
                    var reg = NodeFactory.GetRegistry();
                    var mapped = new HashSet<string>(reg.Keys, StringComparer.OrdinalIgnoreCase);
                    int missingStd = std.Count(s => !mapped.Contains(s));

                    report.AppendLine("## 3) Standard NodeTypes (Maya2026_StandardNodeTypes.txt)");
                    report.AppendLine();
                    report.AppendLine($"- Standard count: **{std.Count}**");
                    report.AppendLine($"- Project missing mappings (standard): **{missingStd}**");
                    report.AppendLine();
                    report.AppendLine("> Note: This is project-wide potential gap, not necessarily used in this file.");
                    report.AppendLine();
                }
                else
                {
                    report.AppendLine("## 3) Standard NodeTypes");
                    report.AppendLine();
                    report.AppendLine("- Standard list not found. (Optional) Put `Assets/MayaImporter/Resources/Maya2026_StandardNodeTypes.txt`");
                    report.AppendLine();
                }

                // 5) Verdict
                bool ok = (summary.missingMappings.Count == 0);
                report.AppendLine("## 4) Verdict");
                report.AppendLine();
                report.AppendLine(ok
                    ? "✅ **OK**: This imported file has **0 missing nodeType mappings**."
                    : "⚠️ **GAP**: This imported file contains **missing nodeType mappings** (see list).");
                report.AppendLine();

                return report.ToString();
            }
            catch (Exception e)
            {
                log.Warn("[PhaseD] Exception: " + e.GetType().Name + ": " + e.Message);
                return report.ToString();
            }
        }

        // ----------------------------
        // Upgrade Unknown/Placeholder inside root
        // ----------------------------
        private struct UpgradeStats
        {
            public int foundUnknown;
            public int upgraded;
            public int noMapping;
            public int alreadyHadMapped;
        }

        private static UpgradeStats UpgradeUnknownNodesUnderRoot(GameObject root, MayaImportLog log)
        {
            var st = new UpgradeStats();

            var unknowns = root.GetComponentsInChildren<MayaUnknownNodeComponent>(true);
            var placeholders = root.GetComponentsInChildren<MayaPlaceholderNode>(true);

            st.foundUnknown = (unknowns?.Length ?? 0) + (placeholders?.Length ?? 0);

            if (unknowns != null)
            {
                for (int i = 0; i < unknowns.Length; i++)
                    UpgradeOne(unknowns[i], ref st, log);
            }
            if (placeholders != null)
            {
                for (int i = 0; i < placeholders.Length; i++)
                    UpgradeOne(placeholders[i], ref st, log);
            }

            log.Info($"[PhaseD] UpgradeUnknownNodesUnderRoot: found={st.foundUnknown} upgraded={st.upgraded} noMapping={st.noMapping} alreadyHad={st.alreadyHadMapped}");
            return st;
        }

        private static void UpgradeOne(MayaNodeComponentBase src, ref UpgradeStats st, MayaImportLog log)
        {
            if (src == null) return;

            var nodeType = src.NodeType;
            if (string.IsNullOrEmpty(nodeType))
            {
                st.noMapping++;
                return;
            }

            var mappedType = NodeFactory.ResolveType(nodeType);
            if (mappedType == null)
            {
                st.noMapping++;
                return;
            }

            if (mappedType == typeof(MayaUnknownNodeComponent) || mappedType == typeof(MayaPlaceholderNode))
            {
                st.noMapping++;
                return;
            }

            var go = src.gameObject;

            if (go.GetComponent(mappedType) != null)
            {
                UnityEngine.Object.DestroyImmediate(src);
                st.alreadyHadMapped++;
                return;
            }

            var dst = go.AddComponent(mappedType) as MayaNodeComponentBase;
            if (dst == null)
            {
                st.noMapping++;
                return;
            }

            dst.NodeName = src.NodeName;
            dst.NodeType = src.NodeType;
            dst.ParentName = src.ParentName;
            dst.Uuid = src.Uuid;

            dst.Attributes = DeepCopyAttributes(src.Attributes);
            dst.Connections = DeepCopyConnections(src.Connections);

            UnityEngine.Object.DestroyImmediate(src);
            st.upgraded++;
        }

        private static List<MayaNodeComponentBase.SerializedAttribute> DeepCopyAttributes(List<MayaNodeComponentBase.SerializedAttribute> src)
        {
            var dst = new List<MayaNodeComponentBase.SerializedAttribute>(src != null ? src.Count : 0);
            if (src == null) return dst;

            for (int i = 0; i < src.Count; i++)
            {
                var a = src[i];
                if (a == null) continue;

                var na = new MayaNodeComponentBase.SerializedAttribute
                {
                    Key = a.Key,
                    TypeName = a.TypeName,
                    Tokens = new List<string>(a.Tokens != null ? a.Tokens.Count : 0)
                };

                if (a.Tokens != null)
                    na.Tokens.AddRange(a.Tokens);

                dst.Add(na);
            }

            return dst;
        }

        private static List<MayaNodeComponentBase.SerializedConnection> DeepCopyConnections(List<MayaNodeComponentBase.SerializedConnection> src)
        {
            var dst = new List<MayaNodeComponentBase.SerializedConnection>(src != null ? src.Count : 0);
            if (src == null) return dst;

            for (int i = 0; i < src.Count; i++)
            {
                var c = src[i];
                if (c == null) continue;

                dst.Add(new MayaNodeComponentBase.SerializedConnection
                {
                    SrcPlug = c.SrcPlug,
                    DstPlug = c.DstPlug,
                    Force = c.Force,

                    RoleForThisNode = c.RoleForThisNode,
                    SrcNodePart = c.SrcNodePart,
                    DstNodePart = c.DstNodePart
                });
            }

            return dst;
        }

        // ----------------------------
        // Coverage summary inside root
        // ----------------------------
        private struct CoverageSummary
        {
            public int nodeCount;
            public int uniqueNodeTypes;
            public int unknownRemaining;
            public List<string> missingMappings;
        }

        private static CoverageSummary BuildCoverageSummary(GameObject root)
        {
            var nodes = root.GetComponentsInChildren<MayaNodeComponentBase>(true);
            int nodeCount = nodes?.Length ?? 0;

            var uniq = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int unknown = 0;

            for (int i = 0; i < nodeCount; i++)
            {
                var n = nodes[i];
                if (n == null) continue;

                if (n is MayaUnknownNodeComponent || n is MayaPlaceholderNode)
                    unknown++;

                var nt = n.NodeType;
                if (!string.IsNullOrEmpty(nt))
                    uniq.Add(nt);
            }

            var miss = new List<string>();
            foreach (var nt in uniq)
            {
                var t = NodeFactory.ResolveType(nt);
                if (t == null || t == typeof(MayaUnknownNodeComponent) || t == typeof(MayaPlaceholderNode))
                    miss.Add(nt);
            }

            miss.Sort(StringComparer.OrdinalIgnoreCase);

            return new CoverageSummary
            {
                nodeCount = nodeCount,
                uniqueNodeTypes = uniq.Count,
                unknownRemaining = unknown,
                missingMappings = miss
            };
        }
    }
}
#endif
