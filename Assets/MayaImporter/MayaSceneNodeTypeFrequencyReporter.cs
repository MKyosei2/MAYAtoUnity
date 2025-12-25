#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using MayaImporter.Core;
using MayaImporter.Animation;

namespace MayaImporter.Editor
{
    public static class MayaSceneNodeTypeFrequencyReporter
    {
        private const string ReportsDir = "Assets/MayaImporter/Reports";
        private const string MenuPath = "Tools/Maya Importer/Audit/Report Scene NodeType Frequency (Counts + Status)";

        [MenuItem(MenuPath)]
        public static void Report()
        {
            Directory.CreateDirectory(ReportsDir);

            var reg = NodeFactory.GetRegistry();
            var mapped = new HashSet<string>(reg.Keys, StringComparer.OrdinalIgnoreCase);

            var nodes = UnityEngine.Object.FindObjectsByType<MayaNodeComponentBase>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int unknownCount = 0;

            for (int i = 0; i < nodes.Length; i++)
            {
                var n = nodes[i];
                if (n == null) continue;
                if (!n.gameObject.scene.IsValid()) continue;

                if (n is MayaUnknownNodeComponent || n is MayaPlaceholderNode) unknownCount++;

                var nt = n.NodeType;
                if (string.IsNullOrEmpty(nt)) continue;

                counts.TryGetValue(nt, out var c);
                counts[nt] = c + 1;
            }

            var rows = new List<Row>(counts.Count);
            foreach (var kv in counts)
            {
                var nt = kv.Key;
                var cnt = kv.Value;

                string status;
                string chosenType = "";
                string implKind = "NONE";

                if (!mapped.Contains(nt))
                {
                    status = "MISSING";
                }
                else
                {
                    var t = NodeFactory.ResolveType(nt);
                    chosenType = t != null ? t.FullName : "";

                    if (OverridesApplyToUnity(t))
                    {
                        status = "IMPLEMENTED";
                        implKind = "ApplyToUnity";
                    }
                    else if (MayaAnimValueGraph.IsSupportedComputeNodeType(nt))
                    {
                        status = "GRAPH";
                        implKind = "GraphCompute";
                    }
                    else
                    {
                        status = "STUB";
                        implKind = "NONE";
                    }
                }

                rows.Add(new Row
                {
                    NodeType = nt,
                    Count = cnt,
                    Status = status,
                    ImplementationKind = implKind,
                    ChosenType = chosenType
                });
            }

            rows.Sort((a, b) =>
            {
                int pa = StatusPri(a.Status);
                int pb = StatusPri(b.Status);
                if (pa != pb) return pa.CompareTo(pb);

                int cc = b.Count.CompareTo(a.Count);
                if (cc != 0) return cc;

                return StringComparer.OrdinalIgnoreCase.Compare(a.NodeType, b.NodeType);
            });

            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var mdPath = $"{ReportsDir}/SceneNodeTypeFrequency__{stamp}.md";
            var csvPath = $"{ReportsDir}/SceneNodeTypeFrequency__{stamp}.csv";

            // CSV
            var csv = new StringBuilder();
            csv.AppendLine("Status,ImplementationKind,Count,NodeType,ChosenType");
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                csv.AppendLine($"{r.Status},{r.ImplementationKind},{r.Count},{Csv(r.NodeType)},{Csv(r.ChosenType)}");
            }
            File.WriteAllText(csvPath, csv.ToString(), new UTF8Encoding(false));

            // MD
            int missing = rows.Count(r => r.Status == "MISSING");
            int stub = rows.Count(r => r.Status == "STUB");
            int graph = rows.Count(r => r.Status == "GRAPH");
            int impl = rows.Count(r => r.Status == "IMPLEMENTED");

            var md = new StringBuilder();
            md.AppendLine("# MayaImporter Scene NodeType Frequency");
            md.AppendLine();
            md.AppendLine($"- Timestamp: `{DateTime.Now:yyyy-MM-dd HH:mm:ss}`");
            md.AppendLine($"- Unity: `{Application.unityVersion}`");
            md.AppendLine($"- MayaNodeComponentBase in scene: **{nodes.Length}**");
            md.AppendLine($"- Unknown/Placeholder components: **{unknownCount}**");
            md.AppendLine();
            md.AppendLine("## Summary");
            md.AppendLine();
            md.AppendLine($"- Unique nodeTypes: **{rows.Count}**");
            md.AppendLine($"- MISSING: **{missing}**");
            md.AppendLine($"- STUB: **{stub}**");
            md.AppendLine($"- GRAPH: **{graph}**");
            md.AppendLine($"- IMPLEMENTED: **{impl}**");
            md.AppendLine();

            md.AppendLine("## List (Priority Order)");
            md.AppendLine();
            md.AppendLine("| Status | Kind | Count | NodeType | Chosen C# Type |");
            md.AppendLine("|---:|---:|---:|---|---|");

            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                md.AppendLine($"| {r.Status} | {r.ImplementationKind} | {r.Count} | `{r.NodeType}` | `{r.ChosenType}` |");
            }

            md.AppendLine();
            md.AppendLine("### Next Action");
            md.AppendLine();
            md.AppendLine("- まず **MISSING** → スタブ生成（1 nodeType = 1 C#）");
            md.AppendLine("- 次に **STUB**（=ApplyToUnityもGraphComputeも無い）を頻度順に実装");
            md.AppendLine("- **GRAPH** は “実装済み扱い” なので後回しでOK");
            md.AppendLine();

            File.WriteAllText(mdPath, md.ToString(), new UTF8Encoding(false));
            AssetDatabase.Refresh();

            Debug.Log($"[MayaImporter] ✅ Scene nodeType frequency written:\n- {mdPath}\n- {csvPath}");
            EditorUtility.DisplayDialog("MayaImporter", $"Scene nodeType frequency を出力しました。\n\n{mdPath}\n{csvPath}", "OK");
        }

        private struct Row
        {
            public string Status;
            public string ImplementationKind;
            public int Count;
            public string NodeType;
            public string ChosenType;
        }

        private static int StatusPri(string status)
        {
            // priority: MISSING -> STUB -> GRAPH -> IMPLEMENTED
            if (status == "MISSING") return 0;
            if (status == "STUB") return 1;
            if (status == "GRAPH") return 2;
            return 3;
        }

        private static bool OverridesApplyToUnity(Type t)
        {
            if (t == null) return false;

            var m = t.GetMethod(
                "ApplyToUnity",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(MayaImportOptions), typeof(MayaImportLog) },
                modifiers: null
            );

            if (m == null) return false;
            return m.DeclaringType != typeof(MayaNodeComponentBase);
        }

        private static string Csv(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0)
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }
    }
}
#endif
