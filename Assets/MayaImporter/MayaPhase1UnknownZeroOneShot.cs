// Assets/MayaImporter/MayaPhase1UnknownZeroOneShot.cs
#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Editor
{
    /// <summary>
    /// Phase① (NO .ma/.mb assumption):
    /// Standard list + CURRENT Unity Scene の union から Missing(nodeType未マッピング) を抽出し、
    /// Opaque stub を一括生成して Unknown/MISSING を 0 に寄せる。
    ///
    /// - フォルダスキャン無し（.ma/.mbが無い前提）
    /// - 生成物は MayaPhaseCOpaqueRuntimeNodeBase 継承（Unity-onlyで100%保持の受け皿）
    /// </summary>
    public static class MayaPhase1UnknownZeroOneShot
    {
        private const string OutputDir = "Assets/MayaImporter/Generated/Phase1_UnknownZero";
        private const string ReportsDir = "Assets/MayaImporter/Reports";

        [MenuItem("Tools/Maya Importer/Phase1/Make Unknown+Missing ZERO (Standard + Scene ONLY)")]
        public static void Run()
        {
            Directory.CreateDirectory(OutputDir);
            Directory.CreateDirectory(ReportsDir);

            // mapped registry (existing implementations)
            var reg = NodeFactory.GetRegistry();
            var mapped = new HashSet<string>(reg.Keys, StringComparer.OrdinalIgnoreCase);

            // targets = standard + scene
            var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1) Standard nodeTypes
            bool hasStandard = MayaStandardNodeTypes.TryGet(out var std) && std != null && std.Count > 0;
            if (hasStandard)
            {
                foreach (var nt in std)
                {
                    if (!string.IsNullOrEmpty(nt))
                        targets.Add(nt);
                }
            }
            else
            {
                Debug.LogWarning(
                    "[MayaImporter] Standard list not found or empty.\n" +
                    "Put: Assets/MayaImporter/Resources/Maya2026_StandardNodeTypes.txt (1 nodeType per line)\n" +
                    "Proceeding with CURRENT Scene only."
                );
            }

            // 2) CURRENT Scene nodeTypes (custom/plugin nodeTypes in your reconstructed scene)
            try
            {
                var nodes = UnityEngine.Object.FindObjectsByType<MayaNodeComponentBase>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None
                );

                for (int i = 0; i < nodes.Length; i++)
                {
                    var n = nodes[i];
                    if (n == null) continue;
                    if (!n.gameObject.scene.IsValid()) continue;

                    var nt = n.NodeType;
                    if (!string.IsNullOrEmpty(nt))
                        targets.Add(nt);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[MayaImporter] Scene scan failed:\n" + ex);
            }

            // missing = targets - mapped
            var missing = targets
                .Where(nt => !mapped.Contains(nt))
                .OrderBy(nt => nt, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (missing.Count == 0)
            {
                EditorUtility.DisplayDialog("MayaImporter (Phase1)", "Missing nodeType は 0 件でした 🎯", "OK");
                Debug.Log("[MayaImporter] Phase1 UnknownZero: missing=0");
                WriteReport(hasStandard, targets, missing, written: 0, skipped: 0, overwrite: false);
                return;
            }

            bool overwrite = EditorUtility.DisplayDialog(
                "MayaImporter (Phase1)",
                $"Missing nodeType: {missing.Count} 件\n\n生成スタブを上書きしますか？\n（おすすめ：上書き＝決定論で揃う）",
                "上書きする",
                "上書きしない"
            );

            int written = 0;
            int skipped = 0;

            // collision-safe class name set
            var usedClassNames = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < missing.Count; i++)
            {
                var nt = missing[i];

                string classBase = "MayaGenerated_" + ToSafePascalIdentifier(nt) + "Node";
                string className = classBase;

                int suffix = 2;
                while (usedClassNames.Contains(className))
                {
                    className = classBase + "_" + suffix;
                    suffix++;
                    if (suffix > 999) break;
                }
                usedClassNames.Add(className);

                var filePath = $"{OutputDir}/{className}.cs";

                if (File.Exists(filePath) && !overwrite)
                {
                    skipped++;
                    continue;
                }

                var code = BuildOpaqueStubCode(nt, className);
                File.WriteAllText(filePath, code, new UTF8Encoding(false));
                written++;
            }

            AssetDatabase.Refresh();

            WriteReport(hasStandard, targets, missing, written, skipped, overwrite);

            Debug.Log($"[MayaImporter] Phase1 UnknownZero done. missing={missing.Count}, written={written}, skipped={skipped}, overwrite={overwrite}");
            EditorUtility.DisplayDialog(
                "MayaImporter (Phase1)",
                $"Phase1 完了。\n\nMissing={missing.Count}\nWritten={written}\nSkipped={skipped}\nOverwrite={overwrite}\n\n出力先:\n{OutputDir}",
                "OK"
            );
        }

        private static void WriteReport(bool hasStandard, HashSet<string> targets, List<string> missing, int written, int skipped, bool overwrite)
        {
            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var mdPath = $"{ReportsDir}/Phase1_UnknownZero__{stamp}.md";

            var md = new StringBuilder();
            md.AppendLine("# Phase1 Unknown/Missing ZERO Report (Standard + Scene ONLY)");
            md.AppendLine();
            md.AppendLine($"- Timestamp: `{DateTime.Now:yyyy-MM-dd HH:mm:ss}`");
            md.AppendLine($"- StandardList: `{(hasStandard ? "FOUND" : "NOT_FOUND")}`");
            md.AppendLine($"- Targets(unique): **{targets.Count}**");
            md.AppendLine($"- Missing: **{missing.Count}**");
            md.AppendLine($"- Written: **{written}**");
            md.AppendLine($"- Skipped: **{skipped}**");
            md.AppendLine($"- Overwrite: **{overwrite}**");
            md.AppendLine();
            md.AppendLine($"- OutputDir: `{OutputDir}`");
            md.AppendLine();
            md.AppendLine("## Missing nodeTypes");
            md.AppendLine();
            foreach (var nt in missing) md.AppendLine($"- `{nt}`");

            File.WriteAllText(mdPath, md.ToString(), new UTF8Encoding(false));
            AssetDatabase.Refresh();

            Debug.Log($"[MayaImporter] Wrote report: {mdPath}");
        }

        private static string BuildOpaqueStubCode(string nodeType, string className)
        {
            var sb = new StringBuilder(2048);
            sb.AppendLine("// AUTO-GENERATED (Phase1 UnknownZero) - Standard + Scene ONLY");
            sb.AppendLine($"// NodeType: {nodeType}");
            sb.AppendLine("// - 1 nodeType = 1 C# mapping");
            sb.AppendLine("// - Opaque runtime marker + attribute preview (Unity-only)");
            sb.AppendLine();
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine("using MayaImporter.Core;");
            sb.AppendLine();
            sb.AppendLine("namespace MayaImporter.Generated");
            sb.AppendLine("{");
            sb.AppendLine("    [DisallowMultipleComponent]");
            sb.AppendLine($"    [MayaNodeType(\"{EscapeForCSharpString(nodeType)}\")]");
            sb.AppendLine($"    public sealed class {className} : MayaPhaseCOpaqueRuntimeNodeBase");
            sb.AppendLine("    {");
            sb.AppendLine("        // Intentionally empty.");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private static string EscapeForCSharpString(string s)
        {
            if (s == null) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static string ToSafePascalIdentifier(string nodeType)
        {
            if (string.IsNullOrEmpty(nodeType)) return "Unknown";

            var parts = new List<string>();
            var cur = new StringBuilder();

            for (int i = 0; i < nodeType.Length; i++)
            {
                char c = nodeType[i];
                if (char.IsLetterOrDigit(c)) cur.Append(c);
                else
                {
                    if (cur.Length > 0) { parts.Add(cur.ToString()); cur.Length = 0; }
                }
            }
            if (cur.Length > 0) parts.Add(cur.ToString());
            if (parts.Count == 0) parts.Add("Node");

            var sb = new StringBuilder();
            foreach (var p in parts)
            {
                if (string.IsNullOrEmpty(p)) continue;
                if (p.Length == 1) sb.Append(char.ToUpperInvariant(p[0]));
                else sb.Append(char.ToUpperInvariant(p[0])).Append(p.Substring(1));
            }

            var id = sb.ToString();
            if (string.IsNullOrEmpty(id)) id = "Node";
            if (char.IsDigit(id[0])) id = "N" + id;
            return id;
        }
    }
}
#endif
