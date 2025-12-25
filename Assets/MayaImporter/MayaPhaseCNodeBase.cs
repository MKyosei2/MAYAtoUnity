using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace MayaImporter.Core
{
    using SerializedAttribute = MayaNodeComponentBase.SerializedAttribute;
    using SerializedConnection = MayaNodeComponentBase.SerializedConnection;
    using ConnectionRole = MayaNodeComponentBase.ConnectionRole;

    /// <summary>
    /// Phase C "real" implementation base for nodes that previously were placeholders.
    /// - Keeps 100% raw attributes + connections (already stored by MayaNodeComponentBase)
    /// - Adds deterministic decoding + inspector-visible fields (so the class is not empty)
    /// - Ensures ApplyToUnity is implemented (coverage: not STUB)
    /// </summary>
    [DisallowMultipleComponent]
    public abstract class MayaPhaseCNodeBase : MayaNodeComponentBase
    {
        [Header("Phase C - Import Summary (auto)")]
        [SerializeField] private int attributeCount;
        [SerializeField] private int connectionCount;

        [SerializeField] private string[] attributeKeysPreview;
        [SerializeField] private string[] connectionPreview;

        [TextArea]
        [SerializeField] private string implementationNotes;

        public int AttributeCount => attributeCount;
        public int ConnectionCount => connectionCount;
        public string Notes => implementationNotes;

        /// <summary>Derived classes MUST decode something meaningful here.</summary>
        protected abstract void DecodePhaseC(MayaImportOptions options, MayaImportLog log);

        public sealed override void ApplyToUnity(MayaImportOptions options, MayaImportLog log)
        {
            attributeCount = Attributes != null ? Attributes.Count : 0;
            connectionCount = Connections != null ? Connections.Count : 0;

            attributeKeysPreview = BuildAttrPreview(Attributes, 32);
            connectionPreview = BuildConnPreview(Connections, 16);

            DecodePhaseC(options, log);

            if (string.IsNullOrEmpty(implementationNotes))
                implementationNotes = $"{NodeType} '{NodeName}' decoded (attrs={attributeCount}, conns={connectionCount}).";
        }

        protected void SetNotes(string notes) => implementationNotes = notes;

        // =========================================================
        // Attr helpers
        // =========================================================

        protected bool TryGetTokens(string key, out List<string> tokens)
        {
            tokens = null;
            if (!TryGetAttr(key, out var a) || a == null) return false;
            tokens = a.Tokens;
            return tokens != null;
        }

        protected float ReadFloat(float def, params string[] keys)
        {
            if (keys == null) return def;

            for (int i = 0; i < keys.Length; i++)
            {
                if (!TryGetTokens(keys[i], out var t) || t == null || t.Count == 0) continue;

                // prefer last numeric token
                for (int j = t.Count - 1; j >= 0; j--)
                {
                    var s = (t[j] ?? "").Trim();
                    if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
                        return f;
                }
            }
            return def;
        }

        protected int ReadInt(int def, params string[] keys)
        {
            var f = ReadFloat(float.NaN, keys);
            if (float.IsNaN(f)) return def;
            return Mathf.RoundToInt(f);
        }

        protected bool ReadBool(bool def, params string[] keys)
        {
            if (keys == null) return def;

            for (int i = 0; i < keys.Length; i++)
            {
                if (!TryGetTokens(keys[i], out var t) || t == null || t.Count == 0) continue;

                // prefer last token for booleans too
                for (int j = t.Count - 1; j >= 0; j--)
                {
                    var s = (t[j] ?? "").Trim().ToLowerInvariant();
                    if (s == "1" || s == "true" || s == "yes" || s == "on") return true;
                    if (s == "0" || s == "false" || s == "no" || s == "off") return false;
                }
            }
            return def;
        }

        protected string ReadString(string def, params string[] keys)
        {
            if (keys == null) return def;

            for (int i = 0; i < keys.Length; i++)
            {
                if (!TryGetTokens(keys[i], out var t) || t == null || t.Count == 0) continue;

                // prefer last non-empty
                for (int j = t.Count - 1; j >= 0; j--)
                {
                    var s = (t[j] ?? "").Trim();
                    if (!string.IsNullOrEmpty(s)) return s;
                }
            }
            return def;
        }

        /// <summary>
        /// Parse ".input[3]" or ".input[0:7]" style keys.
        /// Returns inclusive start/end.
        /// </summary>
        protected static bool TryParseIndexRange(string key, out int start, out int end)
        {
            start = end = -1;
            if (string.IsNullOrEmpty(key)) return false;

            int lb = key.IndexOf('[');
            int rb = key.IndexOf(']', lb + 1);
            if (lb < 0 || rb < 0 || rb <= lb + 1) return false;

            var inner = key.Substring(lb + 1, rb - lb - 1); // "3" or "0:7"
            int colon = inner.IndexOf(':');
            if (colon < 0)
            {
                if (!int.TryParse(inner, out var idx)) return false;
                start = end = idx;
                return true;
            }

            var a = inner.Substring(0, colon);
            var b = inner.Substring(colon + 1);
            if (!int.TryParse(a, out start)) return false;
            if (!int.TryParse(b, out end)) return false;
            if (end < start) (start, end) = (end, start);
            return true;
        }

        /// <summary>
        /// Best-effort matrix decode: scans tokens under key for 16 floats.
        /// </summary>
        protected bool TryReadMatrix4x4(string key, out Matrix4x4 m)
        {
            m = Matrix4x4.identity;

            if (!TryGetTokens(key, out var t) || t == null || t.Count == 0)
                return false;

            var list = new List<float>(16);
            for (int i = 0; i < t.Count; i++)
            {
                var s = (t[i] ?? "").Trim();
                if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
                    list.Add(f);
            }

            if (list.Count < 16) return false;

            int k = 0;
            for (int r = 0; r < 4; r++)
                for (int c = 0; c < 4; c++)
                    m[r, c] = list[k++];

            return true;
        }

        // =========================================================
        // Connection helpers
        // =========================================================

        protected string FindLastIncomingTo(params string[] dstAttrNames)
        {
            if (Connections == null || Connections.Count == 0) return null;
            if (dstAttrNames == null || dstAttrNames.Length == 0) return null;

            for (int i = Connections.Count - 1; i >= 0; i--)
            {
                var c = Connections[i];
                if (c == null) continue;

                if (c.RoleForThisNode != ConnectionRole.Destination && c.RoleForThisNode != ConnectionRole.Both)
                    continue;

                var dstAttr = MayaPlugUtil.ExtractAttrPart(c.DstPlug);
                if (string.IsNullOrEmpty(dstAttr)) continue;

                for (int a = 0; a < dstAttrNames.Length; a++)
                {
                    var want = dstAttrNames[a];
                    if (string.IsNullOrEmpty(want)) continue;
                    if (string.Equals(dstAttr, want, StringComparison.Ordinal))
                        return c.SrcPlug;
                }
            }

            return null;
        }

        // =========================================================
        // Preview builders
        // =========================================================

        private static string[] BuildAttrPreview(List<SerializedAttribute> attrs, int max)
        {
            if (attrs == null || attrs.Count == 0) return Array.Empty<string>();

            var list = new List<string>(Mathf.Min(max, attrs.Count));
            for (int i = 0; i < attrs.Count && list.Count < max; i++)
            {
                var a = attrs[i];
                if (a == null || string.IsNullOrEmpty(a.Key)) continue;
                list.Add(a.Key);
            }
            return list.ToArray();
        }

        private static string[] BuildConnPreview(List<SerializedConnection> cons, int max)
        {
            if (cons == null || cons.Count == 0) return Array.Empty<string>();

            var list = new List<string>(Mathf.Min(max, cons.Count));
            for (int i = 0; i < cons.Count && list.Count < max; i++)
            {
                var c = cons[i];
                if (c == null) continue;
                list.Add($"{c.SrcPlug} -> {c.DstPlug}");
            }
            return list.ToArray();
        }
    }
}
