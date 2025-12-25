// Assets/MayaImporter/MayaAnimCurveNodeComponent.cs
// Phase-1 강화版:
// - ktv/kix/kiy/kox/koy を decode して keys[] を生成（従来通り）
// - preInfinity/postInfinity を WrapMode に best-effort 変換して保持
// - Unity側に AnimCurveNode (times/values/tangents + wrap) を自動生成して “再構築可能” にする
// - 出力が接続されている先(DstPlug)を MayaAnimCurveBindingMetadata に保持（後でClip化/Bindingに使える）
//
// NOTE: Mayaの全仕様完全再現ではないが、「Unity上で再構築できる」ことに寄せた実装。

using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using MayaImporter.Core;
using MayaImporter.Utils;

namespace MayaImporter.Animation
{
    [DisallowMultipleComponent]
    [MayaNodeType("animCurveTA")]
    [MayaNodeType("animCurveTL")]
    [MayaNodeType("animCurveTU")]
    [MayaNodeType("animCurveTT")]
    [MayaNodeType("animCurveUA")]
    [MayaNodeType("animCurveUL")]
    [MayaNodeType("animCurveUU")]
    [MayaNodeType("animCurveUT")]
    public sealed class MayaAnimCurveNodeComponent : MayaNodeComponentBase
    {
        [Serializable]
        public struct Key
        {
            public float timeFrame;   // x (frame OR driven-input)
            public float value;
            public float inSlope;     // dv/dx
            public float outSlope;    // dv/dx
            public bool hasInSlope;
            public bool hasOutSlope;
        }

        public enum InfinityType
        {
            Constant = 0,
            Linear = 1,
            // Maya has more variants; we implement common ones best-effort:
            Cycle = 3,
            CycleRelative = 4,
            Oscillate = 5,
        }

        [Header("Keys (decoded)")]
        public Key[] keys = Array.Empty<Key>();

        [Header("Infinity (Maya enum int)")]
        public int preInfinity = 0;
        public int postInfinity = 0;

        [Header("Unity WrapMode (best-effort)")]
        public WrapMode unityPreWrap = WrapMode.ClampForever;
        public WrapMode unityPostWrap = WrapMode.ClampForever;

