// PATCH: ProductionImpl v6 (Unity-only, retention-first)
﻿using System.Globalization;
using System.Collections.Generic;
using UnityEngine;
using MayaImporter.Core;
using MayaImporter.Components;
using MayaImporter.Constraints;
using System.Text.RegularExpressions;

namespace MayaImporter.Animation
{
    [MayaNodeType("aimConstraint")]
    public sealed class AimConstraintNode : MayaNodeComponentBase
    {
        public override void ApplyToUnity(MayaImportOptions options, MayaImportLog log)
        {
            var meta = GetOrAdd<MayaConstraintMetadata>();
            meta.constraintType = "aim";
            meta.maintainOffset = ReadBool(".mo", false);

            meta.targets.Clear();

            meta.aimAxis = ReadVec3(".a", ".ax", ".ay", ".az", Vector3.forward);
            meta.upAxis = ReadVec3(".u", ".ux", ".uy", ".uz", Vector3.up);

            meta.worldUpVector = ReadVec3(".wu", ".wux", ".wuy", ".wuz", Vector3.up);
            int worldUpType = ReadInt(".wut", ReadInt(".worldUpType", 0));

            var offR = ReadVec3(".o", ".ox", ".oy", ".oz", Vector3.zero);

            meta.worldUpObjectNodeName =
                ResolveIncomingSourceNode(".wuo") ??
                ResolveIncomingSourceNode(".worldUpMatrix") ??
                ResolveIncomingSourceNode(".wum");

            var indices = CollectTargetIndices();
            int maxIndex = indices.Count > 0 ? indices[indices.Count - 1] : -1;

            if (maxIndex >= 0)
            {
                for (int k = 0; k <= maxIndex; k++)
                    meta.targets.Add(new MayaConstraintMetadata.Target { targetNodeName = null, weight = 0f, offsetScale = Vector3.one });
            }

            for (int ii = 0; ii < indices.Count; ii++)
            {
                int ti = indices[ii];

                var tName =
                    ResolveIncomingSourceNode($".tg[{ti}].targetTranslate") ??
                    ResolveIncomingSourceNode($"tg[{ti}].targetTranslate") ??
                    ResolveIncomingSourceNode($".tg[{ti}].targetParentMatrix") ??
                    ResolveIncomingSourceNode($"tg[{ti}].targetParentMatrix") ??
                    ResolveIncomingSourceNode($".target[{ti}].targetParentMatrix") ??
                    ResolveIncomingSourceNode($"target[{ti}].targetParentMatrix") ??
                    $"target_{ti}";

                float w = ReadWeightForIndex(ti, 1f);

                if (ti >= 0 && ti < meta.targets.Count)
                {
                    meta.targets[ti] = new MayaConstraintMetadata.Target
                    {
                        targetNodeName = tName,
                        weight = w,
                        offsetTranslate = Vector3.zero,
                        offsetRotate = Vector3.zero,
                        offsetScale = Vector3.one
                    };
                }
            }

            var constrainedName = ResolveConstrainedNodeName();
            var constrainedTf = MayaNodeLookup.FindTransform(constrainedName);

            if (constrainedTf == null)
            {
                log?.Warn($"[aimConstraint] constrained not found: '{constrainedName}'");
                return;
            }

            var driver = GetOrAdd<MayaConstraintDriver>();
            driver.Constrained = constrainedTf;
            driver.Kind = MayaConstraintKind.Aim;
            driver.DriveLocalChannels = true;
            driver.MaintainOffset = meta.maintainOffset;
            driver.Priority = 0;

            driver.AimAxis = meta.aimAxis;
            driver.UpAxis = meta.upAxis;
            driver.WorldUpVector = meta.worldUpVector;
            driver.WorldUpObject = MayaNodeLookup.FindTransform(meta.worldUpObjectNodeName);
            driver.WorldUpType = worldUpType;

            driver.OffsetTranslate = Vector3.zero;
            driver.OffsetRotateEuler = offR;
            driver.OffsetScale = Vector3.one;

            // ★重要: Targets を tg index と同じ添字で並べる（穴埋め）
            driver.Targets.Clear();
            if (maxIndex >= 0)
            {
                for (int k = 0; k <= maxIndex; k++)
                {
                    driver.Targets.Add(new MayaConstraintDriver.Target
                    {
                        Transform = null,
                        Weight = 0f,
                        Offset = Matrix4x4.identity,
                        OffsetAuthored = false,
                        ScaleOffset = Vector3.one,
                        ScaleOffsetAuthored = false
                    });
                }

                for (int ti = 0; ti <= maxIndex; ti++)
                {
                    if (ti < 0 || ti >= meta.targets.Count) continue;
                    var t = meta.targets[ti];
                    if (string.IsNullOrEmpty(t.targetNodeName)) continue;

                    var tf = MayaNodeLookup.FindTransform(t.targetNodeName);
                    driver.Targets[ti].Transform = tf;
                    driver.Targets[ti].Weight = Mathf.Max(0f, t.weight);
                }
            }

            driver.ForceReinitializeOffsets();
            log?.Info($"[aimConstraint] constrained='{MayaPlugUtil.LeafName(constrainedName)}' targetsIndexMax={maxIndex} wut={worldUpType} mo={driver.MaintainOffset}");
        }

