// Assets/MayaImporter/MayaMissingNodeStubGenerator.cs
#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Editor
{
    /// <summary>
    /// Generate "1 Maya nodeType = 1 C# script" stubs for nodeTypes that appear in CURRENT Unity scene
    /// but have no mapping in NodeFactory.
    ///
    /// Phase①方針に合わせ、生成スタブは MayaPhaseCOpaqueRuntimeNodeBase 継承にする：
    /// - Unknown/MISSING を消す
    /// - Unityに概念が無いものも Opaque runtime marker + attribute preview を常に付与
    /// </summary>
    public static class MayaMissingNodeStubGenerator
    {
        private const string MenuPath = "Tools/Maya Importer/Coverage/Generate Missing Node Stubs (from CURRENT Scene)";
        private const string OutputDir = "Assets/MayaImporter/Generated/Phase1_MissingFromScene";

        [MenuItem(MenuPath)]
        public static void Generate()
        {
            Directory.CreateDirectory(OutputDir);

            var reg = NodeFactory.GetRegistry();
            var mapped = new HashSet<string>(reg.Keys, StringComparer.OrdinalIgnoreCase);

            var nodes = UnityEngine.Object.FindObjectsByType<MayaNodeComponentBase>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

            var sceneTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < nodes.Length; i++)
            {
                var n = nodes[i];
                if (n == null) continue;
                if (!n.gameObject.scene.IsValid()) continue;

                var nt = n.NodeType;
                if (string.IsNullOrEmpty(nt)) continue;
                sceneTypes.Add(nt);
            }

            var missing = sceneTypes
                .Where(nt => !mapped.Contains(nt))
                .OrderBy(nt => nt, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (missing.Count == 0)
            {
                EditorUtility.DisplayDialog("MayaImporter", "CURRENT Scene に MISSING nodeType はありませんでした。", "OK");
                Debug.Log("[MayaImporter] Missing stub generator: no missing nodeTypes in current scene.");
                return;
            }

            int created = 0;
            int overwritten = 0;

            foreach (var nodeType in missing)
            {
                var classNameBase = "MayaGenerated_" + ToSafeClassName(nodeType) + "Node";
                var className = classNameBase;
                var filePath = $"{OutputDir}/{className}.cs";

                // collision safe: suffix
                int suffix = 2;
                while (File.Exists(filePath))
                {
                    className = classNameBase + "_" + suffix;
                    filePath = $"{OutputDir}/{className}.cs";
                    suffix++;
                    if (suffix > 999) break;
                }

                var code = BuildOpaqueStubCode(nodeType, className);

                if (File.Exists(filePath)) overwritten++;
                else created++;

                File.WriteAllText(filePath, code, new UTF8Encoding(false));
            }

            AssetDatabase.Refresh();

            Debug.Log($"[MayaImporter] Generated missing node stubs from CURRENT Scene. created={created}, overwritten={overwritten}. OutputDir={OutputDir}");

            EditorUtility.DisplayDialog(
                "MayaImporter",
                $"CURRENT Scene に出現した MISSING nodeType のスタブを生成しました。\n\n" +
                $"created={created}\n" +
                $"overwritten={overwritten}\n\n" +
                $"出力先:\n{OutputDir}\n\n" +
                $"次に .ma/.mb を再Importすると Unknown が減ります。",
                "OK"
            );
        }

        private static string BuildOpaqueStubCode(string nodeType, string className)
        {
            var sb = new StringBuilder(2048);

            sb.AppendLine("// AUTO-GENERATED (Phase1) from CURRENT Scene");
            sb.AppendLine($"// NodeType: {nodeType}");
            sb.AppendLine("// Purpose:");
            sb.AppendLine("// - eliminate Unknown/MISSING");
            sb.AppendLine("// - always attach opaque runtime marker + attribute preview (Unity-only)");
            sb.AppendLine();
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine("using MayaImporter.Core;");
            sb.AppendLine();
            sb.AppendLine("namespace MayaImporter.Generated");
            sb.AppendLine("{");
            sb.AppendLine("    [DisallowMultipleComponent]");
            sb.AppendLine($"    [MayaNodeType(\"{EscapeForString(nodeType)}\")]");
            sb.AppendLine($"    public sealed class {className} : MayaPhaseCOpaqueRuntimeNodeBase");
            sb.AppendLine("    {");
            sb.AppendLine("        // Intentionally empty.");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static string ToSafeClassName(string nodeType)
        {
            if (string.IsNullOrEmpty(nodeType)) return "MayaNode";

            var parts = new List<string>();
            var cur = new StringBuilder();

            void Flush()
            {
                if (cur.Length == 0) return;
                parts.Add(cur.ToString());
                cur.Length = 0;
            }

            for (int i = 0; i < nodeType.Length; i++)
            {
                char ch = nodeType[i];
                if (char.IsLetterOrDigit(ch))
                {
                    if (cur.Length > 0 && char.IsLetter(ch))
                    {
                        char prev = cur[cur.Length - 1];
                        if (char.IsLower(prev) && char.IsUpper(ch)) Flush();
                    }
                    cur.Append(ch);
                }
                else
                {
                    Flush();
                }
            }
            Flush();

            if (parts.Count == 0) return "MayaNode";

            var pascal = new StringBuilder();
            for (int i = 0; i < parts.Count; i++)
            {
                var p = parts[i];
                if (string.IsNullOrEmpty(p)) continue;

                if (i == 0 && char.IsDigit(p[0])) pascal.Append('N');

                if (p.Length == 1) pascal.Append(char.ToUpperInvariant(p[0]));
                else pascal.Append(char.ToUpperInvariant(p[0])).Append(p.Substring(1));
            }

            var s = pascal.ToString();
            if (s == "Object" || s == "String" || s == "Type" || s == "Namespace") s = "Maya" + s;
            return s;
        }

        private static string EscapeForString(string s)
            => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");

        private static string EscapeForComment(string s)
            => (s ?? "").Replace("*/", "* /");
    }
}
#endif
