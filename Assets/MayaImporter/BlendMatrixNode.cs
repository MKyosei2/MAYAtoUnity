// Assets/MayaImporter/BlendMatrixNode.cs
// NodeType: blendMatrix
//
// Phase C: Implemented (best-effort reconstruction)
//
// - Base: inputMatrix
// - Targets: target[i].targetMatrix + target[i].weight
// - Result: sequential TRS blend (t lerp, r slerp, s lerp), ignoring shear/pivots/rotateAxis etc.
// - Publishes output via MayaImporter.Core.MayaMatrixValue (matrixMaya/matrixUnity + mayaMatrix/unityMatrix aliases)
//
// Note:
// Maya blendMatrix has many features (rotate/scale pivots, offsets, useScale, etc).
// This implementation is deterministic and covers the common "blend matrices by weight" usage.

using System;
using System.Collections.Generic;
using UnityEngine;
using MayaImporter.Core;
using MayaImporter.Utils;

namespace MayaImporter.Nodes
{
    [MayaNodeType("blendMatrix")]
    [DisallowMultipleComponent]
    public sealed class BlendMatrixNode : MayaNodeComponentBase
    {
        public override void ApplyToUnity(MayaImportOptions options, MayaImportLog log)
        {
            options ??= new MayaImportOptions();
            log ??= new MayaImportLog();

            var meta = GetComponent<MayaBlendMatrixMetadata>() ?? gameObject.AddComponent<MayaBlendMatrixMetadata>();
            meta.valid = false;

            var outVal = GetComponent<MayaMatrixValue>() ?? gameObject.AddComponent<MayaMatrixValue>();
            outVal.valid = false;

            // Envelope (often exists). Default 1.
            float envelope = ReadFloat(1f, ".envelope", "envelope", ".env", "env");
            envelope = Mathf.Clamp01(envelope);
            meta.envelope = envelope;

            // ---- Base input matrix (prefer incoming) ----
            if (!TryResolveIncomingMatrixByDstContainsAny(
                    new[] { "inputMatrix", ".inputMatrix", ".im", "im", ".inMatrix", "inMatrix" },
                    out var baseM,
                    out var baseSrc))
            {
                baseM = ReadMatrixOrIdentity(
                    ".inputMatrix", "inputMatrix",
                    ".inMatrix", "inMatrix",
                    ".matrix", "matrix",
                    ".im", "im"
                );
                baseSrc = "LocalAttr";
            }

            meta.baseSource = baseSrc;
            meta.baseMatrixMaya = baseM;

            // ---- Collect targets from attributes + connections ----
            var targets = CollectTargets(options, log);

            // Sort targets by index ascending (deterministic)
            targets.Sort((a, b) => a.index.CompareTo(b.index));
            meta.targets = targets;

            // ---- Blend ----
            Matrix4x4 result = baseM;

            for (int i = 0; i < targets.Count; i++)
            {
                var t = targets[i];
                float w = Mathf.Clamp01(t.weight) * envelope;
                if (w <= 0f) continue;

                result = BlendTRS(result, t.targetMatrixMaya, w);
            }

            meta.outputMatrixMaya = result;
            meta.outputMatrixUnity = MayaToUnityConversion.ConvertMatrix(result, options.Conversion);

            outVal.valid = true;
            // canonical + alias (Core.MayaMatrixValue keeps both names)
            outVal.matrixMaya = meta.outputMatrixMaya;
            outVal.matrixUnity = meta.outputMatrixUnity;

            meta.valid = true;
            meta.lastBuildFrame = Time.frameCount;

            log.Info($"[blendMatrix] '{NodeName}' env={envelope:0.###} base='{baseSrc}' targets={targets.Count} out(Maya)t=({result.m03:0.###},{result.m13:0.###},{result.m23:0.###})");
        }

        // --------------------------
        // Target collection
        // --------------------------

