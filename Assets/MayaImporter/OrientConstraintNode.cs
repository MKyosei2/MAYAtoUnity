// PATCH: ProductionImpl v6 (Unity-only, retention-first)
using System.Globalization;
using System.Collections.Generic;
using UnityEngine;
using MayaImporter.Core;
using MayaImporter.Components;
using MayaImporter.Constraints;
using System.Text.RegularExpressions;

namespace MayaImporter.Animation
{
    /// <summary>
    /// Maya orientConstraint
    /// - Unity-only reconstruction via MayaConstraintDriver
    /// - Preserves target indexing (tg[i]) by hole-filling lists
    /// - Best-effort support for:
    ///   - maintainOffset (.mo)
    ///   - interpolation (.int / .interpType) & cache (.inc / .interpCache)
    ///   - rest position (.erp / enableRestPosition) and restRotate
    ///   - driven-axis inference (connections to rotateX/Y/Z)
    ///
    /// Important: This is the single implementation mapped to nodeType "orientConstraint".
    /// </summary>
    [MayaNodeType("orientConstraint")]
    public sealed class OrientConstraintNode : MayaNodeComponentBase
    {
        public override void ApplyToUnity(MayaImportOptions options, MayaImportLog log)
        {
            var meta = GetOrAdd<MayaConstraintMetadata>();
            meta.constraintType = "orient";
            meta.maintainOffset = ReadBool(".mo", false);

            meta.targets.Clear();

            // Rotation interpolation (Maya: 0=NoFlip 1=Average 2=Shortest 3=Longest 4=Cache)
            meta.interpType = ReadInt(".int", ReadInt(".interpType", 1));
            meta.interpCache = ReadInt(".inc", ReadInt(".interpCache", 0));

            // Rest position
            meta.enableRestPosition = ReadBool(".erp", ReadBool(".enableRestPosition", false));
            meta.restRotate = ReadVec3Any(Vector3.zero,
                ".rsrr", ".rrx", ".rry", ".rrz",
                "restRotate", "restRotateX", "restRotateY", "restRotateZ");

            // Constraint-level rotate offset
            var offR = ReadVec3(".o", ".ox", ".oy", ".oz", Vector3.zero);

            // Target indices: keep alignment with tg[index]
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
                    ResolveIncomingSourceNode($".tg[{ti}].targetRotate") ??
                    ResolveIncomingSourceNode($"tg[{ti}].targetRotate") ??
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
                log?.Warn($"[orientConstraint] constrained not found: '{constrainedName}'");
                return;
            }

            InferDrivenAxes(constrainedName, out meta.driveRotX, out meta.driveRotY, out meta.driveRotZ, "rotate");

            var driver = GetOrAdd<MayaConstraintDriver>();
            driver.Constrained = constrainedTf;
            driver.Kind = MayaConstraintKind.Orient;
            driver.DriveLocalChannels = true;
            driver.MaintainOffset = meta.maintainOffset;
            driver.Priority = 0;

            driver.DriveRotX = meta.driveRotX;
            driver.DriveRotY = meta.driveRotY;
            driver.DriveRotZ = meta.driveRotZ;

            driver.EnableRestPosition = meta.enableRestPosition;
            driver.RestRotateWorldEuler = meta.restRotate;

            driver.RotationInterpType = meta.interpType;
            driver.RotationInterpCache = meta.interpCache;

            driver.OffsetTranslate = Vector3.zero;
            driver.OffsetRotateEuler = offR;
            driver.OffsetScale = Vector3.one;

            // ���d�v: Targets �� tg index �Ɠ����Y���ŕ��ׂ�i�����߁j
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
            log?.Info($"[orientConstraint] constrained='{MayaPlugUtil.LeafName(constrainedName)}' targetsIndexMax={maxIndex} mo={meta.maintainOffset} int={meta.interpType}");
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
                    dstAttr == "r" || dstAttr == "rx" || dstAttr == "ry" || dstAttr == "rz";

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

