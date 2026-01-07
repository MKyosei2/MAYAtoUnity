#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Editor
{
    /// <summary>
    /// GUI-first importer (Portfolio friendly):
    /// - Pick .ma/.mb
    /// - Choose which DAG objects to reconstruct
    /// - Import selected (copy into project + apply per-asset reconstruction selection)
    ///
    /// NOTE:
    /// - Does NOT require Maya/Autodesk API.
    /// - Keeps 100% raw preservation: the actual import is still handled by ScriptedImporter.
    /// - This window only edits the per-asset reconstruction selection (ExcludedNodeNames).
    /// </summary>
    public sealed class MayaImportWizardWindow : EditorWindow
    {
        private const string DefaultOutputFolder = "Assets/MayaImported";

        [Serializable]
        private sealed class DagNode
        {
            public string Name;
            public string NodeType;
            public string Parent;
            public readonly List<DagNode> Children = new List<DagNode>(8);

            public bool Expanded;
            public bool Included = true;

            public int SubtreeCountCached = -1;

            public int GetSubtreeCount()
            {
                if (SubtreeCountCached >= 0) return SubtreeCountCached;
                int c = 1;
                for (int i = 0; i < Children.Count; i++)
                    c += Children[i].GetSubtreeCount();
                SubtreeCountCached = c;
                return c;
            }
        }

        // --------------------
        // Source
        // --------------------
        private UnityEngine.Object _projectMayaAsset;
        private string _externalFilePath;

        private string _sourceAbsolutePath;
        private string _sourceLabel;

        // --------------------
        // Preview
        // --------------------
        private MayaSceneData _previewScene;
        private MayaImportLog _previewLog;

        private List<DagNode> _dagRoots;
        private Dictionary<string, DagNode> _dagByName;

        private Vector2 _scroll;
        private string _search;

        // --------------------
        // Import options (minimal)
        // --------------------
        private DefaultAsset _outputFolder;

        private bool _showHierarchy = false;

        private bool _importMeshes = true;
        private bool _importMaterials = true;
        private bool _importTextures = true;
        private bool _importAnimationClip = true;

        private bool _treatExcludedAsSubtree = true;

        private bool _instantiateIntoActiveScene = true;

        // Console cleanliness
        private enum ConsoleMode
        {
            Silent = 0,
            ErrorsOnly = 1,
            WarningsAndErrors = 2
        }

        private ConsoleMode _consoleMode = ConsoleMode.Silent;

        // --------------------
        // Last import
        // --------------------
        private string _lastImportedAssetPath;
        private string _lastImportSummary;

        // ★修正：メニュー位置を Tools > Maya Importer に変更
        [MenuItem("Tools/Maya Importer/Import Maya Scene (.ma/.mb)...", priority = 10)]
        public static void Open()
        {
            var w = GetWindow<MayaImportWizardWindow>(true, "Import Maya Scene", true);
            w.minSize = new Vector2(760, 540);
            w.Show();
        }

        private void OnEnable()
        {
            if (_outputFolder == null)
            {
                _outputFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(DefaultOutputFolder);
            }
        }

        private void OnGUI()
        {
            DrawHeader();

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawSourcePicker();
                DrawPreviewControls();
            }

            EditorGUILayout.Space(6);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(position.width * 0.60f)))
                {
                    DrawObjectSelection();
                }

                using (new EditorGUILayout.VerticalScope(GUILayout.Width(position.width * 0.40f)))
                {
                    DrawImportOptions();
                    EditorGUILayout.Space(6);
                    DrawImportButtons();
                    EditorGUILayout.Space(6);
                    DrawLastImportSummary();
                }
            }
        }

        private void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope())
                {
                    EditorGUILayout.LabelField("Maya Import Wizard", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("Pick a .ma/.mb file, choose objects to reconstruct, then import selected.", EditorStyles.wordWrappedMiniLabel);
                }

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Open Audit", GUILayout.Width(110), GUILayout.Height(22)))
                {
                    // Optional audit window (portfolio proof)
                    MayaImportAuditWindow.Open();
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

        private void DrawPreviewControls()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = !string.IsNullOrEmpty(_sourceAbsolutePath) && File.Exists(_sourceAbsolutePath);
                if (GUILayout.Button("Read Objects", GUILayout.Height(26)))
                {
                    ReadObjectsFromSource();
                }
                GUI.enabled = true;

                GUILayout.FlexibleSpace();

                _showHierarchy = GUILayout.Toggle(_showHierarchy, "Show hierarchy", GUILayout.Width(120));
            }

            if (_previewScene != null)
            {
                int roots = _dagRoots?.Count ?? 0;
                int totalDag = _dagByName?.Count ?? 0;
                int warn = _previewLog?.Warnings?.Count ?? 0;
                int err = _previewLog?.Errors?.Count ?? 0;

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"Objects: roots {roots} / dag {totalDag}");
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField($"parse warnings {warn}, errors {err}", EditorStyles.miniLabel);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Click 'Read Objects' to parse the file and show selectable objects.", MessageType.Info);
            }
        }

        private void DrawObjectSelection()
        {
            EditorGUILayout.LabelField("Objects", EditorStyles.boldLabel);

            if (_dagRoots == null)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("No preview loaded.");
                    EditorGUILayout.LabelField("Pick a file and click 'Read Objects'.", EditorStyles.miniLabel);
                }
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                _search = EditorGUILayout.TextField(_search ?? string.Empty, GUI.skin.FindStyle("ToolbarSearchTextField"));

                if (GUILayout.Button("", GUI.skin.FindStyle("ToolbarSearchCancelButton")))
                {
                    _search = "";
                    GUI.FocusControl(null);
                }

                if (GUILayout.Button("All", GUILayout.Width(50)))
                    SetAllIncluded(true);

                if (GUILayout.Button("None", GUILayout.Width(50)))
                    SetAllIncluded(false);
            }

            using (var sv = new EditorGUILayout.ScrollViewScope(_scroll, GUILayout.Height(position.height - 170)))
            {
                _scroll = sv.scrollPosition;

                if (_showHierarchy)
                {
                    for (int i = 0; i < _dagRoots.Count; i++)
                        DrawDagNodeRecursive(_dagRoots[i], 0);
                }
                else
                {
                    for (int i = 0; i < _dagRoots.Count; i++)
                        DrawDagNodeFlatRoot(_dagRoots[i]);
                }
            }
        }

        private void DrawDagNodeFlatRoot(DagNode n)
        {
            if (n == null) return;
            if (!PassSearch(n)) return;

            using (new EditorGUILayout.HorizontalScope())
            {
                n.Included = EditorGUILayout.Toggle(n.Included, GUILayout.Width(18));
                EditorGUILayout.LabelField($"{n.Name}", GUILayout.ExpandWidth(true));
                EditorGUILayout.LabelField($"{n.NodeType}", EditorStyles.miniLabel, GUILayout.Width(120));
                EditorGUILayout.LabelField($"({n.GetSubtreeCount()})", EditorStyles.miniLabel, GUILayout.Width(60));
            }
        }

        private void DrawDagNodeRecursive(DagNode n, int depth)
        {
            if (n == null) return;

            // Search filtering: if this node doesn't match, still show if any child matches.
            bool selfMatch = PassSearch(n);
            bool childMatch = false;
            for (int i = 0; i < n.Children.Count; i++)
            {
                if (PassSearchRecursive(n.Children[i]))
                {
                    childMatch = true;
                    break;
                }
            }

            if (!string.IsNullOrEmpty(_search) && !selfMatch && !childMatch)
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(depth * 14);

                if (n.Children.Count > 0)
                    n.Expanded = EditorGUILayout.Foldout(n.Expanded, GUIContent.none, true, EditorStyles.foldout);
                else
                    GUILayout.Space(14);

                bool newIncluded = EditorGUILayout.Toggle(n.Included, GUILayout.Width(18));
                if (newIncluded != n.Included)
                {
                    n.Included = newIncluded;

                    // Propagate selection down (common expectation for package-import style)
                    if (_treatExcludedAsSubtree)
                        SetIncludedRecursive(n, newIncluded);
                }

                EditorGUILayout.LabelField(n.Name, GUILayout.ExpandWidth(true));
                EditorGUILayout.LabelField(n.NodeType, EditorStyles.miniLabel, GUILayout.Width(120));
                EditorGUILayout.LabelField($"({n.GetSubtreeCount()})", EditorStyles.miniLabel, GUILayout.Width(60));
            }

            if (n.Expanded)
            {
                for (int i = 0; i < n.Children.Count; i++)
                    DrawDagNodeRecursive(n.Children[i], depth + 1);
            }
        }

        private bool PassSearchRecursive(DagNode n)
        {
            if (n == null) return false;
            if (PassSearch(n)) return true;
            for (int i = 0; i < n.Children.Count; i++)
                if (PassSearchRecursive(n.Children[i])) return true;
            return false;
        }

        private bool PassSearch(DagNode n)
        {
            if (n == null) return false;
            var s = _search;
            if (string.IsNullOrEmpty(s)) return true;
            s = s.Trim();
            if (s.Length == 0) return true;

            if (!string.IsNullOrEmpty(n.Name) && n.Name.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (!string.IsNullOrEmpty(n.NodeType) && n.NodeType.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        private void DrawImportOptions()
        {
            EditorGUILayout.LabelField("Options", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _outputFolder = (DefaultAsset)EditorGUILayout.ObjectField("Output Folder", _outputFolder, typeof(DefaultAsset), false);

                if (_outputFolder != null)
                {
                    var p = AssetDatabase.GetAssetPath(_outputFolder);
                    if (!AssetDatabase.IsValidFolder(p))
                        EditorGUILayout.HelpBox("Output Folder must be a folder under Assets/.", MessageType.Warning);
                }
                else
                {
                    EditorGUILayout.HelpBox($"Default output folder: {DefaultOutputFolder}", MessageType.None);
                }

                EditorGUILayout.Space(4);

                _importMeshes = EditorGUILayout.ToggleLeft("Import Meshes", _importMeshes);
                _importMaterials = EditorGUILayout.ToggleLeft("Import Materials", _importMaterials);
                _importTextures = EditorGUILayout.ToggleLeft("Import Textures", _importTextures);
                _importAnimationClip = EditorGUILayout.ToggleLeft("Import AnimationClip", _importAnimationClip);

                EditorGUILayout.Space(4);

                _treatExcludedAsSubtree = EditorGUILayout.ToggleLeft("Treat excluded as subtree (recommended)", _treatExcludedAsSubtree);
                _instantiateIntoActiveScene = EditorGUILayout.ToggleLeft("Instantiate into active scene", _instantiateIntoActiveScene);

                EditorGUILayout.Space(4);

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Console", GUILayout.Width(60));
                    _consoleMode = (ConsoleMode)EditorGUILayout.EnumPopup(_consoleMode);
                }

                EditorGUILayout.LabelField("Note: warnings/errors are still saved into 'MayaImportLog' sub-asset.", EditorStyles.miniLabel);
            }
        }

        private void DrawImportButtons()
        {
            GUI.enabled = CanImport();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Import Selected", GUILayout.Height(30)))
                {
                    ImportSelected();
                }

                if (GUILayout.Button("Cancel", GUILayout.Height(30), GUILayout.Width(90)))
                {
                    Close();
                }
            }

            GUI.enabled = true;
        }

        private void DrawLastImportSummary()
        {
            if (string.IsNullOrEmpty(_lastImportedAssetPath) && string.IsNullOrEmpty(_lastImportSummary))
                return;

            EditorGUILayout.LabelField("Result", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (!string.IsNullOrEmpty(_lastImportedAssetPath))
                {
                    EditorGUILayout.LabelField("Asset", EditorStyles.miniBoldLabel);
                    EditorGUILayout.SelectableLabel(_lastImportedAssetPath, GUILayout.Height(EditorGUIUtility.singleLineHeight));

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Ping Asset"))
                        {
                            var obj = AssetDatabase.LoadMainAssetAtPath(_lastImportedAssetPath);
                            if (obj != null)
                            {
                                EditorGUIUtility.PingObject(obj);
                                Selection.activeObject = obj;
                            }
                        }

                        if (GUILayout.Button("Open ImportLog"))
                        {
                            var log = FindNamedSubAsset<TextAsset>(_lastImportedAssetPath, "MayaImportLog");
                            if (log != null)
                            {
                                Selection.activeObject = log;
                                EditorGUIUtility.PingObject(log);
                            }
                        }
                    }
                }

                if (!string.IsNullOrEmpty(_lastImportSummary))
                {
                    EditorGUILayout.LabelField("Summary", EditorStyles.miniBoldLabel);
                    EditorGUILayout.LabelField(_lastImportSummary, EditorStyles.wordWrappedMiniLabel);
                }
            }
        }

        // --------------------
        // Internals
        // --------------------

        private bool CanImport()
        {
            if (_dagRoots == null) return false;

            bool any = false;
            for (int i = 0; i < _dagRoots.Count; i++)
            {
                if (_dagRoots[i] != null && _dagRoots[i].Included)
                {
                    any = true;
                    break;
                }
            }
            if (!any) return false;

            if (string.IsNullOrEmpty(_sourceAbsolutePath) || !File.Exists(_sourceAbsolutePath)) return false;

            // Output folder must be valid if set
            if (_outputFolder != null)
            {
                var p = AssetDatabase.GetAssetPath(_outputFolder);
                if (!AssetDatabase.IsValidFolder(p)) return false;
            }

            return true;
        }

        private void SetAllIncluded(bool included)
        {
            if (_dagRoots == null) return;

            for (int i = 0; i < _dagRoots.Count; i++)
            {
                var r = _dagRoots[i];
                if (r == null) continue;
                r.Included = included;
                if (_showHierarchy && _treatExcludedAsSubtree)
                    SetIncludedRecursive(r, included);
            }
        }

        private static void SetIncludedRecursive(DagNode n, bool included)
        {
            if (n == null) return;
            n.Included = included;
            for (int i = 0; i < n.Children.Count; i++)
                SetIncludedRecursive(n.Children[i], included);
        }

        private void UpdateSourceFromProjectObject(UnityEngine.Object obj)
        {
            _sourceAbsolutePath = null;
            _sourceLabel = null;
            _previewScene = null;
            _previewLog = null;
            _dagRoots = null;
            _dagByName = null;

            if (obj == null) return;

            var assetPath = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(assetPath)) return;

            if (!assetPath.EndsWith(".ma", StringComparison.OrdinalIgnoreCase) &&
                !assetPath.EndsWith(".mb", StringComparison.OrdinalIgnoreCase))
            {
                _sourceLabel = assetPath + " (not .ma/.mb)";
                return;
            }

            if (MayaImporter.Core.MayaImporter.TryGetAbsolutePathFromAssetPath(assetPath, out var abs))
            {
                _sourceAbsolutePath = abs;
                _sourceLabel = assetPath;
            }
            else
            {
                _sourceLabel = assetPath + " (cannot resolve absolute path)";
            }
        }

        private void UpdateSourceFromExternalPath(string absPath)
        {
            _sourceAbsolutePath = null;
            _sourceLabel = null;
            _previewScene = null;
            _previewLog = null;
            _dagRoots = null;
            _dagByName = null;

            if (string.IsNullOrEmpty(absPath)) return;

            var ext = Path.GetExtension(absPath).ToLowerInvariant();
            if (ext != ".ma" && ext != ".mb")
            {
                _sourceLabel = absPath + " (not .ma/.mb)";
                return;
            }

            _sourceAbsolutePath = absPath;
            _sourceLabel = absPath;
        }

        private void ReadObjectsFromSource()
        {
            _previewScene = null;
            _previewLog = null;
            _dagRoots = null;
            _dagByName = null;

            if (string.IsNullOrEmpty(_sourceAbsolutePath) || !File.Exists(_sourceAbsolutePath))
                return;

            try
            {
                EditorUtility.DisplayProgressBar("Maya Import Wizard", "Parsing file (Unity-only)...", 0.2f);

                // Preview parse: we don't need RawStatements to list DAG nodes.
                var opt = new MayaImportOptions
                {
                    KeepRawStatements = false,

                    // Keep .mb recovery paths enabled to match actual import coverage
                    MbTryExtractEmbeddedAscii = true,
                    MbTryExtractNullTerminatedAscii = true,
                    MbAllowLowConfidenceEmbeddedAscii = true,
                    MbAllowLowConfidenceNullTerminatedAscii = true,
                    MbDeterministicEnumerateNodes = true,
                    MbCreateChunkPlaceholderNodes = true,
                };

                _previewScene = MayaImporter.Core.MayaImporter.Parse(_sourceAbsolutePath, opt, out _previewLog);
                BuildDagTree(_previewScene);
            }
            catch (Exception e)
            {
                _previewLog ??= new MayaImportLog();
                _previewLog.Error("Preview parse failed: " + e.GetType().Name + ": " + e.Message);
                _previewScene = null;
                _dagRoots = new List<DagNode>();
                _dagByName = new Dictionary<string, DagNode>(StringComparer.Ordinal);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static bool IsDagLike(NodeRecord rec, Dictionary<string, List<NodeRecord>> childrenMap)
        {
            if (rec == null) return false;
            var t = rec.NodeType ?? "";

            // Typical DAG-ish types
            if (Eq(t, "transform") || Eq(t, "joint") || Eq(t, "camera") || Eq(t, "light") || Eq(t, "locator") ||
                Eq(t, "mesh") || Eq(t, "nurbsCurve") || Eq(t, "nurbsSurface") || Eq(t, "subdiv") ||
                Eq(t, "ikHandle") || Eq(t, "clusterHandle") || Eq(t, "lattice") || Eq(t, "follicle") ||
                Eq(t, "annotationShape") || Eq(t, "distanceDimShape") || Eq(t, "aimConstraint") || Eq(t, "parentConstraint") ||
                Eq(t, "pointConstraint") || Eq(t, "orientConstraint") || Eq(t, "scaleConstraint"))
                return true;

            // Has parent/child relationship recorded -> treat as DAG-like for selection UI
            if (!string.IsNullOrEmpty(rec.ParentName)) return true;
            if (childrenMap != null && childrenMap.TryGetValue(rec.Name, out var kids) && kids != null && kids.Count > 0) return true;

            return false;
        }

        private void BuildDagTree(MayaSceneData scene)
        {
            _dagRoots = new List<DagNode>(256);
            _dagByName = new Dictionary<string, DagNode>(StringComparer.Ordinal);

            if (scene == null || scene.Nodes == null || scene.Nodes.Count == 0)
                return;

            // Build children map from ParentName
            var childrenMap = new Dictionary<string, List<NodeRecord>>(StringComparer.Ordinal);
            foreach (var kv in scene.Nodes)
            {
                var rec = kv.Value;
                if (rec == null) continue;
                var p = rec.ParentName;
                if (string.IsNullOrEmpty(p)) continue;

                if (!childrenMap.TryGetValue(p, out var list))
                {
                    list = new List<NodeRecord>(4);
                    childrenMap[p] = list;
                }
                list.Add(rec);
            }

            // Create DagNodes for dag-like records
            foreach (var kv in scene.Nodes)
            {
                var rec = kv.Value;
                if (!IsDagLike(rec, childrenMap)) continue;

                var n = new DagNode
                {
                    Name = rec.Name,
                    NodeType = rec.NodeType ?? "",
                    Parent = rec.ParentName
                };
                _dagByName[n.Name] = n;
            }

            // Wire children relationships
            foreach (var kv in _dagByName)
            {
                var n = kv.Value;
                if (n == null) continue;

                if (!string.IsNullOrEmpty(n.Parent) && _dagByName.TryGetValue(n.Parent, out var p) && p != null)
                    p.Children.Add(n);
            }

            // Root nodes: parent empty / world / missing
            foreach (var kv in _dagByName)
            {
                var n = kv.Value;
                if (n == null) continue;

                if (string.IsNullOrEmpty(n.Parent) || n.Parent == "|" || !_dagByName.ContainsKey(n.Parent))
                    _dagRoots.Add(n);
            }

            // Sort deterministically
            SortTreeRecursive(_dagRoots);
        }

        private static void SortTreeRecursive(List<DagNode> list)
        {
            if (list == null) return;
            list.Sort((a, b) => string.CompareOrdinal(a?.Name ?? "", b?.Name ?? ""));
            for (int i = 0; i < list.Count; i++)
            {
                var n = list[i];
                if (n == null) continue;
                SortTreeRecursive(n.Children);
            }
        }

        private static bool Eq(string a, string b)
            => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        private void EnsureOutputFolderExists()
        {
            var outFolder = GetOutputFolderPathOrDefault();
            if (AssetDatabase.IsValidFolder(outFolder)) return;

            // Create nested folders
            var parts = outFolder.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return;

            string cur = "Assets";
            for (int i = 1; i < parts.Length; i++)
            {
                var next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }

            AssetDatabase.Refresh();
        }

        private string GetOutputFolderPathOrDefault()
        {
            var path = _outputFolder != null ? AssetDatabase.GetAssetPath(_outputFolder) : null;
            if (string.IsNullOrEmpty(path)) path = DefaultOutputFolder;
            if (!path.StartsWith("Assets", StringComparison.Ordinal)) path = DefaultOutputFolder;
            return path;
        }

        private void ImportSelected()
        {
            if (string.IsNullOrEmpty(_sourceAbsolutePath) || !File.Exists(_sourceAbsolutePath)) return;
            if (_dagRoots == null) return;

            // Determine target asset path
            string targetAssetPath = null;
            string sourceAssetPath = null;

            try
            {
                EditorUtility.DisplayProgressBar("Maya Import Wizard", "Preparing import...", 0.05f);

                // If source is a project asset, we can apply selection directly.
                if (_projectMayaAsset != null)
                {
                    sourceAssetPath = AssetDatabase.GetAssetPath(_projectMayaAsset);
                    if (!string.IsNullOrEmpty(sourceAssetPath) && (sourceAssetPath.EndsWith(".ma", StringComparison.OrdinalIgnoreCase) || sourceAssetPath.EndsWith(".mb", StringComparison.OrdinalIgnoreCase)))
                        targetAssetPath = sourceAssetPath;
                }

                // External file -> copy into Assets
                if (string.IsNullOrEmpty(targetAssetPath))
                {
                    EnsureOutputFolderExists();
                    var outFolder = GetOutputFolderPathOrDefault();

                    var fileName = Path.GetFileName(_sourceAbsolutePath);
                    var dst = AssetDatabase.GenerateUniqueAssetPath(outFolder.TrimEnd('/') + "/" + fileName);

                    var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
                    if (string.IsNullOrEmpty(projectRoot))
                        throw new InvalidOperationException("Cannot resolve project root.");

                    var absDst = Path.GetFullPath(Path.Combine(projectRoot, dst));
                    Directory.CreateDirectory(Path.GetDirectoryName(absDst) ?? projectRoot);

                    File.Copy(_sourceAbsolutePath, absDst, overwrite: true);

                    AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceSynchronousImport);
                    targetAssetPath = dst;
                }

                EditorUtility.DisplayProgressBar("Maya Import Wizard", "Applying selection...", 0.30f);

                ApplySelectionToImporter(targetAssetPath);

                EditorUtility.DisplayProgressBar("Maya Import Wizard", "Reimporting...", 0.60f);

                var importer = AssetImporter.GetAtPath(targetAssetPath);
                importer?.SaveAndReimport();

                _lastImportedAssetPath = targetAssetPath;

                EditorUtility.DisplayProgressBar("Maya Import Wizard", "Finalizing...", 0.90f);

                if (_instantiateIntoActiveScene)
                    InstantiateImportedRootIntoScene(targetAssetPath);

                _lastImportSummary = BuildImportSummaryFromImportLog(targetAssetPath);

                // Optional: mark scene dirty if instanced
                if (_instantiateIntoActiveScene)
                    EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            }
            catch (Exception e)
            {
                _lastImportSummary = "Import failed: " + e.GetType().Name + ": " + e.Message;
                // Keep console clean: do not Debug.LogException here.
                ShowNotification(new GUIContent("Import failed (see window summary)"));
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private void ApplySelectionToImporter(string mayaAssetPath)
        {
            if (string.IsNullOrEmpty(mayaAssetPath)) return;

            var imp = AssetImporter.GetAtPath(mayaAssetPath);
            if (imp == null) throw new InvalidOperationException("AssetImporter not found for " + mayaAssetPath);

            // ScriptedImporter instance
            var so = new SerializedObject(imp);

            // Enable reconstruction selection
            var useSel = so.FindProperty("_useReconstructionSelection");
            if (useSel != null) useSel.boolValue = true;

            var recon = so.FindProperty("_reconstruction");
            if (recon == null) throw new InvalidOperationException("_reconstruction not found (importer schema mismatch)");

            recon.FindPropertyRelative("Enabled").boolValue = true;
            recon.FindPropertyRelative("ReconstructGameObjects").boolValue = true;

            recon.FindPropertyRelative("ImportMeshes").boolValue = _importMeshes;
            recon.FindPropertyRelative("ImportMaterials").boolValue = _importMaterials;
            recon.FindPropertyRelative("ImportTextures").boolValue = _importTextures;
            recon.FindPropertyRelative("ImportAnimationClip").boolValue = _importAnimationClip;

            recon.FindPropertyRelative("ExcludeByNameEnabled").boolValue = true;
            recon.FindPropertyRelative("TreatExcludedAsSubtree").boolValue = _treatExcludedAsSubtree;

            // Exclusion list: unchecked roots (and optionally unchecked hierarchy nodes)
            var excluded = new HashSet<string>(StringComparer.Ordinal);

            if (_showHierarchy)
            {
                // Exclude any unchecked nodes (subtree handling is handled by builder)
                for (int i = 0; i < _dagRoots.Count; i++)
                    CollectExcludedRecursive(_dagRoots[i], excluded);
            }
            else
            {
                // Root-only mode: exclude unchecked roots.
                for (int i = 0; i < _dagRoots.Count; i++)
                {
                    var r = _dagRoots[i];
                    if (r != null && !r.Included)
                        excluded.Add(r.Name);
                }
            }

            var listProp = recon.FindPropertyRelative("ExcludedNodeNames");
            if (listProp != null)
            {
                listProp.ClearArray();
                int idx = 0;
                foreach (var name in excluded.OrderBy(s => s, StringComparer.Ordinal))
                {
                    listProp.InsertArrayElementAtIndex(idx);
                    listProp.GetArrayElementAtIndex(idx).stringValue = name;
                    idx++;
                }
            }

            // Console mode (optional, importer-side fields)
            var warnProp = so.FindProperty("_emitLogWarningsToConsole");
            var errProp = so.FindProperty("_emitLogErrorsToConsole");
            var exProp = so.FindProperty("_emitExceptionToConsole");

            if (warnProp != null)
                warnProp.boolValue = _consoleMode == ConsoleMode.WarningsAndErrors;

            if (errProp != null)
                errProp.boolValue = _consoleMode != ConsoleMode.Silent;

            if (exProp != null)
                exProp.boolValue = false;

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CollectExcludedRecursive(DagNode n, HashSet<string> excluded)
        {
            if (n == null) return;
            if (!n.Included)
                excluded.Add(n.Name);

            for (int i = 0; i < n.Children.Count; i++)
                CollectExcludedRecursive(n.Children[i], excluded);
        }

        private void InstantiateImportedRootIntoScene(string mayaAssetPath)
        {
            try
            {
                var main = AssetDatabase.LoadMainAssetAtPath(mayaAssetPath) as GameObject;
                if (main == null) return;

                var inst = PrefabUtility.InstantiatePrefab(main) as GameObject;
                if (inst != null)
                {
                    Undo.RegisterCreatedObjectUndo(inst, "Import Maya Scene");
                    Selection.activeGameObject = inst;
                }
            }
            catch
            {
                // keep console clean
            }
        }

        private static T FindNamedSubAsset<T>(string assetPath, string name) where T : UnityEngine.Object
        {
            var all = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            if (all == null) return null;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] is T t && t != null && t.name == name)
                    return t;
            }
            return null;
        }

        private static string BuildImportSummaryFromImportLog(string assetPath)
        {
            try
            {
                var ta = FindNamedSubAsset<TextAsset>(assetPath, "MayaImportLog");
                if (ta == null) return "Imported (no ImportLog attached).";

                // Very small summary: count by prefix
                var text = ta.text ?? "";
                int warn = 0;
                int err = 0;

                using (var sr = new StringReader(text))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        if (line.StartsWith("[WARN]", StringComparison.Ordinal)) warn++;
                        else if (line.StartsWith("[ERROR]", StringComparison.Ordinal)) err++;
                    }
                }

                return $"ImportLog: warnings {warn}, errors {err}.";
            }
            catch
            {
                return "Imported.";
            }
        }
    }
}
#endif
