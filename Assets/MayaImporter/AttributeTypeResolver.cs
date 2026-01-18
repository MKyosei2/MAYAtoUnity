using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace MayaImporter.Utils
{
    /// <summary>
    /// Maya Attribute ðu^ÉË¶¹¸vµ¤½ßÌ[eBeBB
    /// Core ¤Ì Attribute NX¼ðêØQÆµÈ¢iUnityPÌ / APIÈµOñjB
    ///
    /// vF
    /// - object attr ©ç
    ///   - string TypeName
    ///   - IReadOnlyList<string> ValueTokens
    /// ðtNVÅæ¾Å«é±Æ
    /// </summary>
    public static class AttributeTypeResolver
    {
        public enum MayaAttrKind
        {
            Unknown = 0,
            Bool,
            Int,
            Float,
            Double,
            String,
            Vector2,
            Vector3,
            Vector4,
            Matrix4x4,
            IntArray,
            FloatArray,
            DoubleArray,
            StringArray
        }

        // =========================
        // Public APIi^ñË¶j
        // =========================

        public static MayaAttrKind GuessKind(object attr)
        {
            if (!TryGetTokens(attr, out var tokens, out var typeName))
                return MayaAttrKind.Unknown;

            return GuessKind(typeName, tokens);
        }

        public static bool TryReadBool(object attr, out bool v)
        {
            v = false;
            if (!TryGetTokens(attr, out var tokens, out _)) return false;
            return MathUtil.TryParseBool(tokens[0], out v);
        }

        public static bool TryReadInt(object attr, out int v)
        {
            v = 0;
            if (!TryGetTokens(attr, out var tokens, out _)) return false;
            return MathUtil.TryParseInt(tokens[0], out v);
        }

        public static bool TryReadFloat(object attr, out float v)
        {
            v = 0f;
            if (!TryGetTokens(attr, out var tokens, out _)) return false;
            return MathUtil.TryParseFloat(tokens[0], out v);
        }

        public static bool TryReadVector2(object attr, out Vector2 v)
        {
            v = default;
            if (!TryGetTokens(attr, out var t, out _) || t.Count < 2) return false;
            if (!MathUtil.TryParseFloat(t[0], out var x)) return false;
            if (!MathUtil.TryParseFloat(t[1], out var y)) return false;
            v = new Vector2(x, y);
            return true;
        }

        public static bool TryReadVector3(object attr, out Vector3 v)
        {
            v = default;
            if (!TryGetTokens(attr, out var t, out _) || t.Count < 3) return false;
            if (!MathUtil.TryParseFloat(t[0], out var x)) return false;
            if (!MathUtil.TryParseFloat(t[1], out var y)) return false;
            if (!MathUtil.TryParseFloat(t[2], out var z)) return false;
            v = new Vector3(x, y, z);
            return true;
        }

        public static bool TryReadVector4(object attr, out Vector4 v)
        {
            v = default;
            if (!TryGetTokens(attr, out var t, out _) || t.Count < 4) return false;
            if (!MathUtil.TryParseFloat(t[0], out var x)) return false;
            if (!MathUtil.TryParseFloat(t[1], out var y)) return false;
            if (!MathUtil.TryParseFloat(t[2], out var z)) return false;
            if (!MathUtil.TryParseFloat(t[3], out var w)) return false;
            v = new Vector4(x, y, z, w);
            return true;
        }

        public static bool TryReadMatrix4x4(object attr, out Matrix4x4 m)
        {
            m = Matrix4x4.identity;
            if (!TryGetTokens(attr, out var t, out _) || t.Count < 16) return false;
            return MatrixUtil.TryParseMatrix4x4(t, 0, out m);
        }

        public static bool TryReadString(object attr, out string s)
        {
            s = null;
            if (!TryGetTokens(attr, out var t, out _) || t.Count == 0) return false;
            s = t[0];
            return true;
        }

        // =========================
        // Internal helpers
        // =========================

        private static MayaAttrKind GuessKind(string typeName, IReadOnlyList<string> tokens)
        {
            typeName ??= string.Empty;

            if (typeName.Equals("bool", StringComparison.OrdinalIgnoreCase)) return MayaAttrKind.Bool;
            if (typeName.Equals("int", StringComparison.OrdinalIgnoreCase) ||
                typeName.Equals("long", StringComparison.OrdinalIgnoreCase))
                return MayaAttrKind.Int;

            if (typeName.Equals("float", StringComparison.OrdinalIgnoreCase)) return MayaAttrKind.Float;
            if (typeName.Equals("double", StringComparison.OrdinalIgnoreCase)) return MayaAttrKind.Double;
            if (typeName.Equals("string", StringComparison.OrdinalIgnoreCase)) return MayaAttrKind.String;

            if (tokens == null || tokens.Count == 0) return MayaAttrKind.Unknown;
            if (tokens.Count == 1)
            {
                if (MathUtil.TryParseBool(tokens[0], out _)) return MayaAttrKind.Bool;
                if (MathUtil.TryParseInt(tokens[0], out _)) return MayaAttrKind.Int;
                if (MathUtil.TryParseFloat(tokens[0], out _)) return MayaAttrKind.Float;
                return MayaAttrKind.String;
            }
            if (tokens.Count == 2) return MayaAttrKind.Vector2;
            if (tokens.Count == 3) return MayaAttrKind.Vector3;
            if (tokens.Count == 4) return MayaAttrKind.Vector4;
            if (tokens.Count == 16) return MayaAttrKind.Matrix4x4;

            return MayaAttrKind.Unknown;
        }

        private static bool TryGetTokens(
            object attr,
            out IReadOnlyList<string> tokens,
            out string typeName)
        {
            tokens = null;
            typeName = null;
            if (attr == null) return false;

	            // Fast-path for Core RawAttributeValue (no reflection).
	            // Note: We avoid referencing Editor-only types or nested types that might not exist
	            // in all configurations.
	            if (attr is global::MayaImporter.Core.RawAttributeValue rav)
	            {
	                typeName = rav.TypeName;
	                tokens = rav.ValueTokens;
	                return tokens != null && tokens.Count > 0;
	            }

            var t = attr.GetType();

            // TypeName
            var typeProp = t.GetProperty("TypeName", BindingFlags.Public | BindingFlags.Instance);
            if (typeProp != null)
                typeName = typeProp.GetValue(attr) as string;

            // ValueTokens or Tokens
            var tokProp =
                t.GetProperty("ValueTokens", BindingFlags.Public | BindingFlags.Instance) ??
                t.GetProperty("Tokens", BindingFlags.Public | BindingFlags.Instance);

            if (tokProp == null) return false;

            tokens = tokProp.GetValue(attr) as IReadOnlyList<string>;
            return tokens != null && tokens.Count > 0;
        }
    }
}