        private List<MayaBlendMatrixTarget> CollectTargets(MayaImportOptions options, MayaImportLog log)
        {
            var list = new List<MayaBlendMatrixTarget>(8);

            // 1) From raw attributes: target[<i>].targetMatrix / target[<i>].weight (or close variants)
            if (Attributes != null)
            {
                // temporary maps
                var matByIndex = new Dictionary<int, (string key, Matrix4x4 m)>();
                var wByIndex = new Dictionary<int, (string key, float w)>();

                for (int i = 0; i < Attributes.Count; i++)
                {
                    var a = Attributes[i];
                    if (a == null) continue;

                    var key = a.Key ?? "";
                    if (key.Length == 0) continue;

                    // weight
                    if (LooksLikeTargetWeightKey(key))
                    {
                        int idx = ExtractBracketIndexOrMinus1(key);
                        if (idx < 0) idx = 0;

                        if (a.Tokens != null && a.Tokens.Count > 0 && MathUtil.TryParseFloat(a.Tokens[0], out var w))
                            wByIndex[idx] = (key, w);

                        continue;
                    }

                    // targetMatrix
                    if (LooksLikeTargetMatrixKey(key))
                    {
                        int idx = ExtractBracketIndexOrMinus1(key);
                        if (idx < 0) idx = 0;

                        if (a.Tokens != null && a.Tokens.Count >= 16 && MatrixUtil.TryParseMatrix4x4(a.Tokens, 0, out var m))
                            matByIndex[idx] = (key, m);

                        continue;
                    }
                }

                // merge
                foreach (var kv in matByIndex)
                {
                    int idx = kv.Key;
                    float w = 0f;
                    string wSrc = "Attr:(missing weight => 0)";
                    if (wByIndex.TryGetValue(idx, out var ww))
                    {
                        w = ww.w;
                        wSrc = $"Attr:{ww.key}";
                    }

                    list.Add(new MayaBlendMatrixTarget
                    {
                        index = idx,
                        weight = w,
                        weightSource = wSrc,
                        targetMatrixMaya = kv.Value.m,
                        matrixSource = $"Attr:{kv.Value.key}",
                    });
                }

                // weights with no matrices are ignored (common in partially wired graphs)
            }

            // 2) From incoming connections: targetMatrix / weight
            // We do best-effort: if src node has MayaMatrixValue or MayaFloatValue, use it.
            if (Connections != null)
            {
                // We merge into existing entries by index
                for (int i = 0; i < Connections.Count; i++)
                {
                    var c = Connections[i];
                    if (c == null) continue;

                    if (c.RoleForThisNode != ConnectionRole.Destination && c.RoleForThisNode != ConnectionRole.Both)
                        continue;

                    var dst = c.DstPlug ?? "";
                    if (dst.Length == 0) continue;

                    int idx = ExtractBracketIndexOrMinus1(dst);
                    if (idx < 0) idx = 0;

                    // target matrix connection?
                    if (dst.Contains("targetMatrix", StringComparison.Ordinal) || dst.Contains("tmat", StringComparison.Ordinal))
                    {
                        if (TryGetConnectedMatrix(c, out var m, out var srcNode))
                        {
                            var t = FindOrCreate(list, idx);
                            t.targetMatrixMaya = m;
                            t.matrixSource = $"Conn:{srcNode}";
                            Set(list, t);
                        }
                        continue;
                    }

                    // weight connection?
                    if (dst.Contains("weight", StringComparison.Ordinal) || dst.Contains(".w", StringComparison.Ordinal))
                    {
                        if (TryGetConnectedFloat(c, out var w, out var srcNode))
                        {
                            var t = FindOrCreate(list, idx);
                            t.weight = w;
                            t.weightSource = $"Conn:{srcNode}";
                            Set(list, t);
                        }
                        continue;
                    }
                }
            }

            // Ensure deterministic defaults for any created entries
            for (int i = 0; i < list.Count; i++)
            {
                var t = list[i];
                if (t.matrixSource == null) t.matrixSource = "Unknown";
                if (t.weightSource == null) t.weightSource = "Unknown";
                list[i] = t;
            }

            return list;
        }

        private static MayaBlendMatrixTarget FindOrCreate(List<MayaBlendMatrixTarget> list, int idx)
        {
            for (int i = 0; i < list.Count; i++)
                if (list[i].index == idx)
                    return list[i];

            var t = new MayaBlendMatrixTarget
            {
                index = idx,
                weight = 0f,
                weightSource = "Conn:(unset)",
                targetMatrixMaya = Matrix4x4.identity,
                matrixSource = "Conn:(unset)",
            };
            list.Add(t);
            return t;
        }

