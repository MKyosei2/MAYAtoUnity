#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using MayaImporter.Core;
using MayaImporter.Animation;

namespace MayaImporter.EditorTools
{
    /// <summary>
    /// D-4: シーン上に既に存在する animCurveTL/TA/TU から AnimationClip を生成して、
    /// 指定Rootへ legacy Animation としてアタッチするだけのツール。
    /// （.ma/.mbのImport処理やNodeFactory呼び出しは一切しない＝API差異で壊れない）
    /// </summary>
    public sealed class MayaImporterWindow : EditorWindow
    {
        [MenuItem("Tools/Maya Importer/Build AnimationClip From AnimCurves")]
        public static void Open()
        {
            GetWindow<MayaImporterWindow>("Maya AnimClip");
        }

        private GameObject _targetRoot;
        private string _clipName = "MayaClip";
        private float _timeScale = 1.0f; // Maya time がフレームなら 1/30 等にする（必要時）

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Build AnimationClip from imported animCurves", EditorStyles.boldLabel);

            _targetRoot = (GameObject)EditorGUILayout.ObjectField("Target Root", _targetRoot, typeof(GameObject), true);
            _clipName = EditorGUILayout.TextField("Clip Name", _clipName);
            _timeScale = EditorGUILayout.FloatField("Time Scale", _timeScale);

            EditorGUILayout.Space(8);

            if (GUILayout.Button("Build Clip & Attach (Legacy Animation)"))
            {
                BuildAndAttach();
            }

            EditorGUILayout.HelpBox(
                "This tool does NOT import .ma/.mb.\n" +
                "It creates a legacy AnimationClip from animCurveTL/TA/TU nodes already present in the scene.",
                MessageType.Info);
        }

        private void BuildAndAttach()
        {
            var root = _targetRoot != null ? _targetRoot : Selection.activeGameObject;
            if (root == null)
            {
                Debug.LogWarning("[MayaImporter] Target Root is null. Select a GameObject or assign Target Root.");
                return;
            }

            var log = new MayaImportLog();

            // Gather animCurve components in the current scene
            var list = new List<MayaNodeComponentBase>();
            var all = Resources.FindObjectsOfTypeAll<MayaNodeComponentBase>();

            for (int i = 0; i < all.Length; i++)
            {
                var c = all[i];
                if (c == null) continue;
                if (!c.gameObject.scene.IsValid()) continue;

                var t = c.NodeType;
                if (t == "animCurveTL" || t == "animCurveTA" || t == "animCurveTU")
                    list.Add(c);
            }

            if (list.Count == 0)
            {
                Debug.LogWarning("[MayaImporter] No animCurveTL/TA/TU nodes found in the current scene.");
                return;
            }

            var clip = MayaAnimationClipBuilder.BuildClipFromAnimCurves(list, root.transform, log,
                string.IsNullOrEmpty(_clipName) ? "MayaClip" : _clipName,
                _timeScale);

            // Attach legacy Animation component (namespace collision回避)
            var anim = root.GetComponent<global::UnityEngine.Animation>();
            if (anim == null) anim = root.AddComponent<global::UnityEngine.Animation>();

            anim.clip = clip;
            anim.AddClip(clip, clip.name);
            anim.Play(clip.name);

            Debug.Log($"[MayaImporter] AnimationClip built & attached: '{clip.name}' curveNodes={list.Count} targetRoot='{root.name}'");
        }
    }
}
#else
namespace MayaImporter.EditorTools { public sealed class MayaImporterWindow { } }
#endif