        private string ResolveConstrainedNodeName()
        {
            if (Connections == null || Connections.Count == 0) return null;

            string best = null;

            for (int i = 0; i < Connections.Count; i++)
            {
                var c = Connections[i];
                if (c == null) continue;

                if (c.RoleForThisNode != ConnectionRole.Source &&
                    c.RoleForThisNode != ConnectionRole.Both)
                    continue;

                if (string.IsNullOrEmpty(c.DstPlug)) continue;

                var dstAttr = MayaPlugUtil.ExtractAttrPart(c.DstPlug) ?? "";
                bool looksLikeConstrained =
                    dstAttr.StartsWith("rotate", System.StringComparison.Ordinal) ||
                    dstAttr == "r";

                if (looksLikeConstrained)
                    return c.DstNodePart;

                best ??= c.DstNodePart;
            }

            return best;
        }

        private string ResolveIncomingSourceNode(string dstPlugSuffix)
        {
            if (Connections == null || Connections.Count == 0) return null;

            var suf = NormalizeSuffix(dstPlugSuffix);

            for (int i = 0; i < Connections.Count; i++)
            {
                var c = Connections[i];
                if (c == null) continue;

                if (c.RoleForThisNode != ConnectionRole.Destination &&
                    c.RoleForThisNode != ConnectionRole.Both)
                    continue;

                if (string.IsNullOrEmpty(c.DstPlug)) continue;

                if (EndsWithSuffixCompat(c.DstPlug, suf))
                {
                    if (!string.IsNullOrEmpty(c.SrcNodePart)) return c.SrcNodePart;
                    return MayaPlugUtil.ExtractNodePart(c.SrcPlug);
                }
            }

            return null;
        }

        private List<int> CollectTargetIndices()
        {
            var set = new HashSet<int>();

            if (Attributes != null)
            {
                for (int i = 0; i < Attributes.Count; i++)
                {
                    var a = Attributes[i];
                    if (a == null || string.IsNullOrEmpty(a.Key)) continue;
                    TryCollectIndexFromString(a.Key, set);
                }
            }

            if (Connections != null)
            {
                for (int i = 0; i < Connections.Count; i++)
                {
                    var c = Connections[i];
                    if (c == null) continue;
                    if (!string.IsNullOrEmpty(c.DstPlug)) TryCollectIndexFromString(c.DstPlug, set);
                }
            }

            var list = new List<int>(set);
            list.Sort();
            return list;
        }

        private static void TryCollectIndexFromString(string s, HashSet<int> set)
        {
            if (string.IsNullOrEmpty(s) || set == null) return;
            if (TryExtractBracketIndex(s, ".tg[", out var i)) set.Add(i);
            if (TryExtractBracketIndex(s, "tg[", out i)) set.Add(i);
            if (TryExtractBracketIndex(s, ".target[", out i)) set.Add(i);
            if (TryExtractBracketIndex(s, "target[", out i)) set.Add(i);
        }

        private static bool TryExtractBracketIndex(string s, string marker, out int idx)
        {
            idx = -1;
            int p = s.IndexOf(marker, System.StringComparison.Ordinal);
            if (p < 0) return false;
            p += marker.Length;
            int r = s.IndexOf(']', p);
            if (r < 0 || r <= p) return false;
            return int.TryParse(s.Substring(p, r - p), out idx) && idx >= 0;
        }

        private float ReadWeightForIndex(int index, float def)
        {
            if (TryGetAttr($".tg[{index}].tw", out var a)) return ReadFloatFromAttr(a, def);
            if (TryGetAttr($"tg[{index}].tw", out a)) return ReadFloatFromAttr(a, def);
            if (TryGetAttr($"target[{index}].targetWeight", out a)) return ReadFloatFromAttr(a, def);
            if (TryGetAttr($".target[{index}].targetWeight", out a)) return ReadFloatFromAttr(a, def);
            if (TryGetAttr($"w{index}", out a)) return ReadFloatFromAttr(a, def);
            if (TryGetAttr($".w{index}", out a)) return ReadFloatFromAttr(a, def);
            return def;
        }

        private Vector3 ReadVec3(string packedOrX, string x, string y, string z, Vector3 def)
        {
            if (TryGetAttr(packedOrX, out var a) && a.Tokens != null && a.Tokens.Count >= 3 &&
                TryF(a.Tokens[0], out var vx) &&
                TryF(a.Tokens[1], out var vy) &&
                TryF(a.Tokens[2], out var vz))
                return new Vector3(vx, vy, vz);

            var sx = ReadFloat(x, def.x);
            var sy = ReadFloat(y, def.y);
            var sz = ReadFloat(z, def.z);
            return new Vector3(sx, sy, sz);
        }