        private static void Set(List<MayaBlendMatrixTarget> list, MayaBlendMatrixTarget t)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].index == t.index)
                {
                    list[i] = t;
                    return;
                }
            }
            list.Add(t);
        }

        private static bool LooksLikeTargetMatrixKey(string key)
        {
            // common: "target[0].targetMatrix"
            // generated tokens sometimes include ".target[0].targetMatrix"
            return key.Contains("target[", StringComparison.Ordinal) &&
                   (key.Contains("targetMatrix", StringComparison.Ordinal) || key.Contains("tmat", StringComparison.Ordinal));
        }

        private static bool LooksLikeTargetWeightKey(string key)
        {
            // common: "target[0].weight"
            return key.Contains("target[", StringComparison.Ordinal) &&
                   key.Contains("weight", StringComparison.Ordinal);
        }

        private static int ExtractBracketIndexOrMinus1(string s)
        {
            if (string.IsNullOrEmpty(s)) return -1;
            int lb = s.IndexOf('[', StringComparison.Ordinal);
            if (lb < 0) return -1;
            int rb = s.IndexOf(']', lb + 1);
            if (rb < 0) return -1;

            var sub = s.Substring(lb + 1, rb - lb - 1);
            return MathUtil.TryParseInt(sub, out var v) ? v : -1;
        }

        private bool TryGetConnectedMatrix(SerializedConnection c, out Matrix4x4 m, out string srcNode)
        {
            m = Matrix4x4.identity;
            srcNode = null;

            srcNode = c.SrcNodePart;
            if (string.IsNullOrEmpty(srcNode))
                srcNode = MayaPlugUtil.ExtractNodePart(c.SrcPlug);

            if (string.IsNullOrEmpty(srcNode))
                return false;

            var tr = MayaNodeLookup.FindTransform(srcNode);
            if (tr == null) return false;

            var mv = tr.GetComponent<MayaMatrixValue>();
            if (mv != null && mv.valid)
            {
                m = mv.matrixMaya;
                return true;
            }

            return false;
        }

        private bool TryGetConnectedFloat(SerializedConnection c, out float v, out string srcNode)
        {
            v = 0f;
            srcNode = null;

            srcNode = c.SrcNodePart;
            if (string.IsNullOrEmpty(srcNode))
                srcNode = MayaPlugUtil.ExtractNodePart(c.SrcPlug);

            if (string.IsNullOrEmpty(srcNode))
                return false;

            var tr = MayaNodeLookup.FindTransform(srcNode);
            if (tr == null) return false;

            // Prefer MayaFloatValue carrier if present (from addDoubleLinear etc)
            var fv = tr.GetComponent<MayaFloatValue>();
            if (fv != null && fv.valid)
            {
                v = fv.value;
                return true;
            }

            // Fallback: try typical scalar output attributes on the source node
            var node = tr.GetComponent<MayaNodeComponentBase>();
            if (node != null && node.Attributes != null)
            {
                string[] keys = { ".output", "output", ".outValue", "outValue", ".out", "out", ".o", "o" };
                for (int i = 0; i < keys.Length; i++)
                {
                    if (TryGetAttrStatic(node, keys[i], out var a) &&
                        a.Tokens != null && a.Tokens.Count > 0 &&
                        MathUtil.TryParseFloat(a.Tokens[0], out v))
                        return true;
                }
            }

            return false;
        }

        private static bool TryGetAttrStatic(MayaNodeComponentBase node, string key, out MayaNodeComponentBase.SerializedAttribute attr)
        {
            attr = null;
            if (node == null || node.Attributes == null || string.IsNullOrEmpty(key)) return false;

            for (int i = 0; i < node.Attributes.Count; i++)
            {
                var a = node.Attributes[i];
                if (a == null) continue;
                if (string.Equals(a.Key, key, StringComparison.Ordinal))
                {
                    attr = a;
                    return true;
                }
            }

            var dot = key.StartsWith(".", StringComparison.Ordinal) ? key.Substring(1) : "." + key;
            for (int i = 0; i < node.Attributes.Count; i++)
            {
                var a = node.Attributes[i];
                if (a == null) continue;
                if (string.Equals(a.Key, dot, StringComparison.Ordinal))
                {
                    attr = a;
                    return true;
                }
            }

            return false;
        }

        // --------------------------
        // Core math (TRS blend)
        // --------------------------

        private static Matrix4x4 BlendTRS(in Matrix4x4 a, in Matrix4x4 b, float w01)
        {
            w01 = Mathf.Clamp01(w01);

            MatrixUtil.DecomposeTRS(a, out var ta, out var ra, out var sa);
            MatrixUtil.DecomposeTRS(b, out var tb, out var rb, out var sb);

            // hemisphere fix for stable slerp
            if (Quaternion.Dot(ra, rb) < 0f)
                rb = new Quaternion(-rb.x, -rb.y, -rb.z, -rb.w);

            var t = Vector3.Lerp(ta, tb, w01);
            var r = Quaternion.Slerp(ra, rb, w01);
            var s = Vector3.Lerp(sa, sb, w01);

            return MatrixUtil.ComposeTRS(t, r, s);
        }

        // --------------------------
        // Local attribute readers
        // --------------------------

        private Matrix4x4 ReadMatrixOrIdentity(params string[] keys)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                if (TryGetAttr(keys[i], out var a) && a.Tokens != null && a.Tokens.Count >= 16)
                {
                    if (MatrixUtil.TryParseMatrix4x4(a.Tokens, 0, out var m))
                        return m;
                }
            }
            return Matrix4x4.identity;
        }

        private float ReadFloat(float def, params string[] keys)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                if (TryGetAttr(keys[i], out var a) && a.Tokens != null && a.Tokens.Count > 0 &&
                    MathUtil.TryParseFloat(a.Tokens[0], out var f))
                    return f;
            }
            return def;
        }

        private bool TryResolveIncomingMatrixByDstContainsAny(string[] dstContainsAny, out Matrix4x4 m, out string srcSummary)
        {
            m = Matrix4x4.identity;
            srcSummary = "None";

            if (Connections == null || dstContainsAny == null || dstContainsAny.Length == 0)
                return false;

            for (int i = 0; i < Connections.Count; i++)
            {
                var c = Connections[i];
                if (c == null) continue;

                if (c.RoleForThisNode != ConnectionRole.Destination && c.RoleForThisNode != ConnectionRole.Both)
                    continue;

                var dst = c.DstPlug ?? "";
                if (dst.Length == 0) continue;

                bool hit = false;
                for (int k = 0; k < dstContainsAny.Length; k++)
                {
                    var key = dstContainsAny[k];
                    if (!string.IsNullOrEmpty(key) && dst.Contains(key, StringComparison.Ordinal))
                    {
                        hit = true;
                        break;
                    }
                }
                if (!hit) continue;

                var srcNode = c.SrcNodePart;
                if (string.IsNullOrEmpty(srcNode))
                    srcNode = MayaPlugUtil.ExtractNodePart(c.SrcPlug);

                if (string.IsNullOrEmpty(srcNode))
                    continue;

                var tr = MayaNodeLookup.FindTransform(srcNode);
                if (tr == null) continue;

                var mv = tr.GetComponent<MayaMatrixValue>();
                if (mv != null && mv.valid)
                {
                    m = mv.matrixMaya;
                    srcSummary = $"Incoming:{srcNode}";
                    return true;
                }

                srcSummary = $"Incoming:{srcNode}(no MayaMatrixValue)";
            }

            return false;
        }
    }

    [Serializable]
    public struct MayaBlendMatrixTarget
    {
        public int index;

        public Matrix4x4 targetMatrixMaya;
        public float weight;

        public string matrixSource;
        public string weightSource;
    }

    [DisallowMultipleComponent]
    public sealed class MayaBlendMatrixMetadata : MonoBehaviour
    {
        public bool valid;

        [Header("Envelope")]
        public float envelope = 1f;

        [Header("Base")]
        public string baseSource;
        public Matrix4x4 baseMatrixMaya = Matrix4x4.identity;

        [Header("Targets (collected)")]
        public List<MayaBlendMatrixTarget> targets = new List<MayaBlendMatrixTarget>();

        [Header("Output (best-effort)")]
        public Matrix4x4 outputMatrixMaya = Matrix4x4.identity;
        public Matrix4x4 outputMatrixUnity = Matrix4x4.identity;

        [Header("Debug")]
        public int lastBuildFrame;
    }
}
