// MAYAIMPORTER_PATCH_V4: mb provenance/evidence + audit determinism (generated 2026-01-05)
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using MayaImporter.Runtime;

namespace MayaImporter.Core
{
    /// <summary>
    /// UnitySceneBuilderV2:
    /// - MayaTimeEvaluationPlayer を直参照せず reflection で付与（存在する場合のみ）
    /// - 追加: MayaReconstructionSelectionContext により「再構築する/しない」を反映
    /// </summary>
    public sealed class UnitySceneBuilderV2
    {
        private readonly MayaImportOptions _options;
        private readonly MayaImportLog _log;

        public UnitySceneBuilderV2(MayaImportOptions options, MayaImportLog log)
        {
            _options = options ?? new MayaImportOptions();
            _log = log ?? new MayaImportLog();
        }

        public GameObject Build(MayaSceneData scene)
        {
            var root = new GameObject(
                string.IsNullOrEmpty(scene?.SourcePath)
                    ? "MayaScene"
                    : System.IO.Path.GetFileNameWithoutExtension(scene.SourcePath));

            var sel = MayaReconstructionSelectionContext.Current;
            bool useSel = sel != null && sel.Enabled;

            if (scene == null || scene.Nodes == null || scene.Nodes.Count == 0)
            {
                AttachRootInfo(root, scene, _log);
                return root;
            }

            // 選別で「階層再構築しない」場合：rootのみ（Raw/SceneDataは別途保持される）
            if (useSel && !sel.ReconstructGameObjects)
            {
                AttachRootInfo(root, scene, _log);
                return root;
            }

            bool ShouldReconstruct(NodeRecord rec)
            {
                if (rec == null) return false;
                if (!useSel) return true;

                var nt = rec.NodeType ?? "";

                // ノードタイプ除外
                if (sel.ExcludeByNodeTypeEnabled && sel.IsNodeTypeExcluded(nt))
                    return false;

                // ノード名除外（自分自身）
                if (sel.ExcludeByNameEnabled && sel.IsNodeNameExcluded(rec.Name))
                    return false;

                // 親が除外なら子孫も除外（推奨）
                if (sel.ExcludeByNameEnabled && sel.TreatExcludedAsSubtree)
                {
                    string p = rec.ParentName;
                    int guard = 0;
                    while (!string.IsNullOrEmpty(p) && guard++ < 256)
                    {
                        if (sel.IsNodeNameExcluded(p))
                            return false;

                        if (!scene.Nodes.TryGetValue(p, out var pr) || pr == null)
                            break;

                        p = pr.ParentName;
                    }
                }

                // カテゴリトグル（粗いが「持ってくる/持ってこない」として実用）
                if (!sel.ImportAnimationClip)
                {
                    if (nt.StartsWith("animCurve", StringComparison.OrdinalIgnoreCase)) return false;
                    if (nt.IndexOf("anim", StringComparison.OrdinalIgnoreCase) >= 0) return false;
                }

                if (!sel.ImportMaterials && !sel.ImportTextures)
                {
                    if (Eq(nt, "shadingEngine")) return false;
                    if (LooksLikeShaderOrTextureNode(nt)) return false;
                }

                if (!sel.ImportMeshes)
                {
                    if (Eq(nt, "mesh")) return false;
                    if (nt.IndexOf("mesh", StringComparison.OrdinalIgnoreCase) >= 0) return false;
                    if (nt.IndexOf("nurbs", StringComparison.OrdinalIgnoreCase) >= 0) return false;
                    if (nt.IndexOf("curve", StringComparison.OrdinalIgnoreCase) >= 0) return false;
                    if (nt.IndexOf("surface", StringComparison.OrdinalIgnoreCase) >= 0) return false;
                    if (nt.IndexOf("poly", StringComparison.OrdinalIgnoreCase) >= 0) return false;
                    if (nt.IndexOf("subdiv", StringComparison.OrdinalIgnoreCase) >= 0) return false;
                }

                return true;
            }

            // --- deterministic ordering ---
            var allRecords = scene.Nodes.Values
                .Where(r => r != null && !string.IsNullOrEmpty(r.Name))
                .OrderBy(r => r.Name, StringComparer.Ordinal)
                .ToList();

            // ここで選別を反映
            var records = new List<NodeRecord>(allRecords.Count);
            for (int i = 0; i < allRecords.Count; i++)
            {
                var r = allRecords[i];
                if (ShouldReconstruct(r))
                    records.Add(r);
            }

            var goByName = new Dictionary<string, GameObject>(StringComparer.Ordinal);
            var comps = new List<MayaNodeComponentBase>(records.Count);

            // Hidden graph container for non-object Maya DG nodes (utility/shading/anim/etc)
            var graphRoot = new GameObject("_MayaGraph");
            graphRoot.transform.SetParent(root.transform, false);
            graphRoot.hideFlags = HideFlags.HideInHierarchy | HideFlags.NotEditable;

            // 1) Create GameObjects + Components
            for (int i = 0; i < records.Count; i++)
            {
                var rec = records[i];
                if (rec == null) continue;

                bool visibleObject = NodeFactory.ShouldCreateVisibleGameObject(rec.NodeType);

                var go = new GameObject(GetLeaf(rec.Name));
                if (!visibleObject)
                {
                    // Keep DG nodes out of the user's hierarchy while still representing them as components.
                    go.hideFlags = HideFlags.HideInHierarchy | HideFlags.NotEditable;
                    go.transform.SetParent(graphRoot.transform, false);
                }

                goByName[rec.Name] = go;

                var comp = NodeFactory.CreateComponent(go, rec.NodeType);
                if (comp == null) comp = go.AddComponent<MayaUnknownNodeComponent>();

                // NOTE: We still inject the full connection list here for correctness.
                // (Performance is addressed by connection indexing in other patches.)
                comp.InitializeFromRecord(rec, scene.Connections);
                comps.Add(comp);
            }

            // Register mapping for downstream phases/tools
            MayaBuildContext.RegisterUnityObjects(root, graphRoot, goByName);

            // 2) Parenting (only for visible objects; hidden DG nodes stay under _MayaGraph)
// 2) Parenting（親が除外されていた場合は、辿れる範囲で最も近い再構築親へ）
            for (int i = 0; i < records.Count; i++)
            {
                var rec = records[i];
                if (rec == null) continue;
                if (!NodeFactory.ShouldCreateVisibleGameObject(rec.NodeType)) continue;
                if (!goByName.TryGetValue(rec.Name, out var go)) continue;

                Transform parentTr = root.transform;

                string p = rec.ParentName;
                int guard = 0;
                while (!string.IsNullOrEmpty(p) && guard++ < 256)
                {
                    if (goByName.TryGetValue(p, out var parentGo))
                    {
                        parentTr = parentGo.transform;
                        break;
                    }

                    if (!scene.Nodes.TryGetValue(p, out var pr) || pr == null)
                        break;

                    p = pr.ParentName;
                }

                go.transform.SetParent(parentTr, false);
            }

            // 3) Scene-wide context during ApplyToUnity
            MayaBuildContext.Push(scene, _options, _log);

            try
            {
                var buckets = new SortedDictionary<int, List<MayaNodeComponentBase>>();

                for (int i = 0; i < comps.Count; i++)
                {
                    var c = comps[i];
                    if (c == null) continue;

                    int stage = GetStagePriority(c.NodeType);

                    if (!buckets.TryGetValue(stage, out var list))
                    {
                        list = new List<MayaNodeComponentBase>(32);
                        buckets.Add(stage, list);
                    }

                    list.Add(c);
                }

                foreach (var kv in buckets)
                {
                    var list = kv.Value;
                    list.Sort(CompareWithinStage);

                    for (int i = 0; i < list.Count; i++)
                    {
                        var c = list[i];
                        try
                        {
                            c.ApplyToUnity(_options, _log);
                        }
                        catch (Exception e)
                        {
                            _log.Warn($"[ApplyToUnity] {c.NodeName} ({c.NodeType}) {e.GetType().Name}: {e.Message}");
                        }
                    }
                }

                // Phase-B: materials are finalized AFTER all nodes are applied.
                if (_options.SaveMaterials || _options.SaveTextures)
                    MayaMaterialPostProcessor.Apply(root, scene, _options, _log);

                // Scene settings
                var settings = root.GetComponent<MayaSceneSettings>() ?? root.AddComponent<MayaSceneSettings>();
                settings.InitializeFrom(scene, _options);

                // Optional runtime evaluation player (no compile-time dependency)
                var player = EnsureComponentByName(root, "MayaImporter.Animation.MayaTimeEvaluationPlayer");
                if (player != null)
                    TrySetBoolMember(player, "loop", settings.loop);
            }
            finally
            {
                MayaBuildContext.Pop();
            }

            // Phase3: finalize node representations + attach runtime evaluators
            MayaPhase3FinalizeAndRuntimeSetup.Run_BestEffort(root, _options, _log);

            // Portfolio / Audit proof
            AttachRootInfo(root, scene, _log);

            return root;
        }

