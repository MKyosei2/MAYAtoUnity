// Assets/MayaImporter/PlusMinusAverageNode.cs
// Production implementation (not stub)
// NodeType: plusMinusAverage
//
// - Decodes operation + input1D[]/input2D[]/input3D[] (holes & ranges supported)
// - Computes best-effort output and publishes:
//    * MayaFloatValue (Nodes) for 1D
//    * MayaVector3Value (Core) for 2D/3D (2D uses z=0)
// - Stores connection hints for later wiring

using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using MayaImporter.Core;
using MayaImporter.Utils;

namespace MayaImporter.Nodes
{
    [MayaNodeType("plusMinusAverage")]
    [DisallowMultipleComponent]
    public sealed class PlusMinusAverageNode : MayaNodeComponentBase
    {
        public override void ApplyToUnity(MayaImportOptions options, MayaImportLog log)
        {
            options ??= new MayaImportOptions();
            log ??= new MayaImportLog();

            var meta = GetComponent<MayaPlusMinusAverageMetadata>() ?? gameObject.AddComponent<MayaPlusMinusAverageMetadata>();
            meta.valid = false;

            meta.operation = ReadInt(1, ".operation", "operation", ".op", "op"); // 1=sum,2=subtract,3=average

            // collect inputs (holes + ranges)
            meta.input1D.Clear();
            meta.input2D.Clear();
            meta.input3D.Clear();

            CollectIndexedFloatInputs("input1D", meta.input1D);
            CollectIndexedVecInputs("input2D", 2, meta.input2D);
            CollectIndexedVecInputs("input3D", 3, meta.input3D);

            // connection hints (best-effort)
            meta.incomingInputPlugs.Clear();
            CollectIncomingPlugsContains(meta.incomingInputPlugs, "input1D", "input2D", "input3D", "input");

            // choose dimension (prefer 3D > 2D > 1D)
            meta.mode = MayaPlusMinusAverageMetadata.Mode.None;
            meta.output1D = 0f;
            meta.output3D = Vector3.zero;

            if (meta.input3D.Count > 0)
            {
                meta.mode = MayaPlusMinusAverageMetadata.Mode.Input3D;
                meta.output3D = EvalVector(meta.operation, meta.input3D, is2D: false);
            }
            else if (meta.input2D.Count > 0)
            {
                meta.mode = MayaPlusMinusAverageMetadata.Mode.Input2D;
                meta.output3D = EvalVector(meta.operation, meta.input2D, is2D: true);
            }
            else if (meta.input1D.Count > 0)
            {
                meta.mode = MayaPlusMinusAverageMetadata.Mode.Input1D;
                meta.output1D = EvalScalar(meta.operation, meta.input1D);
            }
            else
            {
                meta.mode = MayaPlusMinusAverageMetadata.Mode.None;
            }

            // publish outputs
            var outFloat = GetComponent<MayaFloatValue>() ?? gameObject.AddComponent<MayaFloatValue>();
            var outVec = GetComponent<MayaVector3Value>() ?? gameObject.AddComponent<MayaVector3Value>();

            outFloat.valid = false;
            outVec.valid = false;

            if (meta.mode == MayaPlusMinusAverageMetadata.Mode.Input1D)
            {
                outFloat.valid = true;
                outFloat.value = meta.output1D;
            }
            else if (meta.mode == MayaPlusMinusAverageMetadata.Mode.Input2D || meta.mode == MayaPlusMinusAverageMetadata.Mode.Input3D)
            {
                outVec.Set(MayaVector3Value.Kind.Vector, meta.output3D, meta.output3D);
            }

            meta.valid = true;
            meta.lastBuildFrame = Time.frameCount;

            log.Info($"[plusMinusAverage] '{NodeName}' op={meta.operation} mode={meta.mode} in1D={meta.input1D.Count} in2D={meta.input2D.Count} in3D={meta.input3D.Count} " +
                     $"out1D={meta.output1D:0.###} outV=({meta.output3D.x:0.###},{meta.output3D.y:0.###},{meta.output3D.z:0.###}) plugs={meta.incomingInputPlugs.Count}");
        }

        private float EvalScalar(int op, List<float> values)
        {
            if (values == null || values.Count == 0) return 0f;

            switch (op)
            {
                case 2: // subtract
                    {
                        float v = values[0];
                        for (int i = 1; i < values.Count; i++) v -= values[i];
                        return v;
                    }
                case 3: // average
                    {
                        float sum = 0f;
                        for (int i = 0; i < values.Count; i++) sum += values[i];
                        return sum / Mathf.Max(1, values.Count);
                    }
                default: // 1 sum
                    {
                        float sum = 0f;
                        for (int i = 0; i < values.Count; i++) sum += values[i];
                        return sum;
                    }
            }
        }

