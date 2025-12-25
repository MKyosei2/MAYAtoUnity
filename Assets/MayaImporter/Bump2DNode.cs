using System.Globalization;
using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Shading
{
    /// <summary>
    /// Maya bump2d node (Unified).
    /// Stores bumpDepth / bumpInterp / invert for future material reconstruction.
    /// (Core resolver is unchanged in this step.)
    /// </summary>
    [DisallowMultipleComponent]
    [MayaNodeType("bump2d")]
    public sealed class Bump2DNode : MayaNodeComponentBase
    {
        [Header("Bump Settings (best-effort)")]
        public float bumpDepth = 1f;
        public int bumpInterp = 0;
        public bool invert = false;

        public override void ApplyToUnity(MayaImportOptions options, MayaImportLog log)
        {
            log ??= new MayaImportLog();

            bumpDepth = ReadFloat(new[] { "bumpDepth", ".bumpDepth", "depth", ".depth" }, bumpDepth);
            bumpInterp = ReadInt(new[] { "bumpInterp", ".bumpInterp", "interp", ".interp" }, bumpInterp);
            invert = ReadBool(new[] { "invert", ".invert" }, invert);

            log.Info($"[bump2d] depth={bumpDepth} interp={bumpInterp} invert={invert}");
        }

        private float ReadFloat(string[] keys, float def)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                if (!TryGetAttr(keys[i], out var a) || a.Tokens == null || a.Tokens.Count == 0) continue;
                if (TryF(a.Tokens[0], out var f)) return f;
            }
            return def;
        }

        private int ReadInt(string[] keys, int def)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                if (!TryGetAttr(keys[i], out var a) || a.Tokens == null || a.Tokens.Count == 0) continue;
                if (int.TryParse(a.Tokens[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)) return v;
            }
            return def;
        }

        private bool ReadBool(string[] keys, bool def)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                if (!TryGetAttr(keys[i], out var a) || a.Tokens == null || a.Tokens.Count == 0) continue;
                var s = a.Tokens[0].Trim().ToLowerInvariant();
                if (s == "1" || s == "true") return true;
                if (s == "0" || s == "false") return false;
            }
            return def;
        }

        private static bool TryF(string s, out float f)
            => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out f);
    }
}
