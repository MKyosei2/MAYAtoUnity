// Assets/MayaImporter/MayaAnimValueGraph.cs
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Animation
{
    /// <summary>
    /// Evaluate Maya attribute connection graph at runtime (best-effort).
    /// Designed to work with MayaSceneData (parsed .ma/.mb) + Unity-side MayaNodeComponentBase instances.
    /// </summary>
    public sealed partial class MayaAnimValueGraph
    {
        private readonly MayaSceneData _scene;
        private readonly List<MayaNodeComponentBase> _allNodes;
        private readonly List<MayaAnimCurveNodeComponent> _curves;

        // dstPlug -> list<srcPlug>
        private readonly Dictionary<string, List<string>> _incomingByDstPlug =
            new Dictionary<string, List<string>>(StringComparer.Ordinal);

        private readonly Dictionary<string, MayaNodeComponentBase> _nodeByExactName =
            new Dictionary<string, MayaNodeComponentBase>(StringComparer.Ordinal);

        private readonly Dictionary<string, MayaAnimCurveNodeComponent> _curveByExactName =
            new Dictionary<string, MayaAnimCurveNodeComponent>(StringComparer.Ordinal);

        // per-frame cache
        private float _currentFrame = float.NaN;
        private readonly Dictionary<string, float> _valueCache =
            new Dictionary<string, float>(StringComparer.Ordinal);

        // recursion guard
        private readonly HashSet<string> _evalStack =
            new HashSet<string>(StringComparer.Ordinal);

        public MayaAnimValueGraph(
            MayaSceneData scene,
            List<MayaNodeComponentBase> allNodes,
            List<MayaAnimCurveNodeComponent> curves)
        {
            _scene = scene;
            _allNodes = allNodes ?? new List<MayaNodeComponentBase>();
            _curves = curves ?? new List<MayaAnimCurveNodeComponent>();

            // index nodes
            for (int i = 0; i < _allNodes.Count; i++)
            {
                var n = _allNodes[i];
                if (n == null) continue;
                if (string.IsNullOrEmpty(n.NodeName)) continue;
                if (!_nodeByExactName.ContainsKey(n.NodeName))
                    _nodeByExactName[n.NodeName] = n;
            }

            // index curves
            for (int i = 0; i < _curves.Count; i++)
            {
                var c = _curves[i];
                if (c == null) continue;
                if (string.IsNullOrEmpty(c.NodeName)) continue;
                if (!_curveByExactName.ContainsKey(c.NodeName))
                    _curveByExactName[c.NodeName] = c;
            }

            // build incoming map from MayaSceneData.Connections
            if (_scene != null && _scene.Connections != null)
            {
                for (int i = 0; i < _scene.Connections.Count; i++)
                {
                    var con = _scene.Connections[i];
                    if (con == null) continue;

                    var dst = NormalizePlug(con.DstPlug);
                    var src = NormalizePlug(con.SrcPlug);

                    if (string.IsNullOrEmpty(dst) || string.IsNullOrEmpty(src))
                        continue;

                    if (!_incomingByDstPlug.TryGetValue(dst, out var list))
                    {
                        list = new List<string>(2);
                        _incomingByDstPlug[dst] = list;
                    }

                    list.Add(src);
                }
            }
        }

        /// <summary>
        /// Coverage reporters use this to avoid false "STUB" classification.
        /// If a nodeType is computed here, it is functionally implemented even without ApplyToUnity override.
        /// </summary>
        public static bool IsSupportedComputeNodeType(string nodeType)
        {
            if (string.IsNullOrEmpty(nodeType)) return false;

            if (nodeType.StartsWith("animBlendNode", StringComparison.OrdinalIgnoreCase)) return true;

            switch (nodeType)
            {
                case "blendTwoAttr":
                case "unitConversion":
                case "addDoubleLinear":
                case "multDoubleLinear":
                case "plusMinusAverage":

                // Common DG nodes used in rigs:
                case "multiplyDivide":
                case "condition":
                case "clamp":
                case "setRange":
                case "blendColors":
                case "reverse":
                case "remapValue":
                case "choice":
                case "blendWeighted":
                case "pairBlend":
                    return true;

                // Trigonometry:
                case "sin":
                case "sinDL":
                case "cos":
                case "cosDL":
                case "tan":
                case "tanDL":
                case "asin":
                case "asinDL":
                case "acos":
                case "acosDL":
                case "atan":
                case "atanDL":
                case "atan2":
                case "atan2DL":
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Public because MayaAnimationManager binds sources as: (frame) => _graph.EvaluatePlug(srcPlug, frame)
        /// </summary>
        public float EvaluatePlug(string plug, float frame)
        {
            if (string.IsNullOrEmpty(plug))
                return 0f;

            if (!Mathf.Approximately(_currentFrame, frame))
            {
                _currentFrame = frame;
                _valueCache.Clear();
                _evalStack.Clear();
            }

            plug = NormalizePlug(plug);

            if (_valueCache.TryGetValue(plug, out var cached))
                return cached;

            if (_evalStack.Contains(plug))
                return 0f; // break cycles

            _evalStack.Add(plug);

            float v = 0f;
            try
            {
                // If this plug is a destination that has an incoming, follow that (graph recursion)
                if (TryGetSingleIncoming(plug, out var src))
                {
                    v = EvaluatePlug(src, frame);
                }
                else
                {
                    // Otherwise interpret as node.attr and evaluate leaf
                    if (TryParsePlug(plug, out var nodeName, out var attrPath))
                    {
                        // animCurve node output
                        if (_curveByExactName.TryGetValue(nodeName, out var curve))
                        {
                            v = EvaluateAnimCurve(curve, frame);
                        }
                        else if (_nodeByExactName.TryGetValue(nodeName, out var node))
                        {
                            v = EvaluateComputeOrLocal(node, attrPath, frame);
                        }
                        else
                        {
                            v = 0f;
                        }
                    }
                }
            }
            catch
            {
                v = 0f;
            }
            finally
            {
                _evalStack.Remove(plug);
            }

            _valueCache[plug] = v;
            return v;
        }

        private float EvaluateAnimCurve(MayaAnimCurveNodeComponent curve, float frame)
        {
            if (curve == null) return 0f;

            // driven-key style: if curve.i is connected, evaluate by driver instead of time
            var inputPlug = curve.NodeName + ".i";
            if (TryGetSingleIncoming(inputPlug, out var driverSrc))
            {
                var driver = EvaluatePlug(driverSrc, frame);
                return curve.Evaluate(driver);
            }

            return curve.Evaluate(frame);
        }

        private float EvaluateComputeOrLocal(MayaNodeComponentBase node, string attrPath, float frame)
        {
            if (node == null) return 0f;
            if (string.IsNullOrEmpty(attrPath)) return 0f;

            var nodeType = node.NodeType ?? string.Empty;

            // animBlendNode*
            if (nodeType.StartsWith("animBlendNode", StringComparison.OrdinalIgnoreCase))
            {
                float a = GetInputValue(node, frame, "inputA", "ia", "input[0]");
                float b = GetInputValue(node, frame, "inputB", "ib", "input[1]");
                float w = GetInputValue(node, frame, "weight", "w", "weightA");
                w = Mathf.Clamp01(w);

                bool additive = nodeType.IndexOf("Additive", StringComparison.OrdinalIgnoreCase) >= 0;
                return additive ? (a + b * w) : Mathf.Lerp(a, b, w);
            }

            // blendTwoAttr
            if (nodeType == "blendTwoAttr")
            {
                float a = GetInputValue(node, frame, "input[0]", "input1", "i[0]", "i1");
                float b = GetInputValue(node, frame, "input[1]", "input2", "i[1]", "i2");
                float w = GetInputValue(node, frame, "attributesBlender", "ab", "weight", "w");
                w = Mathf.Clamp01(w);
                return Mathf.Lerp(a, b, w);
            }

            // unitConversion
            if (nodeType == "unitConversion")
            {
                float input = GetInputValue(node, frame, "input", "i", "inputValue");
                float factor = GetAttrFloat(node, 1f,
                    ".cf", "cf",
                    ".conversionFactor", "conversionFactor",
                    ".factor", "factor");
                return input * factor;
            }

            // addDoubleLinear
            if (nodeType == "addDoubleLinear")
            {
                float a = GetInputValue(node, frame, "input1", "i1");
                float b = GetInputValue(node, frame, "input2", "i2");
                return a + b;
            }

            // multDoubleLinear
            if (nodeType == "multDoubleLinear")
            {
                float a = GetInputValue(node, frame, "input1", "i1");
                float b = GetInputValue(node, frame, "input2", "i2");
                return a * b;
            }

            // plusMinusAverage (scalar best-effort)
            if (nodeType == "plusMinusAverage")
            {
                int op = Mathf.RoundToInt(GetAttrFloat(node, 1f, ".operation", "operation")); // 1=sum, 2=subtract, 3=average (best-effort)

                float sum = 0f;
                int count = 0;

                // Gather connected input1D[*]
                string prefix = node.NodeName + ".input1D[";
                foreach (var kv in _incomingByDstPlug)
                {
                    if (!kv.Key.StartsWith(prefix, StringComparison.Ordinal))
                        continue;

                    var list = kv.Value;
                    if (list == null || list.Count == 0) continue;

                    float v = EvaluatePlug(list[list.Count - 1], frame);
                    sum += v;
                    count++;
                }

                // If no connections, try local input1D[0..7]
                if (count == 0)
                {
                    for (int i = 0; i < 8; i++)
                    {
                        string key = $"input1D[{i}]";
                        float v = ReadLocalFloat(node, key, float.NaN);
                        if (float.IsNaN(v)) continue;
                        sum += v;
                        count++;
                    }
                }

                if (count == 0) return 0f;

                if (op == 3) return sum / Mathf.Max(1, count);

                if (op == 2)
                {
                    // subtract: input[0] - input[1] - ...
                    float first = 0f;
                    bool hasFirst = false;

                    string firstPlug = node.NodeName + ".input1D[0]";
                    if (TryGetSingleIncoming(firstPlug, out var src0))
                    {
                        first = EvaluatePlug(src0, frame);
                        hasFirst = true;
                    }
                    else
                    {
                        float lv = ReadLocalFloat(node, "input1D[0]", float.NaN);
                        if (!float.IsNaN(lv)) { first = lv; hasFirst = true; }
                    }

                    if (!hasFirst) first = sum; // fallback
                    return first - (sum - first);
                }

                return sum; // op==1 sum
            }

            // ----------------------------
            // Common DG nodes used in rigs
            // ----------------------------

            // multiplyDivide (scalar / per-axis best-effort)
            // operation: 1=Multiply, 2=Divide, 3=Power
            if (nodeType == "multiplyDivide")
            {
                if (!LooksLikeOutputAttr(attrPath))
                    return ReadLocalFloat(node, attrPath, 0f);

                int op = Mathf.RoundToInt(GetAttrFloat(node, 1f, ".operation", "operation", ".op", "op"));
                string axis = ExtractAxis(attrPath);

                float i1 = GetInputValue(node, frame, AxisKey("input1", "i1", axis), AxisKey("input1X", "i1x", axis), "input1");
                float i2 = GetInputValue(node, frame, AxisKey("input2", "i2", axis), AxisKey("input2X", "i2x", axis), "input2");

                switch (op)
                {
                    default:
                    case 1: return i1 * i2;
                    case 2: return Mathf.Abs(i2) < 1e-8f ? 0f : (i1 / i2);
                    case 3: return Mathf.Pow(i1, i2);
                }
            }

            // condition
            // operation: 0==,1!=,2>,3>=,4<,5<=
            if (nodeType == "condition")
            {
                if (!LooksLikeOutputAttr(attrPath))
                    return ReadLocalFloat(node, attrPath, 0f);

                float first = GetInputValue(node, frame, "firstTerm", "ft");
                float second = GetInputValue(node, frame, "secondTerm", "st");
                int op = Mathf.RoundToInt(GetAttrFloat(node, 0f, ".operation", "operation", ".op", "op"));

                bool cond = false;
                switch (op)
                {
                    case 0: cond = Mathf.Approximately(first, second); break;
                    case 1: cond = !Mathf.Approximately(first, second); break;
                    case 2: cond = first > second; break;
                    case 3: cond = first >= second; break;
                    case 4: cond = first < second; break;
                    case 5: cond = first <= second; break;
                    default: cond = false; break;
                }

                string axis = ExtractRGB(attrPath);

                if (string.Equals(attrPath, "outAlpha", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(attrPath, "oa", StringComparison.OrdinalIgnoreCase))
                {
                    float ta = GetInputValue(node, frame, "colorIfTrueA", "cta");
                    float fa = GetInputValue(node, frame, "colorIfFalseA", "cfa");
                    return cond ? ta : fa;
                }

                float t = GetInputValue(node, frame, AxisKey("colorIfTrue", "ct", axis));
                float f = GetInputValue(node, frame, AxisKey("colorIfFalse", "cf", axis));
                return cond ? t : f;
            }

            // clamp
            if (nodeType == "clamp")
            {
                if (!LooksLikeOutputAttr(attrPath))
                    return ReadLocalFloat(node, attrPath, 0f);

                string axis = ExtractRGB(attrPath);
                float input = GetInputValue(node, frame, AxisKey("input", "ip", axis), AxisKey("inputR", "ipr", axis), "input");
                float min = GetInputValue(node, frame, AxisKey("min", "mn", axis), AxisKey("minR", "mnr", axis), "min");
                float max = GetInputValue(node, frame, AxisKey("max", "mx", axis), AxisKey("maxR", "mxr", axis), "max");
                return Mathf.Clamp(input, min, max);
            }

            // setRange
            if (nodeType == "setRange")
            {
                if (!LooksLikeOutputAttr(attrPath))
                    return ReadLocalFloat(node, attrPath, 0f);

                string axis = ExtractAxis(attrPath);
                float v = GetInputValue(node, frame, AxisKey("value", "val", axis), "value");
                float omin = GetInputValue(node, frame, AxisKey("oldMin", "omn", axis), "oldMin");
                float omax = GetInputValue(node, frame, AxisKey("oldMax", "omx", axis), "oldMax");
                float nmin = GetInputValue(node, frame, AxisKey("min", "mn", axis), "min");
                float nmax = GetInputValue(node, frame, AxisKey("max", "mx", axis), "max");

                float t = Mathf.Abs(omax - omin) < 1e-8f ? 0f : (v - omin) / (omax - omin);
                return Mathf.Lerp(nmin, nmax, t);
            }

            // blendColors
            if (nodeType == "blendColors")
            {
                if (!LooksLikeOutputAttr(attrPath))
                    return ReadLocalFloat(node, attrPath, 0f);

                float blender = Mathf.Clamp01(GetInputValue(node, frame, "blender", "b"));
                string axis = ExtractRGB(attrPath);
                float c1 = GetInputValue(node, frame, AxisKey("color1", "c1", axis));
                float c2 = GetInputValue(node, frame, AxisKey("color2", "c2", axis));
                return Mathf.Lerp(c1, c2, blender);
            }

            // reverse
            if (nodeType == "reverse")
            {
                if (!LooksLikeOutputAttr(attrPath))
                    return ReadLocalFloat(node, attrPath, 0f);

                string axis = ExtractAxis(attrPath);
                float input = GetInputValue(node, frame, AxisKey("input", "ix", axis), "inputX", "inputY", "inputZ", "input");
                return 1f - input;
            }

            // remapValue
            if (nodeType == "remapValue")
            {
                if (!LooksLikeOutputAttr(attrPath))
                    return ReadLocalFloat(node, attrPath, 0f);

                float input = GetInputValue(node, frame, "inputValue", "iv");
                float inMin = GetInputValue(node, frame, "inputMin", "imn");
                float inMax = GetInputValue(node, frame, "inputMax", "imx");
                float outMin = GetInputValue(node, frame, "outputMin", "omn");
                float outMax = GetInputValue(node, frame, "outputMax", "omx");

                float t = Mathf.Abs(inMax - inMin) < 1e-8f ? 0f : (input - inMin) / (inMax - inMin);
                return Mathf.Lerp(outMin, outMax, t);
            }

            // ----------------------------
            // NEW: choice (scalar best-effort)
            // ----------------------------
            if (nodeType == "choice")
            {
                // choice は出力名が色々だが、数値用途なら「どの attr を聞かれても選択結果」を返す方が実用的
                float selF = GetInputValue(node, frame, "selector", "sel", "index");
                int sel = Mathf.Max(0, Mathf.RoundToInt(selF));

                // まずは input[sel] へ接続があるかを見る（最優先）
                string direct = $"{node.NodeName}.input[{sel}]";
                if (TryGetSingleIncoming(direct, out var srcDirect))
                    return EvaluatePlug(srcDirect, frame);

                // ローカル値
                float local = ReadLocalFloat(node, $"input[{sel}]", float.NaN);
                if (!float.IsNaN(local)) return local;

                // それでも無い場合：存在するインデックス集合から最寄りを選ぶ
                var idx = CollectArrayIndices(node, "input");
                if (idx != null && idx.Count > 0)
                {
                    int clamped = Mathf.Clamp(sel, 0, idx.Count - 1);
                    int chosenIndex = idx[clamped];

                    string plug = $"{node.NodeName}.input[{chosenIndex}]";
                    if (TryGetSingleIncoming(plug, out var src))
                        return EvaluatePlug(src, frame);

                    float lv = ReadLocalFloat(node, $"input[{chosenIndex}]", float.NaN);
                    if (!float.IsNaN(lv)) return lv;
                }

                return 0f;
            }

            // ----------------------------
            // NEW: blendWeighted
            // sum(input[i] * weight[i])
            // ----------------------------
            if (nodeType == "blendWeighted")
            {
                bool wantsWeightSum =
                    attrPath.Equals("weightSum", StringComparison.OrdinalIgnoreCase) ||
                    attrPath.Equals("ws", StringComparison.OrdinalIgnoreCase);

                var indices = CollectArrayIndicesUnion(node, "input", "weight");
                if (indices == null || indices.Count == 0)
                    return wantsWeightSum ? 0f : 0f;

                float sum = 0f;
                float wsum = 0f;

                for (int i = 0; i < indices.Count; i++)
                {
                    int idx = indices[i];
                    float input = GetArrayElementValue(node, frame, "input", idx, 0f);
                    float w = GetArrayElementValue(node, frame, "weight", idx, 1f);
                    sum += input * w;
                    wsum += w;
                }

                if (wantsWeightSum) return wsum;

                bool normalize = GetAttrFloat(node, 0f, ".normalizeWeights", "normalizeWeights", ".normalize", "normalize") >= 0.5f;
                if (normalize && Mathf.Abs(wsum) > 1e-8f) sum /= wsum;

                return sum;
            }

            // ----------------------------
            // NEW: pairBlend (scalar view of outTranslate/outRotate)
            // ----------------------------
            if (nodeType == "pairBlend")
            {
                // pairBlend は outTranslate/outRotate の各軸へ接続されるケースが多い
                // ここでは「要求された attrPath の軸だけ返す」実装にする

                float w = GetInputValue(node, frame, "weight", "w", "blend");
                w = Mathf.Clamp01(w);

                int rotInterp = Mathf.RoundToInt(GetAttrFloat(node, 0f,
                    ".rotInterpolation", "rotInterpolation",
                    ".rotationInterpolation", "rotationInterpolation",
                    ".ri", "ri"));

                int ro = Mathf.Clamp(Mathf.RoundToInt(GetAttrFloat(node, 0f,
                    ".rotateOrder", "rotateOrder",
                    ".ro", "ro")), 0, 5);

                Vector3 t1 = ReadVec3Input(node, frame,
                    packedKeys: new[] { "inTranslate1", "it1" },
                    xKeys: new[] { "inTranslate1X", "it1x" },
                    yKeys: new[] { "inTranslate1Y", "it1y" },
                    zKeys: new[] { "inTranslate1Z", "it1z" });

                Vector3 t2 = ReadVec3Input(node, frame,
                    packedKeys: new[] { "inTranslate2", "it2" },
                    xKeys: new[] { "inTranslate2X", "it2x" },
                    yKeys: new[] { "inTranslate2Y", "it2y" },
                    zKeys: new[] { "inTranslate2Z", "it2z" });

                Vector3 r1 = ReadVec3Input(node, frame,
                    packedKeys: new[] { "inRotate1", "ir1" },
                    xKeys: new[] { "inRotate1X", "ir1x" },
                    yKeys: new[] { "inRotate1Y", "ir1y" },
                    zKeys: new[] { "inRotate1Z", "ir1z" });

                Vector3 r2 = ReadVec3Input(node, frame,
                    packedKeys: new[] { "inRotate2", "ir2" },
                    xKeys: new[] { "inRotate2X", "ir2x" },
                    yKeys: new[] { "inRotate2Y", "ir2y" },
                    zKeys: new[] { "inRotate2Z", "ir2z" });

                Vector3 outT = Vector3.Lerp(t1, t2, w);
                Vector3 outR;

                if (rotInterp != 0)
                {
                    var q1 = MayaEulerRotationApplier.ToQuaternion(r1, ro);
                    var q2 = MayaEulerRotationApplier.ToQuaternion(r2, ro);
                    var qb = Quaternion.Slerp(q1, q2, w);
                    outR = MayaEulerRotationApplier.FromQuaternion(qb, ro);
                }
                else
                {
                    outR = Vector3.Lerp(r1, r2, w);
                }

                string axis = ExtractAxis(attrPath);

                bool wantTranslate =
                    attrPath.IndexOf("Translate", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    attrPath.IndexOf("outTranslate", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    attrPath.Equals("ot", StringComparison.OrdinalIgnoreCase);

                bool wantRotate =
                    attrPath.IndexOf("Rotate", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    attrPath.IndexOf("outRotate", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    attrPath.Equals("or", StringComparison.OrdinalIgnoreCase);

                if (wantTranslate)
                {
                    if (axis == "Y") return outT.y;
                    if (axis == "Z") return outT.z;
                    return outT.x;
                }

                if (wantRotate)
                {
                    if (axis == "Y") return outR.y;
                    if (axis == "Z") return outR.z;
                    return outR.x;
                }

                // 不明なら weight を返す（安全なデバッグ fallback）
                return w;
            }

            // ----------------------------
            // Trigonometry nodes
            // ----------------------------

            if (TryEvaluateTrigonometry(node, attrPath, frame, out var trigOut))
                return trigOut;

            if (TryEvaluateInverseTrigonometry(node, attrPath, frame, out var invOut))
                return invOut;

            // fallback: local float on requested attr
            return ReadLocalFloat(node, attrPath, 0f);
        }

        private float GetInputValue(MayaNodeComponentBase node, float frame, params string[] possibleAttrNames)
        {
            if (node == null) return 0f;

            // connections first
            for (int i = 0; i < possibleAttrNames.Length; i++)
            {
                var a = possibleAttrNames[i];
                if (string.IsNullOrEmpty(a)) continue;

                var dstPlug = node.NodeName + "." + a;
                if (TryGetSingleIncoming(dstPlug, out var src))
                    return EvaluatePlug(src, frame);
            }

            // local
            for (int i = 0; i < possibleAttrNames.Length; i++)
            {
                var a = possibleAttrNames[i];
                if (string.IsNullOrEmpty(a)) continue;

                var v = ReadLocalFloat(node, a, float.NaN);
                if (!float.IsNaN(v)) return v;
            }

            return 0f;
        }

        private bool TryGetSingleIncoming(string dstPlug, out string srcPlug)
        {
            srcPlug = null;
            if (string.IsNullOrEmpty(dstPlug)) return false;

            dstPlug = NormalizePlug(dstPlug);

            if (_incomingByDstPlug.TryGetValue(dstPlug, out var list) && list != null && list.Count > 0)
            {
                srcPlug = list[list.Count - 1];
                return true;
            }

            return false;
        }

        private static bool TryParsePlug(string plug, out string nodeName, out string attrPath)
        {
            nodeName = null;
            attrPath = null;

            if (string.IsNullOrEmpty(plug)) return false;

            plug = NormalizePlug(plug);
            int dot = plug.IndexOf('.');
            if (dot <= 0 || dot >= plug.Length - 1) return false;

            nodeName = plug.Substring(0, dot);
            attrPath = plug.Substring(dot + 1);
            return true;
        }

        private static float ReadLocalFloat(MayaNodeComponentBase node, string attrPath, float defaultValue)
        {
            if (node == null) return defaultValue;
            if (string.IsNullOrEmpty(attrPath)) return defaultValue;

            var attrs = node.Attributes;
            if (attrs == null) return defaultValue;

            string key = attrPath.StartsWith(".", StringComparison.Ordinal) ? attrPath.Substring(1) : attrPath;

            for (int i = 0; i < attrs.Count; i++)
            {
                var a = attrs[i];
                if (a == null || string.IsNullOrEmpty(a.Key)) continue;

                if (string.Equals(a.Key, key, StringComparison.Ordinal) ||
                    string.Equals(a.Key, "." + key, StringComparison.Ordinal))
                {
                    return TokenToFloat(a.Tokens, defaultValue);
                }
            }

            return defaultValue;
        }

        private static float TokenToFloat(List<string> tokens, float defaultValue)
        {
            if (tokens == null || tokens.Count == 0) return defaultValue;

            for (int i = tokens.Count - 1; i >= 0; i--)
            {
                var t = tokens[i];
                if (string.IsNullOrEmpty(t)) continue;
                t = t.Trim().Trim('"');

                if (float.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
                    return f;
            }

            return defaultValue;
        }

        private static float GetAttrFloat(MayaNodeComponentBase node, float defaultValue, params string[] keys)
        {
            if (node == null) return defaultValue;
            if (keys == null || keys.Length == 0) return defaultValue;

            var attrs = node.Attributes;
            if (attrs == null) return defaultValue;

            for (int i = 0; i < attrs.Count; i++)
            {
                var a = attrs[i];
                if (a == null || string.IsNullOrEmpty(a.Key)) continue;

                bool keyMatch = false;
                for (int k = 0; k < keys.Length; k++)
                {
                    var kk = keys[k];
                    if (string.IsNullOrEmpty(kk)) continue;
                    if (string.Equals(a.Key, kk, StringComparison.Ordinal))
                    {
                        keyMatch = true;
                        break;
                    }
                }

                if (!keyMatch) continue;

                return TokenToFloat(a.Tokens, defaultValue);
            }

            return defaultValue;
        }

        // =========================================================
        // NEW helpers (arrays / vec3)
        // =========================================================

        private List<int> CollectArrayIndices(MayaNodeComponentBase node, string arrayAttrBase)
        {
            if (node == null || string.IsNullOrEmpty(node.NodeName) || string.IsNullOrEmpty(arrayAttrBase))
                return null;

            var set = new HashSet<int>();

            // from connections
            string prefix = node.NodeName + "." + arrayAttrBase + "[";
            foreach (var kv in _incomingByDstPlug)
            {
                if (kv.Key == null) continue;
                if (!kv.Key.StartsWith(prefix, StringComparison.Ordinal)) continue;
                if (TryExtractIndex(kv.Key, out var idx)) set.Add(idx);
            }

            // from local attrs
            if (node.Attributes != null)
            {
                string p0 = arrayAttrBase + "[";
                string p1 = "." + arrayAttrBase + "[";
                for (int i = 0; i < node.Attributes.Count; i++)
                {
                    var a = node.Attributes[i];
                    if (a == null || string.IsNullOrEmpty(a.Key)) continue;
                    var k = a.Key;
                    if (k.StartsWith(p0, StringComparison.Ordinal) || k.StartsWith(p1, StringComparison.Ordinal))
                    {
                        if (TryExtractIndex(k, out var idx)) set.Add(idx);
                    }
                }
            }

            if (set.Count == 0) return null;
            var list = new List<int>(set);
            list.Sort();
            return list;
        }

        private List<int> CollectArrayIndicesUnion(MayaNodeComponentBase node, string a, string b)
        {
            var sa = CollectArrayIndices(node, a);
            var sb = CollectArrayIndices(node, b);

            if ((sa == null || sa.Count == 0) && (sb == null || sb.Count == 0)) return null;

            var set = new HashSet<int>();
            if (sa != null) for (int i = 0; i < sa.Count; i++) set.Add(sa[i]);
            if (sb != null) for (int i = 0; i < sb.Count; i++) set.Add(sb[i]);

            var list = new List<int>(set);
            list.Sort();
            return list;
        }

        private float GetArrayElementValue(MayaNodeComponentBase node, float frame, string arrayAttrBase, int idx, float defaultValue)
        {
            if (node == null || string.IsNullOrEmpty(node.NodeName)) return defaultValue;

            string attr = $"{arrayAttrBase}[{idx}]";
            string dstPlug = node.NodeName + "." + attr;

            if (TryGetSingleIncoming(dstPlug, out var src))
                return EvaluatePlug(src, frame);

            return ReadLocalFloat(node, attr, defaultValue);
        }

        private Vector3 ReadVec3Input(
            MayaNodeComponentBase node,
            float frame,
            string[] packedKeys,
            string[] xKeys,
            string[] yKeys,
            string[] zKeys)
        {
            // axis connections first
            float x = ReadAxis(node, frame, xKeys, float.NaN);
            float y = ReadAxis(node, frame, yKeys, float.NaN);
            float z = ReadAxis(node, frame, zKeys, float.NaN);

            if (!float.IsNaN(x) || !float.IsNaN(y) || !float.IsNaN(z))
            {
                if (float.IsNaN(x)) x = 0f;
                if (float.IsNaN(y)) y = 0f;
                if (float.IsNaN(z)) z = 0f;
                return new Vector3(x, y, z);
            }

            // packed local (double3)
            if (TryReadLocalPackedVec3(node, packedKeys, out var packed))
                return packed;

            // per-axis local
            x = ReadAxisLocal(node, xKeys, 0f);
            y = ReadAxisLocal(node, yKeys, 0f);
            z = ReadAxisLocal(node, zKeys, 0f);
            return new Vector3(x, y, z);
        }

        private float ReadAxis(MayaNodeComponentBase node, float frame, string[] keys, float def)
        {
            if (node == null || keys == null) return def;

            for (int i = 0; i < keys.Length; i++)
            {
                var a = keys[i];
                if (string.IsNullOrEmpty(a)) continue;

                var dstPlug = node.NodeName + "." + a;
                if (TryGetSingleIncoming(dstPlug, out var src))
                    return EvaluatePlug(src, frame);
            }

            return def;
        }

        private static float ReadAxisLocal(MayaNodeComponentBase node, string[] keys, float def)
        {
            if (node == null || keys == null) return def;

            for (int i = 0; i < keys.Length; i++)
            {
                var a = keys[i];
                if (string.IsNullOrEmpty(a)) continue;

                float v = ReadLocalFloat(node, a, float.NaN);
                if (!float.IsNaN(v)) return v;
            }

            return def;
        }

        private static bool TryReadLocalPackedVec3(MayaNodeComponentBase node, string[] packedKeys, out Vector3 v)
        {
            v = Vector3.zero;
            if (node == null || packedKeys == null) return false;
            if (node.Attributes == null) return false;

            for (int k = 0; k < packedKeys.Length; k++)
            {
                var key = packedKeys[k];
                if (string.IsNullOrEmpty(key)) continue;

                for (int i = 0; i < node.Attributes.Count; i++)
                {
                    var a = node.Attributes[i];
                    if (a == null || string.IsNullOrEmpty(a.Key) || a.Tokens == null) continue;

                    if (!string.Equals(a.Key, key, StringComparison.Ordinal) &&
                        !string.Equals(a.Key, "." + key, StringComparison.Ordinal))
                        continue;

                    if (TryTokenToVec3(a.Tokens, out v))
                        return true;
                }
            }

            return false;
        }

        private static bool TryTokenToVec3(List<string> tokens, out Vector3 v)
        {
            v = Vector3.zero;
            if (tokens == null || tokens.Count == 0) return false;

            float x = 0f, y = 0f, z = 0f;
            int found = 0;

            for (int i = 0; i < tokens.Count; i++)
            {
                var t = tokens[i];
                if (string.IsNullOrEmpty(t)) continue;
                t = t.Trim().Trim('"');

                if (!float.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
                    continue;

                if (found == 0) x = f;
                else if (found == 1) y = f;
                else if (found == 2) z = f;

                found++;
                if (found >= 3)
                {
                    v = new Vector3(x, y, z);
                    return true;
                }
            }

            return false;
        }

        private static bool TryExtractIndex(string key, out int idx)
        {
            idx = -1;
            if (string.IsNullOrEmpty(key)) return false;

            int lb = key.IndexOf('[');
            int rb = key.IndexOf(']', lb + 1);
            if (lb < 0 || rb < 0 || rb <= lb + 1) return false;

            var inner = key.Substring(lb + 1, rb - lb - 1); // "3" or "0:7"
            int colon = inner.IndexOf(':');
            if (colon >= 0) inner = inner.Substring(0, colon);
            return int.TryParse(inner, NumberStyles.Integer, CultureInfo.InvariantCulture, out idx);
        }

        private static string NormalizePlug(string plug)
        {
            if (string.IsNullOrEmpty(plug)) return plug;
            plug = plug.Trim();
            if (plug.Length >= 2 && plug[0] == '"' && plug[plug.Length - 1] == '"')
                plug = plug.Substring(1, plug.Length - 2);
            return plug;
        }

        // --------------------- small helpers ---------------------

        private static bool LooksLikeOutputAttr(string attrPath)
        {
            if (string.IsNullOrEmpty(attrPath)) return false;
            return attrPath.IndexOf("out", StringComparison.OrdinalIgnoreCase) >= 0
                || attrPath.IndexOf("output", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string ExtractAxis(string attrPath)
        {
            if (string.IsNullOrEmpty(attrPath)) return "";

            if (attrPath.EndsWith("X", StringComparison.OrdinalIgnoreCase)) return "X";
            if (attrPath.EndsWith("Y", StringComparison.OrdinalIgnoreCase)) return "Y";
            if (attrPath.EndsWith("Z", StringComparison.OrdinalIgnoreCase)) return "Z";

            if (attrPath.IndexOf("X", StringComparison.Ordinal) >= 0 && attrPath.EndsWith("x", StringComparison.OrdinalIgnoreCase)) return "X";
            if (attrPath.IndexOf("Y", StringComparison.Ordinal) >= 0 && attrPath.EndsWith("y", StringComparison.OrdinalIgnoreCase)) return "Y";
            if (attrPath.IndexOf("Z", StringComparison.Ordinal) >= 0 && attrPath.EndsWith("z", StringComparison.OrdinalIgnoreCase)) return "Z";

            return "";
        }

        private static string ExtractRGB(string attrPath)
        {
            if (string.IsNullOrEmpty(attrPath)) return "";
            if (attrPath.EndsWith("R", StringComparison.OrdinalIgnoreCase)) return "R";
            if (attrPath.EndsWith("G", StringComparison.OrdinalIgnoreCase)) return "G";
            if (attrPath.EndsWith("B", StringComparison.OrdinalIgnoreCase)) return "B";
            return "";
        }

        private static string AxisKey(string longBase, string shortBase, string axis)
        {
            if (string.IsNullOrEmpty(axis)) return longBase;
            return longBase + axis;
        }
    }
}
