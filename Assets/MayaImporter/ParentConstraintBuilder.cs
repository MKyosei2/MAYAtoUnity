using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Phase3.Evaluation
{
    public static class ParentConstraintBuilder
    {
        // Mayaの代表的なターゲット入力（これが来ていれば「ターゲット接続」）
        private static readonly Regex TargetInputRegex =
            new Regex(@"^target\[(?<i>\d+)\]\.(targetParentMatrix|targetTranslate|targetRotate|targetScale|targetParent)", RegexOptions.Compiled);

        // Mayaの代表的なウェイト入力（これが来ていれば「ウェイト接続」）
        private static readonly Regex TargetWeightRegex =
            new Regex(@"^target\[(?<i>\d+)\]\.(targetWeight|targetWeight\[\d+\]|targetWeightValue|targetWeight)", RegexOptions.Compiled);

        // まれに w0 / w1 のようなエイリアスが来るケースの保険
        private static readonly Regex AliasWRegex =
            new Regex(@"^w(?<i>\d+)$", RegexOptions.Compiled);

        public static ParentConstraintEvalNode Build(
            MayaNode constraintNode,
            MayaScene scene)
        {
            var constrainedName = FindConstrained(constraintNode, scene);
            if (constrainedName == null) return null;

            var constrainedGo = GameObject.Find(constrainedName);
            if (constrainedGo == null) return null;

            var constrainedTf = constrainedGo.transform;

            bool maintainOffset = GetBool(constraintNode, "maintainOffset");

            // -----------------------------
            // 1) target[index] → targetNodeName を attribute で厳密解決
            // -----------------------------
            var targetByIndex = new Dictionary<int, string>();
            foreach (var c in scene.ConnectionGraph.Connections)
            {
                if (c.DstNode != constraintNode.NodeName) continue;
                if (string.IsNullOrEmpty(c.DstAttr)) continue;

                var m = TargetInputRegex.Match(c.DstAttr);
                if (!m.Success) continue;

                if (!int.TryParse(m.Groups["i"].Value, out int idx)) continue;

                var src = scene.GetNode(c.SrcNode);
                if (src == null) continue;

                // ターゲットは通常 transform/joint
                if (src.NodeType != "transform" && src.NodeType != "joint") continue;

                if (!targetByIndex.ContainsKey(idx))
                    targetByIndex[idx] = c.SrcNode;
            }

            if (targetByIndex.Count == 0)
                return null;

            // index 昇順で確定
            var indices = new List<int>(targetByIndex.Keys);
            indices.Sort();

            // -----------------------------
            // 2) weight[index] を attribute で厳密解決（animCurve接続）
            // -----------------------------
            var weightNodeByIndex = new Dictionary<int, WeightEvalNode>();
            foreach (var c in scene.ConnectionGraph.Connections)
            {
                if (c.DstNode != constraintNode.NodeName) continue;
                if (string.IsNullOrEmpty(c.DstAttr)) continue;

                int idx = -1;

                // target[i].targetWeight 系
                var mw = TargetWeightRegex.Match(c.DstAttr);
                if (mw.Success && int.TryParse(mw.Groups["i"].Value, out int wi))
                    idx = wi;

                // w0 / w1 系エイリアス
                if (idx < 0)
                {
                    var ma = AliasWRegex.Match(c.DstAttr);
                    if (ma.Success && int.TryParse(ma.Groups["i"].Value, out int ai))
                        idx = ai;
                }

                if (idx < 0) continue;

                var src = scene.GetNode(c.SrcNode);
                if (src == null) continue;

                if (!string.IsNullOrEmpty(src.NodeType) && src.NodeType.StartsWith("animCurve"))
                {
                    // 既に登録済みなら上書きしない（複数接続の事故防止）
                    if (!weightNodeByIndex.ContainsKey(idx))
                        weightNodeByIndex[idx] = new WeightEvalNode(src);
                }
            }

            // -----------------------------
            // 3) EvalNode 構築（targets / offsets / weights）
            // -----------------------------
            var targets = new List<Transform>();
            var offsets = new List<Matrix4x4>();
            var weightNodes = new List<WeightEvalNode>();
            var defaultWeights = new List<float>();

            foreach (var idx in indices)
            {
                var tName = targetByIndex[idx];
                var go = GameObject.Find(tName);
                if (go == null) continue;

                var tf = go.transform;
                targets.Add(tf);

                if (maintainOffset)
                    offsets.Add(tf.worldToLocalMatrix * constrainedTf.localToWorldMatrix);
                else
                    offsets.Add(Matrix4x4.identity);

                // weight: animCurve があればそれを使う。なければ MayaNode 属性値 or 1.0
                if (weightNodeByIndex.TryGetValue(idx, out var wNode))
                {
                    weightNodes.Add(wNode);
                    defaultWeights.Add(0f); // 未使用
                }
                else
                {
                    weightNodes.Add(null);
                    defaultWeights.Add(GetInlineWeightFallback(constraintNode, idx, 1f));
                }
            }

            if (targets.Count == 0)
                return null;

            return new ParentConstraintEvalNode(
                constraintNode.NodeName,
                constrainedTf,
                targets,
                offsets,
                weightNodes,
                defaultWeights);
        }

        // -----------------------------
        // helpers
        // -----------------------------

        private static string FindConstrained(MayaNode constraint, MayaScene scene)
        {
            // constraint → constrained (translate/rotate など) を優先
            foreach (var c in scene.ConnectionGraph.Connections)
            {
                if (c.SrcNode != constraint.NodeName) continue;

                var dst = scene.GetNode(c.DstNode);
                if (dst == null) continue;

                if (dst.NodeType == "transform" || dst.NodeType == "joint")
                    return c.DstNode;
            }
            return null;
        }

        private static bool GetBool(MayaNode n, string k)
        {
            if (n.Attributes.TryGetValue(k, out var a))
            {
                if (a.Data?.Value is int i) return i != 0;
                if (a.Data?.Value is bool b) return b;
            }
            return false;
        }

        private static float GetInlineWeightFallback(MayaNode constraintNode, int index, float def)
        {
            // .ma setAttr で "target[0].targetWeight" が保存されている場合がある
            var key1 = $"target[{index}].targetWeight";
            if (constraintNode.Attributes.TryGetValue(key1, out var a))
            {
                if (a.Data?.Value is float f) return f;
                if (a.Data?.Value is int i) return i;
            }

            // w0 のような別名で入っている場合の保険
            var key2 = $"w{index}";
            if (constraintNode.Attributes.TryGetValue(key2, out var b))
            {
                if (b.Data?.Value is float f2) return f2;
                if (b.Data?.Value is int i2) return i2;
            }

            return def;
        }
    }
}
