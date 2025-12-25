#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using MayaImporter.Core;
using MayaImporter.Animation;
using MayaImporter.Constraints;

namespace MayaImporter.EditorTools
{
    public sealed class MayaPortfolioOneShotWindow : EditorWindow
    {
        private string _mayaFilePath = "";
        private MayaImportOptions _options = new MayaImportOptions();

        [MenuItem("Tools/Maya Importer/Portfolio/One-Shot Import + Play (Anim+Constraint)")]
        public static void Open()
        {
            GetWindow<MayaPortfolioOneShotWindow>("Maya One-Shot");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Portfolio One-Shot (Unity-only)", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Maya File (.ma / .mb)");
            EditorGUILayout.BeginHorizontal();
            _mayaFilePath = EditorGUILayout.TextField(_mayaFilePath);
            if (GUILayout.Button("Browse", GUILayout.Width(80)))
            {
                var path = EditorUtility.OpenFilePanel("Select Maya File", "", "ma,mb");
                if (!string.IsNullOrEmpty(path))
                    _mayaFilePath = path;
                GUI.FocusControl(null);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Recommended preset for demo", EditorStyles.miniBoldLabel);

            // 推奨：ワンボタンで「成果物」まで出す
            _options.SaveAssets = EditorGUILayout.ToggleLeft("Save Assets", _options.SaveAssets);
            using (new EditorGUI.DisabledScope(!_options.SaveAssets))
            {
                _options.OutputFolder = EditorGUILayout.TextField("Output Folder (Assets/...)", _options.OutputFolder);
                _options.SaveAnimationClip = EditorGUILayout.ToggleLeft("Save AnimationClip", _options.SaveAnimationClip);
                _options.AnimationClipName = EditorGUILayout.TextField("Clip Name", _options.AnimationClipName);
                _options.AnimationTimeScale = EditorGUILayout.FloatField("Time Scale", _options.AnimationTimeScale);
                _options.SavePrefab = EditorGUILayout.ToggleLeft("Save Prefab", _options.SavePrefab);
                _options.KeepImportedRootInScene = EditorGUILayout.ToggleLeft("Keep Root In Scene", _options.KeepImportedRootInScene);
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Proof components (100%)", EditorStyles.miniBoldLabel);
            _options.AttachOpaqueRuntimeMarker = EditorGUILayout.ToggleLeft("Marker", _options.AttachOpaqueRuntimeMarker);
            _options.AttachOpaqueAttributePreview = EditorGUILayout.ToggleLeft("Attr Preview", _options.AttachOpaqueAttributePreview);
            _options.AttachOpaqueConnectionPreview = EditorGUILayout.ToggleLeft("Conn Preview", _options.AttachOpaqueConnectionPreview);
            _options.AttachDecodedAttributeSummary = EditorGUILayout.ToggleLeft("Typed Summary", _options.AttachDecodedAttributeSummary);
            _options.OpaquePreviewMaxEntries = EditorGUILayout.IntField("Preview Max Entries", _options.OpaquePreviewMaxEntries);

            EditorGUILayout.Space(10);

            GUI.enabled = File.Exists(_mayaFilePath);
            if (GUILayout.Button("ONE SHOT: Import → Assetize → Play + Constraints"))
            {
                RunOneShot();
            }
            GUI.enabled = true;

            EditorGUILayout.HelpBox(
                "This runs:\n" +
                "1) ImportIntoScene (.ma/.mb)\n" +
                "2) Assetize (optional)\n" +
                "3) If AnimationClip exists, assign it to MayaTimeEvaluationPlayer and PlayOnStart\n" +
                "4) Evaluate constraints every frame (TimePlayer does it)\n",
                MessageType.Info);
        }

        private void RunOneShot()
        {
            if (!File.Exists(_mayaFilePath))
            {
                Debug.LogError("[MayaImporter] File not found: " + _mayaFilePath);
                return;
            }

            var ext = Path.GetExtension(_mayaFilePath).ToLowerInvariant();
            if (ext != ".ma" && ext != ".mb")
            {
                Debug.LogError("[MayaImporter] Unsupported extension: " + ext);
                return;
            }

            MayaSceneData scene;
            MayaImportLog log;

            var root = MayaImporter.Core.MayaImporter.ImportIntoScene(_mayaFilePath, _options, out scene, out log);
            if (root == null)
            {
                Debug.LogError("[MayaImporter] Import failed (root is null).");
                return;
            }

            Selection.activeGameObject = root;
            EditorGUIUtility.PingObject(root);

            // Assetize (Editor)
            if (_options.SaveAssets)
            {
                MayaAssetPipeline.AssetizeImportedRoot(root, scene, _options, log);
            }

            // Ensure constraint manager exists
            MayaConstraintManager.EnsureExists();

            // Try to find AnimationClip attached by Assetize
            AnimationClip clip = null;
            var legacy = root.GetComponent<global::UnityEngine.Animation>();
            if (legacy != null && legacy.clip != null)
                clip = legacy.clip;

            // If no clip via assetize, try build directly from animCurves under root
            if (clip == null)
            {
                var nodes = root.GetComponentsInChildren<MayaNodeComponentBase>(true);
                var list = new System.Collections.Generic.List<MayaNodeComponentBase>(256);

                for (int i = 0; i < nodes.Length; i++)
                {
                    var n = nodes[i];
                    if (n == null) continue;
                    var t = n.NodeType;
                    if (t == "animCurveTL" || t == "animCurveTA" || t == "animCurveTU")
                        list.Add(n);
                }

                if (list.Count > 0)
                {
                    string clipName = string.IsNullOrEmpty(_options.AnimationClipName) ? "MayaClip" : _options.AnimationClipName;
                    clip = MayaAnimationClipBuilder.BuildClipFromAnimCurves(list, root.transform, log, clipName, _options.AnimationTimeScale);
                }
            }

            // Attach to TimePlayer for “anim → constraint” demo
            var player = root.GetComponent<MayaTimeEvaluationPlayer>();
            if (player == null) player = root.AddComponent<MayaTimeEvaluationPlayer>();

            player.Clip = clip;
            player.PlayOnStart = true;

            Debug.Log($"[MayaImporter OneShot] Done. root='{root.name}', nodes={scene?.Nodes?.Count ?? 0}, clip={(clip != null ? clip.name : "none")}");
        }
    }
}
#endif
