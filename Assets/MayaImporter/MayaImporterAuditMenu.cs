// MayaImporter/MayaImporterAuditMenu.cs
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

namespace MayaImporter.Editor
{
    /// <summary>
    /// Phase1-2: “100点の証拠”を自動生成するためのワンクリック監査メニュー。
    /// Phase1追加:
    /// - Selected Maya Asset を Reimport する導線（正規Importerへ寄せる）
    /// </summary>
    public static class MayaImporterAuditMenu
    {
        private const string RootMenu = "Tools/Maya Importer/Audit/";
        private const string RootFolder = "Assets/MayaImporter";
        private const string ReportsDir = "Assets/MayaImporter/Reports";
        private const string TodoMarker = "// TODO: implement";

        private static readonly HashSet<string> NoNodeTypeRequired_FullNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "MayaImporter.Core.MayaPlaceholderNode",
            "MayaImporter.Core.MayaUnknownNodeComponent",
            "MayaImporter.Shader.UnknownShaderNodeComponent",
        };

        // -------------------- Phase1: Import path helpers --------------------

        [MenuItem(RootMenu + "Reimport Selected Maya Assets (.ma/.mb)")]
        public static void ReimportSelectedMayaAssets()
        {
            var paths = GetSelectedMayaAssetPaths();
            if (paths.Count == 0)
            {
                EditorUtility.DisplayDialog("MayaImporter", "Projectウィンドウで .ma/.mb を選択してください。", "OK");
                return;
            }

            for (int i = 0; i < paths.Count; i++)
                AssetDatabase.ImportAsset(paths[i], ImportAssetOptions.ForceUpdate);

            Debug.Log($"[MayaImporter] Reimport requested: {paths.Count} asset(s).");
        }

        [MenuItem(RootMenu + "Reimport ALL Maya Assets in Project (.ma/.mb)")]
        public static void ReimportAllMayaAssetsInProject()
        {
            var paths = FindAllMayaAssetPathsInProject();
            if (paths.Count == 0)
            {
                EditorUtility.DisplayDialog("MayaImporter", "Project内に .ma/.mb が見つかりませんでした。", "OK");
                return;
            }

            for (int i = 0; i < paths.Count; i++)
                AssetDatabase.ImportAsset(paths[i], ImportAssetOptions.ForceUpdate);

            Debug.Log($"[MayaImporter] Reimport requested: {paths.Count} asset(s).");
        }

        private static List<string> GetSelectedMayaAssetPaths()
        {
            var list = new List<string>(16);
            var objs = Selection.objects;
            if (objs == null || objs.Length == 0) return list;

            for (int i = 0; i < objs.Length; i++)
            {
                var p = AssetDatabase.GetAssetPath(objs[i]);
                if (string.IsNullOrEmpty(p)) continue;

                p = p.Replace('\\', '/');
                if (!p.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)) continue;

                var ext = Path.GetExtension(p).ToLowerInvariant();
                if (ext != ".ma" && ext != ".mb") continue;

                if (!list.Contains(p)) list.Add(p);
            }

