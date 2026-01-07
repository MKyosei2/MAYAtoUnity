// Assets/MayaImporter/MayaExpressionRuntime.cs
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Animation
{
    /// <summary>
    /// Phase5:
    /// Unity-only subset evaluator for Maya "expression" nodes.
    ///
    /// Goals:
    /// - 100%: preserve expression source + parsed assignments as Unity components (no Maya API required).
    /// - 100点: enable a practical subset used in rigs:
    ///     - assigns to transform channels: tx/ty/tz, rx/ry/rz, sx/sy/sz
    ///     - supports: + - * / ^, parentheses, unary -, numbers
    ///     - supports: time/frame variables, pi/e, and basic functions
    ///     - supports: reading other transform channels via "node.attr"
    ///
    /// Anything outside the supported subset is preserved but not executed.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MayaExpressionRuntime : MonoBehaviour
    {
        [Serializable]
        public struct Assignment
        {
            public string targetNodeName; // Maya node name (we map by GameObject name)
            public string targetAttr;     // tx/ty/tz/rx/ry/rz/sx/sy/sz
            public string rhsExpression;  // right hand side as text
        }

        [Header("Maya Identity")]
        public string mayaExpressionNodeName;

        [Header("Source")]
        [TextArea(3, 20)]
        public string expressionSource;

        [Header("Execution")]
        public bool execute = true;

        [Tooltip("Coordinate conversion used for TRS channels (MirrorZ is self-inverse).")]
        public CoordinateConversion conversion = CoordinateConversion.MayaToUnity_MirrorZ;

        [Header("Parsed Assignments (subset)")]
        public Assignment[] assignments = Array.Empty<Assignment>();

        private readonly Dictionary<string, Transform> _targetCache = new Dictionary<string, Transform>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<int>> _indicesByTarget = new Dictionary<string, List<int>>(StringComparer.Ordinal);

        private ExpressionEvaluator _evaluator;

        public void Configure(
            string exprNodeName,
            string source,
            Assignment[] parsedAssignments,
            CoordinateConversion conv)
        {
            mayaExpressionNodeName = exprNodeName ?? "";
            expressionSource = source ?? "";
            assignments = parsedAssignments ?? Array.Empty<Assignment>();
            conversion = conv;

            RebuildIndices();
        }

        private void Awake()
        {
            if (_evaluator == null)
                _evaluator = new ExpressionEvaluator(ResolveVariableOrPlug);
            RebuildIndices();
        }

        private void OnEnable()
        {
            if (_evaluator == null)
                _evaluator = new ExpressionEvaluator(ResolveVariableOrPlug);
            RebuildIndices();
        }

        private void RebuildIndices()
        {
            _indicesByTarget.Clear();
            if (assignments == null) return;

            for (int i = 0; i < assignments.Length; i++)
            {
                var a = assignments[i];
                if (string.IsNullOrEmpty(a.targetNodeName) || string.IsNullOrEmpty(a.targetAttr))
                    continue;

                if (!_indicesByTarget.TryGetValue(a.targetNodeName, out var list))
                {
                    list = new List<int>(4);
                    _indicesByTarget[a.targetNodeName] = list;
                }
                list.Add(i);
            }
        }

        /// <summary>Called by MayaRuntimePostSampleSolvers after clip sample and DG evaluation.</summary>
        public void Evaluate(float frame)
        {
            if (!execute) return;
            if (assignments == null || assignments.Length == 0) return;

            if (_evaluator == null)
                _evaluator = new ExpressionEvaluator(ResolveVariableOrPlug);

            // Evaluate per target for stable partial-channel writes.
            foreach (var kv in _indicesByTarget)
            {
                var targetName = kv.Key;
                var indices = kv.Value;
                if (indices == null || indices.Count == 0) continue;

                var tf = GetTargetTransform(targetName);
                if (tf == null) continue;

                var eulerApplier = tf.GetComponent<MayaEulerRotationApplier>();

                // Read current values (Maya space best-effort)
                Vector3 mayaT = UnityToMayaPosition(tf.localPosition, conversion);
                Vector3 mayaS = tf.localScale;

                Vector3 mayaR;
                if (eulerApplier != null)
                {
                    mayaR = eulerApplier.eulerDeg;
                }
                else
                {
                    // best-effort: convert Unity Euler back to Maya channels
                    mayaR = MayaToUnityConversion.ConvertEulerDegrees(tf.localEulerAngles, conversion);
                }

                // Apply all assignments for this target
                for (int i = 0; i < indices.Count; i++)
                {
                    var a = assignments[indices[i]];
                    if (string.IsNullOrEmpty(a.rhsExpression)) continue;

                    float v;
                    try
                    {
                        v = _evaluator.Evaluate(a.rhsExpression, frame);
                    }
                    catch
                    {
                        continue;
                    }

                    switch (a.targetAttr)
                    {
                        case "tx": mayaT.x = v; break;
                        case "ty": mayaT.y = v; break;
                        case "tz": mayaT.z = v; break;

                        case "rx": mayaR.x = v; break;
                        case "ry": mayaR.y = v; break;
                        case "rz": mayaR.z = v; break;

                        case "sx": mayaS.x = v; break;
                        case "sy": mayaS.y = v; break;
                        case "sz": mayaS.z = v; break;
                    }
                }

                // Write back (Unity space)
                tf.localPosition = MayaToUnityConversion.ConvertPosition(mayaT, conversion);
                tf.localScale = mayaS;

                if (eulerApplier != null)
                {
                    eulerApplier.eulerDeg = mayaR;
                }
                else
                {
                    // Fallback: xyz order (0)
                    var unityEuler = MayaToUnityConversion.ConvertEulerDegrees(mayaR, conversion);
                    tf.localRotation = MayaEulerRotationApplier.ToQuaternion(unityEuler, 0);
                }
            }
        }

        private Transform GetTargetTransform(string mayaNodeName)
        {
            if (string.IsNullOrEmpty(mayaNodeName)) return null;

            if (_targetCache.TryGetValue(mayaNodeName, out var tf) && tf != null)
                return tf;

            var go = GameObject.Find(mayaNodeName);
            tf = go != null ? go.transform : null;
            _targetCache[mayaNodeName] = tf;
            return tf;
        }

        private static Vector3 UnityToMayaPosition(Vector3 unityLocalPos, CoordinateConversion conv)
        {
            // MirrorZ is self-inverse.
            return MayaToUnityConversion.ConvertPosition(unityLocalPos, conv);
        }

        private float ResolveVariableOrPlug(string token, float frame)
        {
            if (string.IsNullOrEmpty(token)) return 0f;

            // variables
            if (string.Equals(token, "time", StringComparison.OrdinalIgnoreCase)) return frame;
            if (string.Equals(token, "frame", StringComparison.OrdinalIgnoreCase)) return frame;
            if (string.Equals(token, "pi", StringComparison.OrdinalIgnoreCase)) return (float)Math.PI;
            if (string.Equals(token, "e", StringComparison.OrdinalIgnoreCase)) return (float)Math.E;

            // plug reference like node.attr
            int dot = token.LastIndexOf('.');
            if (dot > 0 && dot < token.Length - 1)
            {
                string node = token.Substring(0, dot);
                string attr = token.Substring(dot + 1);
                return ResolveTransformPlug(node, attr);
            }

            return 0f;
        }

        private float ResolveTransformPlug(string nodeName, string attr)
        {
            if (string.IsNullOrEmpty(nodeName) || string.IsNullOrEmpty(attr)) return 0f;

            // Normalize attr (strip leading '.')
            if (attr.StartsWith(".", StringComparison.Ordinal)) attr = attr.Substring(1);

            var tf = GetTargetTransform(nodeName);
            if (tf == null) return 0f;

            switch (attr)
            {
                case "tx": return UnityToMayaPosition(tf.localPosition, conversion).x;
                case "ty": return UnityToMayaPosition(tf.localPosition, conversion).y;
                case "tz": return UnityToMayaPosition(tf.localPosition, conversion).z;

                case "sx": return tf.localScale.x;
                case "sy": return tf.localScale.y;
                case "sz": return tf.localScale.z;

                case "rx":
                case "ry":
                case "rz":
                {
                    var applier = tf.GetComponent<MayaEulerRotationApplier>();
                    Vector3 mayaR = applier != null
                        ? applier.eulerDeg
                        : MayaToUnityConversion.ConvertEulerDegrees(tf.localEulerAngles, conversion);

                    if (attr == "rx") return mayaR.x;
                    if (attr == "ry") return mayaR.y;
                    return mayaR.z;
                }
            }

            return 0f;
        }

        // ============================================================
        // Minimal expression evaluator (safe subset)
        // ============================================================
        private sealed class ExpressionEvaluator
        {
            private readonly Func<string, float, float> _resolveToken;

            public ExpressionEvaluator(Func<string, float, float> resolveToken)
            {
                _resolveToken = resolveToken;
            }

            public float Evaluate(string expr, float frame)
            {
                if (string.IsNullOrWhiteSpace(expr)) return 0f;

                var tokens = Tokenize(expr);
                var rpn = ToRpn(tokens);
                return EvalRpn(rpn, frame);
            }

            private enum TokenType { Number, Identifier, Operator, LParen, RParen, Comma }

            private readonly struct Tok
            {
                public readonly TokenType Type;
                public readonly string Text;
                public readonly float Number;

                public Tok(TokenType type, string text, float number = 0f)
                {
                    Type = type;
                    Text = text;
                    Number = number;
                }
            }

            private static List<Tok> Tokenize(string s)
            {
                var list = new List<Tok>(64);
                int i = 0;
                while (i < s.Length)
                {
                    char c = s[i];

                    if (char.IsWhiteSpace(c)) { i++; continue; }

                    // number
                    if (char.IsDigit(c) || (c == '.' && i + 1 < s.Length && char.IsDigit(s[i + 1])))
                    {
                        int start = i;
                        i++;
                        while (i < s.Length)
                        {
                            char cc = s[i];
                            if (char.IsDigit(cc) || cc == '.' || cc == 'e' || cc == 'E' || cc == '+' || cc == '-')
                            {
                                // keep +,- only if in exponent
                                if ((cc == '+' || cc == '-') && i > start)
                                {
                                    char prev = s[i - 1];
                                    if (prev != 'e' && prev != 'E') break;
                                }
                                i++;
                                continue;
                            }
                            break;
                        }

                        var sub = s.Substring(start, i - start);
                        if (float.TryParse(sub, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
                            list.Add(new Tok(TokenType.Number, sub, f));
                        else
                            list.Add(new Tok(TokenType.Number, sub, 0f));
                        continue;
                    }

                    // identifier (includes Maya node names: |, :, _, ., digits)
                    if (char.IsLetter(c) || c == '_' || c == '|' || c == ':')
                    {
                        int start = i;
                        i++;
                        while (i < s.Length)
                        {
                            char cc = s[i];
                            if (char.IsLetterOrDigit(cc) || cc == '_' || cc == '|' || cc == ':' || cc == '.')
                            {
                                i++;
                                continue;
                            }
                            break;
                        }
                        var id = s.Substring(start, i - start);
                        list.Add(new Tok(TokenType.Identifier, id));
                        continue;
                    }

                    // symbols
                    switch (c)
                    {
                        case '+':
                        case '-':
                        case '*':
                        case '/':
                        case '^':
                            list.Add(new Tok(TokenType.Operator, c.ToString()));
                            i++;
                            continue;
                        case '(':
                            list.Add(new Tok(TokenType.LParen, "("));
                            i++;
                            continue;
                        case ')':
                            list.Add(new Tok(TokenType.RParen, ")"));
                            i++;
                            continue;
                        case ',':
                            list.Add(new Tok(TokenType.Comma, ","));
                            i++;
                            continue;
                        default:
                            // ignore unknown chars to keep safe
                            i++;
                            continue;
                    }
                }

                return list;
            }

            private static int Prec(string op) => op switch
            {
                "neg" => 4,
                "^" => 3,
                "*" or "/" => 2,
                "+" or "-" => 1,
                _ => 0
            };

            private static bool RightAssoc(string op) => op == "^" || op == "neg";

            private static bool IsFunction(string id)
            {
                if (string.IsNullOrEmpty(id)) return false;
                switch (id.ToLowerInvariant())
                {
                    case "sin":
                    case "cos":
                    case "tan":
                    case "asin":
                    case "acos":
                    case "atan":
                    case "atan2":
                    case "abs":
                    case "sqrt":
                    case "pow":
                    case "min":
                    case "max":
                    case "clamp":
                    case "floor":
                    case "ceil":
                    case "exp":
                    case "log":
                        return true;
                    default:
                        return false;
                }
            }

            private static List<Tok> ToRpn(List<Tok> input)
            {
                var output = new List<Tok>(input.Count);
                var stack = new Stack<Tok>();

                Tok? prev = null;

                for (int i = 0; i < input.Count; i++)
                {
                    var t = input[i];

                    if (t.Type == TokenType.Number)
                    {
                        output.Add(t);
                        prev = t;
                        continue;
                    }

                    if (t.Type == TokenType.Identifier)
                    {
                        // function call if next is '(' and identifier is a known function
                        bool func = (i + 1 < input.Count) && input[i + 1].Type == TokenType.LParen && IsFunction(t.Text);
                        if (func)
                        {
                            stack.Push(new Tok(TokenType.Identifier, t.Text)); // function marker on stack
                        }
                        else
                        {
                            output.Add(t);
                        }
                        prev = t;
                        continue;
                    }

                    if (t.Type == TokenType.Comma)
                    {
                        while (stack.Count > 0 && stack.Peek().Type != TokenType.LParen)
                            output.Add(stack.Pop());
                        prev = t;
                        continue;
                    }

                    if (t.Type == TokenType.Operator)
                    {
                        // unary minus => neg
                        string op = t.Text;
                        if (op == "-")
                        {
                            bool isUnary = (prev == null) ||
                                           (prev.Value.Type == TokenType.Operator) ||
                                           (prev.Value.Type == TokenType.LParen) ||
                                           (prev.Value.Type == TokenType.Comma);
                            if (isUnary) op = "neg";
                        }

                        var o1 = new Tok(TokenType.Operator, op);

                        while (stack.Count > 0)
                        {
                            var top = stack.Peek();
                            if (top.Type != TokenType.Operator) break;

                            var o2 = top.Text;
                            int p1 = Prec(op);
                            int p2 = Prec(o2);

                            if ((RightAssoc(op) && p1 < p2) || (!RightAssoc(op) && p1 <= p2))
                                output.Add(stack.Pop());
                            else
                                break;
                        }

                        stack.Push(o1);
                        prev = t;
                        continue;
                    }

                    if (t.Type == TokenType.LParen)
                    {
                        stack.Push(t);
                        prev = t;
                        continue;
                    }

                    if (t.Type == TokenType.RParen)
                    {
                        while (stack.Count > 0 && stack.Peek().Type != TokenType.LParen)
                            output.Add(stack.Pop());

                        if (stack.Count > 0 && stack.Peek().Type == TokenType.LParen)
                            stack.Pop(); // discard '('

                        // if function is on stack, pop it to output
                        if (stack.Count > 0 && stack.Peek().Type == TokenType.Identifier && IsFunction(stack.Peek().Text))
                            output.Add(stack.Pop());

                        prev = t;
                        continue;
                    }
                }

                while (stack.Count > 0)
                    output.Add(stack.Pop());

                return output;
            }

            private float EvalRpn(List<Tok> rpn, float frame)
            {
                var st = new Stack<float>(32);

                for (int i = 0; i < rpn.Count; i++)
                {
                    var t = rpn[i];

                    if (t.Type == TokenType.Number)
                    {
                        st.Push(t.Number);
                        continue;
                    }

                    if (t.Type == TokenType.Identifier)
                    {
                        // function call
                        if (IsFunction(t.Text))
                        {
                            EvalFunction(t.Text, st);
                            continue;
                        }

                        // variable / plug
                        st.Push(_resolveToken != null ? _resolveToken(t.Text, frame) : 0f);
                        continue;
                    }

                    if (t.Type == TokenType.Operator)
                    {
                        switch (t.Text)
                        {
                            case "neg":
                                st.Push(st.Count > 0 ? -st.Pop() : 0f);
                                break;

                            case "+":
                            case "-":
                            case "*":
                            case "/":
                            case "^":
                            {
                                float b = st.Count > 0 ? st.Pop() : 0f;
                                float a = st.Count > 0 ? st.Pop() : 0f;
                                st.Push(t.Text switch
                                {
                                    "+" => a + b,
                                    "-" => a - b,
                                    "*" => a * b,
                                    "/" => Mathf.Abs(b) < 1e-8f ? 0f : (a / b),
                                    "^" => Mathf.Pow(a, b),
                                    _ => 0f
                                });
                                break;
                            }
                        }
                    }
                }

                return st.Count > 0 ? st.Pop() : 0f;
            }

            private static void EvalFunction(string fn, Stack<float> st)
            {
                fn = fn.ToLowerInvariant();

                float Pop() => st.Count > 0 ? st.Pop() : 0f;

                switch (fn)
                {
                    case "sin": st.Push(Mathf.Sin(Pop())); break;
                    case "cos": st.Push(Mathf.Cos(Pop())); break;
                    case "tan": st.Push(Mathf.Tan(Pop())); break;

                    case "asin": st.Push(Mathf.Asin(Pop())); break;
                    case "acos": st.Push(Mathf.Acos(Pop())); break;
                    case "atan": st.Push(Mathf.Atan(Pop())); break;

                    case "atan2":
                    {
                        float b = Pop();
                        float a = Pop();
                        st.Push(Mathf.Atan2(a, b));
                        break;
                    }

                    case "abs": st.Push(Mathf.Abs(Pop())); break;
                    case "sqrt": st.Push(Mathf.Sqrt(Mathf.Max(0f, Pop()))); break;
                    case "pow":
                    {
                        float b = Pop();
                        float a = Pop();
                        st.Push(Mathf.Pow(a, b));
                        break;
                    }
                    case "min":
                    {
                        float b = Pop();
                        float a = Pop();
                        st.Push(Mathf.Min(a, b));
                        break;
                    }
                    case "max":
                    {
                        float b = Pop();
                        float a = Pop();
                        st.Push(Mathf.Max(a, b));
                        break;
                    }
                    case "clamp":
                    {
                        float max = Pop();
                        float min = Pop();
                        float v = Pop();
                        st.Push(Mathf.Clamp(v, min, max));
                        break;
                    }

                    case "floor": st.Push(Mathf.Floor(Pop())); break;
                    case "ceil": st.Push(Mathf.Ceil(Pop())); break;

                    case "exp": st.Push(Mathf.Exp(Pop())); break;
                    case "log": st.Push(Mathf.Log(Mathf.Max(1e-8f, Pop()))); break;
                }
            }
        }
    }
}
