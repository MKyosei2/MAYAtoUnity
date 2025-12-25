#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using MayaImporter.Animation;
using UnityEditor;
using UnityEngine;

namespace MayaImporter.Core
{
    /// <summary>
    /// Phase C (全部盛り):
    /// C-4: 変化しているチャンネルだけベイク（Clip軽量化）
    /// C-5: Determinism（Clipハッシュ）を保存して再現性を証明
    /// C-6: Constraint/IK/Expression 等の“Unityに概念がない”ものを保持（欠損ゼロ）
    /// C-7: Audit情報を 1画面に集約（Inspector強化はEditorスクリプト）
    ///
    /// 名前空間差異に強い：Constraints/IK/TimeNode等は反射でbest-effort
    /// 例外を投げず import を止めない（100%保持思想）
    /// </summary>
    public static class MayaPhaseCAutoBakeOnImport
    {
        // C-4: 変化検出の閾値（ノイズでキーが増えすぎるのを抑える）
        private const float PosEps = 1e-6f;
        private const float ScaleEps = 1e-6f;
        private const float RotEps = 1e-7f;
        private const float BlendShapeEps = 1e-4f;

        public static void Run_BestEffort(GameObject root, MayaSceneData scene, MayaImportOptions options, MayaImportLog log)
        {
            options ??= new MayaImportOptions();
            log ??= new MayaImportLog();
            if (root == null) return;

            try
            {
                // 1) AnimationManager
                var mgr = root.GetComponent<MayaAnimationManager>();
                if (mgr == null) mgr = root.AddComponent<MayaAnimationManager>();
                mgr.InitializeFromScene(scene);

                // 2) Constraint/IK/Expression retention (C-6)
                MayaConstraintRetentionComponent.Capture_BestEffort(root, scene, log);

                // 3) Audit component (C-7 data)
                var audit = EnsureAuditComponent(root, scene, mgr, log);

                // 4) Skip if nothing to bake
                if (!mgr.HasAnimCurves && !mgr.HasConstraints && !mgr.HasExpressions)
                {
                    log.Info("[PhaseC] No animation/constraints/expressions detected. Skip auto-bake.");
                    audit.MarkNoBake();
                    return;
                }

                // 5) time range
                ResolveTimeRange_BestEffort(root, out var start, out var end, out var fps, log);
                fps = Mathf.Clamp(fps, 1f, 240f);
                if (end <= start) end = start + 1f;

                // 6) Bake (C-4)
                var clip = BakeByMayaEvaluation_ChangedOnly(root, mgr, start, end, fps, bakeBlendShapes: true, log,
                    out var bakeStats);

                if (clip == null)
                {
                    log.Warn("[PhaseC] Auto-bake failed (clip null). Data is preserved; runtime evaluation remains available.");
                    audit.MarkBakeFailed(start, end, fps);
                    return;
                }

                clip.name = string.IsNullOrEmpty(options.AnimationClipName)
                    ? (root.name + "_Baked")
                    : (options.AnimationClipName + "_Baked");

                // 7) Register clip to Animation (sub-asset pipeline can pick it up)
                var anim = root.GetComponent<UnityEngine.Animation>();
                if (anim == null) anim = root.AddComponent<UnityEngine.Animation>();

                anim.AddClip(clip, clip.name);
                anim.clip = clip;

                if (mgr.Clips == null) mgr.Clips = new List<AnimationClip>();
                mgr.Clips.Add(clip);

                clip.EnsureQuaternionContinuity();

                // 8) Determinism hash (C-5)
                var newHash = MayaClipDeterminism.ComputeStableHash(clip);
                audit.SetBakeResult(
                    start, end, fps,
                    clip.name,
                    bakeStats.curveCount,
                    bakeStats.keyCount,
                    newHash);

                log.Info($"[PhaseC] Auto-baked AnimationClip created: name='{clip.name}' range=[{start},{end}] fps={fps} curves={bakeStats.curveCount} keys={bakeStats.keyCount} hash={newHash}");
            }
            catch (Exception e)
            {
                log.Warn("[PhaseC] Exception in auto-bake: " + e.GetType().Name + ": " + e.Message);
            }
        }