        private void InferDrivenAxes(string constrainedNodeName, out bool x, out bool y, out bool z, string channel)
        {
            x = y = z = true;

            if (Connections == null || Connections.Count == 0 || string.IsNullOrEmpty(constrainedNodeName))
                return;

            bool anyAxis = false;
            bool full = false;

            for (int i = 0; i < Connections.Count; i++)
            {
                var c = Connections[i];
                if (c == null) continue;

                if (c.RoleForThisNode != ConnectionRole.Source && c.RoleForThisNode != ConnectionRole.Both)
                    continue;

                if (!string.Equals(c.DstNodePart, constrainedNodeName, System.StringComparison.Ordinal))
                    continue;

                var attr = MayaPlugUtil.ExtractAttrPart(c.DstPlug) ?? "";

                if (channel == "rotate")
                {
                    if (attr == "rotate" || attr == "r") full = true;
                    if (attr == "rotateX" || attr == "rx") anyAxis = true;
                    if (attr == "rotateY" || attr == "ry") anyAxis = true;
                    if (attr == "rotateZ" || attr == "rz") anyAxis = true;
                }
            }

            if (full || !anyAxis)
            {
                x = y = z = true;
                return;
            }

            bool px = false, py = false, pz = false;

            for (int i = 0; i < Connections.Count; i++)
            {
                var c = Connections[i];
                if (c == null) continue;
                if (c.RoleForThisNode != ConnectionRole.Source && c.RoleForThisNode != ConnectionRole.Both) continue;
                if (!string.Equals(c.DstNodePart, constrainedNodeName, System.StringComparison.Ordinal)) continue;

                var attr = MayaPlugUtil.ExtractAttrPart(c.DstPlug) ?? "";
                if (attr == "rotateX" || attr == "rx") px = true;
                if (attr == "rotateY" || attr == "ry") py = true;
                if (attr == "rotateZ" || attr == "rz") pz = true;
            }

            x = px;
            y = py;
            z = pz;
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
                TryF(a.Tokens[0], out var vx) && TryF(a.Tokens[1], out var vy) && TryF(a.Tokens[2], out var vz))
                return new Vector3(vx, vy, vz);

            return new Vector3(ReadFloat(x, def.x), ReadFloat(y, def.y), ReadFloat(z, def.z));
        }

        private Vector3 ReadVec3Any(Vector3 def,
            string packedA, string ax, string ay, string az,
            string packedB, string bx, string by, string bz)
        {
            if (TryGetAttr(packedA, out var a) && a.Tokens != null && a.Tokens.Count >= 3 &&
                TryF(a.Tokens[0], out var vx) && TryF(a.Tokens[1], out var vy) && TryF(a.Tokens[2], out var vz))
                return new Vector3(vx, vy, vz);

            if (TryGetAttr(packedB, out var b) && b.Tokens != null && b.Tokens.Count >= 3 &&
                TryF(b.Tokens[0], out vx) && TryF(b.Tokens[1], out vy) && TryF(b.Tokens[2], out vz))
                return new Vector3(vx, vy, vz);

            float x = ReadFloat(ax, float.NaN);
            float y = ReadFloat(ay, float.NaN);
            float z = ReadFloat(az, float.NaN);
            if (float.IsFinite(x) && float.IsFinite(y) && float.IsFinite(z))
                return new Vector3(x, y, z);

            return new Vector3(ReadFloat(bx, def.x), ReadFloat(by, def.y), ReadFloat(bz, def.z));
        }