        // ---- Deterministic staging ----
        private static int GetStagePriority(string nodeType)
        {
            if (string.IsNullOrEmpty(nodeType)) return 900;

            if (Eq(nodeType, "transform") || Eq(nodeType, "joint")) return 0;

            if (Eq(nodeType, "camera")) return 10;
            if (nodeType.EndsWith("Light", StringComparison.OrdinalIgnoreCase)) return 10;

            if (Eq(nodeType, "mesh") || Eq(nodeType, "nurbsCurve") || Eq(nodeType, "nurbsSurface")) return 20;

            if (Eq(nodeType, "blendShape") || Eq(nodeType, "skinCluster")) return 30;

            if (nodeType.IndexOf("constraint", StringComparison.OrdinalIgnoreCase) >= 0) return 40;
            if (nodeType.IndexOf("ik", StringComparison.OrdinalIgnoreCase) >= 0) return 40;
            if (nodeType.IndexOf("motionPath", StringComparison.OrdinalIgnoreCase) >= 0) return 40;

            if (Eq(nodeType, "shadingEngine")) return 60;
            if (LooksLikeShaderOrTextureNode(nodeType)) return 50;

            if (nodeType.StartsWith("animCurve", StringComparison.OrdinalIgnoreCase)) return 70;
            if (nodeType.IndexOf("anim", StringComparison.OrdinalIgnoreCase) >= 0) return 70;

            return 800;
        }

