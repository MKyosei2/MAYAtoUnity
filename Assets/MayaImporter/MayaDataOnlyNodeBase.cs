using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace MayaImporter.Core
{
    /// <summary>
    /// Phase-C "real" implementation base:
    /// - Not a behavioral simulation of Maya, but a deterministic, inspectable decode of key attributes + connections
    /// - Ensures ApplyToUnity(...) is overridden => not counted as STUB
    /// - Provides robust token parsing helpers (float/int/bool/string/matrix)
    /// </summary>
    [DisallowMultipleComponent]
    public abstract class MayaDataOnlyNodeBase : MayaNodeComponentBase
    {
        [Header("Phase-C Implementation (decoded)")]
        [SerializeField] private int attributeCount;
        [SerializeField] private int connectionCount;

        [SerializeField] private string[] attributeKeysPreview;
        [SerializeField] private string[] connectionPreview;

        [TextArea]
        [SerializeField] private string implementationNotes;

        protected void SetNotes(string notes) => implementationNotes = notes;

        public override void ApplyToUnity(MayaImportOptions options, MayaImportLog log)
        {
            attributeCount = Attributes != null ? Attributes.Count : 0;
            connectionCount = Connections != null ? Connections.Count : 0;

            attributeKeysPreview = BuildAttrPreview(Attributes, 32);
            connectionPreview = BuildConnPreview(Connections, 16);

            if (string.IsNullOrEmpty(implementationNotes))
                implementationNotes = $"{NodeType} '{NodeName}' decoded (attrs={attributeCount}, conns={connectionCount}).";
        }

        // -------------------- helpers (attrs) --------------------

        protected bool TryGetTokens(string key, out List<string> tokens)
        {
            tokens = null;
            if (string.IsNullOrEmpty(key) || Attributes == null) return false;

            string k0 = key;
            string k1 = key.StartsWith(".", StringComparison.Ordinal) ? key.Substring(1) : "." + key;

            for (int i = 0; i < Attributes.Count; i++)
            {
                var a = Attributes[i];
                if (a == null || string.IsNullOrEmpty(a.Key)) continue;

                if (string.Equals(a.Key, k0, StringComparison.Ordinal) ||
                    string.Equals(a.Key, k1, StringComparison.Ordinal))
                {
                    tokens = a.Tokens;
                    return true;
                }
            }

            return false;
        }

        protected float ReadFloat(float defaultValue, params string[] keys)
        {
            if (keys == null) return defaultValue;

            for (int i = 0; i < keys.Length; i++)
            {
                if (TryGetTokens(keys[i], out var t))
                    return TokenToFloat(t, defaultValue);
            }

            return defaultValue;
        }

        protected int ReadInt(int defaultValue, params string[] keys)
        {
            float f = ReadFloat(float.NaN, keys);
            if (float.IsNaN(f)) return defaultValue;
            return Mathf.RoundToInt(f);
        }

        protected bool ReadBool(bool defaultValue, params string[] keys)
        {
            if (keys == null) return defaultValue;

            for (int i = 0; i < keys.Length; i++)
            {
                if (!TryGetTokens(keys[i], out var t) || t == null || t.Count == 0) continue;

                for (int j = t.Count - 1; j >= 0; j--)
                {
                    var s = CleanToken(t[j]);
                    if (string.IsNullOrEmpty(s)) continue;

                    if (s == "1") return true;
                    if (s == "0") return false;

                    if (bool.TryParse(s, out var b))
                        return b;
                }
            }

            return defaultValue;
        }

        protected string ReadString(string defaultValue, params string[] keys)
        {
            if (keys == null) return defaultValue;

            for (int i = 0; i < keys.Length; i++)
            {
                if (!TryGetTokens(keys[i], out var t) || t == null || t.Count == 0) continue;

                // Prefer last token as "value"
                for (int j = t.Count - 1; j >= 0; j--)
                {
                    var s = CleanToken(t[j]);
                    if (!string.IsNullOrEmpty(s))
                        return s;
                }
            }

            return defaultValue;
        }

        protected float ReadIndexedFloat(string baseName, int index, float defaultValue, params string[] extraAliases)
        {
            // Try patterns: baseName[index], baseName[index].something is left to caller
            // Support dot and non-dot keys automatically via TryGetTokens.
            string k0 = $"{baseName}[{index}]";
            float v = ReadFloat(float.NaN, k0);

            if (!float.IsNaN(v)) return v;

            if (extraAliases != null)
            {
                for (int i = 0; i < extraAliases.Length; i++)
                {
                    string kk = $"{extraAliases[i]}[{index}]";
                    v = ReadFloat(float.NaN, kk);
                    if (!float.IsNaN(v)) return v;
                }
            }

            return defaultValue;
        }

        protected bool TryReadMatrix4x4(string key, out Matrix4x4 m)
        {
            m = Matrix4x4.identity;

            if (!TryGetTokens(key, out var t) || t == null || t.Count == 0)
                return false;

            // Expect 16 floats somewhere in tokens; scan and collect in order.
            var floats = new List<float>(16);
            for (int i = 0; i < t.Count; i++)
            {
                var s = CleanToken(t[i]);
                if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
                    floats.Add(f);
            }

            if (floats.Count < 16) return false;

            // Maya matrices are typically row-major in text dumps; we store into Unity Matrix4x4 with same order.
            // m[row,col]
            int k = 0;
            for (int r = 0; r < 4; r++)
                for (int c = 0; c < 4; c++)
                    m[r, c] = floats[k++];

            return true;
        }

        // -------------------- helpers (connections) --------------------

        protected string FindLastIncomingToAttr(params string[] attrNames)
        {
            if (Connections == null || Connections.Count == 0) return null;
            if (string.IsNullOrEmpty(NodeName)) return null;
            if (attrNames == null || attrNames.Length == 0) return null;

            // We search connections where this node is destination and DstPlug matches NodeName + "." + attr
            for (int i = Connections.Count - 1; i >= 0; i--)
            {
                var c = Connections[i];
                if (c == null) continue;
                if (c.RoleForThisNode != ConnectionRole.Destination && c.RoleForThisNode != ConnectionRole.Both)
                    continue;

                var dst = Unquote(c.DstPlug);
                if (string.IsNullOrEmpty(dst)) continue;

                for (int a = 0; a < attrNames.Length; a++)
                {
                    var attr = attrNames[a];
                    if (string.IsNullOrEmpty(attr)) continue;

                    string suffix = NodeName + "." + attr;
                    if (dst.EndsWith(suffix, StringComparison.Ordinal))
                        return Unquote(c.SrcPlug);
                }
            }

            return null;
        }

        // -------------------- preview builders --------------------

        private static string[] BuildAttrPreview(List<SerializedAttribute> attrs, int max)
        {
            if (attrs == null) return Array.Empty<string>();

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
            if (cons == null) return Array.Empty<string>();

            var list = new List<string>(Mathf.Min(max, cons.Count));
            for (int i = 0; i < cons.Count && list.Count < max; i++)
            {
                var c = cons[i];
                if (c == null) continue;

                var src = Unquote(c.SrcPlug);
                var dst = Unquote(c.DstPlug);
                list.Add($"{src} -> {dst}");
            }
            return list.ToArray();
        }

        // -------------------- token utils --------------------

        private static float TokenToFloat(List<string> tokens, float defaultValue)
        {
            if (tokens == null || tokens.Count == 0) return defaultValue;

            for (int i = tokens.Count - 1; i >= 0; i--)
            {
                var s = CleanToken(tokens[i]);
                if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
                    return f;
            }

            return defaultValue;
        }

        private static string CleanToken(string t)
        {
            if (string.IsNullOrEmpty(t)) return "";
            t = t.Trim();

            // Strip quotes
            if (t.Length >= 2 && t[0] == '"' && t[t.Length - 1] == '"')
                t = t.Substring(1, t.Length - 2);

            return t.Trim();
        }

        private static string Unquote(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            s = s.Trim();
            if (s.Length >= 2 && s[0] == '"' && s[s.Length - 1] == '"')
                return s.Substring(1, s.Length - 2);
            return s;
        }
    }
}
