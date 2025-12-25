using System;
using System.Collections.Generic;

namespace MayaImporter.Core
{
    public static class MayaMbTextureHintTagger
    {
        private const string KeyMB = ".mbTextureHint";
        private const string Key = ".textureHint";

        public static void Apply(MayaSceneData scene, MayaImportLog log)
        {
            if (scene == null) return;
            if (scene.SourceKind != MayaSourceKind.BinaryMb) return;
            if (scene.MbIndex?.Chunks == null) return;
            if (scene.Nodes == null || scene.Nodes.Count == 0) return;

            var meshKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var kv in scene.Nodes)
            {
                var rec = kv.Value;
                if (rec == null) continue;

                var leaf = LeafOfDag(rec.Name);
                if (rec.NodeType == "mesh") meshKeys.Add(rec.Name);
                else if (!string.IsNullOrEmpty(leaf) && leaf.EndsWith("Shape", StringComparison.Ordinal)) meshKeys.Add(rec.Name);
            }

            if (meshKeys.Count == 0)
            {
                log?.Info(".mb texhint: no mesh nodes found (OK).");
                return;
            }

            string currentMeshKey = null;
            int currentDepth = -1;

            int tagged = 0;

            foreach (var ch in scene.MbIndex.Chunks)
            {
                if (ch == null || ch.IsContainer) continue;

                if (TryFindMeshKeyInDecodedStrings(meshKeys, ch.DecodedStrings, out var mk))
                {
                    currentMeshKey = mk;
                    currentDepth = ch.Depth;
                }

                if (string.IsNullOrEmpty(currentMeshKey)) continue;

                if (currentDepth >= 0 && ch.Depth < currentDepth - 2)
                {
                    currentMeshKey = null;
                    currentDepth = -1;
                    continue;
                }

                if (!scene.Nodes.TryGetValue(currentMeshKey, out var meshRec) || meshRec == null) continue;

                var ds = ch.DecodedStrings;
                if (ds == null || ds.Length == 0) continue;

                for (int i = 0; i < ds.Length; i++)
                {
                    var s = ds[i];
                    if (!LooksLikeTexturePathOrFile(s)) continue;

                    // write both MB and unified
                    if (TrySetSlot(meshRec, KeyMB, s, 4)) tagged++;
                    TrySetSlot(meshRec, Key, s, 4);
                }
            }

            if (tagged > 0) log?.Info($".mb texhint: tagged={tagged} (Stage: Step18 Unified tags).");
            else log?.Info(".mb texhint: no texture-like strings found (still OK).");
        }

        private static bool TrySetSlot(NodeRecord rec, string baseKey, string value, int maxSlots)
        {
            if (rec.Attributes == null) return false;
            if (string.IsNullOrEmpty(value)) return false;

            for (int i = 1; i <= maxSlots; i++)
            {
                var key = (i == 1) ? baseKey : baseKey + i;
                if (rec.Attributes.TryGetValue(key, out var existing) && existing?.ValueTokens?.Count > 0)
                {
                    if (string.Equals(existing.ValueTokens[0], value, StringComparison.Ordinal))
                        return false;
                }
            }

            for (int i = 1; i <= maxSlots; i++)
            {
                var key = (i == 1) ? baseKey : baseKey + i;
                if (!rec.Attributes.ContainsKey(key))
                {
                    rec.Attributes[key] = new RawAttributeValue("string", new List<string> { value });
                    return true;
                }
            }

            return false;
        }

        private static bool LooksLikeTexturePathOrFile(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            if (s.Length < 5 || s.Length > 260) return false;

            var ls = s.ToLowerInvariant();
            return ls.EndsWith(".png") || ls.EndsWith(".jpg") || ls.EndsWith(".jpeg") ||
                   ls.EndsWith(".tga") || ls.EndsWith(".tif") || ls.EndsWith(".tiff") ||
                   ls.EndsWith(".bmp") || ls.EndsWith(".exr") || ls.EndsWith(".hdr");
        }

        private static bool TryFindMeshKeyInDecodedStrings(HashSet<string> meshKeys, string[] decodedStrings, out string meshKey)
        {
            meshKey = null;
            if (decodedStrings == null || decodedStrings.Length == 0) return false;

            for (int i = 0; i < decodedStrings.Length; i++)
            {
                var s = decodedStrings[i];
                if (LooksLikeDagPath(s))
                {
                    var norm = NormalizeDag(s);
                    if (meshKeys.Contains(norm))
                    {
                        meshKey = norm;
                        return true;
                    }
                }
            }

            for (int i = 0; i < decodedStrings.Length; i++)
            {
                var s = decodedStrings[i];
                if (string.IsNullOrEmpty(s)) continue;
                if (!s.EndsWith("Shape", StringComparison.Ordinal)) continue;

                foreach (var k in meshKeys)
                {
                    if (string.Equals(LeafOfDag(k), s, StringComparison.Ordinal))
                    {
                        meshKey = k;
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool LooksLikeDagPath(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            if (s.Length < 3 || s.Length > 200) return false;
            return s[0] == '|' && s.IndexOf('|', 1) > 0;
        }

        private static string NormalizeDag(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            if (s[0] != '|') s = "|" + s;
            while (s.Length > 1 && s[s.Length - 1] == '|')
                s = s.Substring(0, s.Length - 1);
            return s;
        }

        private static string LeafOfDag(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            int last = name.LastIndexOf('|');
            if (last >= 0 && last < name.Length - 1) return name.Substring(last + 1);
            return name;
        }
    }
}