        private Vector3 EvalVector(int op, List<Vector3> values, bool is2D)
        {
            if (values == null || values.Count == 0) return Vector3.zero;

            Vector3 v;

            switch (op)
            {
                case 2: // subtract
                    v = values[0];
                    for (int i = 1; i < values.Count; i++) v -= values[i];
                    break;

                case 3: // average
                    v = Vector3.zero;
                    for (int i = 0; i < values.Count; i++) v += values[i];
                    v /= Mathf.Max(1, values.Count);
                    break;

                default: // sum
                    v = Vector3.zero;
                    for (int i = 0; i < values.Count; i++) v += values[i];
                    break;
            }

            if (is2D) v.z = 0f;
            return v;
        }

        // -------- decode helpers --------

        private int ReadInt(int def, params string[] keys)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                if (TryGetAttr(keys[i], out var a) && a.Tokens != null && a.Tokens.Count > 0)
                {
                    // prefer last numeric
                    for (int t = a.Tokens.Count - 1; t >= 0; t--)
                        if (MathUtil.TryParseInt(a.Tokens[t], out var v)) return v;
                }
            }
            return def;
        }

        private void CollectIndexedFloatInputs(string baseName, List<float> dst)
        {
            if (dst == null) return;

            // gather (index->value) then pack in index order
            var map = new SortedDictionary<int, float>();

            if (Attributes != null)
            {
                for (int i = 0; i < Attributes.Count; i++)
                {
                    var a = Attributes[i];
                    if (a == null || string.IsNullOrEmpty(a.Key) || a.Tokens == null || a.Tokens.Count == 0) continue;

                    if (!TryParseIndexedKey(a.Key, baseName, out var start, out var end, out var isRange))
                        continue;

                    if (!isRange)
                    {
                        if (TryFloatPreferLast(a.Tokens, out var f)) map[start] = f;
                        continue;
                    }

                    int count = end - start + 1;
                    int n = Mathf.Min(count, a.Tokens.Count);
                    for (int k = 0; k < n; k++)
                        if (MathUtil.TryParseFloat(a.Tokens[k], out var f)) map[start + k] = f;
                }
            }

            dst.Clear();
            foreach (var kv in map) dst.Add(kv.Value);
        }

        private void CollectIndexedVecInputs(string baseName, int dims, List<Vector3> dst)
        {
            if (dst == null) return;

            var map = new SortedDictionary<int, Vector3>();

            if (Attributes != null)
            {
                for (int i = 0; i < Attributes.Count; i++)
                {
                    var a = Attributes[i];
                    if (a == null || string.IsNullOrEmpty(a.Key) || a.Tokens == null || a.Tokens.Count == 0) continue;

                    if (!TryParseIndexedKey(a.Key, baseName, out var start, out var end, out var isRange))
                        continue;

                    // For vectors: range is uncommon; handle only single index robustly
                    if (isRange) continue;

                    // tokens: at least dims floats
                    float x = 0f, y = 0f, z = 0f;

                    if (a.Tokens.Count >= 1 && MathUtil.TryParseFloat(a.Tokens[0], out var fx)) x = fx;
                    if (a.Tokens.Count >= 2 && MathUtil.TryParseFloat(a.Tokens[1], out var fy)) y = fy;
                    if (dims >= 3 && a.Tokens.Count >= 3 && MathUtil.TryParseFloat(a.Tokens[2], out var fz)) z = fz;

                    map[start] = new Vector3(x, y, z);
                }
            }

            dst.Clear();
            foreach (var kv in map) dst.Add(kv.Value);
        }

        private static bool TryFloatPreferLast(List<string> tokens, out float f)
        {
            f = 0f;
            if (tokens == null || tokens.Count == 0) return false;

            for (int i = tokens.Count - 1; i >= 0; i--)
                if (MathUtil.TryParseFloat(tokens[i], out f)) return true;

            return false;
        }

        private static bool TryParseIndexedKey(string key, string baseName, out int start, out int end, out bool isRange)
        {
            start = end = -1;
            isRange = false;

            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(baseName)) return false;

            string k = key.StartsWith(".", StringComparison.Ordinal) ? key.Substring(1) : key;
            if (!k.StartsWith(baseName + "[", StringComparison.Ordinal)) return false;

            int lb = k.IndexOf('[');
            int rb = k.IndexOf(']', lb + 1);
            if (lb < 0 || rb < 0 || rb <= lb + 1) return false;

            var inner = k.Substring(lb + 1, rb - lb - 1); // "3" or "0:7"
            int colon = inner.IndexOf(':');

            if (colon < 0)
            {
                if (!int.TryParse(inner, NumberStyles.Integer, CultureInfo.InvariantCulture, out var idx))
                    return false;
                start = end = idx;
                isRange = false;
                return true;
            }

            var a = inner.Substring(0, colon);
            var b = inner.Substring(colon + 1);

            if (!int.TryParse(a, NumberStyles.Integer, CultureInfo.InvariantCulture, out start)) return false;
            if (!int.TryParse(b, NumberStyles.Integer, CultureInfo.InvariantCulture, out end)) return false;

            if (end < start) (start, end) = (end, start);
            isRange = true;
            return true;
        }

        private void CollectIncomingPlugsContains(List<string> dst, params string[] patterns)
        {
            if (dst == null) return;
            dst.Clear();
            if (Connections == null || Connections.Count == 0) return;
            if (patterns == null || patterns.Length == 0) return;

            var set = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < Connections.Count; i++)
            {
                var c = Connections[i];
                if (c == null) continue;

                if (c.RoleForThisNode != ConnectionRole.Destination && c.RoleForThisNode != ConnectionRole.Both)
                    continue;

                var dstAttr = MayaPlugUtil.ExtractAttrPart(c.DstPlug);
                if (string.IsNullOrEmpty(dstAttr)) continue;

                bool hit = false;
                for (int p = 0; p < patterns.Length; p++)
                {
                    var pat = patterns[p];
                    if (string.IsNullOrEmpty(pat)) continue;
                    if (dstAttr.Contains(pat, StringComparison.Ordinal)) { hit = true; break; }
                }
                if (!hit) continue;

                if (set.Add(c.SrcPlug))
                    dst.Add(c.SrcPlug);
            }
        }
    }

    public sealed class MayaPlusMinusAverageMetadata : MonoBehaviour
    {
        public enum Mode
        {
            None,
            Input1D,
            Input2D,
            Input3D
        }

        public bool valid;
        public int lastBuildFrame;

        [Header("Decoded")]
        public int operation = 1;
        public Mode mode = Mode.None;

        [Header("Inputs (packed in index order)")]
        public List<float> input1D = new List<float>();
        public List<Vector3> input2D = new List<Vector3>();
        public List<Vector3> input3D = new List<Vector3>();

        [Header("Outputs (best-effort)")]
        public float output1D;
        public Vector3 output3D;

        [Header("Connection Hints")]
        public List<string> incomingInputPlugs = new List<string>();
    }
}


