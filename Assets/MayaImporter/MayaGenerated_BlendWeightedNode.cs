// PATCH: ProductionImpl v6 (Unity-only, retention-first)
// NodeType: blendWeighted
// Production implementation: meaningful decode + best-effort evaluation
// - Decodes input[i] and weight[i] (holes & ranges supported)
// - output = Σ (input[i] * weight[i])
// - Publishes output via MayaImporter.Core.MayaFloatValue (maya/unity same)

using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Generated
{
    [DisallowMultipleComponent]
    [MayaNodeType("blendWeighted")]
    public sealed class MayaGenerated_BlendWeightedNode : MayaNodeComponentBase
    {
        public override void ApplyToUnity(MayaImportOptions options, MayaImportLog log)
        {
            options ??= new MayaImportOptions();
            log ??= new MayaImportLog();

            var meta = GetComponent<MayaBlendWeightedMetadata>() ?? gameObject.AddComponent<MayaBlendWeightedMetadata>();
            meta.valid = false;

            meta.inputsMap.Clear();
            meta.weightsMap.Clear();

            CollectIndexedFloat("input", meta.inputsMap);
            CollectIndexedFloat("weight", meta.weightsMap);

            meta.pairs.Clear();

            float sumW = 0f;
            float outv = 0f;

            var all = new SortedSet<int>();
            foreach (var kv in meta.inputsMap) all.Add(kv.Key);
            foreach (var kv in meta.weightsMap) all.Add(kv.Key);

            foreach (var idx in all)
            {
                float inV = meta.inputsMap.TryGetValue(idx, out var iv) ? iv : 0f;
                float w = meta.weightsMap.TryGetValue(idx, out var ww) ? ww : 0f;

                meta.pairs.Add(new MayaBlendWeightedMetadata.Pair { index = idx, input = inV, weight = w });

                sumW += w;
                outv += inV * w;
            }

            meta.weightSum = sumW;
            meta.output = outv;

            meta.incomingInputPlug = FindIncomingByDstContains("input", "in");
            meta.incomingWeightPlug = FindIncomingByDstContains("weight", "w");

            // ✅ publish scalar via Core carrier
            var outVal = GetComponent<MayaFloatValue>() ?? gameObject.AddComponent<MayaFloatValue>();
            outVal.Set(meta.output, meta.output);

            meta.valid = true;
            meta.lastBuildFrame = Time.frameCount;

            log.Info($"[blendWeighted] '{NodeName}' pairs={meta.pairs.Count} weightSum={meta.weightSum:0.###} out={meta.output:0.###} " +
                     $"srcIn='{meta.incomingInputPlug ?? "null"}' srcW='{meta.incomingWeightPlug ?? "null"}'");
        }

        private void CollectIndexedFloat(string baseName, SortedDictionary<int, float> map)
        {
            if (map == null) return;
            map.Clear();

            if (Attributes == null) return;

            for (int i = 0; i < Attributes.Count; i++)
            {
                var a = Attributes[i];
                if (a == null || string.IsNullOrEmpty(a.Key) || a.Tokens == null || a.Tokens.Count == 0) continue;

                if (!TryParseIndexedKey(a.Key, baseName, out var start, out var end, out var isRange))
                    continue;

                if (!isRange)
                {
                    if (TryFloatPreferLast(a.Tokens, out var f))
                        map[start] = f;
                    continue;
                }

                int count = end - start + 1;
                int n = Mathf.Min(count, a.Tokens.Count);
                for (int k = 0; k < n; k++)
                {
                    if (TryF(a.Tokens[k], out var f))
                        map[start + k] = f;
                }
            }
        }

        private static bool TryFloatPreferLast(List<string> tokens, out float f)
        {
            f = 0f;
            if (tokens == null || tokens.Count == 0) return false;

            for (int i = tokens.Count - 1; i >= 0; i--)
                if (TryF(tokens[i], out f)) return true;

            return false;
        }

        private static bool TryF(string s, out float f)
            => float.TryParse((s ?? "").Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out f);

        private static bool TryParseIndexedKey(string key, string baseName, out int start, out int end, out bool isRange)
        {
            start = end = -1;
            isRange = false;

            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(baseName)) return false;

            string k = key.StartsWith(".", StringComparison.Ordinal) ? key.Substring(1) : key;
            if (!k.StartsWith(baseName + "[", StringComparison.Ordinal)) return false;

            int lb = k.IndexOf('[');
            int rb = k.IndexOf(']', lb + 1);
            if (lb < 0 || rb < 0 || rb <= lb + 1) return false;

            var inner = k.Substring(lb + 1, rb - lb - 1);
            int colon = inner.IndexOf(':');

            if (colon < 0)
            {
                if (!int.TryParse(inner, NumberStyles.Integer, CultureInfo.InvariantCulture, out var idx))
                    return false;
                start = end = idx;
                isRange = false;
                return true;
            }

            var a = inner.Substring(0, colon);
            var b = inner.Substring(colon + 1);

            if (!int.TryParse(a, NumberStyles.Integer, CultureInfo.InvariantCulture, out start)) return false;
            if (!int.TryParse(b, NumberStyles.Integer, CultureInfo.InvariantCulture, out end)) return false;

            if (end < start) (start, end) = (end, start);
            isRange = true;
            return true;
        }

        private string FindIncomingByDstContains(params string[] patterns)
        {
            if (Connections == null || Connections.Count == 0) return null;
            if (patterns == null || patterns.Length == 0) return null;

            for (int i = Connections.Count - 1; i >= 0; i--)
            {
                var c = Connections[i];
                if (c == null) continue;

                if (c.RoleForThisNode != ConnectionRole.Destination &&
                    c.RoleForThisNode != ConnectionRole.Both)
                    continue;

                var dstAttr = MayaPlugUtil.ExtractAttrPart(c.DstPlug);
                if (string.IsNullOrEmpty(dstAttr)) continue;

                for (int p = 0; p < patterns.Length; p++)
                {
                    var pat = patterns[p];
                    if (string.IsNullOrEmpty(pat)) continue;
                    if (dstAttr.Contains(pat, StringComparison.Ordinal))
                        return c.SrcPlug;
                }
            }

            return null;
        }
    }

    [DisallowMultipleComponent]
    public sealed class MayaBlendWeightedMetadata : MonoBehaviour
    {
        public bool valid;
        public int lastBuildFrame;

        [NonSerialized] public SortedDictionary<int, float> inputsMap = new SortedDictionary<int, float>();
        [NonSerialized] public SortedDictionary<int, float> weightsMap = new SortedDictionary<int, float>();

        [Serializable]
        public struct Pair
        {
            public int index;
            public float input;
            public float weight;
        }

        public List<Pair> pairs = new List<Pair>();

        public float weightSum;
        public float output;

        public string incomingInputPlug;
        public string incomingWeightPlug;
    }
}
