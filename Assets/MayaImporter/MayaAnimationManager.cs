// MAYAIMPORTER_PATCH_V4: mb provenance/evidence + audit determinism (generated 2026-01-05)
using System;
using System.Collections.Generic;
using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Animation
{
    /// <summary>
    /// Mayaアニメーションの集約・管理コンポーネント。
    ///
    /// 100%方針:
    /// - scene内の animCurve / expression / constraints 等は MayaNodeComponentBase で保持されている
    /// - Unity側では "再生" と "保持" を分離し、保持=100%を先に成立させる
    /// - 再生は MayaTimeEvaluationPlayer で最低限 + 拡張フックを提供
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MayaAnimationManager : MonoBehaviour
    {
        [Header("Clips (Unity)")]
        public List<AnimationClip> Clips = new List<AnimationClip>();

        [Header("Playback")]
        public MayaTimeEvaluationPlayer Player;

        [Header("Audit")]
        public bool HasAnimCurves;
        public bool HasConstraints;
        public bool HasExpressions;
        public string Note;

        public void InitializeFromScene(MayaSceneData scene)
        {
            HasAnimCurves = false;
            HasConstraints = false;
            HasExpressions = false;

            if (scene != null && scene.Nodes != null)
            {
                foreach (var kv in scene.Nodes)
                {
                    var n = kv.Value;
                    if (n == null) continue;

                    var t = n.NodeType ?? "";
                    if (t.StartsWith("animCurve", StringComparison.Ordinal))
                        HasAnimCurves = true;
                    if (t.IndexOf("constraint", StringComparison.OrdinalIgnoreCase) >= 0)
                        HasConstraints = true;
                    if (string.Equals(t, "expression", StringComparison.OrdinalIgnoreCase))
                        HasExpressions = true;
                }
            }

            if (Player == null)
                Player = GetComponent<MayaTimeEvaluationPlayer>() ?? gameObject.AddComponent<MayaTimeEvaluationPlayer>();

            Note =
                $"Animation audit: clips={Clips.Count}, animCurves={(HasAnimCurves ? "yes" : "no")}, " +
                $"constraints={(HasConstraints ? "yes" : "no")}, expressions={(HasExpressions ? "yes" : "no")}.";
        }

        public void PlayFirstClip()
        {
            if (Player == null)
                Player = GetComponent<MayaTimeEvaluationPlayer>() ?? gameObject.AddComponent<MayaTimeEvaluationPlayer>();

            if (Clips == null || Clips.Count == 0) return;

            Player.Clip = Clips[0];
            Player.PlayOnStart = true;
            Player.Play();
        }

        /// <summary>
        /// Editor/Tools API: Evaluate immediately at the given Maya frame.
        /// Used by MayaBakeAnimationWindow.
        /// </summary>
        public void EvaluateNow(float frame)
        {
            if (Player == null)
                Player = GetComponent<MayaTimeEvaluationPlayer>() ?? gameObject.AddComponent<MayaTimeEvaluationPlayer>();

            // If a clip exists and Player has none assigned yet, use first.
            if (Player.Clip == null && Clips != null && Clips.Count > 0)
                Player.Clip = Clips[0];

            Player.EvaluateAtFrame(frame);
        }
    }
}