        private int ReadInt(string key, int def)
        {
            if (!TryGetAttr(key, out var a) || a.Tokens == null || a.Tokens.Count == 0 || a.Tokens[0] == null) return def;
            return int.TryParse(a.Tokens[0].ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : def;
        }

        private float ReadFloat(string key, float def)
        {
            if (!TryGetAttr(key, out var a) || a.Tokens == null || a.Tokens.Count == 0 || a.Tokens[0] == null) return def;
            return float.TryParse(a.Tokens[0].ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : def;
        }

        private bool ReadBool(string key, bool def)
        {
            if (!TryGetAttr(key, out var a) || a.Tokens == null || a.Tokens.Count == 0 || a.Tokens[0] == null) return def;
            var s = a.Tokens[0].ToString();
            if (s == "1" || string.Equals(s, "true", System.StringComparison.OrdinalIgnoreCase)) return true;
            if (s == "0" || string.Equals(s, "false", System.StringComparison.OrdinalIgnoreCase)) return false;
            return def;
        }

        private static bool TryF(object o, out float v)
        {
            v = 0f;
            return o != null && float.TryParse(o.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out v);
        }

        private static float ReadFloatFromAttr(SerializedAttribute a, float def)
        {
            if (a == null || a.Tokens == null || a.Tokens.Count == 0 || a.Tokens[0] == null) return def;
            return float.TryParse(a.Tokens[0].ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : def;
        }

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
// INTEGRATED: OrientConstraintBuilder.cs, OrientConstraintEvalNode.cs
// -----------------------------------------------------------------------------
// MAYAIMPORTER_PATCH_V4: mb provenance/evidence + audit determinism (generated 2026-01-05)

namespace MayaImporter.Phase3.Evaluation
{
    public static class OrientConstraintBuilder
    {
        private static readonly Regex TargetInputRegex =
            new Regex(@"^target\[(?<i>\d+)\]\.(targetRotate|targetParentMatrix|target)", RegexOptions.Compiled);

        private static readonly Regex TargetWeightRegex =
            new Regex(@"^target\[(?<i>\d+)\]\.(targetWeight|targetWeightValue)", RegexOptions.Compiled);

        private static readonly Regex AliasWRegex =
            new Regex(@"^w(?<i>\d+)$", RegexOptions.Compiled);

        public static OrientConstraintEvalNode Build(
            MayaNode constraintNode,
            MayaScene scene)
        {
            var constrainedName = FindConstrained(constraintNode, scene);
            if (constrainedName == null) return null;

            var constrainedGo = GameObject.Find(constrainedName);
            if (constrainedGo == null) return null;

            var constrainedTf = constrainedGo.transform;
            bool maintainOffset = GetBool(constraintNode, "maintainOffset");

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
                if (src.NodeType != "transform" && src.NodeType != "joint") continue;

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
                    offsets.Add(Quaternion.Inverse(t.rotation) * constrainedTf.rotation);
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

            return new OrientConstraintEvalNode(
                constraintNode.NodeName,
                constrainedTf,
                targets,
                offsets,
                weightNodes,
                defaultWeights);
        }

        // ---------- helpers ----------

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

// PATCH: ProductionImpl v6 (Unity-only, retention-first)

namespace MayaImporter.Phase3.Evaluation
{
    public class OrientConstraintEvalNode : EvalNode
    {
        private readonly Transform _constrained;
        private readonly List<Transform> _targets;
        private readonly List<Quaternion> _offsets;
        private readonly List<WeightEvalNode> _weightNodes;
        private readonly List<float> _defaultWeights;

        public OrientConstraintEvalNode(
            string nodeName,
            Transform constrained,
            List<Transform> targets,
            List<Quaternion> offsets,
            List<WeightEvalNode> weightNodes,
            List<float> defaultWeights)
            : base(nodeName)
        {
            _constrained = constrained;
            _targets = targets;
            _offsets = offsets;
            _weightNodes = weightNodes;
            _defaultWeights = defaultWeights;

            for (int i = 0; i < _weightNodes.Count; i++)
                if (_weightNodes[i] != null)
                    AddInput(_weightNodes[i]);
        }

        protected override void Evaluate(EvalContext ctx)
        {
            Quaternion rot = Quaternion.identity;
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

                var r = t.rotation * _offsets[i];
                rot = Quaternion.Slerp(rot, r, w / Mathf.Max(total, Mathf.Epsilon));
            }

            if (total > 0f)
                _constrained.rotation = rot;
        }
    }
}
