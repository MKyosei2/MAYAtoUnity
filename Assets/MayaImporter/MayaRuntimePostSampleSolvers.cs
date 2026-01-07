// Assets/MayaImporter/MayaRuntimePostSampleSolvers.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using MayaImporter.Constraints;
using MayaImporter.IK;

namespace MayaImporter.Animation
{
    /// <summary>
    /// Phase5:
    /// - Wires "post animation sample" evaluation order without any Maya/Autodesk API.
    /// - Designed for Unity-only environments.
    ///
    /// Order (best-effort):
    ///   1) Expression (subset) -> writes TRS (Maya space)
    ///   2) Constraints (MayaConstraintManager)
    ///   3) IK (MayaIkManager)
    ///
    /// Why:
    /// - MayaRuntimeGraphEvaluator updates driven channels after clip sampling.
    /// - But constraints/IK (LateUpdate) won't run when scrubbing/baking via EvaluateAtFrame().
    /// - This hook makes scrubbing deterministic: AfterSample => (Expressions/Constraints/IK).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MayaRuntimePostSampleSolvers : MonoBehaviour
    {
        [Header("Enable")]
        public bool enablePostSampleSolvers = true;

        [Tooltip("If false, solvers will only run in PlayMode.")]
        public bool runInEditMode = false;

        [Header("Solvers")]
        public bool enableExpressions = true;
        public bool enableConstraints = true;
        public bool enableIk = true;

        [Header("Stats (debug)")]
        public int expressionSolverCount = 0;

        private MayaTimeEvaluationPlayer _player;
        private readonly List<MayaExpressionRuntime> _expressions = new List<MayaExpressionRuntime>(64);

        public static MayaRuntimePostSampleSolvers EnsureOnRoot(GameObject root)
        {
            if (root == null) return null;
            var c = root.GetComponent<MayaRuntimePostSampleSolvers>();
            if (c == null) c = root.AddComponent<MayaRuntimePostSampleSolvers>();
            c.RebuildCaches();
            return c;
        }

        private void OnEnable()
        {
            Hook();
            RebuildCaches();
        }

        private void OnDisable()
        {
            Unhook();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Keep caches fresh when toggles change in editor
            if (!Application.isPlaying && runInEditMode)
                RebuildCaches();
        }
#endif

        public void RebuildCaches()
        {
            _expressions.Clear();
            GetComponentsInChildren(true, _expressions);
            expressionSolverCount = _expressions.Count;
        }

        private void Hook()
        {
            _player = GetComponent<MayaTimeEvaluationPlayer>();
            if (_player == null) return;

            _player.AfterSample -= OnAfterSample;
            _player.AfterSample += OnAfterSample;
        }

        private void Unhook()
        {
            if (_player != null)
                _player.AfterSample -= OnAfterSample;
        }

        private void OnAfterSample(float frame, float timeSec)
        {
            if (!enablePostSampleSolvers) return;
            if (!Application.isPlaying && !runInEditMode) return;

            // Expression -> Constraints -> IK
            if (enableExpressions && _expressions.Count > 0)
            {
                for (int i = 0; i < _expressions.Count; i++)
                {
                    var e = _expressions[i];
                    if (e == null || !e.isActiveAndEnabled) continue;
                    try { e.Evaluate(frame); }
                    catch { /* keep safe */ }
                }
            }

            if (enableConstraints)
            {
                try { MayaConstraintManager.EvaluateNow(frame); }
                catch { /* keep safe */ }
            }

            if (enableIk)
            {
                try { MayaIkManager.EvaluateNow(); }
                catch { /* keep safe */ }
            }
        }
    }
}