// ----------------------------------------------------------------------------- 
// INTEGRATED: PlusMinusAverageEvalNode.cs
// -----------------------------------------------------------------------------
// PATCH: ProductionImpl v6 (Unity-only, retention-first)

namespace MayaImporter.Phase3.Evaluation
{
    public class PlusMinusAverageEvalNode : EvalNode
    {
        private readonly MayaNode _node;

        public PlusMinusAverageEvalNode(MayaNode node)
            : base(node.NodeName)
        {
            _node = node;
        }

        protected override void Evaluate(EvalContext ctx)
        {
            int op = GetInt("operation", 1);

            Vector3 sum = Vector3.zero;
            int count = 0;

            foreach (var kv in _node.Attributes)
            {
                if (!kv.Key.StartsWith("input3D["))
                    continue;

                if (kv.Value.Data?.Value is float[] f && f.Length >= 3)
                {
                    sum += new Vector3(f[0], f[1], f[2]);
                    count++;
                }
            }

            Vector3 outv = op switch
            {
                2 => count > 0 ? -sum : Vector3.zero,
                3 => count > 0 ? sum / count : Vector3.zero,
                _ => sum
            };

            SetVec("output3D", outv);

            ctx?.MarkAttributeDirty($"{NodeName}.output3D");
        }

        // -------- helpers --------

        private int GetInt(string k, int def)
        {
            if (_node.Attributes.TryGetValue(k, out var a) && a.Data?.Value is int i)
                return i;
            return def;
        }

        private void SetVec(string prefix, Vector3 v)
        {
            _node.Attributes[prefix + "[0]"].Data.Value = new float[] { v.x, v.y, v.z };
        }
    }
}
