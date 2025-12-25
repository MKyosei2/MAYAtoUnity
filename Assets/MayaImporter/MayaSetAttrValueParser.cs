using System;
using System.Collections.Generic;
using System.Globalization;

namespace MayaImporter.Core
{
    /// <summary>
    /// Parses setAttr value tokens into structured forms while keeping original tokens (lossless).
    /// Namespace/Unity-engine independent (no Unity types).
    /// </summary>
    public static class MayaSetAttrValueParser
    {
        public static bool TryParse(string typeName, List<string> valueTokens, out MayaAttrValueKind kind, out object parsed)
        {
            kind = MayaAttrValueKind.Tokens;
            parsed = null;

            if (valueTokens == null || valueTokens.Count == 0)
                return false;

            var tn = (typeName ?? "").Trim();

            // =========================================================
            // BlendShape frequently uses "pointArray"/"vectorArray"
            // setAttr -type "pointArray" <N> x y z x y z ...
            // =========================================================
            if (EqualsI(tn, "pointArray") || EqualsI(tn, "vectorArray") ||
                EqualsI(tn, "float3Array") || EqualsI(tn, "double3Array"))
            {
                if (TryParseCountPrefixedFloatArrayWithStride(valueTokens, stride: 3, out var arr3))
                {
                    kind = MayaAttrValueKind.FloatArray;   // stored as flat float[] (len = N*3)
                    parsed = arr3;
                    return true;
                }
                return false;
            }

            // (Optional) sometimes 2D arrays appear in some nodes
            if (EqualsI(tn, "float2Array") || EqualsI(tn, "double2Array"))
            {
                if (TryParseCountPrefixedFloatArrayWithStride(valueTokens, stride: 2, out var arr2))
                {
                    kind = MayaAttrValueKind.FloatArray;   // flat float[] (len = N*2)
                    parsed = arr2;
                    return true;
                }
                return false;
            }

            // 1) Typed array forms (count + values)
            if (EqualsI(tn, "stringArray"))
            {
                if (TryParseCountPrefixedStringArray(valueTokens, out var arr))
                {
                    kind = MayaAttrValueKind.StringArray;
                    parsed = arr;
                    return true;
                }
                return false;
            }

            if (EqualsI(tn, "Int32Array") || EqualsI(tn, "intArray"))
            {
                if (TryParseCountPrefixedIntArray(valueTokens, out var arr))
                {
                    kind = MayaAttrValueKind.IntArray;
                    parsed = arr;
                    return true;
                }
                return false;
            }

            if (EqualsI(tn, "doubleArray") || EqualsI(tn, "floatArray"))
            {
                if (TryParseCountPrefixedFloatArray(valueTokens, out var arr))
                {
                    kind = MayaAttrValueKind.FloatArray;
                    parsed = arr;
                    return true;
                }
                return false;
            }

            // 2) Matrix
            if (EqualsI(tn, "matrix") || EqualsI(tn, "doubleMatrix") || EqualsI(tn, "floatMatrix"))
            {
                if (TryParseFixedFloatCount(valueTokens, 16, out var m16))
                {
                    kind = MayaAttrValueKind.Matrix4x4;
                    parsed = m16; // float[16]
                    return true;
                }
                return false;
            }

            // 3) Vectors
            if (EqualsI(tn, "double2") || EqualsI(tn, "float2"))
            {
                if (TryParseFixedFloatCount(valueTokens, 2, out var v2))
                {
                    kind = MayaAttrValueKind.Vector2;
                    parsed = v2; // float[2]
                    return true;
                }
                return false;
            }

            if (EqualsI(tn, "double3") || EqualsI(tn, "float3"))
            {
                if (TryParseFixedFloatCount(valueTokens, 3, out var v3))
                {
                    kind = MayaAttrValueKind.Vector3;
                    parsed = v3; // float[3]
                    return true;
                }
                return false;
            }

            if (EqualsI(tn, "double4") || EqualsI(tn, "float4"))
            {
                if (TryParseFixedFloatCount(valueTokens, 4, out var v4))
                {
                    kind = MayaAttrValueKind.Vector4;
                    parsed = v4; // float[4]
                    return true;
                }
                return false;
            }

            // 4) Scalar typed
            if (EqualsI(tn, "bool") || EqualsI(tn, "boolean"))
            {
                if (TryParseBool(valueTokens[0], out var b))
                {
                    kind = MayaAttrValueKind.Bool;
                    parsed = b;
                    return true;
                }
                return false;
            }

            if (IsIntType(tn))
            {
                if (TryParseInt(valueTokens[0], out var iv))
                {
                    kind = MayaAttrValueKind.Int;
                    parsed = iv;
                    return true;
                }
                return false;
            }

            if (IsFloatType(tn))
            {
                if (TryParseFloat(valueTokens[0], out var fv))
                {
                    kind = MayaAttrValueKind.Float;
                    parsed = fv;
                    return true;
                }
                return false;
            }

            // 5) Untyped (no -type): best-effort without breaking arrays
            if (string.IsNullOrEmpty(tn))
            {
                if (valueTokens.Count == 1)
                {
                    var s = valueTokens[0];

                    if (TryParseBool(s, out var b))
                    {
                        kind = MayaAttrValueKind.Bool;
                        parsed = b;
                        return true;
                    }

                    if (TryParseInt(s, out var iv))
                    {
                        kind = MayaAttrValueKind.Int;
                        parsed = iv;
                        return true;
                    }

                    if (TryParseFloat(s, out var fv))
                    {
                        kind = MayaAttrValueKind.Float;
                        parsed = fv;
                        return true;
                    }

                    return false;
                }

                if (valueTokens.Count == 2 && TryParseFixedFloatCount(valueTokens, 2, out var v2))
                {
                    kind = MayaAttrValueKind.Vector2;
                    parsed = v2;
                    return true;
                }

                if (valueTokens.Count == 3 && TryParseFixedFloatCount(valueTokens, 3, out var v3))
                {
                    kind = MayaAttrValueKind.Vector3;
                    parsed = v3;
                    return true;
                }

                if (valueTokens.Count == 4 && TryParseFixedFloatCount(valueTokens, 4, out var v4))
                {
                    kind = MayaAttrValueKind.Vector4;
                    parsed = v4;
                    return true;
                }
            }

            return false;
        }

