using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Geometry
{
    /// <summary>
    /// Maya nurbsCurve node -> polyline approximation (for motionPath).
    /// This is not a full NURBS evaluator; it reconstructs CV polyline deterministically.
    /// </summary>
    [MayaNodeType("nurbsCurve")]
    [DisallowMultipleComponent]
    public sealed class MayaNurbsCurveNodeComponent : MayaNodeComponentBase
    {
        [Header("Extracted (best-effort)")]
        public Vector3[] controlVertices;
        public bool closed;

        public override void ApplyToUnity(MayaImportOptions options, MayaImportLog log)
        {
            options ??= new MayaImportOptions();
            log ??= new MayaImportLog();

            controlVertices = ExtractCvPolyline(out closed);

            var poly = GetComponent<MayaCurvePolylineComponent>();
            if (poly == null) poly = gameObject.AddComponent<MayaCurvePolylineComponent>();

            if (controlVertices == null || controlVertices.Length < 2)
            {
                log?.Warn($"[nurbsCurve] CV not found: '{NodeName}'");
                poly.Set(Array.Empty<Vector3>(), false);
                return;
            }

            poly.Set(controlVertices, closed);
            log?.Info($"[nurbsCurve] '{NodeName}' cv={controlVertices.Length} closed={closed}");
        }

        private Vector3[] ExtractCvPolyline(out bool isClosed)
        {
            isClosed = ReadBoolAny(false, ".f", ".form", ".closed");

            // Collect ".cv[...]" (preferred) or ".cp[...]" (fallback)
            var map = new SortedDictionary<int, Vector3>();

            for (int i = 0; i < Attributes.Count; i++)
            {
                var a = Attributes[i];
                if (a == null || string.IsNullOrEmpty(a.Key) || a.Tokens == null || a.Tokens.Count == 0)
                    continue;

                if (!LooksLikeCvKey(a.Key)) continue;

                if (!TryParseRangeIndex(a.Key, out int start, out int end))
                    continue;

                int count = end - start + 1;
                if (count <= 0) continue;

                // tokens are floats only: x y z x y z ...
                int needed = count * 3;
                if (a.Tokens.Count < needed) continue;

                int t = 0;
                for (int k = 0; k < count; k++)
                {
                    if (!TryF(a.Tokens[t++], out var x)) x = 0f;
                    if (!TryF(a.Tokens[t++], out var y)) y = 0f;
                    if (!TryF(a.Tokens[t++], out var z)) z = 0f;

                    map[start + k] = new Vector3(x, y, z);
                }
            }

            if (map.Count == 0)
                return null;

            // Build packed array
            int max = -1;
            foreach (var kv in map) max = Mathf.Max(max, kv.Key);

            var arr = new Vector3[max + 1];
            for (int i = 0; i < arr.Length; i++) arr[i] = Vector3.zero;
            foreach (var kv in map) arr[kv.Key] = kv.Value;

            // Trim trailing zeros is dangerous; keep as-is.
            return arr;
        }

        private bool LooksLikeCvKey(string key)
        {
            // Common in .ma: ".cv[0:3]" / ".cp[0:3]" / ".controlPoints[0:3]"
            return key.Contains(".cv[", StringComparison.Ordinal) ||
                   key.Contains(".cp[", StringComparison.Ordinal) ||
                   key.Contains(".controlPoints[", StringComparison.Ordinal);
        }

        private static bool TryParseRangeIndex(string key, out int start, out int end)
        {
            start = end = -1;
            if (string.IsNullOrEmpty(key)) return false;

            int lb = key.LastIndexOf('[', key.Length - 1);
            int rb = key.LastIndexOf(']', key.Length - 1);
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
            else
            {
                if (!int.TryParse(inner, NumberStyles.Integer, CultureInfo.InvariantCulture, out start)) return false;
                end = start;
                return true;
            }
        }

        private bool ReadBoolAny(bool def, params string[] keys)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                if (TryGetAttr(keys[i], out var a) && a.Tokens != null && a.Tokens.Count > 0)
                {
                    var s = a.Tokens[0].Trim().ToLowerInvariant();
                    if (s == "1" || s == "true") return true;
                    if (s == "0" || s == "false") return false;
                }
            }
            return def;
        }

        private static bool TryF(string s, out float f)
            => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out f);
    }
}
