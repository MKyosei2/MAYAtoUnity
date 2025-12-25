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
using MayaImporter.Runtime;

namespace MayaImporter.Editor
{
    /// <summary>
    /// Phase-2 driver:
    /// Upgrade "Generated STUB" scripts (that appear in CURRENT scene) into Opaque implementations.
    ///
    /// - Keeps 1 nodeType = 1 C# mapping (no duplicates created)
    /// - Overwrites only scripts under Assets/MayaImporter/Generated/Nodes
    /// - Only targets nodeTypes that appear in CURRENT scene AND are currently STUB (no ApplyToUnity override)
    ///
    /// After running:
    /// - Coverage/Frequency will show these as IMPLEMENTED (via ApplyToUnity base)
    /// - They will add MayaOpaqueNodeRuntime at ApplyToUnity time (re-import or Apply pipeline)
    /// </summary>
    public static class MayaGeneratedStubAutoImplementer
    {
        private const string MenuPath = "Tools/Maya Importer/Coverage/Auto-Implement Generated STUBs as Opaque (CURRENT Scene)";
        private const string GeneratedRoot = "Assets/MayaImporter/Generated/Nodes";

        [MenuItem(MenuPath)]
        public static void Run()
        {
            if (!Directory.Exists(GeneratedRoot))
            {
                EditorUtility.DisplayDialog("MayaImporter", $"生成フォルダが見つかりません:\n{GeneratedRoot}", "OK");
                return;
            }

            // Collect nodeTypes present in current scene
            var sceneNodes = UnityEngine.Object.FindObjectsByType<MayaNodeComponentBase>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

            var sceneNodeTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < sceneNodes.Length; i++)
            {
                var n = sceneNodes[i];
                if (n == null) continue;
                if (!n.gameObject.scene.IsValid()) continue;

                if (!string.IsNullOrEmpty(n.NodeType))
                    sceneNodeTypes.Add(n.NodeType);
            }

            if (sceneNodeTypes.Count == 0)
            {
                EditorUtility.DisplayDialog("MayaImporter", "CURRENT Scene に MayaNodeComponentBase が見つかりませんでした。", "OK");
                return;
            }

            int rewritten = 0;
            int skippedNotGenerated = 0;
            int skippedAlreadyImplemented = 0;
            int skippedNoScript = 0;

            foreach (var nodeType in sceneNodeTypes.OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
            {
                var mapped = NodeFactory.ResolveType(nodeType);
                if (mapped == null) continue;

                // Only target Generated stubs
                var full = mapped.FullName ?? "";
                if (!full.StartsWith("MayaImporter.Generated.Nodes.", StringComparison.Ordinal))
                {
                    skippedNotGenerated++;
                    continue;
                }

                // Only target STUB (no override)
                if (OverridesApplyToUnity(mapped))
                {
                    skippedAlreadyImplemented++;
                    continue;
                }

                // Find the script asset for the mapped type
                if (!TryFindScriptPathForType(mapped, out var scriptPath))
                {
                    skippedNoScript++;
                    continue;
                }

                // Only overwrite scripts under GeneratedRoot
                if (!scriptPath.Replace('\\','/').StartsWith(GeneratedRoot, StringComparison.OrdinalIgnoreCase))
                {
                    skippedNotGenerated++;
                    continue;
                }

                // Overwrite entire script with an Opaque implementation
                var className = mapped.Name;
                var code = BuildOpaqueImplementation(nodeType, className);

                File.WriteAllText(scriptPath, code, new UTF8Encoding(false));
                rewritten++;
            }

            AssetDatabase.Refresh();

            Debug.Log($"[MayaImporter] Auto-Implement Generated STUBs: rewritten={rewritten}, " +
                      $"skipped(not generated)={skippedNotGenerated}, skipped(already impl)={skippedAlreadyImplemented}, skipped(no script)={skippedNoScript}");

            EditorUtility.DisplayDialog(
                "MayaImporter",
                $"Auto-Implement 完了\n\nrewritten={rewritten}\nnot generated={skippedNotGenerated}\nalready impl={skippedAlreadyImplemented}\nno script={skippedNoScript}\n\n次は Frequency/Coverage を再実行して STUB が減ったことを確認してください。",
                "OK"
            );
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

        private static bool TryFindScriptPathForType(Type t, out string path)
        {
            path = null;
            if (t == null) return false;

            // FindAssets by class name (best-effort)
            var guids = AssetDatabase.FindAssets($"{t.Name} t:script");
            if (guids == null || guids.Length == 0) return false;

            for (int i = 0; i < guids.Length; i++)
            {
                var p = AssetDatabase.GUIDToAssetPath(guids[i]);
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(p);
                if (script == null) continue;

                var scriptClass = script.GetClass();
                if (scriptClass == t)
                {
                    path = p;
                    return true;
                }
            }

            return false;
        }

        private static string BuildOpaqueImplementation(string nodeType, string className)
        {
            var sb = new StringBuilder(2048);

            sb.AppendLine("using MayaImporter.Core;");
            sb.AppendLine("using MayaImporter.Runtime;");
            sb.AppendLine();
            sb.AppendLine("namespace MayaImporter.Generated.Nodes");
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>");
            sb.AppendLine($"    /// AUTO-GENERATED OPAQUE IMPLEMENTATION for Maya nodeType \"{EscapeForComment(nodeType)}\".");
            sb.AppendLine("    ///");
            sb.AppendLine("    /// This fulfills portfolio requirements:");
            sb.AppendLine("    /// - 1 Maya nodeType = 1 C# script mapping");
            sb.AppendLine("    /// - Unity has no direct concept => create Unity component (MayaOpaqueNodeRuntime)");
            sb.AppendLine("    /// - Full attributes/connections are preserved on MayaNodeComponentBase");
            sb.AppendLine("    ///");
            sb.AppendLine("    /// If later you implement a real Unity reconstruction, replace base class and override ApplyToUnity.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine($"    [MayaNodeType(\"{EscapeForString(nodeType)}\")]");
            sb.AppendLine($"    public sealed class {className} : MayaAutoOpaqueNodeBase");
            sb.AppendLine("    {");
            sb.AppendLine("        // Intentionally empty. Base handles ApplyToUnity by attaching MayaOpaqueNodeRuntime.");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static string EscapeForString(string s)
            => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");

        private static string EscapeForComment(string s)
            => (s ?? "").Replace("*/", "* /");
    }
}
#endif
