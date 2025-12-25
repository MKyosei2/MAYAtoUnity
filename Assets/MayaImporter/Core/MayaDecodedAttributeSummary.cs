using System;
using System.Collections.Generic;
using UnityEngine;

namespace MayaImporter.Core
{
    /// <summary>
    /// Inspector-friendly typed summary of raw Maya attributes.
    ///
    /// Purpose:
    /// - Phase C nodes (and other runtime nodes) can show "what was decoded" without losing raw.
    /// - Bounded preview only; raw data remains in MayaNodeComponentBase.Attributes.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MayaDecodedAttributeSummary : MonoBehaviour
    {
        [Serializable]
        public sealed class AttributeCategory
        {
            public string name;
            public List<Entry> entries = new List<Entry>(64);
        }

        [Serializable]
        public sealed class Entry
        {
            public string key;
            public string type;
            public string valuePreview;
        }

        [Header("Preview Limits")]
        [Min(1)]
        public int maxCategories = 24;

        [Min(1)]
        public int maxEntries = 32;

        /// <summary>
        /// Compatibility alias (older code expected this exact name).
        /// </summary>
        public int maxEntriesPerCategory { get => maxEntries; set => maxEntries = value; }

        [Header("Decoded Counts")]
        public int parsedBools;
        public int parsedInts;
        public int parsedFloats;
        public int parsedVec2;
        public int parsedVec3;
        public int parsedVec4;
        public int parsedMatrices;
        public int parsedStrings;

        [Header("Preview")]
        public List<AttributeCategory> categories = new List<AttributeCategory>(24);

        public void Clear()
        {
            categories.Clear();
            parsedBools = parsedInts = parsedFloats = 0;
            parsedVec2 = parsedVec3 = parsedVec4 = 0;
            parsedMatrices = parsedStrings = 0;
        }

        public void BuildFrom(MayaNodeComponentBase node)
        {
            if (node == null)
            {
                Clear();
                return;
            }

            BuildFrom(node.Attributes);
        }

        public void BuildFrom(List<MayaNodeComponentBase.SerializedAttribute> attrs)
        {
            // MayaNodeComponentBase stores attributes as List<SerializedAttribute>.
            if (attrs == null)
            {
                Clear();
                return;
            }

            var map = new Dictionary<string, RawAttributeValue>(StringComparer.Ordinal);
            for (int i = 0; i < attrs.Count; i++)
            {
                var a = attrs[i];
                if (a == null || string.IsNullOrEmpty(a.Key)) continue;
                map[a.Key] = new RawAttributeValue(a.TypeName ?? "tokens", a.Tokens);
            }

            BuildFrom(map);
        }

        public void BuildFrom(Dictionary<string, RawAttributeValue> attrs)
        {
            Clear();
            if (attrs == null || attrs.Count == 0)
                return;

            // Group by category prefix
            var catMap = new Dictionary<string, AttributeCategory>(StringComparer.Ordinal);

            foreach (var kv in attrs)
            {
                var key = kv.Key;
                var val = kv.Value;

                if (string.IsNullOrEmpty(key) || val == null)
                    continue;

                string catName = CategoryOfKey(key);
                if (!catMap.TryGetValue(catName, out var cat))
                {
                    if (catMap.Count >= Mathf.Max(1, maxCategories))
                        continue;

                    cat = new AttributeCategory { name = catName };
                    catMap.Add(catName, cat);
                    categories.Add(cat);
                }

                if (cat.entries.Count >= Mathf.Max(1, maxEntries))
                    continue;

                // Count typed values
                CountKind(val);

                cat.entries.Add(new Entry
                {
                    key = key,
                    type = val.TypeName,
                    valuePreview = BuildPreview(val)
                });
            }
        }

        private void CountKind(RawAttributeValue v)
        {
            if (v == null)
                return;

            switch (v.Kind)
            {
                case MayaAttrValueKind.Bool:
                    parsedBools++; break;
                case MayaAttrValueKind.Int:
                    parsedInts++; break;
                case MayaAttrValueKind.Float:
                    parsedFloats++; break;
                case MayaAttrValueKind.Vector2:
                    parsedVec2++; break;
                case MayaAttrValueKind.Vector3:
                    parsedVec3++; break;
                case MayaAttrValueKind.Vector4:
                    parsedVec4++; break;
                case MayaAttrValueKind.Matrix4x4:
                    parsedMatrices++; break;
                case MayaAttrValueKind.StringArray:
                    parsedStrings++; break;
                default:
                    // tokens / arrays / others -> ignore in counters for now
                    break;
            }
        }

        private static string CategoryOfKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return "(null)";

            // Maya keys usually start with '.'
            string k = key[0] == '.' ? key.Substring(1) : key;

            // Take prefix before first '.' or '['
            int dot = k.IndexOf('.', StringComparison.Ordinal);
            int br = k.IndexOf('[', StringComparison.Ordinal);

            int cut;
            if (dot < 0) cut = br < 0 ? k.Length : br;
            else cut = br < 0 ? dot : Mathf.Min(dot, br);

            if (cut <= 0) return "(root)";
            if (cut > 48) cut = 48;
            return k.Substring(0, cut);
        }

        private static string BuildPreview(RawAttributeValue v)
        {
            if (v == null) return "";

            try
            {
                // Prefer parsed value
                if (v.HasParsedValue && v.ParsedValue != null)
                {
                    if (v.ParsedValue is float[] fa)
                    {
                        if (fa.Length <= 4)
                            return string.Join(", ", fa);
                        return $"float[{fa.Length}]";
                    }
                    if (v.ParsedValue is int[] ia)
                    {
                        if (ia.Length <= 8)
                            return string.Join(", ", ia);
                        return $"int[{ia.Length}]";
                    }
                    if (v.ParsedValue is string[] sa)
                    {
                        if (sa.Length <= 4)
                            return string.Join(" | ", sa);
                        return $"string[{sa.Length}]";
                    }

                    return v.ParsedValue.ToString();
                }

                // Fallback to token preview
                var t = v.ValueTokens;
                if (t == null || t.Count == 0) return "(empty)";

                if (t.Count == 1) return t[0] ?? "";

                int n = Mathf.Min(t.Count, 12);
                return string.Join(" ", t.GetRange(0, n)) + (t.Count > n ? " ..." : "");
            }
            catch
            {
                return "(preview error)";
            }
        }
    }
}
