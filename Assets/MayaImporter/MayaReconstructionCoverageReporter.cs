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
    public static class MayaReconstructionCoverageReporter
    {
        private const string ReportsDir = "Assets/MayaImporter/Reports";
        private const string RootMenu = "Tools/Maya Importer/Audit/";

        private static readonly HashSet<string> NoNodeTypeRequired_FullNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "MayaImporter.Core.MayaPlaceholderNode",
            "MayaImporter.Core.MayaUnknownNodeComponent",
            "MayaImporter.Shader.UnknownShaderNodeComponent",
        };

        [MenuItem(RootMenu + "Report Reconstruction Coverage (ApplyToUnity OR GraphCompute + Scene)")]
        public static void Report()
        {
            Directory.CreateDirectory(ReportsDir);

            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var mdPath = $"{ReportsDir}/ReconstructionCoverage__{stamp}.md";
            var csvPath = $"{ReportsDir}/ReconstructionCoverage__{stamp}.csv";

            var index = BuildIndex();
            var sceneInfo = BuildSceneInfo(index);

            bool hasStd = MayaStandardNodeTypes.TryGet(out var std) && std != null && std.Count > 0;

            // CSV
            var csv = new StringBuilder();
            csv.AppendLine("NodeType,Status,ImplementationKind,ChosenType,IsDuplicate,AllTypes");

            foreach (var kv in index.ValidNodeTypeToTypes.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
            {
                var nodeType = kv.Key;
                var types = kv.Value ?? new List<Type>();

                var chosen = ChooseDeterministically(types);
                bool dup = types.Count > 1;

                var kind = GetImplementationKind(nodeType, chosen);
                var status = kind == "NONE" ? "STUB" : "IMPLEMENTED";

                var all = string.Join(" | ", types.OrderBy(t => t.FullName, StringComparer.Ordinal).Select(t => t.FullName));
                csv.AppendLine($"{Csv(nodeType)},{status},{kind},{Csv(chosen?.FullName ?? "")},{(dup ? "true" : "false")},{Csv(all)}");
            }

            if (hasStd)
            {
                var present = new HashSet<string>(index.ValidNodeTypeToTypes.Keys, StringComparer.OrdinalIgnoreCase);
                foreach (var nt in std.OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
                {
                    if (present.Contains(nt)) continue;
                    csv.AppendLine($"{Csv(nt)},MISSING,NONE,,false,");
                }
            }

            File.WriteAllText(csvPath, csv.ToString(), new UTF8Encoding(false));

            // Markdown
            var md = new StringBuilder();
            md.AppendLine("# MayaImporter Reconstruction Coverage Report");
            md.AppendLine();
            md.AppendLine($"- Timestamp: `{DateTime.Now:yyyy-MM-dd HH:mm:ss}`");
            md.AppendLine($"- Unity: `{Application.unityVersion}`");
            md.AppendLine();

            md.AppendLine("## 1) Project-wide Summary");
            md.AppendLine();
            md.AppendLine($"- nodeType mappings (valid): **{index.ValidNodeTypeToTypes.Count}**");
            md.AppendLine($"- duplicate nodeTypes: **{index.DuplicateNodeTypes.Count}**");
            md.AppendLine($"- invalid [MayaNodeType] users: **{index.InvalidAttributeUsers.Count}**");
            md.AppendLine($"- derived but no [MayaNodeType] (excluding allowed): **{index.DerivedButNoAttribute.Count}**");
            md.AppendLine();

            int implApply = 0;
            int implGraph = 0;
            int stub = 0;

            foreach (var kv in index.ValidNodeTypeToTypes)
            {
                var chosen = ChooseDeterministically(kv.Value);
                var kind = GetImplementationKind(kv.Key, chosen);

                if (kind == "ApplyToUnity") implApply++;
                else if (kind == "GraphCompute") implGraph++;
                else stub++;
            }

            md.AppendLine($"- IMPLEMENTED via ApplyToUnity: **{implApply}**");
            md.AppendLine($"- IMPLEMENTED via GraphCompute: **{implGraph}**");
            md.AppendLine($"- STUB (neither): **{stub}**");
            md.AppendLine();

            if (hasStd)
            {
                var present = new HashSet<string>(index.ValidNodeTypeToTypes.Keys, StringComparer.OrdinalIgnoreCase);
                int missingStd = std.Count(s => !present.Contains(s));

                md.AppendLine("## 2) Standard List (Maya2026_StandardNodeTypes.txt)");
                md.AppendLine();
                md.AppendLine($"- Standard nodeTypes: **{std.Count}**");
                md.AppendLine($"- Missing mappings: **{missingStd}**");
                md.AppendLine();
            }

            md.AppendLine("## 3) Current Scene Summary");
            md.AppendLine();
            md.AppendLine($"- MayaNodeComponentBase in scene: **{sceneInfo.SceneNodeCount}**");
            md.AppendLine($"- Unique nodeTypes in scene: **{sceneInfo.SceneUniqueNodeTypes.Count}**");
            md.AppendLine($"- Unknown/Placeholder components: **{sceneInfo.SceneUnknownCount}**");
            md.AppendLine($"- Scene nodeTypes missing mapping: **{sceneInfo.SceneMissingMapping.Count}**");
            md.AppendLine($"- Scene nodeTypes stub-only: **{sceneInfo.SceneStubOnly.Count}**");
            md.AppendLine();

            if (sceneInfo.SceneMissingMapping.Count > 0)
            {
                md.AppendLine("### 3.1 Missing Mapping (Appeared in Scene)");
                foreach (var s in sceneInfo.SceneMissingMapping.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                    md.AppendLine($"- `{s}`");
                md.AppendLine();
            }

            if (sceneInfo.SceneStubOnly.Count > 0)
            {
                md.AppendLine("### 3.2 Stub-only (Appeared in Scene, not ApplyToUnity nor GraphCompute)");
                foreach (var s in sceneInfo.SceneStubOnly.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                    md.AppendLine($"- `{s}`");
                md.AppendLine();
                md.AppendLine("> Phase-2 task: Implement these nodeTypes first (they are present in your real test scene).");
                md.AppendLine();
            }

            md.AppendLine("## 4) Top Stub NodeTypes (Project-wide, first 100)");
            md.AppendLine();

            var topStub = index.ValidNodeTypeToTypes
                .Select(kv =>
                {
                    var chosen = ChooseDeterministically(kv.Value);
                    var kind = GetImplementationKind(kv.Key, chosen);
                    return new
                    {
                        NodeType = kv.Key,
                        Kind = kind,
                        Dup = kv.Value.Count > 1,
                        Chosen = chosen?.FullName ?? ""
                    };
                })
                .Where(x => x.Kind == "NONE")
                .OrderBy(x => x.NodeType, StringComparer.OrdinalIgnoreCase)
                .Take(100)
                .ToArray();

            foreach (var x in topStub)
                md.AppendLine($"- `{x.NodeType}`  →  {x.Chosen}{(x.Dup ? "  (DUP)" : "")}");
            md.AppendLine();

            File.WriteAllText(mdPath, md.ToString(), new UTF8Encoding(false));
            AssetDatabase.Refresh();

            Debug.Log($"[MayaImporter] ✅ Reconstruction coverage written:\n- {mdPath}\n- {csvPath}");
            EditorUtility.DisplayDialog("MayaImporter", $"Reconstruction coverage を出力しました。\n\n{mdPath}\n{csvPath}", "OK");
        }

        // --------------------- scene ---------------------

        private sealed class SceneInfo
        {
            public int SceneNodeCount;
            public int SceneUnknownCount;
            public HashSet<string> SceneUniqueNodeTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public List<string> SceneMissingMapping = new List<string>();
            public List<string> SceneStubOnly = new List<string>();
        }

        private static SceneInfo BuildSceneInfo(Index index)
        {
            var info = new SceneInfo();

            var nodes = UnityEngine.Object.FindObjectsByType<MayaNodeComponentBase>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

            info.SceneNodeCount = nodes.Length;

            var mapped = new HashSet<string>(index.ValidNodeTypeToTypes.Keys, StringComparer.OrdinalIgnoreCase);

            var chosenByNodeType = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in index.ValidNodeTypeToTypes)
                chosenByNodeType[kv.Key] = ChooseDeterministically(kv.Value);

            for (int i = 0; i < nodes.Length; i++)
            {
                var n = nodes[i];
                if (n == null) continue;
                if (!n.gameObject.scene.IsValid()) continue;

                if (n is MayaUnknownNodeComponent || n is MayaPlaceholderNode)
                    info.SceneUnknownCount++;

                var nt = n.NodeType;
                if (string.IsNullOrEmpty(nt)) continue;

                info.SceneUniqueNodeTypes.Add(nt);
            }

            info.SceneMissingMapping = info.SceneUniqueNodeTypes
                .Where(nt => !mapped.Contains(nt))
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                .ToList();

            info.SceneStubOnly = info.SceneUniqueNodeTypes
                .Where(nt => mapped.Contains(nt))
                .Where(nt =>
                {
                    if (!chosenByNodeType.TryGetValue(nt, out var t) || t == null) return true;
                    return GetImplementationKind(nt, t) == "NONE";
                })
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return info;
        }

        // --------------------- index ---------------------

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
                    catch { return Enumerable.Empty<Type>(); }
                })
                .Where(t => t != null && !t.IsAbstract)
                .ToArray();

            foreach (var t in allTypes)
            {
                var attrs = t.GetCustomAttributes(typeof(MayaNodeTypeAttribute), false);
                bool hasAttr = attrs != null && attrs.Length > 0;

                bool derives = baseType.IsAssignableFrom(t);

                if (hasAttr && !derives)
                {
                    idx.InvalidAttributeUsers.Add(t);
                    continue;
                }

                if (derives && !hasAttr)
                {
                    if (!NoNodeTypeRequired_FullNames.Contains(t.FullName))
                        idx.DerivedButNoAttribute.Add(t);
                    continue;
                }

                if (derives && hasAttr)
                {
                    for (int i = 0; i < attrs.Length; i++)
                    {
                        var a = (MayaNodeTypeAttribute)attrs[i];
                        if (a == null || string.IsNullOrEmpty(a.NodeType)) continue;

                        if (!idx.ValidNodeTypeToTypes.TryGetValue(a.NodeType, out var list))
                        {
                            list = new List<Type>(1);
                            idx.ValidNodeTypeToTypes[a.NodeType] = list;
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

        // --------------------- helpers ---------------------

        private static Type ChooseDeterministically(List<Type> types)
        {
            if (types == null || types.Count == 0) return null;
            return types.OrderBy(t => t.FullName, StringComparer.Ordinal).First();
        }

        private static string GetImplementationKind(string nodeType, Type chosenType)
        {
            if (OverridesApplyToUnity(chosenType))
                return "ApplyToUnity";

            if (MayaAnimValueGraph.IsSupportedComputeNodeType(nodeType))
                return "GraphCompute";

            return "NONE";
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
