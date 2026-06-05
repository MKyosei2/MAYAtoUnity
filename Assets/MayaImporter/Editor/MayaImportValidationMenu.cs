// MAYAIMPORTER_PATCH_V5: Editor validation menu for sample import reports
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MayaImporter.Core.EditorTools
{
    /// <summary>
    /// Portfolio / QA helper for running deterministic validation imports from the Unity Editor.
    /// This intentionally lives in an Editor folder and does not affect runtime builds.
    /// </summary>
    public static class MayaImportValidationMenu
    {
        private const string SamplesFolder = "Samples";

        [MenuItem("Tools/MAYAtoUnity/Validate All Samples")]
        public static void ValidateAllSamples()
        {
            string projectRoot = Directory.GetCurrentDirectory();
            string samplesRoot = Path.Combine(projectRoot, SamplesFolder);

            if (!Directory.Exists(samplesRoot))
            {
                EditorUtility.DisplayDialog("MAYAtoUnity", "Samples folder was not found: " + samplesRoot, "OK");
                return;
            }

            string[] files = Directory.GetFiles(samplesRoot, "*.*", SearchOption.AllDirectories);
            int total = 0;
            int ok = 0;
            int failed = 0;

            try
            {
                for (int i = 0; i < files.Length; i++)
                {
                    string path = files[i];
                    string ext = Path.GetExtension(path).ToLowerInvariant();
                    if (ext != ".ma" && ext != ".mb") continue;

                    total++;
                    EditorUtility.DisplayProgressBar(
                        "MAYAtoUnity sample validation",
                        Path.GetFileName(path),
                        total > 0 ? i / (float)files.Length : 0f);

                    bool result = ValidateOne(path);
                    if (result) ok++;
                    else failed++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.Refresh();
            }

            EditorUtility.DisplayDialog(
                "MAYAtoUnity validation complete",
                string.Format("Samples checked: {0}\nSuccess: {1}\nFailed: {2}\nReports: Assets/MayaImported/Reports", total, ok, failed),
                "OK");
        }

        [MenuItem("Tools/MAYAtoUnity/Validate Selected Maya File")]
        public static void ValidateSelectedMayaFile()
        {
            Object selected = Selection.activeObject;
            string assetPath = selected != null ? AssetDatabase.GetAssetPath(selected) : null;

            if (string.IsNullOrEmpty(assetPath) || !MayaImporter.IsSupportedFilePath(assetPath))
            {
                EditorUtility.DisplayDialog("MAYAtoUnity", "Select a .ma or .mb asset first.", "OK");
                return;
            }

            if (!MayaImporter.TryGetAbsolutePathFromAssetPath(assetPath, out string absolute))
            {
                EditorUtility.DisplayDialog("MAYAtoUnity", "Could not resolve selected Maya file: " + assetPath, "OK");
                return;
            }

            bool result = ValidateOne(absolute);
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("MAYAtoUnity", result ? "Validation succeeded." : "Validation completed with errors. Check the report and console.", "OK");
        }

        private static bool ValidateOne(string absolutePath)
        {
            var options = new MayaImportOptions
            {
                KeepRawStatements = true,
                GenerateImportReport = true,
                ReportOutputFolder = "Assets/MayaImported/Reports",
                SaveAssets = false,
                SavePrefab = false,
                KeepImportedRootInScene = true
            };

            MayaSceneData scene;
            MayaImportLog log;
            GameObject root = MayaImporter.ImportIntoScene(absolutePath, options, out scene, out log);

            bool ok = scene != null && scene.Nodes != null && scene.Nodes.Count > 0 && (log == null || !log.HasErrors);
            if (!ok)
            {
                Debug.LogWarning("[MAYAtoUnity Validation] Validation found issues for: " + absolutePath + "\n" + (log != null ? log.ToString() : "No log"));
            }
            else
            {
                Debug.Log("[MAYAtoUnity Validation] OK: " + absolutePath + " nodes=" + scene.Nodes.Count + " connections=" + scene.Connections.Count);
            }

            return ok;
        }
    }
}
#endif
