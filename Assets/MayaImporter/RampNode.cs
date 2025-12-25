// File: Assets/MayaImporter/RampNode.cs
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using MayaImporter.Core;
using MayaImporter.Shader;

namespace MayaImporter.Shading
{
    [DisallowMultipleComponent]
    [MayaNodeType("ramp")]
    public sealed class RampNode : MayaProceduralTextureNodeBase
    {
        [Serializable]
        public struct RampEntry
        {
            [Range(0f, 1f)] public float position;
            public Color color;
        }

        [Header("Ramp Params (best-effort)")]
        public int rampType = 0;
        public List<RampEntry> entries = new List<RampEntry>();

        // ★ public override に統一
        public override void ApplyToUnity(MayaImportOptions options, MayaImportLog log)
        {
            log ??= new MayaImportLog();

            rampType = ReadInt(new[] { ".type", "type", ".rampType", "rampType" }, rampType);
            entries = DecodeEntriesFromRawAttrs();
            if (entries == null || entries.Count == 0)
            {
                entries = new List<RampEntry>
                {
                    new RampEntry{ position = 0f, color = Color.black },
                    new RampEntry{ position = 1f, color = Color.white },
                };
            }
            entries.Sort((a, b) => a.position.CompareTo(b.position));

            BakeToTextureMeta(log,
                bakeFunc: () =>
                {
                    var texMeta = EnsureTexMeta();
                    var bakeEntries = new List<MayaProceduralTextureBaker.RampEntry>(entries.Count);
                    for (int i = 0; i < entries.Count; i++)
                    {
                        bakeEntries.Add(new MayaProceduralTextureBaker.RampEntry
                        {
                            position = Mathf.Clamp01(entries[i].position),
                            color = entries[i].color
                        });
                    }

                    return MayaProceduralTextureBaker.BakeRamp(
                        Mathf.Max(2, bakeWidth), Mathf.Max(2, bakeHeight),
                        bakeEntries,
                        rampType,
                        texMeta.repeatUV, texMeta.offsetUV, texMeta.rotateUVDegrees);
                },
                bakeLabel: "ramp");

            log.Info($"[ramp] type={rampType} entries={entries.Count}");
        }

        private List<RampEntry> DecodeEntriesFromRawAttrs()
        {
            var mapPos = new Dictionary<int, float>();
            var mapCol = new Dictionary<int, Color>();

            if (Attributes == null) return new List<RampEntry>();

            for (int i = 0; i < Attributes.Count; i++)
            {
                var a = Attributes[i];
                if (a == null || string.IsNullOrEmpty(a.Key) || a.Tokens == null || a.Tokens.Count == 0)
                    continue;

                var key = a.Key;
                int idx = ExtractFirstBracketIndex(key);
                if (idx < 0) continue;

                if (key.Contains(".position", StringComparison.Ordinal) || key.EndsWith(".p", StringComparison.Ordinal))
                {
                    if (TryF(a.Tokens[a.Tokens.Count - 1], out var p))
                        mapPos[idx] = Mathf.Clamp01(p);
                    continue;
                }

                if (key.Contains(".color", StringComparison.Ordinal) || key.EndsWith(".c", StringComparison.Ordinal))
                {
                    if (a.Tokens.Count >= 3 &&
                        TryF(a.Tokens[0], out var r) &&
                        TryF(a.Tokens[1], out var g) &&
                        TryF(a.Tokens[2], out var b))
                    {
                        mapCol[idx] = new Color(r, g, b, 1f);
                    }
                    continue;
                }
            }

            var list = new List<RampEntry>();
            foreach (var kv in mapPos)
            {
                var idx = kv.Key;
                var pos = kv.Value;
                mapCol.TryGetValue(idx, out var col);
                if (col == default) col = Color.white;
                list.Add(new RampEntry { position = pos, color = col });
            }

            if (list.Count == 0 && mapCol.Count > 0)
            {
                var keys = new List<int>(mapCol.Keys);
                keys.Sort();
                for (int i = 0; i < keys.Count; i++)
                {
                    float t = keys.Count <= 1 ? 0f : (float)i / (keys.Count - 1);
                    list.Add(new RampEntry { position = t, color = mapCol[keys[i]] });
                }
            }

            return list;
        }

        private static int ExtractFirstBracketIndex(string key)
        {
            int lb = key.IndexOf('[');
            int rb = key.IndexOf(']', lb + 1);
            if (lb < 0 || rb < 0 || rb <= lb + 1) return -1;

            var inner = key.Substring(lb + 1, rb - lb - 1);
            int colon = inner.IndexOf(':');
            if (colon >= 0) inner = inner.Substring(0, colon);

            if (int.TryParse(inner, out var idx)) return idx;
            return -1;
        }

        private static bool TryF(string s, out float f)
            => float.TryParse((s ?? "").Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out f);
    }
}
