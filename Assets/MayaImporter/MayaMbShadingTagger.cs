// MAYAIMPORTER_PATCH_V4: mb provenance/evidence + audit determinism (generated 2026-01-05)
using System;
using System.Collections.Generic;

namespace MayaImporter.Core
{
    /// <summary>
    /// Phase2 Step18 (MB):
    /// Best-effort: infer SG/material names from .mb decoded strings (chunk context),
    /// and write BOTH:
    ///  - legacy mb keys: .mbShadingGroupName / .mbMaterialName
    ///  - unified keys  : .shadingGroupName / .materialName
    /// </summary>
    public static class MayaMbShadingTagger
    {
        private const string KeyMB_SG = ".mbShadingGroupName";
        private const string KeyMB_Mat = ".mbMaterialName";

        private const string Key_SG = ".shadingGroupName";
        private const string Key_Mat = ".materialName";

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
                log?.Info(".mb shading-tag: no mesh nodes found (OK).");
                return;
            }

            string currentMeshKey = null;
            int currentDepth = -1;

            int sgTagged = 0;
            int matTagged = 0;

            var chunks = scene.MbIndex.Chunks;

            for (int i = 0; i < chunks.Count; i++)
            {
                var ch = chunks[i];
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

                // shadingEngine marker + nearby name
                if (ContainsToken(ds, "shadingEngine"))
                {
                    var sg = FindNearbyName(ds, "shadingEngine");
                    if (!string.IsNullOrEmpty(sg) && LooksLikeSGName(sg))
                    {
                        if (TrySetBoth(meshRec, KeyMB_SG, Key_SG, sg, 4)) sgTagged++;
                    }
                }

                // any SG-like token
                for (int t = 0; t < ds.Length; t++)
                {
                    var s = ds[t];
                    if (!LooksLikeIdentifier(s)) continue;
                    if (LooksLikeSGName(s))
                    {
                        if (TrySetBoth(meshRec, KeyMB_SG, Key_SG, s, 4)) sgTagged++;
                    }
                }

                // material-ish marker + nearby name
                if (ContainsToken(ds, "surfaceShader") || ContainsToken(ds, "material"))
                {
                    var m = FindNearbyName(ds, "surfaceShader") ?? FindNearbyName(ds, "material");
                    if (!string.IsNullOrEmpty(m) && LooksLikeMaterialName(m))
                    {
                        if (TrySetBoth(meshRec, KeyMB_Mat, Key_Mat, m, 2)) matTagged++;
                    }
                }
            }

            if (sgTagged > 0 || matTagged > 0)
                log?.Info($".mb shading-tag: sg={sgTagged}, mat={matTagged} (Stage: Step18 Unified tags).");
            else
                log?.Info(".mb shading-tag: no SG/material hints found (still OK).");
        }

        private static bool TrySetBoth(NodeRecord rec, string keyA, string keyB, string value, int maxSlots)
        {
            bool wrote = false;
            wrote |= TrySetSlot(rec, keyA, value, maxSlots);
            wrote |= TrySetSlot(rec, keyB, value, maxSlots);
            return wrote;
        }

        private static bool TrySetSlot(NodeRecord rec, string baseKey, string value, int maxSlots)
        {
            if (rec.Attributes == null) return false;
            if (string.IsNullOrEmpty(value)) return false;

            // dedupe
            for (int i = 1; i <= maxSlots; i++)
            {
                var key = (i == 1) ? baseKey : baseKey + i;
                var ex = rec.Attributes.TryGetValue(key, out var e) ? e : null;
                if (ex?.ValueTokens?.Count > 0 && string.Equals(ex.ValueTokens[0], value, StringComparison.Ordinal))
                    return false;
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

        private static bool ContainsToken(string[] ds, string token)
        {
            for (int i = 0; i < ds.Length; i++)
                if (string.Equals(ds[i], token, StringComparison.Ordinal))
                    return true;
            return false;
        }

        private static string FindNearbyName(string[] ds, string marker)
        {
            for (int i = 0; i < ds.Length; i++)
            {
                if (!string.Equals(ds[i], marker, StringComparison.Ordinal)) continue;
                if (i + 1 < ds.Length && LooksLikeIdentifier(ds[i + 1])) return ds[i + 1];
                if (i - 1 >= 0 && LooksLikeIdentifier(ds[i - 1])) return ds[i - 1];
            }
            return null;
        }

        private static bool LooksLikeSGName(string s)
        {
            if (!LooksLikeIdentifier(s)) return false;
            if (s.EndsWith("SG", StringComparison.Ordinal)) return true;
            if (s.IndexOf("ShadingGroup", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (s.IndexOf("shadingGroup", StringComparison.Ordinal) >= 0) return true;
            return false;
        }

        private static bool LooksLikeMaterialName(string s)
        {
            if (!LooksLikeIdentifier(s)) return false;
            if (s.EndsWith("SG", StringComparison.Ordinal)) return false;
            if (s.IndexOf("mat", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (s.IndexOf("lambert", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (s.IndexOf("phong", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (s.IndexOf("blinn", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (s.IndexOf("standardSurface", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (s.IndexOf("aiStandard", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return s.Length >= 4;
        }

        private static bool TryFindMeshKeyInDecodedStrings(HashSet<string> meshKeys, string[] decodedStrings, out string meshKey)
        {
            meshKey = null;
            if (decodedStrings == null || decodedStrings.Length == 0) return false;

            // Prefer dag path
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

            // Fallback leaf "Shape"
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

        private static bool LooksLikeIdentifier(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            if (s.Length < 2 || s.Length > 120) return false;
            if (s[0] == '|') return false;
            if (s.IndexOf('|') >= 0) return false;
            if (s.IndexOf('/') >= 0 || s.IndexOf('\\') >= 0) return false;

            bool hasLetter = false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (char.IsLetter(c)) hasLetter = true;

                if (char.IsLetterOrDigit(c)) continue;
                if (c == '_' || c == ':' || c == '-') continue;
                return false;
            }
            return hasLetter;
        }
    }
}