        private static int CompareWithinStage(MayaNodeComponentBase a, MayaNodeComponentBase b)
        {
            if (ReferenceEquals(a, b)) return 0;
            if (a == null) return 1;
            if (b == null) return -1;

            int da = GetDepth(a.transform);
            int db = GetDepth(b.transform);
            if (da != db) return da.CompareTo(db);

            var ta = a.NodeType ?? "";
            var tb = b.NodeType ?? "";
            int ct = StringComparer.OrdinalIgnoreCase.Compare(ta, tb);
            if (ct != 0) return ct;

            var na = a.NodeName ?? "";
            var nb = b.NodeName ?? "";
            return StringComparer.Ordinal.Compare(na, nb);
        }

        private static bool LooksLikeShaderOrTextureNode(string nodeType)
        {
            if (string.IsNullOrEmpty(nodeType)) return false;

            if (Eq(nodeType, "file")) return true;
            if (Eq(nodeType, "place2dTexture")) return true;

            if (nodeType.IndexOf("shader", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (nodeType.IndexOf("surface", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (nodeType.IndexOf("texture", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (nodeType.IndexOf("bump", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (nodeType.IndexOf("ramp", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (nodeType.IndexOf("remap", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (nodeType.IndexOf("aiStandardSurface", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (nodeType.IndexOf("standardSurface", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (Eq(nodeType, "lambert") || Eq(nodeType, "phong") || Eq(nodeType, "blinn")) return true;

            return false;
        }

        private static bool Eq(string a, string b)
            => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        private static int GetDepth(Transform t)
        {
            int d = 0;
            while (t != null && t.parent != null)
            {
                d++;
                t = t.parent;
            }
            return d;
        }

        private static string GetLeaf(string dag)
        {
            if (string.IsNullOrEmpty(dag)) return "node";
            int idx = dag.LastIndexOf('|');
            return (idx >= 0 && idx < dag.Length - 1) ? dag.Substring(idx + 1) : dag;
        }

        // =========================================================
        // Portfolio/Audit proof component attach
        // =========================================================

                private static void AttachRootInfo(GameObject root, MayaSceneData scene, MayaImportLog log)
        {
            if (root == null) return;

            var info = root.GetComponent<MayaImportedRootInfo>();
            if (info == null) info = root.AddComponent<MayaImportedRootInfo>();

            info.sourcePath = scene?.SourcePath ?? "";
            info.sourceKind = scene != null ? scene.SourceKind.ToString() : "Unknown";
            info.schemaVersion = scene != null ? scene.SchemaVersion : 0;
            info.rawSha256 = scene?.RawSha256 ?? "";
            info.rawByteCount = scene?.RawBinaryBytes != null ? scene.RawBinaryBytes.Length : 0;

            info.nodeCount = scene?.Nodes?.Count ?? 0;
            info.connectionCount = scene?.Connections?.Count ?? 0;

            // .mb extra proofs
            if (scene != null && scene.SourceKind == MayaSourceKind.BinaryMb)
            {
                info.mbHeader4CC = scene.MbIndex?.Header4CC ?? "";
                info.mbChunkCount = scene.MbIndex?.Chunks?.Count ?? 0;
                info.mbExtractedStringCount = scene.MbIndex?.ExtractedStrings?.Count ?? 0;

                info.mbExtractedAsciiStatements = scene.MbExtractedAsciiStatementCount;
                info.mbExtractedAsciiScore = scene.MbExtractedAsciiConfidence;
                info.mbExtractedAsciiChars = string.IsNullOrEmpty(scene.MbExtractedAsciiText) ? 0 : scene.MbExtractedAsciiText.Length;

                // detect whether embedded-ascii was actually merged/used
                bool embeddedUsed = false;
                bool chunkPlaceholderUsed = false;

                var raws = scene.RawStatements;
                if (raws != null)
                {
                    for (int i = 0; i < raws.Count; i++)
                    {
                        var rs = raws[i];
                        if (rs == null) continue;
                        if (string.Equals(rs.Command, "mbEmbeddedAscii", StringComparison.Ordinal)) embeddedUsed = true;
                        if (string.Equals(rs.Command, "mbChunkPlaceholder", StringComparison.Ordinal)) chunkPlaceholderUsed = true;
                    }
                }

                info.mbEmbeddedAsciiParsed = embeddedUsed;
                info.mbUsedChunkPlaceholderNodes = chunkPlaceholderUsed;
                info.mbFallbackReason = chunkPlaceholderUsed ? "chunkIndexPlaceholders" : "";
            }
            else
            {
                info.mbHeader4CC = "";
                info.mbChunkCount = 0;
                info.mbExtractedStringCount = 0;

                info.mbExtractedAsciiStatements = 0;
                info.mbExtractedAsciiScore = 0;
                info.mbExtractedAsciiChars = 0;
                info.mbEmbeddedAsciiParsed = false;

                info.mbUsedChunkPlaceholderNodes = false;
                info.mbFallbackReason = "";
            }

            // Unity-side counts
            var allNodeComps = root.GetComponentsInChildren<MayaNodeComponentBase>(true);
            info.unityNodeComponentCount = allNodeComps != null ? allNodeComps.Length : 0;

            int unknown = 0;
            if (allNodeComps != null)
            {
                for (int i = 0; i < allNodeComps.Length; i++)
                {
                    if (allNodeComps[i] is MayaUnknownNodeComponent) unknown++;
                }
            }
            info.unknownNodeComponentCount = unknown;

            var opaque = root.GetComponentsInChildren<MayaOpaqueNodeRuntime>(true);
            info.opaqueRuntimeNodeCount = opaque != null ? opaque.Length : 0;

            info.warningCount = log?.Warnings?.Count ?? 0;
            info.errorCount = log?.Errors?.Count ?? 0;

            if (scene != null)
            {
                var typeCounts = scene.CountNodeTypes();
                var top = typeCounts
                    .OrderByDescending(kv => kv.Value)
                    .ThenBy(kv => kv.Key, StringComparer.Ordinal)
                    .Take(12)
                    .ToList();

                info.topNodeTypes = top.Select(x => x.Key).ToArray();
                info.topNodeTypeCounts = top.Select(x => x.Value).ToArray();
            }
            else
            {
                info.topNodeTypes = Array.Empty<string>();
                info.topNodeTypeCounts = Array.Empty<int>();
            }

            info.lastUpdatedUtc = DateTime.UtcNow.ToString("o");
        }



        // =========================================================
        // Reflection helpers (avoid hard dependency)
        // =========================================================

        private static Component EnsureComponentByName(GameObject go, string fullTypeName)
        {
            if (go == null || string.IsNullOrEmpty(fullTypeName)) return null;

            var t = FindType(fullTypeName);
            if (t == null) return null;

            var existing = go.GetComponent(t);
            return existing != null ? existing : go.AddComponent(t);
        }

        private static Type FindType(string fullTypeName)
        {
            var t0 = Type.GetType(fullTypeName);
            if (t0 != null) return t0;

            var asms = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < asms.Length; i++)
            {
                var t = asms[i].GetType(fullTypeName);
                if (t != null) return t;
            }
            return null;
        }

        private static void TrySetBoolMember(Component c, string memberName, bool value)
        {
            if (c == null || string.IsNullOrEmpty(memberName)) return;

            var t = c.GetType();

            var p = t.GetProperty(memberName);
            if (p != null && p.CanWrite && p.PropertyType == typeof(bool))
            {
                p.SetValue(c, value);
                return;
            }

            var f = t.GetField(memberName);
            if (f != null && f.FieldType == typeof(bool))
            {
                f.SetValue(c, value);
            }
        }
    }
}

#if UNITY_EDITOR
namespace MayaImporter.Editor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using UnityEditor;
    using UnityEngine;
    using MayaImporter.Core;

    /// <summary>
    /// Portfolio-friendly audit window.
    /// Works on a PC with only Unity installed (no Maya / Autodesk API).
    ///
    /// Features:
    /// - Parse a .ma/.mb (project asset or external file)
    /// - Show warnings/errors without spamming the Console
    /// - Generate coverage summary and optionally export CSVs
    /// - Checks whether Maya2026 standard node type list is installed
    /// </summary>
    public sealed class MayaImportAuditWindow : EditorWindow
    {
        private const string StandardListExpectedPath = "Assets/MayaImporter/Resources/Maya2026_StandardNodeTypes.txt";

        private UnityEngine.Object _projectMayaAsset;
        private string _externalFilePath;
        private string _sourceAbsolutePath;
        private string _sourceLabel;

        private Vector2 _scroll;

        private MayaSceneData _scene;
        private MayaImportLog _log;
        private MayaCoverageReporter.CoverageResult _coverage;
        private HashSet<string> _standard;
        private bool _hasStandard;

        [MenuItem("Tools/Maya Importer/Import Audit...", priority = 20)]
        public static void Open()
        {
            var w = GetWindow<MayaImportAuditWindow>(true, "Maya Import Audit", true);
            w.minSize = new Vector2(760, 540);
            w.Show();
        }

        private void OnGUI()
        {
            DrawHeader();
            EditorGUILayout.Space(6);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawSourcePicker();
                DrawActions();
            }

            EditorGUILayout.Space(6);
            DrawResults();
        }

        private void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope())
                {
                    EditorGUILayout.LabelField("Maya Import Audit", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("Analyze .ma/.mb without Maya/Autodesk API. Generates coverage proof for portfolios.", EditorStyles.wordWrappedMiniLabel);
                }

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Clear", GUILayout.Width(90), GUILayout.Height(22)))
                {
                    _scene = null;
                    _log = null;
                    _coverage = null;
                    _standard = null;
                    _hasStandard = false;
                    _scroll = Vector2.zero;
                }
            }
        }

        private void DrawSourcePicker()
        {
            EditorGUILayout.LabelField("Source", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Project Asset", GUILayout.Width(90));
                var newObj = EditorGUILayout.ObjectField(_projectMayaAsset, typeof(UnityEngine.Object), false);
                if (newObj != _projectMayaAsset)
                {
                    _projectMayaAsset = newObj;
                    _externalFilePath = null;
                    UpdateSourceFromProjectObject(_projectMayaAsset);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("External File", GUILayout.Width(90));
                EditorGUILayout.SelectableLabel(string.IsNullOrEmpty(_externalFilePath) ? "(none)" : _externalFilePath, GUILayout.Height(EditorGUIUtility.singleLineHeight));

                if (GUILayout.Button("Browse...", GUILayout.Width(90)))
                {
                    var path = EditorUtility.OpenFilePanel("Select Maya .ma/.mb", "", "ma,mb");
                    if (!string.IsNullOrEmpty(path))
                    {
                        _projectMayaAsset = null;
                        _externalFilePath = path;
                        UpdateSourceFromExternalPath(path);
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Selected", GUILayout.Width(90));
                EditorGUILayout.SelectableLabel(string.IsNullOrEmpty(_sourceLabel) ? "(none)" : _sourceLabel, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            }
        }

        private void DrawActions()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = !string.IsNullOrEmpty(_sourceAbsolutePath) && File.Exists(_sourceAbsolutePath);

                if (GUILayout.Button("Analyze", GUILayout.Height(26)))
                {
                    RunAnalysis();
                }

                if (GUILayout.Button("Export CSV Reports", GUILayout.Height(26), GUILayout.Width(160)))
                {
                    ExportCsvReports();
                }

                GUI.enabled = true;

                GUILayout.FlexibleSpace();

                DrawStandardListStatus();
            }
        }

        private void DrawStandardListStatus()
        {
            bool exists = AssetDatabase.LoadAssetAtPath<TextAsset>(StandardListExpectedPath) != null;
            var label = exists
                ? "Maya2026_StandardNodeTypes: OK"
                : "Maya2026_StandardNodeTypes: MISSING";

            var style = exists ? EditorStyles.miniLabel : EditorStyles.miniBoldLabel;
            EditorGUILayout.LabelField(label, style, GUILayout.Width(260));

            if (!exists)
            {
                if (GUILayout.Button("Ping Expected Path", GUILayout.Width(140)))
                {
                    var folder = "Assets/MayaImporter/Resources";
                    var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(folder);
                    if (obj != null)
                    {
                        Selection.activeObject = obj;
                        EditorGUIUtility.PingObject(obj);
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("MayaImporter", "Create folder: Assets/MayaImporter/Resources\nThen add: Maya2026_StandardNodeTypes.txt", "OK");
                    }
                }
            }
        }

        private void DrawResults()
        {
            if (_scene == null)
            {
                EditorGUILayout.HelpBox("Select a .ma/.mb and click Analyze.", MessageType.Info);
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Summary", EditorStyles.boldLabel);

                EditorGUILayout.LabelField($"Source: {_sourceLabel}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"Nodes: {_scene.Nodes?.Count ?? 0}   Connections: {_scene.Connections?.Count ?? 0}   RawStatements: {_scene.RawStatements?.Count ?? 0}", EditorStyles.miniLabel);

                if (_coverage != null)
                {
                    EditorGUILayout.LabelField($"NodeTypes: {_coverage.NodeTypeRows?.Count ?? 0}   UnknownCommands: {_coverage.UnknownCommandCounts?.Count ?? 0}", EditorStyles.miniLabel);
                }

                if (_hasStandard && _standard != null)
                    EditorGUILayout.LabelField($"Maya2026 Standard List: {_standard.Count} entries (loaded)", EditorStyles.miniLabel);
                else
                    EditorGUILayout.LabelField("Maya2026 Standard List: not loaded (optional)", EditorStyles.miniLabel);
            }

            using (var sv = new EditorGUILayout.ScrollViewScope(_scroll))
            {
                _scroll = sv.scrollPosition;

                DrawLogSection();
                EditorGUILayout.Space(6);
                DrawCoverageSection();
            }
        }

        private void DrawLogSection()
        {
            EditorGUILayout.LabelField("Import Log", EditorStyles.boldLabel);

            if (_log == null)
            {
                EditorGUILayout.HelpBox("(no log)", MessageType.None);
                return;
            }

            if ((_log.Errors?.Count ?? 0) > 0)
            {
                EditorGUILayout.LabelField($"Errors: {_log.Errors.Count}", EditorStyles.miniBoldLabel);
                for (int i = 0; i < _log.Errors.Count; i++)
                    EditorGUILayout.SelectableLabel(_log.Errors[i], GUILayout.Height(EditorGUIUtility.singleLineHeight));
            }

            if ((_log.Warnings?.Count ?? 0) > 0)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField($"Warnings: {_log.Warnings.Count}", EditorStyles.miniBoldLabel);
                for (int i = 0; i < _log.Warnings.Count; i++)
                    EditorGUILayout.SelectableLabel(_log.Warnings[i], GUILayout.Height(EditorGUIUtility.singleLineHeight));
            }

            if ((_log.Infos?.Count ?? 0) > 0)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField($"Infos: {_log.Infos.Count}", EditorStyles.miniBoldLabel);
                // Infos can be large; show last 50
                int start = Mathf.Max(0, _log.Infos.Count - 50);
                for (int i = start; i < _log.Infos.Count; i++)
                    EditorGUILayout.SelectableLabel(_log.Infos[i], GUILayout.Height(EditorGUIUtility.singleLineHeight));
            }
        }

        private void DrawCoverageSection()
        {
            EditorGUILayout.LabelField("Coverage", EditorStyles.boldLabel);

            if (_coverage == null)
            {
                EditorGUILayout.HelpBox("(coverage not generated)", MessageType.None);
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Top NodeTypes", EditorStyles.miniBoldLabel);
                var rows = _coverage.NodeTypeRows;
                int n = rows != null ? Mathf.Min(20, rows.Count) : 0;
                for (int i = 0; i < n; i++)
                {
                    var r = rows[i];
                    EditorGUILayout.LabelField($"{r.NodeType}  x{r.Count}", EditorStyles.miniLabel);
                }

                if ((_coverage.UnknownCommandCounts?.Count ?? 0) > 0)
                {
                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField("Unknown Commands", EditorStyles.miniBoldLabel);
                    int shown = 0;
                    foreach (var kv in _coverage.UnknownCommandCounts)
                    {
                        EditorGUILayout.LabelField($"{kv.Key}  x{kv.Value}", EditorStyles.miniLabel);
                        if (++shown >= 20) break;
                    }
                }
            }
        }

        private void RunAnalysis()
        {
            try
            {
                var opt = new MayaImportOptions
                {
                    // Audit requires raw statements for command coverage
                    KeepRawStatements = true,
                    // Do not create Unity objects in audit
                    CreateUnityComponents = false,
                    SaveAssets = false,
                    SaveMeshes = false,
                    SaveMaterials = false,
                    SaveTextures = false,
                    SaveAnimationClip = false,
                    SavePrefab = false,
                };

                _scene = MayaImporter.Parse(_sourceAbsolutePath, opt, out _log);
                _coverage = MayaCoverageReporter.Generate(_scene);

                _hasStandard = MayaStandardNodeTypes.TryGet(out _standard);
            }
            catch (Exception e)
            {
                _scene = new MayaSceneData { SourcePath = _sourceAbsolutePath };
                _log = new MayaImportLog();
                _log.Error($"Audit failed: {e.GetType().Name}: {e.Message}");
                _coverage = null;
                _standard = null;
                _hasStandard = false;
            }
        }

        private void ExportCsvReports()
        {
            if (_scene == null || _coverage == null)
            {
                EditorUtility.DisplayDialog("MayaImporter", "Run Analyze first.", "OK");
                return;
            }

            try
            {
                // Write next to source file (simple + deterministic)
                MayaCoverageReporter.WriteCsvReports(_sourceAbsolutePath, _coverage, _hasStandard ? _standard : null, _log);
                EditorUtility.RevealInFinder(Path.GetDirectoryName(_sourceAbsolutePath));
            }
            catch (Exception e)
            {
                _log ??= new MayaImportLog();
                _log.Error($"CSV export failed: {e.GetType().Name}: {e.Message}");
            }
        }

        private void UpdateSourceFromProjectObject(UnityEngine.Object obj)
        {
            _sourceAbsolutePath = "";
            _sourceLabel = "";

            if (obj == null) return;

            var assetPath = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(assetPath)) return;

            if (!MayaImporter.IsSupportedFilePath(assetPath))
            {
                _sourceLabel = assetPath + " (unsupported)";
                return;
            }

            if (MayaImporter.TryGetAbsolutePathFromAssetPath(assetPath, out var abs))
            {
                _sourceAbsolutePath = abs;
                _sourceLabel = assetPath;
            }
            else
            {
                _sourceLabel = assetPath + " (missing on disk?)";
            }
        }

        private void UpdateSourceFromExternalPath(string path)
        {
            _sourceAbsolutePath = "";
            _sourceLabel = "";

            if (string.IsNullOrEmpty(path)) return;
            if (!File.Exists(path))
            {
                _sourceLabel = path + " (not found)";
                return;
            }

            if (!MayaImporter.IsSupportedFilePath(path))
            {
                _sourceLabel = path + " (unsupported)";
                return;
            }

            _sourceAbsolutePath = path;
            _sourceLabel = path;
        }
    }
}
#endif
