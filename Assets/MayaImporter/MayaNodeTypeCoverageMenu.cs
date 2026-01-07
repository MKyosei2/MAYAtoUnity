// Assets/MayaImporter/MayaNodeTypeCoverageMenu.cs
// Editor-only: NodeTypeカバレッジ（標準nodeType一覧 vs 実装済み[MayaNodeType]）をレポート化する。

#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.EditorTools
{
    public static class MayaNodeTypeCoverageMenu
    {
        private const string MenuRoot = "Tools/Maya Importer/Coverage/";
        private const string ReportsDir = "Assets/MayaImporter/Reports";
        private const string ReportFileName = "Maya2026_NodeTypeCoverage.txt";

        [MenuItem(MenuRoot + "Generate NodeType Coverage Report (Maya2026)...", priority = 200)]
        public static void GenerateNodeTypeCoverageReport()
        {
            try
            {
                // Registry snapshot（[MayaNodeType]属性の付いたコンポーネント群を収集）
                var snap = MayaNodeTypeRegistryCache.GetOrBuild(forceRebuild: true);

                // 実装済み nodeType
                var implemented = new HashSet<string>(snap.AllNodeTypes ?? new HashSet<string>(), StringComparer.OrdinalIgnoreCase);

                // 標準一覧（無い場合は空扱い）
                var standard = snap.Maya2026StandardNodeTypesOrNull;

                var missing = new List<string>();
                var extra = new List<string>();

                if (standard != null && standard.Count > 0)
                {
                    // Missing = standard - implemented
                    foreach (var s in standard)
                        if (!implemented.Contains(s))
                            missing.Add(s);

                    // Extra = implemented - standard
                    var standardSetCI = new HashSet<string>(standard, StringComparer.OrdinalIgnoreCase);
                    foreach (var i in implemented)
                        if (!standardSetCI.Contains(i))
                            extra.Add(i);

                    missing.Sort(StringComparer.OrdinalIgnoreCase);
                    extra.Sort(StringComparer.OrdinalIgnoreCase);
                }

                Directory.CreateDirectory(ReportsDir);
                var outPath = Path.Combine(ReportsDir, ReportFileName);

                var sb = new StringBuilder(16 * 1024);
                sb.AppendLine("Maya 2026 NodeType Coverage Report");
                sb.AppendLine("================================");
                sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine();

                sb.AppendLine($"Implemented nodeTypes ([MayaNodeType]): {implemented.Count}");
                sb.AppendLine($"Duplicate nodeTypes (violates 1 nodeType = 1 script): {(snap.DuplicatesByNodeType != null ? snap.DuplicatesByNodeType.Count : 0)}");

                if (standard == null || standard.Count == 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("Standard list: NOT FOUND");
                    sb.AppendLine("Put this file to enable strict 100% proof:");
                    sb.AppendLine("  Assets/MayaImporter/Resources/Maya2026_StandardNodeTypes.txt");
                }
                else
                {
                    sb.AppendLine($"Standard nodeTypes: {standard.Count}");
                    sb.AppendLine($"Missing (standard - implemented): {missing.Count}");
                    sb.AppendLine($"Extra (implemented - standard): {extra.Count}");

                    sb.AppendLine();
                    sb.AppendLine("---- Missing (standard - implemented) ----");
                    for (int i = 0; i < missing.Count; i++)
                        sb.AppendLine(missing[i]);

                    sb.AppendLine();
                    sb.AppendLine("---- Extra (implemented - standard) ----");
                    for (int i = 0; i < extra.Count; i++)
                        sb.AppendLine(extra[i]);
                }

                // Duplicates詳細
                if (snap.DuplicatesByNodeType != null && snap.DuplicatesByNodeType.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("---- Duplicates (must fix) ----");
                    foreach (var kv in snap.DuplicatesByNodeType.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
                    {
                        sb.Append("  ").Append(kv.Key).Append(" -> ");
                        if (kv.Value == null) { sb.AppendLine("(null)"); continue; }
                        sb.AppendLine(string.Join(", ", kv.Value.Select(t => t != null ? t.FullName : "null")));
                    }
                }

                File.WriteAllText(outPath, sb.ToString(), Encoding.UTF8);
                AssetDatabase.Refresh();

                Debug.Log($"[MayaImporter] Wrote NodeType coverage report: {outPath}");

                if (EditorUtility.DisplayDialog("Maya Importer", $"Wrote report:\n{outPath}\n\nOpen folder?", "Open", "Close"))
                {
                    EditorUtility.RevealInFinder(outPath);
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EditorUtility.DisplayDialog("Maya Importer", "Failed to generate report:\n" + ex.Message, "OK");
            }
        }
    }
}
#endif
