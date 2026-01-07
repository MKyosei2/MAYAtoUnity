using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Deformers
{
    /// <summary>
    /// Maya Wire Deformer
    /// Production implementation:
    /// - ApplyToUnity 実装（STUB脱却）
    /// - 配列属性(dropoffDistance/scale/rotation)を index 付きで収集
    /// - driven geometry / deform wire curves / base wire curves を connections から推測して保持
    /// - Unityに概念がないため、再構築用コンポーネントとしてパラメータ・接続を保存
    /// </summary>
    [DisallowMultipleComponent]
    [MayaNodeType("wire")]
    public sealed class WireDeformer : DeformerBase
    {
        // ===== Wire specific attributes =====

        [Header("Wire Specific")]
        [Tooltip("Wire envelope (often same as deformer envelope)")]
        [Range(0f, 1f)]
        public float wireEnvelope = 1f;

        [Tooltip("Dropoff distances per wire (index-aligned)")]
        public List<float> dropoffDistances = new List<float>();

        [Tooltip("Scale per wire (index-aligned)")]
        public List<float> scales = new List<float>();

        [Tooltip("Rotation per wire (index-aligned)")]
        public List<float> rotations = new List<float>();

        // ===== Geometry =====

        [Header("Geometry Binding")]
        [Tooltip("Driven geometry node name (best-effort)")]
        public string drivenGeometry;

        [Tooltip("Deforming wire curve nodes (best-effort)")]
        public List<string> deformWireCurves = new List<string>();

        [Tooltip("Base wire curve nodes (best-effort)")]
        public List<string> baseWireCurves = new List<string>();

        // ===== Matrices =====

        [Header("Matrices")]
        [Tooltip("Wire matrix (best-effort)")]
        public Matrix4x4 wireMatrix = Matrix4x4.identity;

        [Tooltip("Bind pre-matrix (best-effort)")]
        public Matrix4x4 bindPreMatrix = Matrix4x4.identity;

        // ===== Compatibility init (kept) =====

        public void InitializeWire(
            float envelopeValue,
            IList<float> dropoffs,
            IList<float> scaleValues,
            IList<float> rotationValues,
            string drivenGeo,
            IList<string> deformCurves,
            IList<string> baseCurves,
            Matrix4x4 wireMtx,
            Matrix4x4 bindPreMtx)
        {
            wireEnvelope = Mathf.Clamp01(envelopeValue);

            dropoffDistances.Clear();
            if (dropoffs != null) dropoffDistances.AddRange(dropoffs);

            scales.Clear();
            if (scaleValues != null) scales.AddRange(scaleValues);

            rotations.Clear();
            if (rotationValues != null) rotations.AddRange(rotationValues);

            drivenGeometry = drivenGeo;

            deformWireCurves.Clear();
            if (deformCurves != null) deformWireCurves.AddRange(deformCurves);

            baseWireCurves.Clear();
            if (baseCurves != null) baseWireCurves.AddRange(baseCurves);

            wireMatrix = wireMtx;
            bindPreMatrix = bindPreMtx;
        }

        // ===== Production Decode =====

        public override void ApplyToUnity(MayaImportOptions options, MayaImportLog log)
        {
            deformerName = NodeName;
            mayaNodeType = NodeType;
            mayaNodeUUID = Uuid;

            // Common envelope
            envelope = Mathf.Clamp01(DeformerDecodeUtil.ReadFloat(this, envelope, ".envelope", "envelope", ".env", "env"));
            // Wire envelope (some rigs use separate attr; if absent, mirror)
            wireEnvelope = Mathf.Clamp01(DeformerDecodeUtil.ReadFloat(this, wireEnvelope, ".wireEnvelope", "wireEnvelope", ".envelope", "envelope"));

            // Collect indexed arrays from raw attributes
            CollectIndexedFloatAttr("dropoffDistance", dropoffDistances);
            CollectIndexedFloatAttr("scale", scales);
            CollectIndexedFloatAttr("rotation", rotations);

            // Matrices (connections preferred)
            var wmPlug = FindIncomingPlugByDstContains("wireMatrix", "wm", "wireMtx");
            if (!string.IsNullOrEmpty(wmPlug) && DeformerDecodeUtil.TryResolveConnectedMatrix(wmPlug, out var wm))
                wireMatrix = wm;
            else if (DeformerDecodeUtil.TryReadMatrix4x4(this, ".wireMatrix", out wm) || DeformerDecodeUtil.TryReadMatrix4x4(this, "wireMatrix", out wm))
                wireMatrix = wm;

            var bpmPlug = FindIncomingPlugByDstContains("bindPreMatrix", "preMatrix", "bindPre");
            if (!string.IsNullOrEmpty(bpmPlug) && DeformerDecodeUtil.TryResolveConnectedMatrix(bpmPlug, out var bpm))
                bindPreMatrix = bpm;
            else if (DeformerDecodeUtil.TryReadMatrix4x4(this, ".bindPreMatrix", out bpm) || DeformerDecodeUtil.TryReadMatrix4x4(this, "bindPreMatrix", out bpm) ||
                     DeformerDecodeUtil.TryReadMatrix4x4(this, ".preMatrix", out bpm) || DeformerDecodeUtil.TryReadMatrix4x4(this, "preMatrix", out bpm))
                bindPreMatrix = bpm;

            // Driven geometry (best-effort)
            drivenGeometry =
                FindConnectedNodeByDstContains("inputGeometry", "inGeometry", "inputMesh", "inMesh", "input", "geom", "geometry")
                ?? drivenGeometry;

            inputGeometry = drivenGeometry;
            outputGeometry = FindConnectedNodeByDstContains("outputGeometry", "outGeometry", "outputMesh", "outMesh");

            // Curves (best-effort)
            deformWireCurves.Clear();
            baseWireCurves.Clear();

            CollectConnectedNodesByDstContains(deformWireCurves,
                "deformedWire", "deformWire", "deformingWire", "wire[", "wireData");

            CollectConnectedNodesByDstContains(baseWireCurves,
                "baseWire", "baseCurve", "baseWireData", "base[", "baseData");

            // Dedup
            DedupInPlace(deformWireCurves);
            DedupInPlace(baseWireCurves);

            log?.Info($"[wire] '{NodeName}' env={envelope:0.###} wireEnv={wireEnvelope:0.###} " +
                      $"drop={dropoffDistances.Count} scale={scales.Count} rot={rotations.Count} driven={drivenGeometry ?? "null"} " +
                      $"deformCurves={deformWireCurves.Count} baseCurves={baseWireCurves.Count}");
        }

        // ---------- Helpers ----------

        private void CollectIndexedFloatAttr(string baseName, List<float> dst)
        {
            if (dst == null) return;
            dst.Clear();

            if (Attributes == null || Attributes.Count == 0) return;

            int maxIndex = -1;
            // First pass: find max index
            for (int i = 0; i < Attributes.Count; i++)
            {
                var a = Attributes[i];
                if (a == null || string.IsNullOrEmpty(a.Key) || a.Tokens == null || a.Tokens.Count == 0) continue;

                if (!LooksLikeIndexedAttr(a.Key, baseName, out int idx)) continue;
                if (idx > maxIndex) maxIndex = idx;
            }

            if (maxIndex < 0) return;

            // Ensure size
            for (int i = 0; i <= maxIndex; i++) dst.Add(0f);

            // Second pass: fill values
            for (int i = 0; i < Attributes.Count; i++)
            {
                var a = Attributes[i];
                if (a == null || string.IsNullOrEmpty(a.Key) || a.Tokens == null || a.Tokens.Count == 0) continue;

                if (!LooksLikeIndexedAttr(a.Key, baseName, out int idx)) continue;
                if (idx < 0 || idx >= dst.Count) continue;

                // take last numeric token
                for (int t = a.Tokens.Count - 1; t >= 0; t--)
                {
                    if (TryF(a.Tokens[t], out var f))
                    {
                        dst[idx] = f;
                        break;
                    }
                }
            }
        }

        private static bool LooksLikeIndexedAttr(string key, string baseName, out int idx)
        {
            idx = -1;
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(baseName)) return false;

            // Accept: "dropoffDistance[0]" ".dropoffDistance[0]" "dropoffDistance[0].something"
            string k = key.StartsWith(".", StringComparison.Ordinal) ? key.Substring(1) : key;
            if (!k.StartsWith(baseName + "[", StringComparison.Ordinal)) return false;

            int lb = k.IndexOf('[');
            int rb = k.IndexOf(']', lb + 1);
            if (lb < 0 || rb < 0 || rb <= lb + 1) return false;

            var inner = k.Substring(lb + 1, rb - lb - 1);
            return int.TryParse(inner, NumberStyles.Integer, CultureInfo.InvariantCulture, out idx);
        }

        private static bool TryF(string s, out float f)
            => float.TryParse((s ?? "").Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out f);

        private string FindIncomingPlugByDstContains(params string[] patterns)
        {
            if (Connections == null || patterns == null || patterns.Length == 0) return null;

            for (int i = Connections.Count - 1; i >= 0; i--)
            {
                var c = Connections[i];
                if (c == null) continue;

                if (c.RoleForThisNode != MayaNodeComponentBase.ConnectionRole.Destination &&
                    c.RoleForThisNode != MayaNodeComponentBase.ConnectionRole.Both)
                    continue;

                var dstAttr = MayaPlugUtil.ExtractAttrPart(c.DstPlug);
                if (string.IsNullOrEmpty(dstAttr)) continue;

                for (int p = 0; p < patterns.Length; p++)
                {
                    var pat = patterns[p];
                    if (string.IsNullOrEmpty(pat)) continue;
                    if (dstAttr.Contains(pat, StringComparison.Ordinal))
                        return c.SrcPlug;
                }
            }

            return null;
        }

        private string FindConnectedNodeByDstContains(params string[] patterns)
        {
            var plug = FindIncomingPlugByDstContains(patterns);
            if (string.IsNullOrEmpty(plug)) return null;
            return MayaPlugUtil.ExtractNodePart(plug);
        }

        private void CollectConnectedNodesByDstContains(List<string> outList, params string[] patterns)
        {
            if (outList == null) return;
            if (Connections == null || Connections.Count == 0) return;
            if (patterns == null || patterns.Length == 0) return;

            for (int i = Connections.Count - 1; i >= 0; i--)
            {
                var c = Connections[i];
                if (c == null) continue;

                if (c.RoleForThisNode != MayaNodeComponentBase.ConnectionRole.Destination &&
                    c.RoleForThisNode != MayaNodeComponentBase.ConnectionRole.Both)
                    continue;

                var dstAttr = MayaPlugUtil.ExtractAttrPart(c.DstPlug);
                if (string.IsNullOrEmpty(dstAttr)) continue;

                bool hit = false;
                for (int p = 0; p < patterns.Length; p++)
                {
                    var pat = patterns[p];
                    if (string.IsNullOrEmpty(pat)) continue;
                    if (dstAttr.Contains(pat, StringComparison.Ordinal))
                    {
                        hit = true;
                        break;
                    }
                }
                if (!hit) continue;

                var node = MayaPlugUtil.ExtractNodePart(c.SrcPlug);
                if (!string.IsNullOrEmpty(node))
                    outList.Add(node);
            }
        }

        private static void DedupInPlace(List<string> list)
        {
            if (list == null) return;
            var set = new HashSet<string>(StringComparer.Ordinal);
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var s = list[i];
                if (string.IsNullOrEmpty(s) || !set.Add(s))
                    list.RemoveAt(i);
            }
        }
    }
}