        // ---------------- helpers ----------------

        private static bool EqualsI(string a, string b) =>
            string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        private static bool IsIntType(string tn)
        {
            return EqualsI(tn, "long")
                || EqualsI(tn, "short")
                || EqualsI(tn, "byte")
                || EqualsI(tn, "int")
                || EqualsI(tn, "enum")
                || EqualsI(tn, "long2")
                || EqualsI(tn, "short2");
        }

        private static bool IsFloatType(string tn)
        {
            return EqualsI(tn, "double")
                || EqualsI(tn, "float")
                || EqualsI(tn, "doubleLinear")
                || EqualsI(tn, "doubleAngle")
                || EqualsI(tn, "time")
                || EqualsI(tn, "doubleTime")
                || EqualsI(tn, "floatLinear")
                || EqualsI(tn, "floatAngle");
        }

        private static bool TryParseBool(string s, out bool b)
        {
            b = false;
            if (s == null) return false;
            var t = s.Trim().ToLowerInvariant();
            if (t == "1" || t == "true" || t == "yes" || t == "on") { b = true; return true; }
            if (t == "0" || t == "false" || t == "no" || t == "off") { b = false; return true; }
            return false;
        }

        private static bool TryParseInt(string s, out int v)
        {
            return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v);
        }

        private static bool TryParseFloat(string s, out float v)
        {
            return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v);
        }

        private static bool TryParseFixedFloatCount(List<string> tokens, int count, out float[] arr)
        {
            arr = null;
            if (tokens == null || tokens.Count != count) return false;

            var tmp = new float[count];
            for (int i = 0; i < count; i++)
            {
                if (!TryParseFloat(tokens[i], out tmp[i]))
                    return false;
            }

            arr = tmp;
            return true;
        }

        private static bool TryParseCountPrefixedIntArray(List<string> tokens, out int[] arr)
        {
            arr = null;
            if (tokens == null || tokens.Count < 1) return false;
            if (!TryParseInt(tokens[0], out var n)) return false;
            if (n < 0) return false;
            if (tokens.Count < 1 + n) return false;

            var tmp = new int[n];
            for (int i = 0; i < n; i++)
            {
                if (!TryParseInt(tokens[1 + i], out tmp[i]))
                    return false;
            }

            arr = tmp;
            return true;
        }

        private static bool TryParseCountPrefixedFloatArray(List<string> tokens, out float[] arr)
        {
            arr = null;
            if (tokens == null || tokens.Count < 1) return false;
            if (!TryParseInt(tokens[0], out var n)) return false;
            if (n < 0) return false;
            if (tokens.Count < 1 + n) return false;

            var tmp = new float[n];
            for (int i = 0; i < n; i++)
            {
                if (!TryParseFloat(tokens[1 + i], out tmp[i]))
                    return false;
            }

            arr = tmp;
            return true;
        }

        private static bool TryParseCountPrefixedFloatArrayWithStride(List<string> tokens, int stride, out float[] arr)
        {
            arr = null;
            if (tokens == null || tokens.Count < 1) return false;
            if (stride <= 0) return false;
            if (!TryParseInt(tokens[0], out var n)) return false;
            if (n < 0) return false;

            long needed = 1L + (long)n * stride;
            if (tokens.Count < needed) return false;

            var tmp = new float[n * stride];
            int outIdx = 0;

            for (int i = 0; i < n * stride; i++)
            {
                if (!TryParseFloat(tokens[1 + i], out tmp[outIdx++]))
                    return false;
            }

            arr = tmp;
            return true;
        }

        private static bool TryParseCountPrefixedStringArray(List<string> tokens, out string[] arr)
        {
            arr = null;
            if (tokens == null || tokens.Count < 1) return false;
            if (!TryParseInt(tokens[0], out var n)) return false;
            if (n < 0) return false;
            if (tokens.Count < 1 + n) return false;

            var tmp = new string[n];
            for (int i = 0; i < n; i++)
                tmp[i] = tokens[1 + i];

            arr = tmp;
            return true;
        }
    }
}
