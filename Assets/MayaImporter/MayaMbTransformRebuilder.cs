using System;
using System.Collections.Generic;
using System.Globalization;

namespace MayaImporter.Core
{
    /// <summary>
    /// Phase2 Step8:
    /// Confirm/assign transform attributes (.t/.r/.s or .tx etc) from decoded chunk samples.
    /// Additive only: never removes Raw.
    /// </summary>
    public static class MayaMbTransformRebuilder
    {
        private static readonly string[] NodeTypePriority = { "transform", "joint", "camera" };

        public static void Apply(MayaSceneData scene, MayaImportLog log)
        {
            if (scene?.MbIndex == null) return;
            if (scene.Nodes == null) return;

            // Build leaf->fullpaths for fallback name matching
            var leafToFull = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var kv in scene.Nodes)
            {
                var full = kv.Key;
                var leaf = LeafOfDag(full);
                if (string.IsNullOrEmpty(leaf)) continue;
                if (!leafToFull.TryGetValue(leaf, out var list))
                {
                    list = new List<string>(2);
                    leafToFull[leaf] = list;
                }
                list.Add(full);
            }

            int applied = 0;

            var chunks = scene.MbIndex.Chunks;
            for (int ci = 0; ci < chunks.Count; ci++)
            {
                var ch = chunks[ci];
                if (ch == null || ch.IsContainer) continue;

                // We need at least some hint strings
                var ds = ch.DecodedStrings;
                if (ds == null || ds.Length == 0) continue;

                // Identify target node
                var targetKeys = ResolveTargetNodes(scene, leafToFull, ds);
                if (targetKeys == null || targetKeys.Count == 0) continue;

                // Identify attribute kind (t/r/s, axis, etc)
                if (!TryFindAttrHint(ds, out var attrKey, out var expects3))
                    continue;

                // Extract numeric values (prefer decoded floats)
                if (!TryExtractValues(ch, ds, expects3 ? 3 : 1, out var values))
                    continue;

                // Apply to all resolved nodes, but only if nodeType looks DAG-ish
                for (int ni = 0; ni < targetKeys.Count; ni++)
                {
                    var key = targetKeys[ni];
                    if (!scene.Nodes.TryGetValue(key, out var rec) || rec == null) continue;

                    if (!IsDagish(rec.NodeType)) continue;

                    ApplyAttr(rec, attrKey, values, expects3, ch);
                    applied++;
                }
            }

            if (applied > 0)
                log?.Info($".mb structured: applied transform attribute candidates to {applied} nodes (Stage: TRS).");
            else
                log?.Info(".mb structured: no TRS candidates applied (still OK, raw preserved).");
        }

        private static bool IsDagish(string nodeType)
        {
            if (string.IsNullOrEmpty(nodeType)) return false;
            for (int i = 0; i < NodeTypePriority.Length; i++)
                if (string.Equals(nodeType, NodeTypePriority[i], StringComparison.Ordinal))
                    return true;
            // allow unknown/mesh placeholders too (mesh shape often not holding trs; but safe)
            if (string.Equals(nodeType, "unknown", StringComparison.Ordinal)) return true;
            if (string.Equals(nodeType, "mesh", StringComparison.Ordinal)) return true;
            return false;
        }

        private static List<string> ResolveTargetNodes(
            MayaSceneData scene,
            Dictionary<string, List<string>> leafToFull,
            string[] decodedStrings)
        {
            // Prefer explicit dag path like "|a|b|c"
            for (int i = 0; i < decodedStrings.Length; i++)
            {
                var s = decodedStrings[i];
                if (string.IsNullOrEmpty(s)) continue;
                if (s.Length >= 3 && s[0] == '|' && s.IndexOf('|', 1) > 0)
                {
                    var full = NormalizeDag(s);
                    return new List<string>(1) { full };
                }
            }

            // else: try leaf name match
            for (int i = 0; i < decodedStrings.Length; i++)
            {
                var s = decodedStrings[i];
                if (!LooksLikeNodeLeaf(s)) continue;

                if (leafToFull.TryGetValue(s, out var list) && list.Count > 0)
                    return list;
            }

            return null;
        }

