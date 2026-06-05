// MAYAIMPORTER_PATCH_V13: One-button Maya import window for simplified portfolio/user workflow
#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MayaImporter.Core.EditorTools
{
    /// <summary>
    /// A simplified one-button importer UI.
    /// This is the user-facing entry point; lower-level validation menus remain available for QA.
    /// </summary>
    public sealed class MayaImportWizardWindow : EditorWindow
    {
        private const string WindowTitle = "MAYAtoUnity Importer";
        private const string ReportFolder = "Assets/MayaImported/Reports";
        private const string OutputFolder = "Assets/MayaImported";

        private string selectedPath = string.Empty;
        private string lastReportPath = string.Empty;
        private string lastRootName = string.Empty;
        private string status = "Select a Maya .ma or .mb file, then press Import.";
        private Vector2 scroll;
        private bool advanced;
        private bool keepRawStatements = true;
        private bool generateReport = true;
        private bool saveAssets;
        private bool savePrefab;
        private bool keepRootInScene = true;
        private bool createUnityComponents = true;
        private bool importInProgress;
        private int lastNodeCount;
        private int lastConnectionCount;
        private int lastWarningCount;
        private int lastErrorCount;
        private string lastLogText = string.Empty;
        private GameObject lastRoot;

        [MenuItem("Tools/MAYAtoUnity/Open Importer", priority = 0)]
        public static void Open()
        {
            MayaImportWizardWindow w = GetWindow<MayaImportWizardWindow>(false, WindowTitle, true);
            w.minSize = new Vector2(560f, 520f);
            w.TryUseCurrentSelection();
            w.Show();
        }

        [MenuItem("Tools/MAYAtoUnity/Import Maya Scene (.ma/.mb)", priority = 1)]
        public static void OpenImportMayaScene()
        {
            Open();
        }

        [MenuItem("Assets/MAYAtoUnity/Import Selected Maya Scene", priority = 2000)]
        public static void ImportSelectedAssetMenu()
        {
            Open();
        }

        [MenuItem("Assets/MAYAtoUnity/Import Selected Maya Scene", validate = true)]
        public static bool ValidateImportSelectedAssetMenu()
        {
            UnityEngine.Object selected = Selection.activeObject;
            string assetPath = selected != null ? AssetDatabase.GetAssetPath(selected) : null;
            return IsSupportedAssetPath(assetPath);
        }

        private void OnEnable()
        {
            TryUseCurrentSelection();
        }

        private void OnSelectionChange()
        {
            if (string.IsNullOrEmpty(selectedPath)) TryUseCurrentSelection();
            Repaint();
        }

        private void OnGUI()
        {
            DrawHeader();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawFileSection();
            DrawDropArea();
            DrawMainButton();
            DrawAdvancedOptions();
            DrawResultSection();
            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            GUILayout.Space(8);
            EditorGUILayout.LabelField("MAYAtoUnity", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("One-button Maya scene import", EditorStyles.largeLabel);
            EditorGUILayout.HelpBox("Choose a Maya .ma/.mb file and press Import. The tool builds a Unity hierarchy, selects the imported root, and writes an audit report.", MessageType.Info);
        }

        private void DrawFileSection()
        {
            GUILayout.Space(8);
            EditorGUILayout.LabelField("1. Maya scene", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.TextField(selectedPath);
            if (GUILayout.Button("Browse...", GUILayout.Width(96)))
            {
                string start = Directory.Exists(Application.dataPath) ? Directory.GetParent(Application.dataPath).FullName : Application.dataPath;
                string path = EditorUtility.OpenFilePanel("Select Maya scene", start, "ma,mb");
                if (!string.IsNullOrEmpty(path)) SetSelectedPath(path);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Use Selected Project Asset")) TryUseCurrentSelection(true);
            if (GUILayout.Button("Clear", GUILayout.Width(96)))
            {
                selectedPath = string.Empty;
                status = "Select a Maya .ma or .mb file, then press Import.";
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawDropArea()
        {
            GUILayout.Space(8);
            Rect rect = GUILayoutUtility.GetRect(0f, 70f, GUILayout.ExpandWidth(true));
            GUI.Box(rect, "Drag & drop .ma / .mb file here", EditorStyles.helpBox);

            Event e = Event.current;
            if (e == null) return;
            if (!rect.Contains(e.mousePosition)) return;

            if (e.type == EventType.DragUpdated || e.type == EventType.DragPerform)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                if (e.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    if (DragAndDrop.paths != null && DragAndDrop.paths.Length > 0)
                    {
                        string path = DragAndDrop.paths[0];
                        if (!Path.IsPathRooted(path)) path = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), path));
                        SetSelectedPath(path);
                    }
                }
                e.Use();
            }
        }

        private void DrawMainButton()
        {
            GUILayout.Space(12);
            bool valid = IsSupportedAbsolutePath(selectedPath) && File.Exists(selectedPath) && !importInProgress;
            GUI.enabled = valid;
            if (GUILayout.Button("Import Maya Scene", GUILayout.Height(42)))
            {
                ImportSelectedPath();
            }
            GUI.enabled = true;

            MessageType type = lastErrorCount > 0 ? MessageType.Error : (lastWarningCount > 0 ? MessageType.Warning : MessageType.None);
            if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, type == MessageType.None ? MessageType.Info : type);
        }

        private void DrawAdvancedOptions()
        {
            GUILayout.Space(6);
            advanced = EditorGUILayout.Foldout(advanced, "Advanced options");
            if (!advanced) return;

            EditorGUI.indentLevel++;
            keepRawStatements = EditorGUILayout.ToggleLeft("Keep raw Maya statements for audit", keepRawStatements);
            generateReport = EditorGUILayout.ToggleLeft("Generate import report", generateReport);
            createUnityComponents = EditorGUILayout.ToggleLeft("Create Unity components", createUnityComponents);
            saveAssets = EditorGUILayout.ToggleLeft("Save generated assets under Assets/MayaImported", saveAssets);
            savePrefab = EditorGUILayout.ToggleLeft("Save imported root as Prefab", savePrefab);
            keepRootInScene = EditorGUILayout.ToggleLeft("Keep imported root in current scene", keepRootInScene);
            EditorGUI.indentLevel--;
        }

        private void DrawResultSection()
        {
            GUILayout.Space(12);
            EditorGUILayout.LabelField("2. Result", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            DrawResultRow("Imported root", string.IsNullOrEmpty(lastRootName) ? "-" : lastRootName);
            DrawResultRow("Nodes", lastNodeCount.ToString());
            DrawResultRow("Connections", lastConnectionCount.ToString());
            DrawResultRow("Warnings", lastWarningCount.ToString());
            DrawResultRow("Errors", lastErrorCount.ToString());
            DrawResultRow("Report", string.IsNullOrEmpty(lastReportPath) ? "-" : lastReportPath);
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginHorizontal();
            GUI.enabled = lastRoot != null;
            if (GUILayout.Button("Select Imported Root"))
            {
                Selection.activeGameObject = lastRoot;
                EditorGUIUtility.PingObject(lastRoot);
            }
            GUI.enabled = !string.IsNullOrEmpty(lastReportPath) && File.Exists(lastReportPath);
            if (GUILayout.Button("Reveal Report")) EditorUtility.RevealInFinder(lastReportPath);
            GUI.enabled = Directory.Exists(ToAbsoluteProjectPath(OutputFolder));
            if (GUILayout.Button("Reveal Output Folder")) EditorUtility.RevealInFinder(ToAbsoluteProjectPath(OutputFolder));
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(lastLogText))
            {
                GUILayout.Space(8);
                EditorGUILayout.LabelField("Import log", EditorStyles.boldLabel);
                EditorGUILayout.TextArea(lastLogText, GUILayout.MinHeight(120));
            }
        }

        private static void DrawResultRow(string label, string value)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(130));
            EditorGUILayout.SelectableLabel(value, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            EditorGUILayout.EndHorizontal();
        }

        private void ImportSelectedPath()
        {
            if (!IsSupportedAbsolutePath(selectedPath) || !File.Exists(selectedPath))
            {
                status = "Select an existing .ma or .mb file first.";
                return;
            }

            importInProgress = true;
            lastErrorCount = 0;
            lastWarningCount = 0;
            lastLogText = string.Empty;
            lastReportPath = string.Empty;
            lastRootName = string.Empty;
            lastRoot = null;

            try
            {
                EditorUtility.DisplayProgressBar(WindowTitle, "Importing " + Path.GetFileName(selectedPath), 0.25f);

                MayaImportOptions options = CreateOptions();
                string beforeLatestReport = FindLatestReportForSelectedPath(selectedPath);

                MayaSceneData scene;
                MayaImportLog log;
                GameObject root = MayaImporter.ImportIntoScene(selectedPath, options, out scene, out log);

                lastRoot = root;
                lastRootName = root != null ? root.name : string.Empty;
                lastNodeCount = scene != null && scene.Nodes != null ? scene.Nodes.Count : 0;
                lastConnectionCount = scene != null && scene.Connections != null ? scene.Connections.Count : 0;
                lastWarningCount = log != null && log.Warnings != null ? log.Warnings.Count : 0;
                lastErrorCount = log != null && log.Errors != null ? log.Errors.Count : 0;
                lastLogText = log != null ? log.ToString() : string.Empty;
                lastReportPath = FindLatestReportForSelectedPath(selectedPath);
                if (string.Equals(lastReportPath, beforeLatestReport, StringComparison.OrdinalIgnoreCase)) lastReportPath = FindLatestReportAny();

                if (root != null)
                {
                    Selection.activeGameObject = root;
                    EditorGUIUtility.PingObject(root);
                }

                AssetDatabase.Refresh();
                status = lastErrorCount > 0
                    ? "Import finished with errors. Check the log and report."
                    : "Import complete. The imported root is selected in the Hierarchy.";
            }
            catch (Exception ex)
            {
                lastErrorCount = 1;
                lastLogText = ex.ToString();
                status = "Import failed: " + ex.GetType().Name + ": " + ex.Message;
                Debug.LogError("[MAYAtoUnity Importer] " + ex);
            }
            finally
            {
                importInProgress = false;
                EditorUtility.ClearProgressBar();
                Repaint();
            }
        }

        private MayaImportOptions CreateOptions()
        {
            MayaImportOptions options = new MayaImportOptions();
            options.KeepRawStatements = keepRawStatements;
            options.GenerateImportReport = generateReport;
            options.ReportOutputFolder = ReportFolder;
            options.OutputFolder = OutputFolder;
            options.SaveAssets = saveAssets;
            options.SaveMeshes = saveAssets;
            options.SaveMaterials = saveAssets;
            options.SaveTextures = saveAssets;
            options.SavePrefab = savePrefab;
            options.KeepImportedRootInScene = keepRootInScene;
            options.CreateUnityComponents = createUnityComponents;
            options.AttachDecodedAttributeSummary = true;
            return options;
        }

        private void TryUseCurrentSelection(bool showDialogOnFailure = false)
        {
            UnityEngine.Object selected = Selection.activeObject;
            string assetPath = selected != null ? AssetDatabase.GetAssetPath(selected) : null;
            if (!IsSupportedAssetPath(assetPath))
            {
                if (showDialogOnFailure) EditorUtility.DisplayDialog(WindowTitle, "Select a .ma or .mb asset in the Project window first.", "OK");
                return;
            }

            string absolute;
            if (MayaImporter.TryGetAbsolutePathFromAssetPath(assetPath, out absolute)) SetSelectedPath(absolute);
            else if (showDialogOnFailure) EditorUtility.DisplayDialog(WindowTitle, "Could not resolve selected file: " + assetPath, "OK");
        }

        private void SetSelectedPath(string path)
        {
            selectedPath = NormalizePath(path);
            if (!IsSupportedAbsolutePath(selectedPath)) status = "Only Maya .ma and .mb files are supported by this simplified importer window.";
            else if (!File.Exists(selectedPath)) status = "File does not exist: " + selectedPath;
            else status = "Ready to import: " + Path.GetFileName(selectedPath);
        }

        private static bool IsSupportedAssetPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return false;
            string ext = Path.GetExtension(assetPath).ToLowerInvariant();
            return ext == ".ma" || ext == ".mb";
        }

        private static bool IsSupportedAbsolutePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return ext == ".ma" || ext == ".mb";
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return string.Empty;
            try { return Path.GetFullPath(path).Replace('\\', '/'); }
            catch { return path.Replace('\\', '/'); }
        }

        private static string ToAbsoluteProjectPath(string projectRelativePath)
        {
            if (string.IsNullOrEmpty(projectRelativePath)) return string.Empty;
            if (Path.IsPathRooted(projectRelativePath)) return NormalizePath(projectRelativePath);
            return NormalizePath(Path.Combine(Directory.GetCurrentDirectory(), projectRelativePath));
        }

        private string FindLatestReportForSelectedPath(string sourcePath)
        {
            string folder = ToAbsoluteProjectPath(ReportFolder);
            if (!Directory.Exists(folder)) return string.Empty;
            string baseName = MakeSafeFileName(Path.GetFileNameWithoutExtension(sourcePath));
            string[] reports = Directory.GetFiles(folder, baseName + "_ImportReport_*.md", SearchOption.TopDirectoryOnly);
            return LatestFile(reports);
        }

        private static string FindLatestReportAny()
        {
            string folder = ToAbsoluteProjectPath(ReportFolder);
            if (!Directory.Exists(folder)) return string.Empty;
            string[] reports = Directory.GetFiles(folder, "*_ImportReport_*.md", SearchOption.TopDirectoryOnly);
            return LatestFile(reports);
        }

        private static string LatestFile(string[] files)
        {
            if (files == null || files.Length == 0) return string.Empty;
            string latest = string.Empty;
            DateTime latestTime = DateTime.MinValue;
            for (int i = 0; i < files.Length; i++)
            {
                string f = files[i];
                DateTime t = File.GetLastWriteTimeUtc(f);
                if (string.IsNullOrEmpty(latest) || t > latestTime)
                {
                    latest = f;
                    latestTime = t;
                }
            }
            return NormalizePath(latest);
        }

        private static string MakeSafeFileName(string s)
        {
            if (string.IsNullOrEmpty(s)) return "MayaScene";
            foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s.Replace(' ', '_');
        }
    }
}
#endif