        private int ReadInt(string key, int def)
        {
            if (!TryGetAttr(key, out var a) || a.Tokens == null || a.Tokens.Count == 0) return def;
            return int.TryParse(a.Tokens[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : def;
        }

        private float ReadFloat(string key, float def)
        {
            if (!TryGetAttr(key, out var a) || a.Tokens == null || a.Tokens.Count == 0) return def;
            return TryF(a.Tokens[0], out var f) ? f : def;
        }

        private float ReadFloatFromAttr(SerializedAttribute a, float def)
        {
            if (a == null || a.Tokens == null || a.Tokens.Count == 0) return def;
            return TryF(a.Tokens[0], out var f) ? f : def;
        }

        private bool ReadBool(string key, bool def)
        {
            if (!TryGetAttr(key, out var a) || a.Tokens == null || a.Tokens.Count == 0) return def;
            var s = a.Tokens[0].Trim().ToLowerInvariant();
            if (s == "1" || s == "true") return true;
            if (s == "0" || s == "false") return false;
            return def;
        }

        private static bool TryF(string s, out float f)
            => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out f);

        private static string NormalizeSuffix(string s)
            => string.IsNullOrEmpty(s) ? s : (s.StartsWith(".", System.StringComparison.Ordinal) ? s : "." + s);

        private static bool EndsWithSuffixCompat(string plug, string suffixWithDot)
        {
            if (plug.EndsWith(suffixWithDot, System.StringComparison.Ordinal)) return true;
            var noDot = suffixWithDot.StartsWith(".", System.StringComparison.Ordinal) ? suffixWithDot.Substring(1) : suffixWithDot;
            return plug.EndsWith(noDot, System.StringComparison.Ordinal);
        }

        private T GetOrAdd<T>() where T : Component
            => GetComponent<T>() ?? gameObject.AddComponent<T>();
    }
}


// ----------------------------------------------------------------------------- 
// INTEGRATED: AimConstraintBuilder.cs, AimConstraintEvalNode.cs
// -----------------------------------------------------------------------------
// MAYAIMPORTER_PATCH_V4: mb provenance/evidence + audit determinism (generated 2026-01-05)

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
            // attribute 擾
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
            // target[index] 
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

// PATCH: ProductionImpl v6 (Unity-only, retention-first)

namespace MayaImporter.Phase3.Evaluation
{
    public class AimConstraintEvalNode : EvalNode
    {
        private readonly Transform _constrained;
        private readonly List<Transform> _targets;
        private readonly List<Quaternion> _offsets;
        private readonly List<WeightEvalNode> _weightNodes;
        private readonly List<float> _defaultWeights;

        private readonly Vector3 _aimVector;
        private readonly Vector3 _upVector;
        private readonly Transform _worldUpObject;

        public AimConstraintEvalNode(
            string nodeName,
            Transform constrained,
            List<Transform> targets,
            List<Quaternion> offsets,
            List<WeightEvalNode> weightNodes,
            List<float> defaultWeights,
            Vector3 aimVector,
            Vector3 upVector,
            Transform worldUpObject)
            : base(nodeName)
        {
            _constrained = constrained;
            _targets = targets;
            _offsets = offsets;
            _weightNodes = weightNodes;
            _defaultWeights = defaultWeights;
            _aimVector = aimVector.normalized;
            _upVector = upVector.normalized;
            _worldUpObject = worldUpObject;

            for (int i = 0; i < _weightNodes.Count; i++)
                if (_weightNodes[i] != null)
                    AddInput(_weightNodes[i]);
        }

        protected override void Evaluate(EvalContext ctx)
        {
            Vector3 aimDir = Vector3.zero;
            float total = 0f;

            for (int i = 0; i < _targets.Count; i++)
            {
                var t = _targets[i];
                if (t == null) continue;

                float w = (_weightNodes[i] != null)
                    ? _weightNodes[i].Value
                    : _defaultWeights[i];

                if (w <= 0f) continue;

                total += w;
                aimDir += (t.position - _constrained.position).normalized * w;
            }

            if (total <= 0f)
                return;

            aimDir.Normalize();

            Vector3 up = _worldUpObject != null
                ? _worldUpObject.up
                : Vector3.up;

            Quaternion aimRot = Quaternion.LookRotation(aimDir, up);

            // Maya �� aimVector / upVector ���l��
            Quaternion axisAdjust = Quaternion.FromToRotation(Vector3.forward, _aimVector);
            Quaternion finalRot = aimRot * Quaternion.Inverse(axisAdjust);

            // offset�imaintainOffset�j
            finalRot *= _offsets[0];

            _constrained.rotation = finalRot;
        }
    }
}
