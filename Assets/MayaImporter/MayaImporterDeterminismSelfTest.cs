// MayaImporter/MayaImporterDeterminismSelfTest.cs
#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Editor
{
    /// <summary>
    /// Phase-1 Proof Tool:
    /// - 推奨: Project内の .ma/.mb（Assets/..）を対象にする
    /// - Import the same .ma/.mb twice (direct parse+build; importer本体はPhase1で追加)
    /// - Compute a stable hash of the reconstructed Unity hierarchy + Maya raw data
    /// - Compare hashes to prove determinism
    /// - Write Markdown report to Assets/MayaImporter/Reports
    /// </summary>
    public static class MayaImporterDeterminismSelfTest
    {
        private const string MenuRoot = "Tools/Maya Importer/Audit/";
        private const string ReportsDir = "Assets/MayaImporter/Reports";

        [MenuItem(MenuRoot + "Run Determinism Self-Test (Selected Maya Asset)")]
        public static void RunSelected()
        {
            var assetPath = TryGetSelectedMayaAssetPath();
            if (string.IsNullOrEmpty(assetPath))
            {
                EditorUtility.DisplayDialog("MayaImporter", "Projectウィンドウで .ma/.mb を選択してください。", "OK");
                return;
            }

            if (!MayaImporter.Core.MayaImporter.TryGetAbsolutePathFromAssetPath(assetPath, out var abs))
            {
                EditorUtility.DisplayDialog("MayaImporter", "選択アセットの実ファイルが見つかりません:\n" + assetPath, "OK");
                return;
            }

            RunInternal(abs, assetPath);
        }

        [MenuItem(MenuRoot + "Run Determinism Self-Test (Pick External File...)")]
        public static void RunPickExternal()
        {
            var path = EditorUtility.OpenFilePanel("Select Maya File for Determinism Test", "", "ma,mb");
            if (string.IsNullOrEmpty(path))
                return;

            if (!File.Exists(path))
            {
                Debug.LogError("[MayaImporter] File does not exist: " + path);
                return;
            }

            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext != ".ma" && ext != ".mb")
            {
                Debug.LogError("[MayaImporter] Unsupported extension: " + ext);
                return;
            }

            RunInternal(path, assetPathHint: "");
        }

        private static void RunInternal(string absolutePath, string assetPathHint)
        {
            Directory.CreateDirectory(ReportsDir);

            // IMPORTANT: This project only defines CoordinateConversion.MayaToUnity_MirrorZ (and None)
            var options = new MayaImportOptions
            {
                KeepRawStatements = true,
                CreateUnityComponents = true,
                Conversion = CoordinateConversion.MayaToUnity_MirrorZ
            };

            var run1 = DoOneRun(absolutePath, options, runIndex: 1);
            var run2 = DoOneRun(absolutePath, options, runIndex: 2);

            bool same = string.Equals(run1.Hash, run2.Hash, StringComparison.Ordinal);
            var verdict = same ? "✅ PASS (Deterministic)" : "❌ FAIL (Non-deterministic)";

            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            var mdPath = $"{ReportsDir}/DeterminismSelfTest__{stamp}.md";

            var md = new StringBuilder();
            md.AppendLine("# MayaImporter Determinism Self-Test");
            md.AppendLine();
            md.AppendLine($"- Timestamp: `{DateTime.Now:yyyy-MM-dd HH:mm:ss}`");
            md.AppendLine($"- Unity: `{Application.unityVersion}`");
            if (!string.IsNullOrEmpty(assetPathHint))
                md.AppendLine($"- Asset: `{assetPathHint}`");
            md.AppendLine($"- File: `{absolutePath}`");
            md.AppendLine($"- Verdict: **{verdict}**");
            md.AppendLine();

            md.AppendLine("## Options");
            md.AppendLine();
            md.AppendLine($"- KeepRawStatements: `{options.KeepRawStatements}`");
            md.AppendLine($"- CreateUnityComponents: `{options.CreateUnityComponents}`");
            md.AppendLine($"- Conversion: `{options.Conversion}`");
            md.AppendLine();

            md.AppendLine("## Run 1 Summary");
            md.AppendLine();
            AppendRunSummary(md, run1);

            md.AppendLine("## Run 2 Summary");
            md.AppendLine();
            AppendRunSummary(md, run2);

            md.AppendLine("## Comparison");
            md.AppendLine();
            md.AppendLine($"- Hash1: `{run1.Hash}`");
            md.AppendLine($"- Hash2: `{run2.Hash}`");
            md.AppendLine($"- Equal: **{same}**");
            md.AppendLine();

            if (!same)
            {
                md.AppendLine("## First Differences (Top 50 Lines)");
                md.AppendLine();
                AppendFirstDiff(md, run1.DebugLines, run2.DebugLines, maxLines: 50);
                md.AppendLine();
                md.AppendLine("### NOTE");
                md.AppendLine("- If FAIL happens, it indicates ordering/initialization dependence.");
                md.AppendLine("- Typical causes: unsorted enumeration, non-deterministic Apply order, unstable name resolution.");
                md.AppendLine();
            }

            File.WriteAllText(mdPath, md.ToString(), new UTF8Encoding(false));
            AssetDatabase.Refresh();

            Debug.Log($"[MayaImporter] Determinism Self-Test: {verdict}\nReport: {mdPath}");

            EditorUtility.DisplayDialog(
                "MayaImporter Determinism Self-Test",
                same ? "PASS: 2回の結果が完全一致しました。\nReports を確認できます。" : "FAIL: 結果が一致しませんでした。\nReports を確認してください。",
                "OK");
        }

        private static string TryGetSelectedMayaAssetPath()
        {
            var obj = Selection.activeObject;
            if (obj == null) return "";

            var path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path)) return "";

            path = path.Replace('\\', '/');
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext != ".ma" && ext != ".mb") return "";

            if (!path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)) return "";

            return path;
        }

        private struct RunResult
        {
            public string Hash;
            public int NodeCount;
            public int UnknownCount;
            public int TotalAttributes;
            public int TotalConnections;
            public int SceneNodeRecords;
            public int SceneConnections;
            public bool LogHasErrors;
            public List<string> DebugLines;
        }

        private static RunResult DoOneRun(string path, MayaImportOptions options, int runIndex)
        {
            MayaSceneData scene = null;
            MayaImportLog log = null;

            GameObject root = null;

            try
            {
                root = MayaImporter.Core.MayaImporter.ImportIntoScene(path, options, out scene, out log);
                if (root == null)
                    throw new Exception("ImportIntoScene returned null root.");

                root.name = $"__MayaImport_TestRun{runIndex}__";

                var debugLines = BuildStableLines(root);
                var hash = Sha256Hex(string.Join("\n", debugLines));

                int unknown = 0;
                int totalAttrs = 0;
                int totalConns = 0;

                var nodes = root.GetComponentsInChildren<MayaNodeComponentBase>(true);
                for (int i = 0; i < nodes.Length; i++)
                {
                    var n = nodes[i];
                    if (n is MayaUnknownNodeComponent) unknown++;
                    totalAttrs += (n.Attributes != null) ? n.Attributes.Count : 0;
                    totalConns += (n.Connections != null) ? n.Connections.Count : 0;
                }

                return new RunResult
                {
                    Hash = hash,
                    NodeCount = nodes.Length,
                    UnknownCount = unknown,
                    TotalAttributes = totalAttrs,
                    TotalConnections = totalConns,
                    SceneNodeRecords = scene?.Nodes?.Count ?? 0,
                    SceneConnections = scene?.Connections?.Count ?? 0,
                    LogHasErrors = (log != null) && log.HasErrors,
                    DebugLines = debugLines
                };
            }
            catch (Exception e)
            {
                Debug.LogError($"[MayaImporter] Determinism run {runIndex} failed: {e.GetType().Name}: {e.Message}");
                Debug.LogException(e);

                return new RunResult
                {
                    Hash = "ERROR",
                    NodeCount = 0,
                    UnknownCount = 0,
                    TotalAttributes = 0,
                    TotalConnections = 0,
                    SceneNodeRecords = scene?.Nodes?.Count ?? 0,
                    SceneConnections = scene?.Connections?.Count ?? 0,
                    LogHasErrors = true,
                    DebugLines = new List<string> { "ERROR" }
                };
            }
            finally
            {
                if (root != null)
                    UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void AppendRunSummary(StringBuilder md, RunResult r)
        {
            md.AppendLine($"- Hash: `{r.Hash}`");
            md.AppendLine($"- MayaSceneData.Nodes: **{r.SceneNodeRecords}**");
            md.AppendLine($"- MayaSceneData.Connections: **{r.SceneConnections}**");
            md.AppendLine($"- Unity MayaNodeComponentBase count: **{r.NodeCount}**");
            md.AppendLine($"- Unknown components: **{r.UnknownCount}**");
            md.AppendLine($"- Total raw Attributes: **{r.TotalAttributes}**");
            md.AppendLine($"- Total related Connections: **{r.TotalConnections}**");
            md.AppendLine($"- Import log has errors: **{r.LogHasErrors}**");
            md.AppendLine();
        }

        private static void AppendFirstDiff(StringBuilder md, List<string> a, List<string> b, int maxLines)
        {
            int n = Math.Min(a.Count, b.Count);
            int first = -1;
            for (int i = 0; i < n; i++)
            {
                if (!string.Equals(a[i], b[i], StringComparison.Ordinal))
                {
                    first = i;
                    break;
                }
            }

            if (first < 0 && a.Count == b.Count)
            {
                md.AppendLine("- No diff found in line comparison (but hash differs).");
                return;
            }

            md.AppendLine($"- FirstDiffIndex: **{first}**");
            md.AppendLine();
            md.AppendLine("```");
            int start = Math.Max(0, first - maxLines / 2);
            int end = Math.Min(Math.Max(a.Count, b.Count), start + maxLines);

            for (int i = start; i < end; i++)
            {
                var la = (i < a.Count) ? a[i] : "<EOF>";
                var lb = (i < b.Count) ? b[i] : "<EOF>";
                string marker = (i == first) ? "<<<" : "   ";
                md.AppendLine($"{marker} [{i:D6}] A: {la}");
                md.AppendLine($"{marker} [{i:D6}] B: {lb}");
                if (i == first) md.AppendLine();
            }
            md.AppendLine("```");
        }

        private static List<string> BuildStableLines(GameObject root)
        {
            var list = new List<string>(4096);

            var nodes = root.GetComponentsInChildren<MayaNodeComponentBase>(true)
                .OrderBy(n => GetTransformPath(n.transform, root.transform), StringComparer.Ordinal)
                .ThenBy(n => n.NodeType ?? "", StringComparer.OrdinalIgnoreCase)
                .ThenBy(n => n.NodeName ?? "", StringComparer.Ordinal)
                .ToArray();

            for (int i = 0; i < nodes.Length; i++)
            {
                var n = nodes[i];
                var path = GetTransformPath(n.transform, root.transform);

                var lp = n.transform.localPosition;
                var lr = n.transform.localRotation;
                var ls = n.transform.localScale;

                var attrs = (n.Attributes ?? new List<MayaNodeComponentBase.SerializedAttribute>())
                    .OrderBy(a => a.Key ?? "", StringComparer.Ordinal)
                    .Select(a =>
                    {
                        var key = a.Key ?? "";
                        var type = a.TypeName ?? "";
                        var tokens = (a.Tokens != null && a.Tokens.Count > 0)
                            ? string.Join(" ", a.Tokens.Select(t => t ?? ""))
                            : "";
                        return $"{key}|{type}|{tokens}";
                    })
                    .ToArray();

                var conns = (n.Connections ?? new List<MayaNodeComponentBase.SerializedConnection>())
                    .OrderBy(c => c.SrcPlug ?? "", StringComparer.Ordinal)
                    .ThenBy(c => c.DstPlug ?? "", StringComparer.Ordinal)
                    .Select(c =>
                    {
                        var src = c.SrcPlug ?? "";
                        var dst = c.DstPlug ?? "";
                        var force = c.Force ? "1" : "0";
                        var role = ((int)c.RoleForThisNode).ToString(CultureInfo.InvariantCulture);
                        return $"{src}->{dst}|F{force}|R{role}";
                    })
                    .ToArray();

                list.Add(
                    "NODE|" +
                    $"P={path}|" +
                    $"Name={n.NodeName ?? ""}|" +
                    $"Type={n.NodeType ?? ""}|" +
                    $"Parent={n.ParentName ?? ""}|" +
                    $"Uuid={n.Uuid ?? ""}|" +
                    $"LP={F(lp.x)},{F(lp.y)},{F(lp.z)}|" +
                    $"LR={F(lr.x)},{F(lr.y)},{F(lr.z)},{F(lr.w)}|" +
                    $"LS={F(ls.x)},{F(ls.y)},{F(ls.z)}|" +
                    $"AttrCount={attrs.Length}|" +
                    $"ConnCount={conns.Length}"
                );

                for (int ai = 0; ai < attrs.Length; ai++)
                    list.Add("A|" + attrs[ai]);

                for (int ci = 0; ci < conns.Length; ci++)
                    list.Add("C|" + conns[ci]);
            }

            return list;
        }

        private static string GetTransformPath(Transform t, Transform root)
        {
            if (t == null) return "";
            if (t == root) return "";

            var stack = new Stack<string>(64);
            var cur = t;
            while (cur != null && cur != root)
            {
                stack.Push(cur.name ?? "");
                cur = cur.parent;
            }

            return string.Join("/", stack.ToArray());
        }

        private static string Sha256Hex(string s)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(s ?? "");
                var hash = sha.ComputeHash(bytes);
                var sb = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                    sb.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
                return sb.ToString();
            }
        }

        private static string F(float v)
            => v.ToString("R", CultureInfo.InvariantCulture);
    }
}
#endif
