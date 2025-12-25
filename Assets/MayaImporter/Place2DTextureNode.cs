using System.Globalization;
using UnityEngine;
using MayaImporter.Core;
using MayaImporter.Components;

namespace MayaImporter.Shading
{
    /// <summary>
    /// Maya place2dTexture node (Unified).
    /// Stores UV transform into MayaTextureMetadata so Unity-only reconstruction can use it.
    /// </summary>
    [DisallowMultipleComponent]
    [MayaNodeType("place2dTexture")]
    public sealed class Place2DTextureNode : MayaNodeComponentBase
    {
        [Header("UV Transform (best-effort)")]
        public Vector2 repeatUV = Vector2.one;
        public Vector2 offsetUV = Vector2.zero;
        public float rotateUVDegrees = 0f;

        public override void ApplyToUnity(MayaImportOptions options, MayaImportLog log)
        {
            log ??= new MayaImportLog();

            repeatUV = ReadFloat2(new[] { "repeatUV", ".repeatUV" }, repeatUV);
            if (repeatUV == Vector2.zero) repeatUV = Vector2.one;

            // repeatU/repeatV fallback
            repeatUV = ReadRepeatUVFallback(repeatUV);

            offsetUV = ReadFloat2(new[] { "offset", ".offset" }, offsetUV);
            offsetUV = ReadOffsetFallback(offsetUV);

            rotateUVDegrees = ReadFloat1(new[] { "rotateUV", ".rotateUV" }, rotateUVDegrees);

            // write into metadata holder (so file node can copy from it if needed)
            var meta = GetComponent<MayaTextureMetadata>() ?? gameObject.AddComponent<MayaTextureMetadata>();
            meta.repeatUV = repeatUV;
            meta.offsetUV = offsetUV;
            meta.rotateUVDegrees = rotateUVDegrees;

            log.Info($"[place2dTexture] repeat={repeatUV} offset={offsetUV} rot={rotateUVDegrees}");
        }

        private Vector2 ReadRepeatUVFallback(Vector2 current)
        {
            float ru = current.x;
            float rv = current.y;

            bool gotU = TryReadFloat("repeatU", ".repeatU", out var u);
            bool gotV = TryReadFloat("repeatV", ".repeatV", out var v);

            if (gotU) ru = u;
            if (gotV) rv = v;

            if (gotU || gotV)
            {
                ru = Mathf.Max(0.0001f, ru);
                rv = Mathf.Max(0.0001f, rv);
                return new Vector2(ru, rv);
            }

            return current;
        }

        private Vector2 ReadOffsetFallback(Vector2 current)
        {
            float ou = current.x;
            float ov = current.y;

            bool gotU = TryReadFloat("offsetU", ".offsetU", out var u);
            bool gotV = TryReadFloat("offsetV", ".offsetV", out var v);

            if (gotU) ou = u;
            if (gotV) ov = v;

            if (gotU || gotV)
                return new Vector2(ou, ov);

            return current;
        }

        private Vector2 ReadFloat2(string[] keys, Vector2 def)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                if (!TryGetAttr(keys[i], out var a) || a.Tokens == null || a.Tokens.Count < 2) continue;

                if (TryF(a.Tokens[0], out var x) && TryF(a.Tokens[1], out var y))
                    return new Vector2(x, y);
            }
            return def;
        }

        private float ReadFloat1(string[] keys, float def)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                if (!TryGetAttr(keys[i], out var a) || a.Tokens == null || a.Tokens.Count == 0) continue;
                if (TryF(a.Tokens[0], out var f)) return f;
            }
            return def;
        }

        private bool TryReadFloat(string k1, string k2, out float f)
        {
            f = 0f;
            if (TryGetAttr(k1, out var a1) && a1.Tokens != null && a1.Tokens.Count > 0 && TryF(a1.Tokens[0], out f)) return true;
            if (TryGetAttr(k2, out var a2) && a2.Tokens != null && a2.Tokens.Count > 0 && TryF(a2.Tokens[0], out f)) return true;
            return false;
        }

        private static bool TryF(string s, out float f)
            => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out f);
    }
}