        private static bool TryFindAttrHint(string[] ds, out string attrKey, out bool expects3)
        {
            attrKey = null;
            expects3 = false;

            // direct maya short attrs
            if (ContainsAny(ds, ".t", "translate", "Translate", "translate3"))
            {
                attrKey = ".t"; expects3 = true; return true;
            }
            if (ContainsAny(ds, ".r", "rotate", "Rotate", "rotate3"))
            {
                attrKey = ".r"; expects3 = true; return true;
            }
            if (ContainsAny(ds, ".s", "scale", "Scale", "scale3"))
            {
                attrKey = ".s"; expects3 = true; return true;
            }

            // axis variants
            if (ContainsAny(ds, ".tx", "translateX", "TranslateX")) { attrKey = ".tx"; expects3 = false; return true; }
            if (ContainsAny(ds, ".ty", "translateY", "TranslateY")) { attrKey = ".ty"; expects3 = false; return true; }
            if (ContainsAny(ds, ".tz", "translateZ", "TranslateZ")) { attrKey = ".tz"; expects3 = false; return true; }

            if (ContainsAny(ds, ".rx", "rotateX", "RotateX")) { attrKey = ".rx"; expects3 = false; return true; }
            if (ContainsAny(ds, ".ry", "rotateY", "RotateY")) { attrKey = ".ry"; expects3 = false; return true; }
            if (ContainsAny(ds, ".rz", "rotateZ", "RotateZ")) { attrKey = ".rz"; expects3 = false; return true; }

            if (ContainsAny(ds, ".sx", "scaleX", "ScaleX")) { attrKey = ".sx"; expects3 = false; return true; }
            if (ContainsAny(ds, ".sy", "scaleY", "ScaleY")) { attrKey = ".sy"; expects3 = false; return true; }
            if (ContainsAny(ds, ".sz", "scaleZ", "ScaleZ")) { attrKey = ".sz"; expects3 = false; return true; }

            return false;
        }

        private static bool TryExtractValues(MayaBinaryIndex.ChunkInfo ch, string[] ds, int needed, out float[] values)
        {
            values = null;

            // 1) Prefer decoded floats sample
            if (ch.DecodedFloats != null && ch.DecodedFloats.Length >= needed)
            {
                values = TakeFirst(ch.DecodedFloats, needed);
                return true;
            }

            // 2) Parse numeric strings inside decodedStrings
            var found = new List<float>(needed);
            for (int i = 0; i < ds.Length; i++)
            {
                var s = ds[i];
                if (string.IsNullOrEmpty(s)) continue;

                if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
                {
                    found.Add(f);
                    if (found.Count >= needed) break;
                }
            }

            if (found.Count >= needed)
            {
                values = found.ToArray();
                return true;
            }

            return false;
        }

        private static void ApplyAttr(NodeRecord rec, string attrKey, float[] vals, bool expects3, MayaBinaryIndex.ChunkInfo ch)
        {
            if (rec.Attributes == null) return;

            var inv = CultureInfo.InvariantCulture;

            if (expects3)
            {
                // store packed vector as last-seen, same as .ma parser policy
                var tokens = new List<string>
                {
                    vals[0].ToString(inv),
                    vals.Length > 1 ? vals[1].ToString(inv) : "0",
                    vals.Length > 2 ? vals[2].ToString(inv) : "0",
                };
                rec.Attributes[attrKey] = new RawAttributeValue("float3", tokens);
            }
            else
            {
                rec.Attributes[attrKey] = new RawAttributeValue("float", new List<string> { vals[0].ToString(inv) });
            }

            // provenance
            if (!rec.Attributes.ContainsKey(".mbTRSChunkId"))
                rec.Attributes[".mbTRSChunkId"] = new RawAttributeValue("string", new List<string> { ch.Id ?? "" });
            if (!rec.Attributes.ContainsKey(".mbTRSChunkOffset"))
                rec.Attributes[".mbTRSChunkOffset"] = new RawAttributeValue("int", new List<string> { ch.Offset.ToString(inv) });
            if (!rec.Attributes.ContainsKey(".mbTRSAttr"))
                rec.Attributes[".mbTRSAttr"] = new RawAttributeValue("string", new List<string> { attrKey });
        }

        private static bool ContainsAny(string[] ds, params string[] keys)
        {
            for (int i = 0; i < ds.Length; i++)
            {
                var s = ds[i];
                if (string.IsNullOrEmpty(s)) continue;
                for (int k = 0; k < keys.Length; k++)
                {
                    if (string.Equals(s, keys[k], StringComparison.Ordinal)) return true;
                }
            }
            return false;
        }

        private static float[] TakeFirst(float[] src, int n)
        {
            n = Math.Min(n, src.Length);
            var a = new float[n];
            Array.Copy(src, a, n);
            return a;
        }

        private static string LeafOfDag(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            int last = name.LastIndexOf('|');
            if (last >= 0 && last < name.Length - 1) return name.Substring(last + 1);
            return name;
        }

        private static bool LooksLikeNodeLeaf(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            if (s.Length < 2 || s.Length > 80) return false;
            if (s.IndexOf('|') >= 0) return false;
            if (s.IndexOf('.') >= 0) return false;
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

        private static string NormalizeDag(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            if (s[0] != '|') s = "|" + s;
            while (s.Length > 1 && s[s.Length - 1] == '|') s = s.Substring(0, s.Length - 1);
            return s;
        }
    }
}
