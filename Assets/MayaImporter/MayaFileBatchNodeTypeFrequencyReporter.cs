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
    /// <summary>
    /// Report nodeType frequency directly from .ma/.mb files (NO Maya API).
    /// - Scans a folder recursively for .ma/.mb
    /// - Parses each file into MayaSceneData (MayaAsciiParser / MayaBinaryParser)
    /// - Aggregates nodeType counts
    /// - Classifies each nodeType:
    ///     MISSING (no [MayaNodeType] mapping)
    ///     STUB (mapped but no ApplyToUnity and not GraphCompute)
    ///     GRAPH (AnimValueGraph supported compute node type)
    ///     IMPLEMENTED (overrides ApplyToUnity)
    /// - Outputs CSV + Markdown under Assets/MayaImporter/Reports
    /// </summary>
    public static class MayaFileBatchNodeTypeFrequencyReporter
    {
        private const string ReportsDir = "Assets/MayaImporter/Reports";

        [MenuItem("Tools/Maya Importer/Audit/Report NodeType Frequency FROM .ma/.mb Folder (Counts + Status)")]
        public static void ReportFromFolder()
        {
            var folderAbs = EditorUtility.OpenFolderPanel(
                "Select folder containing .ma/.mb (recursive scan)",
                Application.dataPath,
                ""
            );

            if (string.IsNullOrEmpty(folderAbs))
                return;

            if (!Directory.Exists(folderAbs))
            {
                EditorUtility.DisplayDialog("MayaImporter", "フォルダが存在しません。", "OK");
                return;
            }

            Directory.CreateDirectory(ReportsDir);

            var files = Directory.GetFiles(folderAbs, "*.*", SearchOption.AllDirectories)
                                 .Where(p =>
                                 {
                                     var ext = Path.GetExtension(p);
                                     return ext != null && (ext.Equals(".ma", StringComparison.OrdinalIgnoreCase) ||
                                                            ext.Equals(".mb", StringComparison.OrdinalIgnoreCase));
                                 })
                                 .ToArray();

            if (files.Length == 0)
            {
                EditorUtility.DisplayDialog("MayaImporter", "指定フォルダ配下に .ma/.mb が見つかりませんでした。", "OK");
                return;
            }

            // Registry
            var reg = NodeFactory.GetRegistry();
            var mapped = new HashSet<string>(reg.Keys, StringComparer.OrdinalIgnoreCase);

            var totals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            int parsedFiles = 0;
            int failedFiles = 0;
            long totalNodes = 0;

            var options = new MayaImportOptions
            {
                KeepRawStatements = false, // frequency-only => memory save
                CreateRootGameObject = false,
                CreateUnityComponents = false,
                Conversion = CoordinateConversion.None
            };

            var log = new MayaImportLog();

            // Progress UI
            for (int i = 0; i < files.Length; i++)
            {
                var path = files[i];
                float p = (files.Length <= 1) ? 1f : (i / (float)(files.Length - 1));
                EditorUtility.DisplayProgressBar("MayaImporter", $"Parsing: {Path.GetFileName(path)}", p);

                try
                {
                    MayaSceneData scene;
                    var ext = Path.GetExtension(path);

                    if (ext.Equals(".ma", StringComparison.OrdinalIgnoreCase))
                    {
                        var parser = new MayaAsciiParser();
                        scene = parser.ParseFile(path, options, log);
                    }
                    else
                    {
                        var parser = new MayaBinaryParser();
                        scene = parser.ParseFile(path, options, log);
                    }

                    parsedFiles++;
                    if (scene != null)
                    {
                        var counts = scene.CountNodeTypes();
                        totalNodes += counts.Values.Sum();

                        foreach (var kv in counts)
                        {
                            totals.TryGetValue(kv.Key, out var c);
                            totals[kv.Key] = c + kv.Value;
                        }
                    }
                }
                catch (Exception ex)
                {
                    failedFiles++;
                    Debug.LogError($"[MayaImporter] Parse failed: {path}\n{ex}");
                }
            }

            EditorUtility.ClearProgressBar();

            // Build rows
            var rows = new List<Row>(totals.Count);
            foreach (var kv in totals)
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

            // Priority order: MISSING -> STUB -> GRAPH -> IMPLEMENTED, then Count desc
            rows.Sort((a, b) =>
            {
                int pa = StatusPri(a.Status);
                int pb = StatusPri(b.Status);
                if (pa != pb) return pa.CompareTo(pb);

                int cc = b.Count.CompareTo(a.Count);
                if (cc != 0) return cc;

                return StringComparer.OrdinalIgnoreCase.Compare(a.NodeType, b.NodeType);
            });

            // Write files
            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var mdPath = $"{ReportsDir}/FileNodeTypeFrequency__{stamp}.md";
            var csvPath = $"{ReportsDir}/FileNodeTypeFrequency__{stamp}.csv";

            // CSV
            var csv = new StringBuilder();
            csv.AppendLine("Status,ImplementationKind,Count,NodeType,ChosenType");
            foreach (var r in rows)
                csv.AppendLine($"{r.Status},{r.ImplementationKind},{r.Count},{Csv(r.NodeType)},{Csv(r.ChosenType)}");
            File.WriteAllText(csvPath, csv.ToString(), new UTF8Encoding(false));

            // MD summary
            int missing = rows.Count(r => r.Status == "MISSING");
            int stub = rows.Count(r => r.Status == "STUB");
            int graph = rows.Count(r => r.Status == "GRAPH");
            int impl = rows.Count(r => r.Status == "IMPLEMENTED");

            var md = new StringBuilder();
            md.AppendLine("# MayaImporter File NodeType Frequency (Priority S Source)");
            md.AppendLine();
            md.AppendLine($"- Timestamp: `{DateTime.Now:yyyy-MM-dd HH:mm:ss}`");
            md.AppendLine($"- Unity: `{Application.unityVersion}`");
            md.AppendLine($"- Source Folder: `{folderAbs}`");
            md.AppendLine($"- Files scanned (.ma/.mb): **{files.Length}**");
            md.AppendLine($"- Files parsed OK: **{parsedFiles}** / failed: **{failedFiles}**");
            md.AppendLine($"- Total nodes counted: **{totalNodes}**");
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
            foreach (var r in rows)
                md.AppendLine($"| {r.Status} | {r.ImplementationKind} | {r.Count} | `{r.NodeType}` | `{r.ChosenType}` |");

            md.AppendLine();
            md.AppendLine("## Next Action (Priority S)");
            md.AppendLine();
            md.AppendLine("- **MISSING**: まず「1 nodeType = 1 C#」のスタブ生成（nodeTypeの受け皿を0にする）");
            md.AppendLine("- **STUB**: 次に“頻度の高い順”に中身を濃くする（本当のPriority S）");
            md.AppendLine("- **GRAPH**: グラフ評価で回るので後回しでもOK");
            md.AppendLine("- **IMPLEMENTED**: 既に実装済み");
            md.AppendLine();

            File.WriteAllText(mdPath, md.ToString(), new UTF8Encoding(false));
            AssetDatabase.Refresh();

            Debug.Log($"[MayaImporter] ✅ File nodeType frequency written:\n- {mdPath}\n- {csvPath}");
            EditorUtility.DisplayDialog("MayaImporter",
                $"ファイルから nodeType頻度（Priority S）を出力しました。\n\n{mdPath}\n{csvPath}",
                "OK");
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
