using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Phase3.Evaluation
{
    public static class PointConstraintBuilder
    {
        private static readonly Regex TargetInputRegex =
            new Regex(@"^target\[(?<i>\d+)\]\.(targetTranslate|targetParentMatrix|target)", RegexOptions.Compiled);

        private static readonly Regex TargetWeightRegex =
            new Regex(@"^target\[(?<i>\d+)\]\.(targetWeight|targetWeightValue)", RegexOptions.Compiled);

        private static readonly Regex AliasWRegex =
            new Regex(@"^w(?<i>\d+)$", RegexOptions.Compiled);

        public static PointConstraintEvalNode Build(
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
            // target[index] âåà
            // -----------------------------
            var targetByIndex = new Dictionary<int, Transform>();

            foreach (var c in scene.ConnectionGraph.Connections)
            {
                if (c.DstNode != constraintNode.NodeName) continue;
                if (string.IsNullOrEmpty(c.DstAttr)) continue;

                var m = TargetInputRegex.Match(c.DstAttr);
                if (!m.Success) continue;

                if (!int.TryParse(m.Groups["i"].Value, out int idx)) continue;

                var srcNode = scene.GetNode(c.SrcNode);
                if (srcNode == null) continue;

                if (srcNode.NodeType != "transform" && srcNode.NodeType != "joint")
                    continue;

                var go = GameObject.Find(srcNode.NodeName);
                if (go == null) continue;

                if (!targetByIndex.ContainsKey(idx))
                    targetByIndex[idx] = go.transform;
            }

            if (targetByIndex.Count == 0)
                return null;

            // index è∏èá
            var indices = new List<int>(targetByIndex.Keys);
            indices.Sort();

            // -----------------------------
            // weight[index] âåà
            // -----------------------------
            var weightNodes = new List<WeightEvalNode>();
            var defaultWeights = new List<float>();
            var targets = new List<Transform>();
            var offsets = new List<Vector3>();

            foreach (var idx in indices)
            {
                var t = targetByIndex[idx];
                targets.Add(t);

                if (maintainOffset)
                    offsets.Add(constrainedTf.position - t.position);
                else
                    offsets.Add(Vector3.zero);

                WeightEvalNode wNode = null;

                foreach (var c in scene.ConnectionGraph.Connections)
                {
                    if (c.DstNode != constraintNode.NodeName) continue;
                    if (string.IsNullOrEmpty(c.DstAttr)) continue;

                    int wi = -1;

                    var mw = TargetWeightRegex.Match(c.DstAttr);
                    if (mw.Success && int.TryParse(mw.Groups["i"].Value, out int tmp))
                        wi = tmp;

                    if (wi < 0)
                    {
                        var ma = AliasWRegex.Match(c.DstAttr);
                        if (ma.Success && int.TryParse(ma.Groups["i"].Value, out int ai))
                            wi = ai;
                    }

                    if (wi != idx) continue;

                    var src = scene.GetNode(c.SrcNode);
                    if (src != null && src.NodeType.StartsWith("animCurve"))
                    {
                        wNode = new WeightEvalNode(src);
                        break;
                    }
                }

                if (wNode != null)
                {
                    weightNodes.Add(wNode);
                    defaultWeights.Add(0f);
                }
                else
                {
                    weightNodes.Add(null);
                    defaultWeights.Add(GetInlineWeight(constraintNode, idx, 1f));
                }
            }

            return new PointConstraintEvalNode(
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

        private static float GetInlineWeight(MayaNode node, int index, float def)
        {
            var k1 = $"target[{index}].targetWeight";
            if (node.Attributes.TryGetValue(k1, out var a))
            {
                if (a.Data?.Value is float f) return f;
                if (a.Data?.Value is int i) return i;
            }

            var k2 = $"w{index}";
            if (node.Attributes.TryGetValue(k2, out var b))
            {
                if (b.Data?.Value is float f) return f;
                if (b.Data?.Value is int i) return i;
            }

            return def;
        }
    }
}
