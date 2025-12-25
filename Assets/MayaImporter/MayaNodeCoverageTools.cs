// Assets/MayaImporter/MayaNodeCoverageTools.cs
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Collections.Generic;
using MayaImporter.Core;

namespace MayaImporter.Editor
{
    public static class MayaNodeCoverageTools
    {
        [MenuItem("Tools/Maya Importer/Coverage/Report Standard NodeType Implementation")]
        public static void ReportStandardImplementation()
        {
            var index = BuildIndex();

            bool hasStd = MayaStandardNodeTypes.TryGet(out var std) && std != null && std.Count > 0;
            if (!hasStd)
            {
                Debug.LogWarning("[MayaImporter] Maya2026 standard node type list not found. " +
                                 "Put Assets/MayaImporter/Resources/Maya2026_StandardNodeTypes.txt to enable standard coverage report.");
            }

            var sb = new StringBuilder();
            sb.AppendLine("NodeType,Status,Implementations,Notes");

            var summary = new List<string>();
            summary.Add($"[MayaImporter] Coverage Report: validImplementations={index.ValidNodeTypeToTypes.Count}, " +
                        $"duplicates={index.DuplicateNodeTypes.Count}, " +
                        $"invalidAttrUsers={index.InvalidAttributeUsers.Count}, " +
                        $"derivedButNoAttr={index.DerivedButNoAttribute.Count}");

            List<string> missingStd = null;
            List<string> extraNotStd = null;

            if (hasStd)
            {
                var present = new HashSet<string>(index.ValidNodeTypeToTypes.Keys, StringComparer.OrdinalIgnoreCase);

                missingStd = std.Where(s => !present.Contains(s))
                                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                                .ToList();

                extraNotStd = index.ValidNodeTypeToTypes.Keys
                                .Where(k => !std.Contains(k))
                                .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
                                .ToList();

                summary.Add($"[MayaImporter] Standard list: {std.Count} nodeTypes, missingImplementations={missingStd.Count}, implementedNotInStandard={extraNotStd.Count}");
            }

            if (hasStd)
            {
                foreach (var nt in std.OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
                {
                    if (!index.ValidNodeTypeToTypes.TryGetValue(nt, out var impls) || impls == null || impls.Count == 0)
                    {
                        sb.AppendLine($"{Csv(nt)},MISSING,,");
                        continue;
                    }

                    var implText = string.Join(" | ", impls.Select(t => t.FullName));
                    var status = (impls.Count >= 2) ? "DUPLICATE" : "OK";
                    sb.AppendLine($"{Csv(nt)},{status},{Csv(implText)},");
                }
            }

            if (hasStd && extraNotStd != null)
            {
                foreach (var nt in extraNotStd)
                {
                    var implText = string.Join(" | ", index.ValidNodeTypeToTypes[nt].Select(t => t.FullName));
                    sb.AppendLine($"{Csv(nt)},IMPLEMENTED_NOT_STANDARD,{Csv(implText)},Likely plugin/custom");
                }
            }

            if (index.InvalidAttributeUsers.Count > 0)
            {
                foreach (var t in index.InvalidAttributeUsers.OrderBy(t => t.FullName, StringComparer.Ordinal))
                {
                    var nts = GetNodeTypesFromAttribute(t);
                    sb.AppendLine($"{Csv(string.Join(" | ", nts))},INVALID_ATTR_USER,{Csv(t.FullName)},Has [MayaNodeType] but does not derive MayaNodeComponentBase");
                }
            }

            if (index.DerivedButNoAttribute.Count > 0)
            {
                foreach (var t in index.DerivedButNoAttribute.OrderBy(t => t.FullName, StringComparer.Ordinal))
                {
                    sb.AppendLine($",DERIVED_NO_ATTR,{Csv(t.FullName)},Derives MayaNodeComponentBase but missing [MayaNodeType]");
                }
            }

            var reportsDir = "Assets/MayaImporter/Reports";
            Directory.CreateDirectory(reportsDir);

            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var path = $"{reportsDir}/NodeImplementationCoverage__{stamp}.csv";

            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            AssetDatabase.Refresh();

            if (hasStd)
            {
                Debug.Log(string.Join("\n", summary));

                if (missingStd != null && missingStd.Count > 0)
                {
                    var top = missingStd.Take(50).ToArray();
                    Debug.LogWarning("[MayaImporter] Missing standard nodeType implementations (top 50):\n- " + string.Join("\n- ", top) +
                                     (missingStd.Count > 50 ? $"\n... and {missingStd.Count - 50} more" : ""));
                }

                if (index.DuplicateNodeTypes.Count > 0)
                {
                    Debug.LogWarning("[MayaImporter] Duplicate nodeType implementations:\n" +
                                     string.Join("\n", index.DuplicateNodeTypes.Select(kv =>
                                         $"- {kv.Key}: {string.Join(", ", kv.Value.Select(t => t.FullName))}")));
                }
            }

            Debug.Log($"[MayaImporter] Wrote coverage CSV: {path}");
        }

        // 速さ優先：出力先固定＆上書き可能
        private const string FixedOutputDir = "Assets/MayaImporter/Generated/Phase1_MissingFromStandard";

        [MenuItem("Tools/Maya Importer/Coverage/Generate Missing Node Stubs (from Standard List, OVERWRITE)")]
        public static void GenerateMissingNodeStubs_Overwrite()
            => GenerateMissingNodeStubs_Internal(overwrite: true);

        [MenuItem("Tools/Maya Importer/Coverage/Generate Missing Node Stubs (from Standard List, SKIP existing)")]
        public static void GenerateMissingNodeStubs_Skip()
            => GenerateMissingNodeStubs_Internal(overwrite: false);

        private static void GenerateMissingNodeStubs_Internal(bool overwrite)
        {
            if (!MayaStandardNodeTypes.TryGet(out var std) || std == null || std.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "MayaImporter",
                    "Maya2026_StandardNodeTypes.txt が見つかりません。\n\n" +
                    "Assets/MayaImporter/Resources/Maya2026_StandardNodeTypes.txt を作成して、1行1nodeTypeで貼り付けてから再実行してください。",
                    "OK"
                );
                return;
            }

            var reg = NodeFactory.GetRegistry();
            var mapped = new HashSet<string>(reg.Keys, StringComparer.OrdinalIgnoreCase);

            var missing = std.Where(s => !mapped.Contains(s))
                             .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                             .ToList();

            if (missing.Count == 0)
            {
                EditorUtility.DisplayDialog("MayaImporter", "標準nodeTypeに対する未実装が 0 件です 🎯", "OK");
                return;
            }

            Directory.CreateDirectory(FixedOutputDir);

            int written = 0;
            int skipped = 0;

            foreach (var nt in missing)
            {
                var classBase = "MayaGenerated_" + ToSafePascalIdentifier(nt) + "Node";
                var className = classBase;

                // collision safe
                var filePath = $"{FixedOutputDir}/{className}.cs";
                int suffix = 2;
                while (File.Exists(filePath) && !overwrite)
                {
                    skipped++;
                    goto Next;
                }
                while (File.Exists(filePath) && overwrite == false)
                {
                    suffix++;
                }

                if (!overwrite)
                {
                    // already handled above
                }
                else
                {
                    // ensure unique name only if overwriting is false; overwriting => keep deterministic path
                    // so no suffix here
                }

                var code = GenerateOpaqueStubCode(nt, className);
                File.WriteAllText(filePath, code, new UTF8Encoding(false));
                written++;

            Next:
                continue;
            }

            AssetDatabase.Refresh();

            Debug.Log($"[MayaImporter] Generated missing stubs from standard list: written={written}, skipped={skipped}, overwrite={overwrite}. OutputDir={FixedOutputDir}");

            EditorUtility.DisplayDialog(
                "MayaImporter",
                $"標準nodeType 未実装 {missing.Count} 件\n\n" +
                $"生成: {written}\nスキップ: {skipped}\n上書き: {overwrite}\n\n" +
                $"出力先:\n{FixedOutputDir}",
                "OK"
            );
        }

