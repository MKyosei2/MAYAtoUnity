using System;
using System.Globalization;

namespace MayaImporter.Utils
{
    /// <summary>
    /// InvariantCulture 前提の数値処理ユーティリティ。
    /// Maya .ma トークン（Tokenizer が quote を除去済み）を安全に扱う。
    /// </summary>
    public static class MathUtil
    {
        public static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        public static bool TryParseInt(string s, out int v)
        {
            s = StringParsingUtil.CleanNumericToken(s);
            return int.TryParse(s, NumberStyles.Integer, Invariant, out v);
        }

        public static bool TryParseLong(string s, out long v)
        {
            s = StringParsingUtil.CleanNumericToken(s);
            return long.TryParse(s, NumberStyles.Integer, Invariant, out v);
        }

        public static bool TryParseFloat(string s, out float v)
        {
            s = StringParsingUtil.CleanNumericToken(s);
            return float.TryParse(s, NumberStyles.Float, Invariant, out v);
        }

        public static bool TryParseDouble(string s, out double v)
        {
            s = StringParsingUtil.CleanNumericToken(s);
            return double.TryParse(s, NumberStyles.Float, Invariant, out v);
        }

        public static int ParseIntOr(string s, int fallback = 0)
            => TryParseInt(s, out var v) ? v : fallback;

        public static float ParseFloatOr(string s, float fallback = 0f)
            => TryParseFloat(s, out var v) ? v : fallback;

        public static double ParseDoubleOr(string s, double fallback = 0.0)
            => TryParseDouble(s, out var v) ? v : fallback;

        public static bool TryParseBool(string s, out bool v)
        {
            if (s == null)
            {
                v = false;
                return false;
            }

            s = s.Trim();
            if (s.Length == 0)
            {
                v = false;
                return false;
            }

            // Maya では 0/1, yes/no, true/false が来うる
            if (string.Equals(s, "1", StringComparison.Ordinal) ||
                string.Equals(s, "true", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(s, "yes", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(s, "on", StringComparison.OrdinalIgnoreCase))
            {
                v = true;
                return true;
            }

            if (string.Equals(s, "0", StringComparison.Ordinal) ||
                string.Equals(s, "false", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(s, "no", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(s, "off", StringComparison.OrdinalIgnoreCase))
            {
                v = false;
                return true;
            }

            // 最後の手段：数値として扱う
            if (TryParseInt(s, out var i))
            {
                v = i != 0;
                return true;
            }

            v = false;
            return false;
        }

        public static float Clamp01(float v)
        {
            if (v < 0f) return 0f;
            if (v > 1f) return 1f;
            return v;
        }

        public static float SafeDivide(float num, float den, float fallback = 0f)
            => Math.Abs(den) < 1e-12f ? fallback : (num / den);

        public static double SafeDivide(double num, double den, double fallback = 0.0)
            => Math.Abs(den) < 1e-24 ? fallback : (num / den);

        public static bool Approximately(float a, float b, float eps = 1e-6f)
            => Math.Abs(a - b) <= eps;

        public static float Deg2Rad(float deg) => (float)(Math.PI / 180.0) * deg;
        public static float Rad2Deg(float rad) => (float)(180.0 / Math.PI) * rad;
    }
}
