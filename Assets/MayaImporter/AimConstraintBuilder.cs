using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Phase3.Evaluation
{
    public static class AimConstraintBuilder
    {
        private static readonly Regex TargetInputRegex =
            new Regex(@"^target\[(?<i>\d+)\]\.(targetParentMatrix|target)", RegexOptions.Compiled);

        private static readonly Regex TargetWeightRegex =
            new Regex(@"^target\[(?<i>\d+)\]\.(targetWeight|targetWeightValue)", RegexOptions.Compiled);

        private static readonly Regex AliasWRegex =
            new Regex(@"^w(?<i>\d+)$", RegexOptions.Compiled);

        public static AimConstraintEvalNode Build(
            MayaNode constraintNode,
            MayaScene scene)
        {
            var constrainedName = FindConstrained(constraintNode, scene);
            if (constrainedName == null) return null;

            var constrainedGo = GameObject.Find(constrainedName);
            if (constrainedGo == null) return null;

            var constrainedTf = constrainedGo.transform;

            // -----------------------------
            // attribute Žæ“¾
            // -----------------------------
            Vector3 aimVector = GetVector(constraintNode, "aimVector", Vector3.forward);
            Vector3 upVector = GetVector(constraintNode, "upVector", Vector3.up);
            bool maintainOffset = GetBool(constraintNode, "maintainOffset");

            int worldUpType = GetInt(constraintNode, "worldUpType"); // 0=scene, 1=object
            string worldUpObjectName = FindWorldUpObject(constraintNode, scene);

            Transform worldUpObject = null;
            if (worldUpType == 1 && !string.IsNullOrEmpty(worldUpObjectName))
            {
                var go = GameObject.Find(worldUpObjectName);
                if (go != null)
                    worldUpObject = go.transform;
            }

            // -----------------------------
            // target[index] ‰ðŒˆ
            // -----------------------------
            var targetByIndex = new Dictionary<int, Transform>();

            foreach (var c in scene.ConnectionGraph.Connections)
            {
                if (c.DstNode != constraintNode.NodeName) continue;
                if (string.IsNullOrEmpty(c.DstAttr)) continue;

                var m = TargetInputRegex.Match(c.DstAttr);
                if (!m.Success) continue;

                if (!int.TryParse(m.Groups["i"].Value, out int idx)) continue;

                var src = scene.GetNode(c.SrcNode);
                if (src == null) continue;

                if (src.NodeType != "transform" && src.NodeType != "joint")
                    continue;

                var go = GameObject.Find(src.NodeName);
                if (go == null) continue;

                if (!targetByIndex.ContainsKey(idx))
                    targetByIndex[idx] = go.transform;
            }

            if (targetByIndex.Count == 0)
                return null;

            var indices = new List<int>(targetByIndex.Keys);
            indices.Sort();

            var targets = new List<Transform>();
            var offsets = new List<Quaternion>();
            var weightNodes = new List<WeightEvalNode>();
            var defaultWeights = new List<float>();

            foreach (var idx in indices)
            {
                var t = targetByIndex[idx];
                targets.Add(t);

                if (maintainOffset)
                    offsets.Add(Quaternion.Inverse(Quaternion.LookRotation(
                        (t.position - constrainedTf.position).normalized,
                        Vector3.up)) * constrainedTf.rotation);
                else
                    offsets.Add(Quaternion.identity);

                WeightEvalNode wNode = FindWeightNode(scene, constraintNode, idx);

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

            return new AimConstraintEvalNode(
                constraintNode.NodeName,
                constrainedTf,
                targets,
                offsets,
                weightNodes,
                defaultWeights,
                aimVector,
                upVector,
                worldUpObject);
        }

        // ---------------- helpers ----------------

        private static string FindConstrained(MayaNode constraint, MayaScene scene)
        {
            foreach (var c in scene.ConnectionGraph.Connections)
            {
                if (c.SrcNode != constraint.NodeName) continue;

                var dst = scene.GetNode(c.DstNode);
                if (dst != null && (dst.NodeType == "transform" || dst.NodeType == "joint"))
                    return c.DstNode;
            }
            return null;
        }

        private static WeightEvalNode FindWeightNode(MayaScene scene, MayaNode node, int idx)
        {
            foreach (var c in scene.ConnectionGraph.Connections)
            {
                if (c.DstNode != node.NodeName) continue;
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
                    return new WeightEvalNode(src);
            }
            return null;
        }

        private static Vector3 GetVector(MayaNode n, string key, Vector3 def)
        {
            if (n.Attributes.TryGetValue(key, out var a) && a.Data?.Value is float[] f && f.Length >= 3)
                return new Vector3(f[0], f[1], f[2]);
            return def;
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

        private static int GetInt(MayaNode n, string k)
        {
            if (n.Attributes.TryGetValue(k, out var a))
            {
                if (a.Data?.Value is int i) return i;
            }
            return 0;
        }

        private static string FindWorldUpObject(MayaNode constraint, MayaScene scene)
        {
            foreach (var c in scene.ConnectionGraph.Connections)
            {
                if (c.DstNode != constraint.NodeName) continue;
                if (c.DstAttr == "worldUpMatrix")
                    return c.SrcNode;
            }
            return null;
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
