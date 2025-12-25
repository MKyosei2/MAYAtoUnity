// MayaImporter/MayaFremImporterWindow.cs
#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MayaImporter.Core
{
    /// <summary>
    /// Phase1/PhaseA:
    /// - 推奨: Assets 内の .ma/.mb を “正規のImporter(=ScriptedImporter)” で扱う
    /// - このWindowは補助導線:
    ///   1) 外部ファイルを Assets にコピーする（推奨導線への橋渡し）
    ///   2) 選択中の Maya Asset を Reimport する
    ///   3) 旧: 外部パスから一時的に Scene へ生成（レガシー）
    ///
    /// ※重要:
    /// Maya無し運用のため、Reimport/Copy の前に Importer Override を先に適用する。
    /// </summary>
    public sealed class MayaFremImporterWindow : EditorWindow
    {
        private const string DefaultImportFolder = "Assets/MayaImporter/Sources";

        private int _tab;
        private Vector2 _scroll;

        // Legacy external import
        private string _mayaFilePath = "";
        private MayaImportOptions _options = new MayaImportOptions();

        [MenuItem("Tools/Maya Importer/Importer Bridge (Project / Legacy)")]
        public static void Open()
        {
            GetWindow<MayaFremImporterWindow>("Maya Importer");
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField("Maya Importer (Unity-only)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "推奨運用:\n" +
                "  - .ma/.mb を Assets 内に置く\n" +
                "  - Importer Override により “Maya無しPC” でも ScriptedImporter で再構築する\n\n" +
                "このWindowは “正規Importer” へ寄せるための橋渡し（コピー/選択Reimport）と、\n" +
                "旧来の外部パス一時Import（レガシー）を提供します。",
                MessageType.Info);

            _tab = GUILayout.Toolbar(_tab, new[] { "Project Asset (Recommended)", "Legacy External Import" });
            EditorGUILayout.Space(8);

            if (_tab == 0) DrawProjectAssetTab();
            else DrawLegacyTab();

            EditorGUILayout.EndScrollView();
        }

        // ----------------------------
        // Project Asset tab (recommended)
        // ----------------------------
        private void DrawProjectAssetTab()
        {
            EditorGUILayout.LabelField("Project Asset Import (Recommended)", EditorStyles.boldLabel);

            var selectedAssetPath = TryGetSelectedMayaAssetPath(out var whyNot);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("Selected Maya Asset", string.IsNullOrEmpty(selectedAssetPath) ? "(none)" : selectedAssetPath);
            }

            if (string.IsNullOrEmpty(selectedAssetPath))
            {
                EditorGUILayout.HelpBox(
                    "Projectウィンドウで .ma か .mb を選択してください。\n" +
                    (string.IsNullOrEmpty(whyNot) ? "" : $"理由: {whyNot}"),
                    MessageType.Warning);
            }
            else
            {
                var hasOverride = MayaImporterOverrideTools.HasOurOverride(selectedAssetPath);

                EditorGUILayout.HelpBox(
                    "Importer Override: " + (hasOverride ? "ON (Unity-only Importer)" : "OFF (Unity default importer)"),
                    hasOverride ? MessageType.Info : MessageType.Warning);

                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button("Apply Override"))
                {
                    MayaImporterOverrideTools.EnsureOverrideForAsset(selectedAssetPath, reimportIfChanged: true);
                }

                if (GUILayout.Button("Clear Override"))
                {
                    MayaImporterOverrideTools.ClearOverrideForAsset(selectedAssetPath, reimportAfter: true);
                }

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button("Reimport Selected"))
                {
                    // Maya無し運用のため、Reimport前に必ずOverrideを適用
                    MayaImporterOverrideTools.EnsureOverrideForAsset(selectedAssetPath, reimportIfChanged: false);
                    AssetDatabase.ImportAsset(selectedAssetPath, ImportAssetOptions.ForceUpdate);
                    Debug.Log("[MayaImporter] Reimport requested: " + selectedAssetPath);
                }

                if (GUILayout.Button("Ping"))
                {
                    var obj = AssetDatabase.LoadMainAssetAtPath(selectedAssetPath);
                    if (obj != null)
                    {
                        Selection.activeObject = obj;
                        EditorGUIUtility.PingObject(obj);
                    }
                }

                if (GUILayout.Button("Reveal in Explorer"))
                {
                    var abs = GetAbsolutePathFromAssetPath(selectedAssetPath);
                    if (!string.IsNullOrEmpty(abs) && File.Exists(abs))
                        EditorUtility.RevealInFinder(abs);
                }

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(8);
                EditorGUILayout.HelpBox(
                    "ここが“100点”の主導線です:\n" +
                    "  - .ma/.mb をAssetsに置く\n" +
                    "  - Override importer で Prefab(main) と sub-assets を生成\n" +
                    "  - Reimport 可能（差分検証・CI・Maya無し運用）",
                    MessageType.None);
            }

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("Bring External File Into Project", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "外部にある .ma/.mb を Assets にコピーします。\n" +
                "コピー直後に Importer Override を適用してから Import するため、Maya無しPCでも安全です。",
                MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Copy External .ma/.mb Into Assets..."))
            {
                CopyExternalIntoAssets();
            }

            if (GUILayout.Button("Open Import Folder"))
            {
                EnsureFolder(DefaultImportFolder);
                EditorUtility.RevealInFinder(Path.GetFullPath(DefaultImportFolder));
            }
            EditorGUILayout.EndHorizontal();
        }

        private static string TryGetSelectedMayaAssetPath(out string whyNot)
        {
            whyNot = "";

            var obj = Selection.activeObject;
            if (obj == null)
            {
                whyNot = "Selection is empty";
                return "";
            }

            var path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path))
            {
                whyNot = "Selected object has no asset path";
                return "";
            }

            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext != ".ma" && ext != ".mb")
            {
                whyNot = "Selected asset is not .ma/.mb";
                return "";
            }

            if (!path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                whyNot = "Asset is not inside Assets/";
                return "";
            }

            return path.Replace('\\', '/');
        }

        private static string GetAbsolutePathFromAssetPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return "";
            assetPath = assetPath.Replace('\\', '/');

            if (!assetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                return "";

            // Project root = parent of Assets
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? "";
            if (string.IsNullOrEmpty(projectRoot)) return "";

            return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
        }

        private void CopyExternalIntoAssets()
        {
            var src = EditorUtility.OpenFilePanel("Select Maya File", "", "ma,mb");
            if (string.IsNullOrEmpty(src)) return;

            if (!File.Exists(src))
            {
                EditorUtility.DisplayDialog("MayaImporter", "File not found:\n" + src, "OK");
                return;
            }

            var ext = Path.GetExtension(src).ToLowerInvariant();
            if (ext != ".ma" && ext != ".mb")
            {
                EditorUtility.DisplayDialog("MayaImporter", "Only .ma/.mb supported.", "OK");
                return;
            }

            EnsureFolder(DefaultImportFolder);

            var fileName = Path.GetFileName(src);
            var dst = Path.Combine(DefaultImportFolder, fileName).Replace('\\', '/');

            // Avoid overwrite by suffixing
            if (File.Exists(dst))
            {
                var name = Path.GetFileNameWithoutExtension(fileName);
                var e = Path.GetExtension(fileName);
                for (int i = 1; i < 9999; i++)
                {
                    var cand = $"{DefaultImportFolder}/{name}__{i}{e}";
                    if (!File.Exists(cand))
                    {
                        dst = cand;
                        break;
                    }
                }
            }

            try
            {
                File.Copy(src, dst, overwrite: false);

                // 重要: Import の前に Override をセットして “Maya無しPC” でも確実にこのImporterへ
                MayaImporterOverrideTools.EnsureOverrideForAsset(dst, reimportIfChanged: false);

                AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceUpdate);

                var obj = AssetDatabase.LoadMainAssetAtPath(dst);
                if (obj != null)
                {
                    Selection.activeObject = obj;
                    EditorGUIUtility.PingObject(obj);
                }

                Debug.Log($"[MayaImporter] Copied external file into project:\nSRC: {src}\nDST: {dst}");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorUtility.DisplayDialog("MayaImporter", "Copy failed.\n" + e.Message, "OK");
            }
        }

        private static void EnsureFolder(string folder)
        {
            folder = (folder ?? "").Replace('\\', '/');
            if (string.IsNullOrEmpty(folder)) return;

            // Create nested folders if needed
            var parts = folder.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return;

            string cur = parts[0];
            if (cur != "Assets") return;

            for (int i = 1; i < parts.Length; i++)
            {
                var next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(cur, parts[i]);
                }
                cur = next;
            }
        }

        // ----------------------------
        // Legacy external import
        // ----------------------------
        private void DrawLegacyTab()
        {
            EditorGUILayout.LabelField("Legacy External Import (Temporary)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "レガシー導線:\n" +
                "外部パスから直接 Scene に生成します。\n\n" +
                "主導線は Project内 .ma/.mb の正規Importer（Override ScriptedImporter）です。\n" +
                "この機能はデバッグ/比較用に残します（再インポート/依存追跡は弱い）。",
                MessageType.Warning);

            DrawFileSelector();
            EditorGUILayout.Space();

            DrawOptions();
            EditorGUILayout.Space();

            GUI.enabled = !string.IsNullOrEmpty(_mayaFilePath) && File.Exists(_mayaFilePath);
            if (GUILayout.Button("Import Into Scene (Legacy)"))
            {
                ImportIntoScene_Legacy();
            }
            GUI.enabled = true;
        }

        private void DrawFileSelector()
        {
            EditorGUILayout.LabelField("External Maya File (.ma / .mb)");

            EditorGUILayout.BeginHorizontal();
            _mayaFilePath = EditorGUILayout.TextField(_mayaFilePath);

            if (GUILayout.Button("Browse", GUILayout.Width(80)))
            {
                var path = EditorUtility.OpenFilePanel("Select Maya File", "", "ma,mb");
                if (!string.IsNullOrEmpty(path))
                {
                    _mayaFilePath = path;
                    GUI.FocusControl(null);
                }
            }

            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(_mayaFilePath) && !File.Exists(_mayaFilePath))
                EditorGUILayout.HelpBox("File does not exist.", MessageType.Warning);
        }

        private void DrawOptions()
        {
            EditorGUILayout.LabelField("Import Options (Legacy)", EditorStyles.boldLabel);

            _options.KeepRawStatements =
                EditorGUILayout.ToggleLeft("Keep Raw Statements", _options.KeepRawStatements);

            _options.CreateUnityComponents =
                EditorGUILayout.ToggleLeft("Create Unity Components (Camera/Light etc.)", _options.CreateUnityComponents);

            _options.Conversion =
                (CoordinateConversion)EditorGUILayout.EnumPopup("Coordinate Conversion", _options.Conversion);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Portfolio Proof (100%)", EditorStyles.boldLabel);

            _options.AttachOpaqueRuntimeMarker =
                EditorGUILayout.ToggleLeft("Attach Reconstructed Marker (MayaOpaqueNodeRuntime)", _options.AttachOpaqueRuntimeMarker);

            _options.AttachOpaqueAttributePreview =
                EditorGUILayout.ToggleLeft("Attach Raw Attribute Preview (MayaOpaqueAttributePreview)", _options.AttachOpaqueAttributePreview);

            _options.AttachOpaqueConnectionPreview =
                EditorGUILayout.ToggleLeft("Attach Connection Preview (MayaOpaqueConnectionPreview)", _options.AttachOpaqueConnectionPreview);

            _options.AttachDecodedAttributeSummary =
                EditorGUILayout.ToggleLeft("Attach Typed Summary (MayaDecodedAttributeSummary)", _options.AttachDecodedAttributeSummary);

            _options.OpaquePreviewMaxEntries =
                EditorGUILayout.IntField("Preview Max Entries", _options.OpaquePreviewMaxEntries);

            _options.OpaqueRuntimeGizmoSize =
                EditorGUILayout.FloatField("Marker Gizmo Size", _options.OpaqueRuntimeGizmoSize);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Legacy Assetize (Not the PhaseA main path)", EditorStyles.boldLabel);

            _options.SaveAssets = EditorGUILayout.ToggleLeft("Save Assets (Mesh/Material/Texture/Anim/Prefab)", _options.SaveAssets);

            using (new EditorGUI.DisabledScope(!_options.SaveAssets))
            {
                _options.OutputFolder = EditorGUILayout.TextField("Output Folder (Assets/...)", _options.OutputFolder);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Save Meshes", GUILayout.Width(100));
                _options.SaveMeshes = EditorGUILayout.Toggle(_options.SaveMeshes, GUILayout.Width(20));
                EditorGUILayout.LabelField("Materials", GUILayout.Width(70));
                _options.SaveMaterials = EditorGUILayout.Toggle(_options.SaveMaterials, GUILayout.Width(20));
                EditorGUILayout.LabelField("Textures", GUILayout.Width(70));
                _options.SaveTextures = EditorGUILayout.Toggle(_options.SaveTextures, GUILayout.Width(20));
                EditorGUILayout.EndHorizontal();

                _options.SaveAnimationClip = EditorGUILayout.ToggleLeft("Save AnimationClip (.anim) from animCurveTL/TA/TU", _options.SaveAnimationClip);
                using (new EditorGUI.DisabledScope(!_options.SaveAnimationClip))
                {
                    _options.AnimationClipName = EditorGUILayout.TextField("Clip Name", _options.AnimationClipName);
                    _options.AnimationTimeScale = EditorGUILayout.FloatField("Time Scale", _options.AnimationTimeScale);
                }

                _options.SavePrefab = EditorGUILayout.ToggleLeft("Save Prefab", _options.SavePrefab);
                _options.KeepImportedRootInScene = EditorGUILayout.ToggleLeft("Keep Imported Root in Scene", _options.KeepImportedRootInScene);
            }
        }

        private void ImportIntoScene_Legacy()
        {
            if (string.IsNullOrEmpty(_mayaFilePath) || !File.Exists(_mayaFilePath))
            {
                Debug.LogError("[MayaImporter] Invalid path.");
                return;
            }

            var ext = Path.GetExtension(_mayaFilePath).ToLowerInvariant();
            if (ext != ".ma" && ext != ".mb")
            {
                Debug.LogError("[MayaImporter] Unsupported file extension: " + ext);
                return;
            }

            try
            {
                MayaSceneData scene;
                MayaImportLog log;

                var root = MayaImporter.ImportIntoScene(_mayaFilePath, _options, out scene, out log);

                if (root != null)
                {
                    Selection.activeGameObject = root;
                    EditorGUIUtility.PingObject(root);
                }

                Debug.Log($"[MayaImporter] Legacy import finished. Nodes={scene?.Nodes?.Count ?? 0}, Connections={scene?.Connections?.Count ?? 0}");

                if (_options.SaveAssets && root != null)
                {
                    MayaAssetPipeline.AssetizeImportedRoot(root, scene, _options, log);

                    if (!_options.KeepImportedRootInScene)
                    {
                        DestroyImmediate(root);
                        Debug.Log("[MayaImporter] Deleted imported root from scene (KeepImportedRootInScene=false).");
                    }
                }

                if (log != null && log.HasErrors)
                    Debug.LogWarning("[MayaImporter] Import completed with errors. See MayaImportLog output for details.");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }
    }
}
#endif