        // ----------------- internals -----------------

        private sealed class Index
        {
            public Dictionary<string, List<Type>> ValidNodeTypeToTypes = new Dictionary<string, List<Type>>(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, List<Type>> DuplicateNodeTypes = new Dictionary<string, List<Type>>(StringComparer.OrdinalIgnoreCase);
            public List<Type> InvalidAttributeUsers = new List<Type>();
            public List<Type> DerivedButNoAttribute = new List<Type>();
        }

        private static Index BuildIndex()
        {
            var baseType = typeof(MayaNodeComponentBase);
            var idx = new Index();

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
                var attrs = t.GetCustomAttributes(typeof(MayaNodeTypeAttribute), false);
                bool hasAttr = attrs != null && attrs.Length > 0;
                bool derivesBase = baseType.IsAssignableFrom(t);

                if (hasAttr && !derivesBase)
                {
                    idx.InvalidAttributeUsers.Add(t);
                    continue;
                }

                if (derivesBase)
                {
                    if (!hasAttr)
                    {
                        idx.DerivedButNoAttribute.Add(t);
                        continue;
                    }

                    foreach (var a in attrs)
                    {
                        var nt = ((MayaNodeTypeAttribute)a).NodeType;
                        if (string.IsNullOrEmpty(nt)) continue;

                        if (!idx.ValidNodeTypeToTypes.TryGetValue(nt, out var list))
                        {
                            list = new List<Type>();
                            idx.ValidNodeTypeToTypes[nt] = list;
                        }
                        if (!list.Contains(t)) list.Add(t);
                    }
                }
            }

            foreach (var kv in idx.ValidNodeTypeToTypes)
            {
                if (kv.Value.Count >= 2)
                    idx.DuplicateNodeTypes[kv.Key] = kv.Value;
            }

            return idx;
        }

        private static List<string> GetNodeTypesFromAttribute(Type t)
        {
            var attrs = t.GetCustomAttributes(typeof(MayaNodeTypeAttribute), false);
            if (attrs == null || attrs.Length == 0) return new List<string>();
            return attrs.Cast<MayaNodeTypeAttribute>()
                        .Select(a => a.NodeType)
                        .Where(s => !string.IsNullOrEmpty(s))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
        }

        private static string GenerateOpaqueStubCode(string nodeType, string className)
        {
            // Phase①の最短ゴール：
            // - nodeTypeの受け皿を0にする（Unknown/MISSING排除）
            // - Unityに概念が無いものも「Opaque runtime marker + preview」で100点条件を満たす
            var sb = new StringBuilder(2048);
            sb.AppendLine("// AUTO-GENERATED (Phase1) by MayaNodeCoverageTools");
            sb.AppendLine($"// NodeType: {nodeType}");
            sb.AppendLine("// Purpose:");
            sb.AppendLine("// - 1 nodeType = 1 C# (mapping exists)");
            sb.AppendLine("// - Opaque finalized: runtime marker + attribute preview (Unity-only)");
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
            sb.AppendLine("        // DecodePhaseC is provided by MayaPhaseCOpaqueRuntimeNodeBase.");
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
                if (char.IsLetterOrDigit(c))
                {
                    cur.Append(c);
                }
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
