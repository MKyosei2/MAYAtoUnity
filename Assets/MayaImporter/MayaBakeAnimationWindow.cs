#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MayaImporter
{
    /// <summary>
    /// Bake AnimationClip by MayaImporter Runtime Evaluation
    /// (Editor-only)
    /// </summary>
    public sealed class MayaBakeAnimationWindow : EditorWindow
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private int _startFrame = 1;
        [SerializeField] private int _endFrame = 120;
        [SerializeField] private int _step = 1;
        [SerializeField] private float _fps = 30f;
        [SerializeField] private bool _bakeBlendShapes = true;

        [MenuItem("Tools/Maya Importer/Bake Animation (Maya Eval)")]
        public static void Open()
        {
            var w = GetWindow<MayaBakeAnimationWindow>();
            w.titleContent = new GUIContent("Maya Bake");
            w.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Bake AnimationClip by MayaImporter Runtime Evaluation", EditorStyles.boldLabel);

            _root = (GameObject)EditorGUILayout.ObjectField("Root", _root, typeof(GameObject), true);
            _startFrame = EditorGUILayout.IntField("Start Frame", _startFrame);
            _endFrame = EditorGUILayout.IntField("End Frame", _endFrame);
            _step = Mathf.Max(1, EditorGUILayout.IntField("Step", _step));
            _fps = Mathf.Max(1f, EditorGUILayout.FloatField("FPS", _fps));
            _bakeBlendShapes = EditorGUILayout.Toggle("Bake BlendShapes", _bakeBlendShapes);

            using (new EditorGUI.DisabledScope(_root == null))
            {
                if (GUILayout.Button("Bake -> AnimationClip"))
                    Bake();
            }
        }

        private void Bake()
        {
            if (_root == null) return;

            int s = Mathf.Min(_startFrame, _endFrame);
            int e = Mathf.Max(_startFrame, _endFrame);
            int step = Mathf.Max(1, _step);
            float fps = Mathf.Max(1f, _fps);

            var animMgr = _root.GetComponent<MayaImporter.Animation.MayaAnimationManager>();
            if (animMgr == null)
            {
                Debug.LogError("MayaAnimationManager not found on Root. Import scene first.");
                return;
            }

            var transforms = _root.GetComponentsInChildren<Transform>(true);
            var smrs = _root.GetComponentsInChildren<SkinnedMeshRenderer>(true);

            try
            {
                var clip = new AnimationClip { frameRate = fps };

                // Curves for transforms
                var curveMap = new Dictionary<Transform, Curves>(transforms.Length);
                for (int i = 0; i < transforms.Length; i++)
                {
                    var t = transforms[i];
                    if (t == null) continue;
                    curveMap[t] = new Curves();
                }

                // Curves for blendshapes
                Dictionary<(SkinnedMeshRenderer smr, int index), AnimationCurve> bsCurves = null;
                if (_bakeBlendShapes)
                {
                    bsCurves = new Dictionary<(SkinnedMeshRenderer, int), AnimationCurve>(512);
                    for (int i = 0; i < smrs.Length; i++)
                    {
                        var r = smrs[i];
                        if (r == null || r.sharedMesh == null) continue;

                        int n = r.sharedMesh.blendShapeCount;
                        for (int k = 0; k < n; k++)
                            bsCurves[(r, k)] = new AnimationCurve();
                    }
                }

                for (int f = s; f <= e; f += step)
                {
                    float timeSec = (f - s) / fps;

                    // Global time state used by some evaluators
                    MayaImporter.Animation.MayaTimeState.Fps = fps;
                    MayaImporter.Animation.MayaTimeState.CurrentFrame = f;

                    // Evaluate scene at this frame
                    // ★ 修正点：2引数版が無い環境でも動くように 1引数呼び出しへ統一
                    animMgr.EvaluateNow(f);

                    // Optional extra evaluation steps (exist in this project)
                    MayaImporter.Animation.MayaMotionPathManager.EvaluateNow();
                    MayaImporter.Constraints.MayaSurfaceConstraintManager.EvaluateNow();
                    MayaImporter.Constraints.MayaConstraintManager.EvaluateNow();
                    MayaImporter.IK.MayaIkManager.EvaluateNow();
                    MayaImporter.Animation.MayaEulerRotationApplier.ApplyAllNow(_root.transform);

                    // Record TRS
                    for (int i = 0; i < transforms.Length; i++)
                    {
                        var t = transforms[i];
                        if (t == null) continue;

                        var c = curveMap[t];
                        var lp = t.localPosition;
                        var lr = t.localRotation;
                        var ls = t.localScale;

                        c.px.AddKey(timeSec, lp.x);
                        c.py.AddKey(timeSec, lp.y);
                        c.pz.AddKey(timeSec, lp.z);

                        c.rx.AddKey(timeSec, lr.x);
                        c.ry.AddKey(timeSec, lr.y);
                        c.rz.AddKey(timeSec, lr.z);
                        c.rw.AddKey(timeSec, lr.w);

                        c.sx.AddKey(timeSec, ls.x);
                        c.sy.AddKey(timeSec, ls.y);
                        c.sz.AddKey(timeSec, ls.z);
                    }

                    // Record blendshape weights
                    if (_bakeBlendShapes && bsCurves != null)
                    {
                        for (int i = 0; i < smrs.Length; i++)
                        {
                            var r = smrs[i];
                            if (r == null || r.sharedMesh == null) continue;

                            int n = r.sharedMesh.blendShapeCount;
                            for (int k = 0; k < n; k++)
                            {
                                var curve = bsCurves[(r, k)];
                                curve.AddKey(timeSec, r.GetBlendShapeWeight(k));
                            }
                        }
                    }
                }

                // Write transform curves
                for (int i = 0; i < transforms.Length; i++)
                {
                    var t = transforms[i];
                    if (t == null) continue;

                    string path = AnimationUtility.CalculateTransformPath(t, _root.transform);
                    var c = curveMap[t];

                    SetCurve(clip, path, typeof(Transform), "m_LocalPosition.x", c.px);
                    SetCurve(clip, path, typeof(Transform), "m_LocalPosition.y", c.py);
                    SetCurve(clip, path, typeof(Transform), "m_LocalPosition.z", c.pz);

                    SetCurve(clip, path, typeof(Transform), "m_LocalRotation.x", c.rx);
                    SetCurve(clip, path, typeof(Transform), "m_LocalRotation.y", c.ry);
                    SetCurve(clip, path, typeof(Transform), "m_LocalRotation.z", c.rz);
                    SetCurve(clip, path, typeof(Transform), "m_LocalRotation.w", c.rw);

                    SetCurve(clip, path, typeof(Transform), "m_LocalScale.x", c.sx);
                    SetCurve(clip, path, typeof(Transform), "m_LocalScale.y", c.sy);
                    SetCurve(clip, path, typeof(Transform), "m_LocalScale.z", c.sz);
                }

                // Write blendshape curves
                if (_bakeBlendShapes && bsCurves != null)
                {
                    foreach (var kv in bsCurves)
                    {
                        var r = kv.Key.smr;
                        int k = kv.Key.index;
                        var curve = kv.Value;

                        if (r == null || r.sharedMesh == null) continue;

                        string path = AnimationUtility.CalculateTransformPath(r.transform, _root.transform);
                        string bsName = r.sharedMesh.GetBlendShapeName(k);
                        SetCurve(clip, path, typeof(SkinnedMeshRenderer), "blendShape." + bsName, curve);
                    }
                }

                // Save clip
                string savePath = EditorUtility.SaveFilePanelInProject(
                    "Save Baked Clip",
                    _root.name + "_Baked.anim",
                    "anim",
                    "Choose location to save AnimationClip");

                if (!string.IsNullOrEmpty(savePath))
                {
                    AssetDatabase.CreateAsset(clip, savePath);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                    EditorGUIUtility.PingObject(clip);
                    Debug.Log($"Baked clip saved: {savePath}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        private static void SetCurve(AnimationClip clip, string path, Type type, string property, AnimationCurve curve)
        {
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, type, property), curve);
        }

        [Serializable]
        private sealed class Curves
        {
            public AnimationCurve px = new AnimationCurve();
            public AnimationCurve py = new AnimationCurve();
            public AnimationCurve pz = new AnimationCurve();

            public AnimationCurve rx = new AnimationCurve();
            public AnimationCurve ry = new AnimationCurve();
            public AnimationCurve rz = new AnimationCurve();
            public AnimationCurve rw = new AnimationCurve();

            public AnimationCurve sx = new AnimationCurve();
            public AnimationCurve sy = new AnimationCurve();
            public AnimationCurve sz = new AnimationCurve();
        }
    }
}
#endif