        public override void ApplyToUnity(MayaImportOptions options, MayaImportLog log)
        {
            options ??= new MayaImportOptions();
            log ??= new MayaImportLog();

            keys = DecodeKeysFromAttributes();
            if (keys == null) keys = Array.Empty<Key>();

            // read infinity (best-effort)
            preInfinity = ReadIntAttr(".preInfinity", "preInfinity", 0);
            postInfinity = ReadIntAttr(".postInfinity", "postInfinity", 0);

            unityPreWrap = MayaAnimCurveWrapModeUtil.ToUnityWrapMode(preInfinity);
            unityPostWrap = MayaAnimCurveWrapModeUtil.ToUnityWrapMode(postInfinity);

            // --- Build Unity-side reconstructable curve data ---
            // Create/Update AnimCurveNode (stores arrays + wrap)
            var ac = GetComponent<AnimCurveNode>();
            if (ac == null) ac = gameObject.AddComponent<AnimCurveNode>();

            BuildArraysForUnity(keys, out var times, out var values, out var inTan, out var outTan);

            ac.Initialize(times, values, inTan, outTan);
            ac.preWrapMode = unityPreWrap;
            ac.postWrapMode = unityPostWrap;

            // targetPath/propertyName は MayaImporter.AnimationManager の Binding 方式では不要だが、
            // 後でClip化したい場合のため、接続先から best-effort 推定して metadata に残す
            var bind = GetComponent<MayaAnimCurveBindingMetadata>();
            if (bind == null) bind = gameObject.AddComponent<MayaAnimCurveBindingMetadata>();

            bind.Clear();
            bind.nodeName = NodeName;
            bind.nodeType = NodeType;
            bind.isDriven = HasIncomingToAttr("i") || HasIncomingToAttr(".i");

            // outgoing connections (this node is source)
            if (Connections != null)
            {
                for (int i = 0; i < Connections.Count; i++)
                {
                    var c = Connections[i];
                    if (c == null) continue;

                    if (c.RoleForThisNode != ConnectionRole.Source &&
                        c.RoleForThisNode != ConnectionRole.Both)
                        continue;

                    // Only keep likely output connections
                    var srcAttr = MayaPlugUtil.ExtractAttrPart(c.SrcPlug) ?? "";
                    if (!LooksLikeOutput(srcAttr))
                        continue;

                    bind.dstPlugs.Add(c.DstPlug);

                    var dstNode = MayaPlugUtil.ExtractNodePart(c.DstPlug);
                    var dstAttr = MayaPlugUtil.ExtractAttrPart(c.DstPlug);

                    if (!string.IsNullOrEmpty(dstNode) && !string.IsNullOrEmpty(dstAttr))
                        bind.dstNodeAttrs.Add(dstNode + "." + dstAttr);
                }
            }

            // best-effort fill for AnimCurveNode
            if (bind.dstNodeAttrs.Count == 1)
            {
                // Example: "pCube1.rotateX"
                var one = bind.dstNodeAttrs[0];
                int dot = one.IndexOf('.');
                if (dot > 0 && dot < one.Length - 1)
                {
                    ac.targetPath = ""; // DAG path is not resolved here (manager uses node-name lookup)
                    ac.propertyName = one.Substring(dot + 1);
                }
            }
            else
            {
                ac.targetPath = "";
                ac.propertyName = "";
            }

            if (keys.Length > 0)
            {
                log.Info($"[animCurve] '{NodeName}' type={NodeType} keys={keys.Length} x=[{keys[0].timeFrame}..{keys[keys.Length - 1].timeFrame}] " +
                         $"preInf={preInfinity} postInf={postInfinity} wrap=({unityPreWrap},{unityPostWrap}) dst={bind.dstNodeAttrs.Count}");
            }
            else
            {
                log.Info($"[animCurve] '{NodeName}' type={NodeType} keys=0 preInf={preInfinity} postInf={postInfinity}");
            }
        }

        /// <summary>Backward compatible time-based evaluation.</summary>
        public float Evaluate(float frame) => EvaluateAt(frame);

        /// <summary>Generic evaluation: x can be frame OR driver value (driven key).</summary>
        public float EvaluateAt(float x)
        {
            if (keys == null || keys.Length == 0) return 0f;
            if (keys.Length == 1) return keys[0].value;

            float x0 = keys[0].timeFrame;
            float xN = keys[keys.Length - 1].timeFrame;

            if (x < x0) return EvaluateWithInfinity(x, preInfinity, isPre: true);
            if (x > xN) return EvaluateWithInfinity(x, postInfinity, isPre: false);

            return EvaluateInRange(x);
        }

        private float EvaluateInRange(float x)
        {
            if (x <= keys[0].timeFrame) return keys[0].value;
            if (x >= keys[keys.Length - 1].timeFrame) return keys[keys.Length - 1].value;

            int i = FindSegment(x);
            var k0 = keys[i];
            var k1 = keys[i + 1];

            float t0 = k0.timeFrame;
            float t1 = k1.timeFrame;
            float dt = Mathf.Max(1e-6f, (t1 - t0));
            float u = Mathf.Clamp01((x - t0) / dt);

            bool useHermite = (k0.hasOutSlope || k1.hasInSlope);
            if (!useHermite)
                return Mathf.Lerp(k0.value, k1.value, u);

            float m0 = k0.hasOutSlope ? k0.outSlope : (k1.value - k0.value) / dt;
            float m1 = k1.hasInSlope ? k1.inSlope : (k1.value - k0.value) / dt;

            float u2 = u * u;
            float u3 = u2 * u;

            float h00 = 2f * u3 - 3f * u2 + 1f;
            float h10 = u3 - 2f * u2 + u;
            float h01 = -2f * u3 + 3f * u2;
            float h11 = u3 - u2;

            return h00 * k0.value + h10 * (m0 * dt) + h01 * k1.value + h11 * (m1 * dt);
        }

