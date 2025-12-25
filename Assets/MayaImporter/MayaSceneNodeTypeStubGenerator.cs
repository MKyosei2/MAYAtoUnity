#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Collections.Generic;
using MayaImporter;
using MayaImporter.Core;

namespace MayaImporter.Editor
{
    public static class MayaSceneNodeTypeStubGenerator
    {
        [MenuItem("Tools/Maya Importer/Coverage/Generate Missing Node Stubs (from CURRENT Scene)")]
        public static void GenerateFromCurrentScene()
        {
            // 1) gather nodeTypes that appear in current scene
            var nodes = UnityEngine.Object.FindObjectsByType<MayaNodeComponentBase>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

            var appeared = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < nodes.Length; i++)
            {
                var n = nodes[i];
                if (n == null) continue;
                if (!n.gameObject.scene.IsValid()) continue;

                var nt = n.NodeType;
                if (string.IsNullOrEmpty(nt)) continue;
                appeared.Add(nt);
            }

            if (appeared.Count == 0)
            {
                EditorUtility.DisplayDialog("MayaImporter", "現在のシーンに MayaNodeComponentBase が見つかりません。", "OK");
                return;
            }

            // 2) gather implemented nodeTypes in project (reflection)
            var implemented = BuildImplementedNodeTypeSet();

            // 3) missing = appeared - implemented
            var missing = appeared.Where(nt => !implemented.Contains(nt))
                                  .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                                  .ToList();

            if (missing.Count == 0)
            {
                EditorUtility.DisplayDialog("MayaImporter", "現在のシーンに出現した nodeType は全て実装済みです 🎯", "OK");
                return;
            }

            // 4) choose output folder under Assets
            var absFolder = EditorUtility.OpenFolderPanel(
                "Select output folder (must be under Assets)",
                Application.dataPath,
                "MayaImporter/Generated/Nodes"
            );

            if (string.IsNullOrEmpty(absFolder))
                return;

            var assetsPath = Application.dataPath.Replace("\\", "/");
            var folderPath = absFolder.Replace("\\", "/");

            if (!folderPath.StartsWith(assetsPath, StringComparison.OrdinalIgnoreCase))
            {
                EditorUtility.DisplayDialog("MayaImporter", "出力先は Assets 配下である必要があります。", "OK");
                return;
            }

            var relFolder = "Assets" + folderPath.Substring(assetsPath.Length);
            Directory.CreateDirectory(relFolder);

            int written = 0;
            foreach (var nt in missing)
            {
                var className = "MayaGenerated_" + ToSafePascalIdentifier(nt) + "Node";
                var fileName = className + ".cs";
                var filePath = Path.Combine(relFolder, fileName).Replace("\\", "/");

                if (File.Exists(filePath))
                    continue;

                File.WriteAllText(filePath, GenerateStubCode(nt, className), new UTF8Encoding(false));
                written++;
            }

            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "MayaImporter",
                $"シーン出現 nodeType の未実装 {missing.Count} 件のうち、{written} 件をスタブ生成しました。\n" +
                $"（同名ファイルが既にある場合はスキップ）",
                "OK"
            );

            Debug.Log("[MayaImporter] Missing nodeTypes (from CURRENT Scene):\n- " + string.Join("\n- ", missing));
            Debug.Log($"[MayaImporter] Generated {written} stubs under: {relFolder}");
        }

        private static HashSet<string> BuildImplementedNodeTypeSet()
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var baseType = typeof(MayaNodeComponentBase);

            var allTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a =>
                {
                    try { return a.GetTypes(); }
                    catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null); }
                })
                .Where(t => t != null && !t.IsAbstract)
                .ToArray();

            foreach (var t in allTypes)
            {
                if (!baseType.IsAssignableFrom(t))
                    continue;

                var attrs = t.GetCustomAttributes(typeof(MayaNodeTypeAttribute), false);
                if (attrs == null || attrs.Length == 0)
                    continue;

                for (int i = 0; i < attrs.Length; i++)
                {
                    var nt = ((MayaNodeTypeAttribute)attrs[i]).NodeType;
                    if (string.IsNullOrEmpty(nt)) continue;
                    set.Add(nt);
                }
            }

            return set;
        }

        private static string GenerateStubCode(string nodeType, string className)
        {
            return
$@"// AUTO-GENERATED by MayaSceneNodeTypeStubGenerator
// NodeType: {nodeType}
// Stub for 1 Maya nodeType = 1 Unity C# Component.
// Customize ApplyToUnity() later if reconstruction is needed.

using UnityEngine;
using MayaImporter;
using MayaImporter.Core;

namespace MayaImporter.Generated
{{
    [DisallowMultipleComponent]
    [MayaNodeType(""{EscapeForCSharpString(nodeType)}"")]
    public sealed class {className} : MayaNodeComponentBase
    {{
        // Intentionally empty.
        // Base.InitializeFromRecord keeps raw attributes & connections for lossless portability.
    }}
}}
";
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
                if (char.IsLetterOrDigit(c))
                    cur.Append(c);
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
                sb.Append(char.ToUpperInvariant(p[0]));
                if (p.Length > 1) sb.Append(p.Substring(1));
            }

            var id = sb.ToString();
            if (string.IsNullOrEmpty(id)) id = "Node";
            if (char.IsDigit(id[0])) id = "N" + id;

            return id;
        }
    }
}
#endif