            return list;
        }

        private static List<string> FindAllMayaAssetPathsInProject()
        {
            var list = new List<string>(256);
            var guids = AssetDatabase.FindAssets("", new[] { "Assets" });

            for (int i = 0; i < guids.Length; i++)
            {
                var p = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(p)) continue;

                p = p.Replace('\\', '/');
                var ext = Path.GetExtension(p).ToLowerInvariant();
                if (ext != ".ma" && ext != ".mb") continue;

                list.Add(p);
            }

            list.Sort(StringComparer.OrdinalIgnoreCase);
            return list;
        }

        // -------------------- Existing audit features --------------------

        [MenuItem(RootMenu + "Run ALL (Audit + Coverage + TODO + Scene Missing + Report)")]
        public static void RunAll()
        {
            try { MayaNodeAudit.RunAudit(); } catch (Exception e) { Debug.LogError(e); }
            try { MayaNodeCoverageTools.ReportStandardImplementation(); } catch (Exception e) { Debug.LogError(e); }
            try { FindTodoOnlyScripts_Internal(logToConsole: true); } catch (Exception e) { Debug.LogError(e); }

            try
            {
                Directory.CreateDirectory(ReportsDir);

                var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var mdPath = $"{ReportsDir}/Audit__{stamp}.md";
                var jsonPath = $"{ReportsDir}/Audit__{stamp}.json";

                var report = BuildReport();

                File.WriteAllText(mdPath, report.Markdown, new UTF8Encoding(false));
                File.WriteAllText(jsonPath, report.Json, new UTF8Encoding(false));

                AssetDatabase.Refresh();

                Debug.Log($"[MayaImporter] ✅ Wrote audit report:\n- {mdPath}\n- {jsonPath}");
                EditorUtility.DisplayDialog("MayaImporter", $"監査レポートを書き出しました。\n\n{mdPath}\n{jsonPath}", "OK");
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                EditorUtility.DisplayDialog("MayaImporter", "監査レポート生成中に例外が発生しました。Console を確認してください。", "OK");
            }
        }

        [MenuItem(RootMenu + "Write Report Only (Markdown + JSON)")]
        public static void WriteReportOnly()
        {
            Directory.CreateDirectory(ReportsDir);

            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var mdPath = $"{ReportsDir}/Audit__{stamp}.md";
            var jsonPath = $"{ReportsDir}/Audit__{stamp}.json";

            var report = BuildReport();

            File.WriteAllText(mdPath, report.Markdown, new UTF8Encoding(false));
            File.WriteAllText(jsonPath, report.Json, new UTF8Encoding(false));
            AssetDatabase.Refresh();

            Debug.Log($"[MayaImporter] ✅ Wrote audit report:\n- {mdPath}\n- {jsonPath}");
        }

        [MenuItem(RootMenu + "Find TODO-only scripts")]
        public static void FindTodoOnlyScripts()
        {
            FindTodoOnlyScripts_Internal(logToConsole: true);
        }

        [MenuItem(RootMenu + "Open Reports Folder")]
        public static void OpenReportsFolder()
        {
            Directory.CreateDirectory(ReportsDir);
            var abs = Path.GetFullPath(ReportsDir);
            EditorUtility.RevealInFinder(abs);
        }

        // -------------------- report core --------------------

        private struct ReportBundle
        {
            public string Markdown;
            public string Json;
        }

        private static ReportBundle BuildReport()
        {
            var index = BuildIndex();

            bool hasStd = MayaStandardNodeTypes.TryGet(out var std) && std != null && std.Count > 0;
            var stdMissing = new List<string>();
            var stdExtra = new List<string>();

            if (hasStd)
            {
                var present = new HashSet<string>(index.ValidNodeTypeToTypes.Keys, StringComparer.OrdinalIgnoreCase);
                stdMissing = std.Where(s => !present.Contains(s))
                                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                                .ToList();

                stdExtra = index.ValidNodeTypeToTypes.Keys
                                .Where(k => !std.Contains(k))
                                .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
                                .ToList();
            }

            var sceneInfo = BuildSceneInfo(index);
            var todoHits = FindTodoOnlyScripts_Internal(logToConsole: false);

            var md = new StringBuilder();
            md.AppendLine("# MayaImporter Audit Report");
            md.AppendLine();
            md.AppendLine($"- Timestamp: `{DateTime.Now:yyyy-MM-dd HH:mm:ss}`");
            md.AppendLine($"- Unity: `{Application.unityVersion}`");
            md.AppendLine($"- Project Root: `{RootFolder}`");
            md.AppendLine();

            md.AppendLine("## 1. Project-wide NodeType Index");
            md.AppendLine();
            md.AppendLine($"- Valid nodeType mappings: **{index.ValidNodeTypeToTypes.Count}**");
            md.AppendLine($"- Duplicate nodeType mappings: **{index.DuplicateNodeTypes.Count}**");
            md.AppendLine($"- Invalid [MayaNodeType] users (not derived): **{index.InvalidAttributeUsers.Count}**");
            md.AppendLine($"- Derived but missing [MayaNodeType] (excluding allowed): **{index.DerivedButNoAttribute.Count}**");
            md.AppendLine();

            if (index.DuplicateNodeTypes.Count > 0)
            {
                md.AppendLine("### Duplicates");
                foreach (var kv in index.DuplicateNodeTypes.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
                {
                    md.AppendLine($"- `{kv.Key}`");
                    foreach (var t in kv.Value.OrderBy(t => t.FullName, StringComparer.Ordinal))
                        md.AppendLine($"  - {t.FullName}");
                }
                md.AppendLine();
            }

            if (index.DerivedButNoAttribute.Count > 0)
            {
                md.AppendLine("### Derived but No Attribute (excluding allowed fallbacks)");
                foreach (var t in index.DerivedButNoAttribute.OrderBy(t => t.FullName, StringComparer.Ordinal))
                    md.AppendLine($"- {t.FullName}");
                md.AppendLine();
            }

            if (index.InvalidAttributeUsers.Count > 0)
            {
                md.AppendLine("### Invalid Attribute Users (has [MayaNodeType] but not derived)");
                foreach (var t in index.InvalidAttributeUsers.OrderBy(t => t.FullName, StringComparer.Ordinal))
                    md.AppendLine($"- {t.FullName}");
                md.AppendLine();
            }

            md.AppendLine("## 2. Standard NodeType Coverage (Maya2026_StandardNodeTypes.txt)");
            md.AppendLine();
            if (!hasStd)
            {
                md.AppendLine("- **Standard list not found** (Resources/Maya2026_StandardNodeTypes.txt)");
                md.AppendLine();
            }
            else
            {
                md.AppendLine($"- Standard nodeTypes: **{std.Count}**");
                md.AppendLine($"- Missing implementations: **{stdMissing.Count}**");
                md.AppendLine($"- Implemented but not in standard: **{stdExtra.Count}**");
                md.AppendLine();

                if (stdMissing.Count > 0)
                {
                    md.AppendLine("### Missing (Standard)");
                    foreach (var s in stdMissing)
                        md.AppendLine($"- `{s}`");
                    md.AppendLine();
                }
            }

            md.AppendLine("## 3. Current Scene Summary");
            md.AppendLine();
            md.AppendLine($"- MayaNodeComponentBase in scene: **{sceneInfo.SceneNodeCount}**");
            md.AppendLine($"- Unique nodeTypes in scene: **{sceneInfo.SceneUniqueNodeTypes.Count}**");
            md.AppendLine($"- Unknown/Placeholder components: **{sceneInfo.SceneUnknownCount}**");
            md.AppendLine($"- Scene nodeTypes missing implementation: **{sceneInfo.SceneMissingImpl.Count}**");
            md.AppendLine();

            if (sceneInfo.SceneMissingImpl.Count > 0)
            {
                md.AppendLine("### Missing Implementation (Appeared in Scene)");
                foreach (var s in sceneInfo.SceneMissingImpl)
                    md.AppendLine($"- `{s}`");
                md.AppendLine();
                md.AppendLine("> Fix: Tools → Maya Importer → Coverage → Generate Missing Node Stubs (from CURRENT Scene)");
                md.AppendLine();
            }

            md.AppendLine("## 4. TODO-only scripts");
            md.AppendLine();
            md.AppendLine($"- Hits: **{todoHits.Length}**");
            if (todoHits.Length > 0)
            {
                foreach (var p in todoHits.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                    md.AppendLine($"- `{p.Replace('\\', '/')}`");
            }
            md.AppendLine();

            var json = new StringBuilder();
            json.Append("{");
            json.Append($"\"timestamp\":\"{EscapeJson(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))}\",");
            json.Append($"\"unity\":\"{EscapeJson(Application.unityVersion)}\",");
            json.Append("\"projectIndex\":{");
            json.Append($"\"validMappings\":{index.ValidNodeTypeToTypes.Count},");
            json.Append($"\"duplicateMappings\":{index.DuplicateNodeTypes.Count},");
            json.Append($"\"invalidAttrUsers\":{index.InvalidAttributeUsers.Count},");
            json.Append($"\"derivedButNoAttr\":{index.DerivedButNoAttribute.Count}");
            json.Append("},");

            json.Append("\"standard\":{");
            json.Append($"\"hasStandardList\":{(hasStd ? "true" : "false")},");
            if (hasStd)
            {
                json.Append($"\"standardCount\":{std.Count},");
                json.Append("\"missing\":[");
                json.Append(string.Join(",", stdMissing.Select(s => $"\"{EscapeJson(s)}\"")));
                json.Append("],");
                json.Append("\"extra\":[");
                json.Append(string.Join(",", stdExtra.Select(s => $"\"{EscapeJson(s)}\"")));
                json.Append("]");
            }
            else
            {
                json.Append("\"standardCount\":0,\"missing\":[],\"extra\":[]");
            }
            json.Append("},");

            json.Append("\"scene\":{");
            json.Append($"\"nodeCount\":{sceneInfo.SceneNodeCount},");
            json.Append($"\"uniqueNodeTypes\":{sceneInfo.SceneUniqueNodeTypes.Count},");
            json.Append($"\"unknownCount\":{sceneInfo.SceneUnknownCount},");
            json.Append("\"missingImpl\":[");
            json.Append(string.Join(",", sceneInfo.SceneMissingImpl.Select(s => $"\"{EscapeJson(s)}\"")));
            json.Append("]");
            json.Append("},");

            json.Append("\"todoOnlyScripts\":[");
            json.Append(string.Join(",", todoHits.Select(s => $"\"{EscapeJson(s.Replace('\\', '/'))}\"")));
            json.Append("]");

            json.Append("}");

            return new ReportBundle { Markdown = md.ToString(), Json = json.ToString() };
        }

        private sealed class SceneInfo
        {
            public int SceneNodeCount;
            public int SceneUnknownCount;
            public HashSet<string> SceneUniqueNodeTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public List<string> SceneMissingImpl = new List<string>();
        }

        private static SceneInfo BuildSceneInfo(Index index)
        {
            var info = new SceneInfo();

            var nodes = UnityEngine.Object.FindObjectsByType<MayaNodeComponentBase>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

            info.SceneNodeCount = nodes.Length;

            var implemented = new HashSet<string>(index.ValidNodeTypeToTypes.Keys, StringComparer.OrdinalIgnoreCase);

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

            info.SceneMissingImpl = info.SceneUniqueNodeTypes
                .Where(nt => !implemented.Contains(nt))
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return info;
        }

        // -------------------- TODO scan --------------------

        private static string[] FindTodoOnlyScripts_Internal(bool logToConsole)
        {
            if (!Directory.Exists(RootFolder))
            {
                if (logToConsole)
                    Debug.LogWarning($"[MayaImporter Audit] Root folder not found: {RootFolder}");
                return Array.Empty<string>();
            }

            var csFiles = Directory.GetFiles(RootFolder, "*.cs", SearchOption.AllDirectories);

            var hits = csFiles.Where(path =>
            {
                try
                {
                    var text = File.ReadAllText(path);
                    return text.Contains(TodoMarker, StringComparison.Ordinal);
                }
                catch { return false; }
            }).ToArray();

            if (logToConsole)
            {
                Debug.Log($"[MayaImporter Audit] TODO marker found in {hits.Length} file(s).");
                foreach (var path in hits)
                {
                    var unityPath = path.Replace('\\', '/');
                    Debug.Log($"[TODO] {unityPath}");
                }

                if (hits.Length == 0)
                    Debug.Log("[MayaImporter Audit] ✅ No TODO-only scripts remain.");
            }

            return hits;
        }

        // -------------------- type index --------------------

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
                        list.Add(t);
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

        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
        }
    }
}
#endif