        private float EvaluateWithInfinity(float x, int infType, bool isPre)
        {
            float x0 = keys[0].timeFrame;
            float xN = keys[keys.Length - 1].timeFrame;
            float v0 = keys[0].value;
            float vN = keys[keys.Length - 1].value;

            float range = xN - x0;
            if (range <= 1e-6f)
                return isPre ? v0 : vN;

            // Constant
            if (infType == (int)InfinityType.Constant || infType == 2 /* unknown */)
                return isPre ? v0 : vN;

            // Linear extrapolation (Unity Wrapでは表現不能なので Evaluate は線形、UnityCurveはClampForever)
            if (infType == (int)InfinityType.Linear)
            {
                if (isPre)
                {
                    float m = keys[0].hasInSlope ? keys[0].inSlope : BoundarySlope_First();
                    return v0 + m * (x - x0);
                }
                else
                {
                    float m = keys[keys.Length - 1].hasOutSlope ? keys[keys.Length - 1].outSlope : BoundarySlope_Last();
                    return vN + m * (x - xN);
                }
            }

            // Cycle / CycleRelative / Oscillate
            if (infType == (int)InfinityType.Cycle ||
                infType == (int)InfinityType.CycleRelative ||
                infType == (int)InfinityType.Oscillate)
            {
                float cyclesF = (x - x0) / range;
                int cycles = (int)Mathf.Floor(cyclesF);

                float wrappedX = x - cycles * range;

                while (wrappedX > xN) { wrappedX -= range; cycles++; }
                while (wrappedX < x0) { wrappedX += range; cycles--; }

                bool reverse = false;
                if (infType == (int)InfinityType.Oscillate)
                    reverse = (Mathf.Abs(cycles) % 2) == 1;

                float evalX = wrappedX;
                if (reverse)
                {
                    float u = (wrappedX - x0) / range;
                    evalX = xN - u * range;
                }

                float baseV = EvaluateInRange(evalX);

                if (infType == (int)InfinityType.CycleRelative)
                {
                    float offsetPerCycle = (vN - v0);
                    baseV += offsetPerCycle * cycles;
                }

                return baseV;
            }

            return isPre ? v0 : vN;
        }

        private float BoundarySlope_First()
        {
            if (keys.Length < 2) return 0f;
            float dx = keys[1].timeFrame - keys[0].timeFrame;
            if (Mathf.Abs(dx) < 1e-6f) return 0f;
            return (keys[1].value - keys[0].value) / dx;
        }

        private float BoundarySlope_Last()
        {
            if (keys.Length < 2) return 0f;
            int n = keys.Length;
            float dx = keys[n - 1].timeFrame - keys[n - 2].timeFrame;
            if (Mathf.Abs(dx) < 1e-6f) return 0f;
            return (keys[n - 1].value - keys[n - 2].value) / dx;
        }

        // ---------------- decode ----------------

        private Key[] DecodeKeysFromAttributes()
        {
            var tvPairs = CollectKtvPairs();
            if (tvPairs.Count == 0)
                return Array.Empty<Key>();

            var inX = CollectIndexedFloatArray(".kix", "kix");
            var inY = CollectIndexedFloatArray(".kiy", "kiy");
            var outX = CollectIndexedFloatArray(".kox", "kox");
            var outY = CollectIndexedFloatArray(".koy", "koy");

            var outKeys = new Key[tvPairs.Count];
            for (int i = 0; i < tvPairs.Count; i++)
            {
                float t = tvPairs[i].t;
                float v = tvPairs[i].v;

                var k = new Key
                {
                    timeFrame = t,
                    value = v,
                    inSlope = 0f,
                    outSlope = 0f,
                    hasInSlope = false,
                    hasOutSlope = false
                };

                if (TryGetIndex(inX, i, out var ix) && TryGetIndex(inY, i, out var iy) && Mathf.Abs(ix) > 1e-8f)
                {
                    k.inSlope = iy / ix;
                    k.hasInSlope = true;
                }

                if (TryGetIndex(outX, i, out var ox) && TryGetIndex(outY, i, out var oy) && Mathf.Abs(ox) > 1e-8f)
                {
                    k.outSlope = oy / ox;
                    k.hasOutSlope = true;
                }

                outKeys[i] = k;
            }

            Array.Sort(outKeys, (a, b) => a.timeFrame.CompareTo(b.timeFrame));
            return outKeys;
        }