        // ----------------------------
        // Time range
        // ----------------------------
        private static void ResolveTimeRange_BestEffort(GameObject root, out float start, out float end, out float fps, MayaImportLog log)
        {
            start = MayaTimeState.StartFrame;
            end = MayaTimeState.EndFrame;
            fps = MayaTimeState.Fps;

            try
            {
                // TimeNode互換探し：startFrame/endFrame/framesPerSecond を持つ Component
                var comps = root.GetComponentsInChildren<Component>(true);
                for (int i = 0; i < comps.Length; i++)
                {
                    var c = comps[i];
                    if (c == null) continue;

                    var t = c.GetType();
                    var tn = t.Name ?? "";

                    // 近い名前だけ見る（ノイズ削減）
                    if (!tn.Contains("Time", StringComparison.OrdinalIgnoreCase) &&
                        !tn.Contains("time", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (TryReadFloatMember(c, "startFrame", out var s) &&
                        TryReadFloatMember(c, "endFrame", out var e))
                    {
                        start = s;
                        end = e;

                        if (TryReadFloatMember(c, "framesPerSecond", out var f) && f > 0)
                            fps = f;

                        MayaTimeState.Fps = fps;
                        MayaTimeState.SetRange(start, end);

                        if (TryReadFloatMember(c, "currentFrame", out var cf))
                            MayaTimeState.CurrentFrame = cf;

                        log?.Info($"[PhaseC] Time range from '{t.FullName}': start={start} end={end} fps={fps}");
                        return;
                    }
                }
            }
            catch { }

            log?.Info($"[PhaseC] Time range from MayaTimeState: start={start} end={end} fps={fps}");
        }

        // ----------------------------
        // Bake (Changed-only) : C-4
        // ----------------------------
        private struct BakeStats
        {
            public int curveCount;
            public int keyCount;
        }

        private static AnimationClip BakeByMayaEvaluation_ChangedOnly(
            GameObject root,
            MayaAnimationManager mgr,
            float startFrame,
            float endFrame,
            float fps,
            bool bakeBlendShapes,
            MayaImportLog log,
            out BakeStats stats)
        {
            stats = default;

            int s = Mathf.FloorToInt(Mathf.Min(startFrame, endFrame));
            int e = Mathf.CeilToInt(Mathf.Max(startFrame, endFrame));
            int step = 1;

            var transforms = root.GetComponentsInChildren<Transform>(true);
            var smrs = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);

            var clip = new AnimationClip { frameRate = fps };

            // state per transform
            var states = new Dictionary<Transform, TrState>(transforms.Length);
            for (int i = 0; i < transforms.Length; i++)
            {
                var t = transforms[i];
                if (t == null) continue;
                states[t] = new TrState();
            }

            // blendshape state
            Dictionary<(SkinnedMeshRenderer smr, int index), BsState> bsStates = null;
            if (bakeBlendShapes)
            {
                bsStates = new Dictionary<(SkinnedMeshRenderer, int), BsState>(512);
                for (int i = 0; i < smrs.Length; i++)
                {
                    var r = smrs[i];
                    if (r == null || r.sharedMesh == null) continue;

                    int n = r.sharedMesh.blendShapeCount;
                    for (int k = 0; k < n; k++)
                        bsStates[(r, k)] = new BsState();
                }
            }

            // extra evaluators (reflection)
            var evalCalls = DiscoverEvaluateNowCalls(log);

            // bake loop
            for (int f = s; f <= e; f += step)
            {
                float timeSec = (f - s) / fps;

                MayaTimeState.Fps = fps;
                MayaTimeState.CurrentFrame = f;

                mgr.EvaluateNow(f);

                for (int i = 0; i < evalCalls.Count; i++)
                {
                    try { evalCalls[i].Invoke(f, root); }
                    catch { /* best-effort */ }
                }

                // record TRS only if changed
                for (int i = 0; i < transforms.Length; i++)
                {
                    var t = transforms[i];
                    if (t == null) continue;

                    var st = states[t];
                    var lp = t.localPosition;
                    var lr = t.localRotation;
                    var ls = t.localScale;

                    st.Record(timeSec, lp, lr, ls);
                    states[t] = st;
                }

                // record blendshapes only if changed
                if (bakeBlendShapes && bsStates != null)
                {
                    for (int i = 0; i < smrs.Length; i++)
                    {
                        var r = smrs[i];
                        if (r == null || r.sharedMesh == null) continue;

                        int n = r.sharedMesh.blendShapeCount;
                        for (int k = 0; k < n; k++)
                        {
                            var key = (r, k);
                            var st = bsStates[key];
                            var w = r.GetBlendShapeWeight(k);
                            st.Record(timeSec, w);
                            bsStates[key] = st;
                        }
                    }
                }
            }

            // write curves (only those that have keys and actually changed)
            int curveCount = 0;
            int keyCount = 0;

            for (int i = 0; i < transforms.Length; i++)
            {
                var t = transforms[i];
                if (t == null) continue;

                var st = states[t];
                if (!st.HasAnyChange) continue; // C-4: 全く変化しないTransformはスキップ

                string path = AnimationUtility.CalculateTransformPath(t, root.transform);

                curveCount += st.WriteToClip(clip, path, ref keyCount);
            }

            if (bakeBlendShapes && bsStates != null)
            {
                foreach (var kv in bsStates)
                {
                    var r = kv.Key.smr;
                    int idx = kv.Key.index;
                    var st = kv.Value;

                    if (r == null || r.sharedMesh == null) continue;
                    if (!st.HasChange) continue;

                    string path = AnimationUtility.CalculateTransformPath(r.transform, root.transform);
                    string bsName = r.sharedMesh.GetBlendShapeName(idx);
                    var curve = st.curve;

                    AnimationUtility.SetEditorCurve(
                        clip,
                        EditorCurveBinding.FloatCurve(path, typeof(SkinnedMeshRenderer), "blendShape." + bsName),
                        curve);

                    curveCount++;
                    keyCount += curve.length;
                }
            }

            stats.curveCount = curveCount;
            stats.keyCount = keyCount;

            // Clipが完全に空（何も変化しない）ならnull扱いにせず、空clipを返す（証拠として残す）
            return clip;
        }

        // ----------------------------
        // Transform state with change detection
        // ----------------------------
        private struct TrState
        {
            // previous
            private bool _hasPrev;
            private Vector3 _prevP;
            private Quaternion _prevR;
            private Vector3 _prevS;

            // curves
            private AnimationCurve _px, _py, _pz;
            private AnimationCurve _rx, _ry, _rz, _rw;
            private AnimationCurve _sx, _sy, _sz;

            // change flags
            private bool _pChanged;
            private bool _rChanged;
            private bool _sChanged;

            public bool HasAnyChange => _pChanged || _rChanged || _sChanged;

            public void Record(float timeSec, Vector3 p, Quaternion r, Vector3 s)
            {
                if (!_hasPrev)
                {
                    _hasPrev = true;
                    _prevP = p;
                    _prevR = r;
                    _prevS = s;

                    // first key always (if we end up changing later, we want a proper start)
                    EnsureCurves();
                    AddP(timeSec, p);
                    AddR(timeSec, r);
                    AddS(timeSec, s);
                    return;
                }

                // detect changes
                bool pCh = (p - _prevP).sqrMagnitude > PosEps;
                bool sCh = (s - _prevS).sqrMagnitude > ScaleEps;
                bool rCh = QuaternionDotDelta(_prevR, r) > RotEps;

                if (pCh) { EnsureCurves(); AddP(timeSec, p); _pChanged = true; _prevP = p; }
                if (rCh) { EnsureCurves(); AddR(timeSec, r); _rChanged = true; _prevR = r; }
                if (sCh) { EnsureCurves(); AddS(timeSec, s); _sChanged = true; _prevS = s; }

                // If nothing changed, do nothing (C-4)
            }

            public int WriteToClip(AnimationClip clip, string path, ref int keyCount)
            {
                int c = 0;
                EnsureCurves();

                if (_pChanged)
                {
                    AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalPosition.x"), _px);
                    AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalPosition.y"), _py);
                    AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalPosition.z"), _pz);
                    c += 3;
                    keyCount += _px.length + _py.length + _pz.length;
                }

                if (_rChanged)
                {
                    AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation.x"), _rx);
                    AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation.y"), _ry);
                    AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation.z"), _rz);
                    AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation.w"), _rw);
                    c += 4;
                    keyCount += _rx.length + _ry.length + _rz.length + _rw.length;
                }

                if (_sChanged)
                {
                    AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalScale.x"), _sx);
                    AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalScale.y"), _sy);
                    AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalScale.z"), _sz);
                    c += 3;
                    keyCount += _sx.length + _sy.length + _sz.length;
                }

                return c;
            }

            private void EnsureCurves()
            {
                _px ??= new AnimationCurve();
                _py ??= new AnimationCurve();
                _pz ??= new AnimationCurve();

                _rx ??= new AnimationCurve();
                _ry ??= new AnimationCurve();
                _rz ??= new AnimationCurve();
                _rw ??= new AnimationCurve();

                _sx ??= new AnimationCurve();
                _sy ??= new AnimationCurve();
                _sz ??= new AnimationCurve();
            }

            private void AddP(float t, Vector3 p)
            {
                _px.AddKey(t, p.x);
                _py.AddKey(t, p.y);
                _pz.AddKey(t, p.z);
            }

            private void AddR(float t, Quaternion r)
            {
                _rx.AddKey(t, r.x);
                _ry.AddKey(t, r.y);
                _rz.AddKey(t, r.z);
                _rw.AddKey(t, r.w);
            }

            private void AddS(float t, Vector3 s)
            {
                _sx.AddKey(t, s.x);
                _sy.AddKey(t, s.y);
                _sz.AddKey(t, s.z);
            }

            private static float QuaternionDotDelta(Quaternion a, Quaternion b)
            {
                // 1 - |dot| が小さいほど近い
                float dot = Mathf.Abs(Quaternion.Dot(a, b));
                return 1f - dot;
            }
        }

        // ----------------------------
        // Blendshape state with change detection
        // ----------------------------
        private struct BsState
        {
            public AnimationCurve curve;
            private bool _hasPrev;
            private float _prev;
            private bool _changed;

            public bool HasChange => _changed && curve != null && curve.length > 0;

            public void Record(float timeSec, float w)
            {
                curve ??= new AnimationCurve();

                if (!_hasPrev)
                {
                    _hasPrev = true;
                    _prev = w;
                    curve.AddKey(timeSec, w);
                    return;
                }

                if (Mathf.Abs(w - _prev) > BlendShapeEps)
                {
                    _changed = true;
                    _prev = w;
                    curve.AddKey(timeSec, w);
                }
            }
        }

        // ----------------------------
        // Evaluator discovery (reflection) - unchanged concept
        // ----------------------------
        private delegate void EvalCall(int frame, GameObject root);

        private static List<EvalCall> DiscoverEvaluateNowCalls(MayaImportLog log)
        {
            var list = new List<EvalCall>(8);

            try
            {
                var asms = AppDomain.CurrentDomain.GetAssemblies();
                for (int ai = 0; ai < asms.Length; ai++)
                {
                    var asm = asms[ai];
                    Type[] types;
                    try { types = asm.GetTypes(); }
                    catch { continue; }

                    for (int ti = 0; ti < types.Length; ti++)
                    {
                        var t = types[ti];
                        if (t == null) continue;

                        var ns = t.Namespace ?? "";
                        if (!ns.Contains("Maya", StringComparison.OrdinalIgnoreCase)) continue;

                        // keep noise low: only likely manager/applier classes
                        var name = t.Name ?? "";
                        if (!name.Contains("Manager", StringComparison.OrdinalIgnoreCase) &&
                            !name.Contains("Constraint", StringComparison.OrdinalIgnoreCase) &&
                            !name.Contains("Ik", StringComparison.OrdinalIgnoreCase) &&
                            !name.Contains("Euler", StringComparison.OrdinalIgnoreCase) &&
                            !name.Contains("Motion", StringComparison.OrdinalIgnoreCase))
                            continue;

                        TryRegisterStatic(t, "EvaluateNow", list);
                        TryRegisterStatic(t, "ApplyAllNow", list);
                    }
                }
            }
            catch { }

            log?.Info($"[PhaseC] Discovered extra evaluators: {list.Count}");
            return list;
        }

        private static void TryRegisterStatic(Type t, string methodName, List<EvalCall> list)
        {
            const BindingFlags BF = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

            MethodInfo m0 = t.GetMethod(methodName, BF, null, Type.EmptyTypes, null);
            if (m0 != null && m0.ReturnType == typeof(void))
            {
                list.Add((frame, root) => m0.Invoke(null, null));
                return;
            }

            MethodInfo m1 = t.GetMethod(methodName, BF, null, new[] { typeof(int) }, null);
            if (m1 != null && m1.ReturnType == typeof(void))
            {
                list.Add((frame, root) => m1.Invoke(null, new object[] { frame }));
                return;
            }

            MethodInfo mT = t.GetMethod(methodName, BF, null, new[] { typeof(Transform) }, null);
            if (mT != null && mT.ReturnType == typeof(void))
            {
                list.Add((frame, root) => mT.Invoke(null, new object[] { root != null ? root.transform : null }));
                return;
            }
        }

        // ----------------------------
        // Audit (C-7)
        // ----------------------------
        private static MayaAnimationAuditComponent EnsureAuditComponent(GameObject root, MayaSceneData scene, MayaAnimationManager mgr, MayaImportLog log)
        {
            var audit = root.GetComponent<MayaAnimationAuditComponent>();
            if (audit == null) audit = root.AddComponent<MayaAnimationAuditComponent>();

            audit.hasAnimCurves = mgr.HasAnimCurves;
            audit.hasConstraints = mgr.HasConstraints;
            audit.hasExpressions = mgr.HasExpressions;

            try
            {
                var rows = MayaAnimationEvaluationLimitationsReporter.Collect(scene);
                audit.SetFrom(rows);
                log.Info($"[PhaseC] Animation audit attached. rows={rows.Count}");
            }
            catch
            {
                // best-effort
            }

            return audit;
        }

        // ----------------------------
        // Reflection helpers
        // ----------------------------
        private static bool TryReadFloatMember(object obj, string memberName, out float value)
        {
            value = 0f;
            if (obj == null || string.IsNullOrEmpty(memberName)) return false;

            var t = obj.GetType();

            var f = t.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null)
            {
                if (TryConvertToFloat(f.GetValue(obj), out value)) return true;
            }

            var p = t.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p != null && p.CanRead)
            {
                if (TryConvertToFloat(p.GetValue(obj, null), out value)) return true;
            }

            return false;
        }

        private static bool TryConvertToFloat(object o, out float value)
        {
            value = 0f;
            if (o == null) return false;

            if (o is float f) { value = f; return true; }
            if (o is double d) { value = (float)d; return true; }
            if (o is int i) { value = i; return true; }
            if (o is long l) { value = l; return true; }
            if (o is short s) { value = s; return true; }

            try { value = Convert.ToSingle(o); return true; }
            catch { return false; }
        }
    }
}
#endif