        private int FindSegment(float x)
        {
            int lo = 0;
            int hi = keys.Length - 2;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) >> 1;
                if (keys[mid].timeFrame <= x) lo = mid;
                else hi = mid - 1;
            }
            return Mathf.Clamp(lo, 0, keys.Length - 2);
        }

        private List<(float t, float v)> CollectKtvPairs()
        {
            var pairs = new List<(float t, float v)>();

            for (int i = 0; i < Attributes.Count; i++)
            {
                var a = Attributes[i];
                if (a == null || string.IsNullOrEmpty(a.Key) || a.Tokens == null || a.Tokens.Count < 2)
                    continue;

                if (!LooksLikeKtv(a.Key)) continue;

                for (int k = 0; k + 1 < a.Tokens.Count; k += 2)
                {
                    if (!TryF(a.Tokens[k], out var t)) continue;
                    if (!TryF(a.Tokens[k + 1], out var v)) continue;
                    pairs.Add((t, v));
                }
            }

            pairs.Sort((x, y) => x.t.CompareTo(y.t));
            return pairs;
        }

        private bool LooksLikeKtv(string key)
        {
            return key.Contains(".ktv", StringComparison.Ordinal) ||
                   key.Contains("keyTimeValue", StringComparison.Ordinal) ||
                   key.Contains("ktv[", StringComparison.Ordinal);
        }

        private Dictionary<int, float> CollectIndexedFloatArray(string shortKey, string shortKeyNoDot)
        {
            var map = new Dictionary<int, float>(64);

            for (int i = 0; i < Attributes.Count; i++)
            {
                var a = Attributes[i];
                if (a == null || string.IsNullOrEmpty(a.Key) || a.Tokens == null || a.Tokens.Count == 0)
                    continue;

                if (a.Key.IndexOf(shortKey, StringComparison.Ordinal) < 0 &&
                    a.Key.IndexOf(shortKeyNoDot, StringComparison.Ordinal) < 0)
                    continue;

                if (!TryParseRange(a.Key, out int start, out int end))
                    continue;

                int count = end - start + 1;
                if (count <= 0) continue;

                int n = Mathf.Min(count, a.Tokens.Count);
                for (int k = 0; k < n; k++)
                {
                    if (TryF(a.Tokens[k], out var f))
                        map[start + k] = f;
                }
            }

            return map;
        }

        private static bool TryParseRange(string key, out int start, out int end)
        {
            start = end = -1;
            int lb = key.LastIndexOf('[');
            int rb = key.LastIndexOf(']');
            if (lb < 0 || rb < 0 || rb <= lb + 1) return false;

            var inner = key.Substring(lb + 1, rb - lb - 1);
            int colon = inner.IndexOf(':');
            if (colon >= 0)
            {
                var a = inner.Substring(0, colon);
                var b = inner.Substring(colon + 1);
                if (!int.TryParse(a, NumberStyles.Integer, CultureInfo.InvariantCulture, out start)) return false;
                if (!int.TryParse(b, NumberStyles.Integer, CultureInfo.InvariantCulture, out end)) return false;
                if (end < start) (start, end) = (end, start);
                return true;
            }

            if (!int.TryParse(inner, NumberStyles.Integer, CultureInfo.InvariantCulture, out start)) return false;
            end = start;
            return true;
        }

        private static bool TryGetIndex(Dictionary<int, float> map, int index, out float v)
        {
            v = 0f;
            if (map == null) return false;
            return map.TryGetValue(index, out v);
        }

        private static bool TryF(string s, out float f)
        {
            s = StringParsingUtil.CleanNumericToken(s);
            return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out f);
        }

        private int ReadIntAttr(string key1, string key2, int defaultValue)
        {
            for (int i = 0; i < Attributes.Count; i++)
            {
                var a = Attributes[i];
                if (a == null) continue;
                if (!string.Equals(a.Key, key1, StringComparison.Ordinal) &&
                    !string.Equals(a.Key, key2, StringComparison.Ordinal))
                    continue;

                if (a.Tokens == null || a.Tokens.Count == 0 || a.Tokens[0] == null)
                    break;

                if (int.TryParse(StringParsingUtil.CleanNumericToken(a.Tokens[0]), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
                    return v;

                break;
            }

            return defaultValue;
        }

        private bool HasIncomingToAttr(string attr)
        {
            if (Connections == null || Connections.Count == 0) return false;

            for (int i = 0; i < Connections.Count; i++)
            {
                var c = Connections[i];
                if (c == null) continue;

                if (c.RoleForThisNode != ConnectionRole.Destination &&
                    c.RoleForThisNode != ConnectionRole.Both)
                    continue;

                var dstAttr = MayaPlugUtil.ExtractAttrPart(c.DstPlug);
                if (string.IsNullOrEmpty(dstAttr)) continue;

                if (dstAttr.Equals(attr, StringComparison.Ordinal) ||
                    dstAttr.Equals(attr.StartsWith(".") ? attr.Substring(1) : ("." + attr), StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static bool LooksLikeOutput(string srcAttr)
        {
            // animCurve output attr candidates
            if (string.IsNullOrEmpty(srcAttr)) return false;

            // "output" / "o" / ".output"
            if (srcAttr.Equals("output", StringComparison.Ordinal) ||
                srcAttr.Equals(".output", StringComparison.Ordinal) ||
                srcAttr.Equals("o", StringComparison.Ordinal) ||
                srcAttr.Equals(".o", StringComparison.Ordinal))
                return true;

            // sometimes nested (rare)
            return srcAttr.Contains("output", StringComparison.Ordinal);
        }

        private static void BuildArraysForUnity(Key[] ks, out float[] times, out float[] values, out float[] inTan, out float[] outTan)
        {
            if (ks == null || ks.Length == 0)
            {
                times = Array.Empty<float>();
                values = Array.Empty<float>();
                inTan = Array.Empty<float>();
                outTan = Array.Empty<float>();
                return;
            }

            int n = ks.Length;
            times = new float[n];
            values = new float[n];
            inTan = new float[n];
            outTan = new float[n];

            for (int i = 0; i < n; i++)
            {
                times[i] = ks[i].timeFrame;
                values[i] = ks[i].value;
            }

            // Tangents: if missing, estimate from neighbors so UnityCurve is stable
            for (int i = 0; i < n; i++)
            {
                float mIn = ks[i].hasInSlope ? ks[i].inSlope : EstimateIn(i, ks);
                float mOut = ks[i].hasOutSlope ? ks[i].outSlope : EstimateOut(i, ks);
                inTan[i] = mIn;
                outTan[i] = mOut;
            }
        }

        private static float EstimateIn(int i, Key[] ks)
        {
            int n = ks.Length;
            if (n < 2) return 0f;

            if (i <= 0)
                return Slope(ks[0], ks[1]);

            return Slope(ks[i - 1], ks[i]);
        }

        private static float EstimateOut(int i, Key[] ks)
        {
            int n = ks.Length;
            if (n < 2) return 0f;

            if (i >= n - 1)
                return Slope(ks[n - 2], ks[n - 1]);

            return Slope(ks[i], ks[i + 1]);
        }

        private static float Slope(Key a, Key b)
        {
            float dx = b.timeFrame - a.timeFrame;
            if (Mathf.Abs(dx) < 1e-6f) return 0f;
            return (b.value - a.value) / dx;
        }
    }
}
