// MAYAIMPORTER_PATCH_V4: mb provenance/evidence + audit determinism (generated 2026-01-05)
using System;
using System.Collections.Generic;
using System.Globalization;

namespace MayaImporter.Core
{
    /// <summary>
    /// Extract face-material (shadingEngine) assignments from .ma raw statements.
    /// Typical Maya ASCII pattern:
    ///   sets -e -forceElement initialShadingGroup "|pCubeShape1.f[0:11]";
    ///
    /// We map: meshNodeName -> (ordered) shadingEngine -> face indices set.
    ///
    /// Why "ordered":
    /// Unity subMesh/material order must be stable across imports.
    /// Dictionary enumeration order is not guaranteed; we store shadingEngines in insertion order.
    ///
    /// Robustness:
    /// - Supports whole-object membership (no .f[]) by expanding to all faces when the face count is known.
    /// - Supports open ranges like f[0:*] by expanding to all faces when the face count is known.
    /// - Learns face count from the mesh node's polyFaces attribute ranges (fc[...]) without needing Maya.
    /// - Caches results per scene+mesh leaf for efficiency.
    /// </summary>
    public static class MayaFaceMaterialAssignments
    {
        public sealed class MeshAssignments
        {
            /// <summary>
            /// Ordered list: shadingEngine -> faces
            /// (Order = first appearance order in .ma "sets -forceElement" statements)
            /// </summary>
            public readonly List<KeyValuePair<string, HashSet<int>>> FacesByShadingEngine
                = new List<KeyValuePair<string, HashSet<int>>>(8);

            /// <summary>
            /// Finds existing face-set for the SG; creates it if missing (preserving order).
            /// </summary>
            public HashSet<int> GetOrCreate(string shadingEngine)
            {
                for (int i = 0; i < FacesByShadingEngine.Count; i++)
                {
                    if (string.Equals(FacesByShadingEngine[i].Key, shadingEngine, StringComparison.Ordinal))
                        return FacesByShadingEngine[i].Value;
                }

                var set = new HashSet<int>();
                FacesByShadingEngine.Add(new KeyValuePair<string, HashSet<int>>(shadingEngine, set));
                return set;
            }
        }

        // sceneKey -> meshLeaf -> assignments
        private static readonly Dictionary<string, Dictionary<string, MeshAssignments>> s_cache
            = new Dictionary<string, Dictionary<string, MeshAssignments>>(StringComparer.Ordinal);

        // Keep cache bounded
        private const int MaxScenesInCache = 16;

        /// <summary>
        /// Returns assignments for the given mesh node name (best-effort).
        /// </summary>
        public static bool TryGetForMesh(MayaSceneData scene, string meshNodeName, out MeshAssignments result)
        {
            result = null;
            if (scene == null || string.IsNullOrEmpty(meshNodeName)) return false;
            if (scene.RawStatements == null || scene.RawStatements.Count == 0) return false;

            var meshLeaf = MayaPlugUtil.LeafName(meshNodeName);
            if (string.IsNullOrEmpty(meshLeaf)) return false;

            var sceneKey = GetSceneCacheKey(scene);
            if (!s_cache.TryGetValue(sceneKey, out var perMesh))
            {
                if (s_cache.Count >= MaxScenesInCache) s_cache.Clear();
                perMesh = new Dictionary<string, MeshAssignments>(StringComparer.Ordinal);
                s_cache[sceneKey] = perMesh;
            }

            if (perMesh.TryGetValue(meshLeaf, out var cached) && cached != null)
            {
                if (cached.FacesByShadingEngine.Count == 0) return false;
                result = cached;
                return true;
            }

            // Determine face count from mesh node record if possible
            var faceCount = TryInferFaceCount(scene, meshLeaf);

            var outAssign = new MeshAssignments();

            for (int i = 0; i < scene.RawStatements.Count; i++)
            {
                var st = scene.RawStatements[i];
                if (st == null) continue;

                var tokens = st.Tokens;
                if (tokens == null || tokens.Count == 0) continue;

                // Prefer Command if present, else tokens[0]
                var cmd = !string.IsNullOrEmpty(st.Command) ? st.Command : tokens[0];
                if (!string.Equals(cmd, "sets", StringComparison.Ordinal))
                    continue;

                // Find -forceElement / -fe
                int fe = IndexOf(tokens, "-forceElement");
                if (fe < 0) fe = IndexOf(tokens, "-fe");
                if (fe < 0) continue;

                if (fe + 1 >= tokens.Count) continue;
                var shadingEngine = TrimQuotes(tokens[fe + 1]);
                if (string.IsNullOrEmpty(shadingEngine)) continue;

                // Remaining tokens may contain members (e.g. "|meshShape.f[0:11]" or "|meshShape")
                for (int t = fe + 2; t < tokens.Count; t++)
                {
                    var member = TrimQuotes(tokens[t]);
                    if (string.IsNullOrEmpty(member)) continue;
                    if (member.StartsWith("-", StringComparison.Ordinal)) continue;

                    if (!TryMatchMemberToMesh(member, meshLeaf, out var inside, out var wholeObject))
                        continue;

                    if (wholeObject)
                    {
                        // Expand only when we know faceCount
                        if (faceCount > 0)
                            AddFaceRange(outAssign, shadingEngine, 0, faceCount - 1);
                        continue;
                    }

                    if (string.IsNullOrEmpty(inside))
                        continue;

                    AddFacesFromBracket(outAssign, shadingEngine, inside, faceCount);
                }
            }

            // Store (even empty) to avoid rescanning repeatedly
            perMesh[meshLeaf] = outAssign;

            if (outAssign.FacesByShadingEngine.Count == 0)
                return false;

            result = outAssign;
            return true;
        }

        private static bool TryMatchMemberToMesh(string memberToken, string meshLeaf, out string inside, out bool wholeObject)
        {
            inside = null;
            wholeObject = false;

            // Face components: something.f[...]
            int faceKey = memberToken.IndexOf(".f[", StringComparison.Ordinal);
            if (faceKey >= 0)
            {
                var nodePart = memberToken.Substring(0, faceKey);
                var nodeLeaf = MayaPlugUtil.LeafName(nodePart);
                if (!string.Equals(nodeLeaf, meshLeaf, StringComparison.Ordinal))
                    return false;

                int lb = memberToken.IndexOf('[', faceKey);
                int rb = memberToken.IndexOf(']', faceKey);
                if (lb < 0 || rb < 0 || rb <= lb + 1) return false;

                inside = memberToken.Substring(lb + 1, rb - lb - 1).Trim();
                wholeObject = false;
                return true;
            }

            // Whole object membership: memberToken is a node path/name
            var leaf = MayaPlugUtil.LeafName(memberToken);
            if (string.Equals(leaf, meshLeaf, StringComparison.Ordinal))
            {
                wholeObject = true;
                return true;
            }

            return false;
        }

        private static void AddFacesFromBracket(MeshAssignments ma, string shadingEngine, string inside, int faceCount)
        {
            inside = inside.Trim();
            if (string.IsNullOrEmpty(inside)) return;

            // patterns:
            //  "12" or "0:11" or "0:*" or "*"
            if (inside == "*")
            {
                if (faceCount > 0)
                    AddFaceRange(ma, shadingEngine, 0, faceCount - 1);
                return;
            }

            int colon = inside.IndexOf(':');
            if (colon < 0)
            {
                if (TryInt(inside, out var one))
                    AddFaceRange(ma, shadingEngine, one, one);
                return;
            }

            var a = inside.Substring(0, colon).Trim();
            var b = inside.Substring(colon + 1).Trim();

            if (!TryInt(a, out var start))
                return;

            if (b == "*")
            {
                if (faceCount > 0)
                    AddFaceRange(ma, shadingEngine, start, faceCount - 1);
                else
                    AddFaceRange(ma, shadingEngine, start, start); // best-effort fallback
                return;
            }

            if (!TryInt(b, out var end))
                return;

            if (end < start) (start, end) = (end, start);
            AddFaceRange(ma, shadingEngine, start, end);
        }

        private static void AddFaceRange(MeshAssignments ma, string shadingEngine, int start, int end)
        {
            if (start < 0) start = 0;
            if (end < start) end = start;

            var set = ma.GetOrCreate(shadingEngine);

            for (int i = start; i <= end; i++)
                set.Add(i);
        }

        private static int TryInferFaceCount(MayaSceneData scene, string meshLeaf)
        {
            // Find matching node record (exact leaf match)
            NodeRecord rec = null;
            if (scene?.Nodes != null)
            {
                foreach (var kv in scene.Nodes)
                {
                    var r = kv.Value;
                    if (r == null) continue;
                    if (r.NodeType != "mesh") continue;

                    var leaf = MayaPlugUtil.LeafName(r.Name);
                    if (string.Equals(leaf, meshLeaf, StringComparison.Ordinal))
                    {
                        rec = r;
                        break;
                    }
                }
            }

            if (rec?.Attributes == null || rec.Attributes.Count == 0)
                return -1;

            int maxFaceIndex = -1;

            foreach (var kv in rec.Attributes)
            {
                var key = kv.Key;
                if (string.IsNullOrEmpty(key)) continue;

                // polyFaces array chunks usually stored as fc[0:123] (sometimes with leading '.')
                if (!(key.StartsWith("fc[", StringComparison.Ordinal) || key.StartsWith(".fc[", StringComparison.Ordinal)))
                    continue;

                if (!TryParseBracketRange(key, out var start, out var end))
                    continue;

                if (end >= 0) maxFaceIndex = Math.Max(maxFaceIndex, end);
                else if (start >= 0) maxFaceIndex = Math.Max(maxFaceIndex, start);
            }

            return maxFaceIndex >= 0 ? (maxFaceIndex + 1) : -1;
        }

        private static bool TryParseBracketRange(string key, out int start, out int end)
        {
            start = -1;
            end = -1;

            int lb = key.IndexOf('[');
            int rb = key.IndexOf(']');
            if (lb < 0 || rb < 0 || rb <= lb + 1) return false;

            var inside = key.Substring(lb + 1, rb - lb - 1).Trim();
            if (string.IsNullOrEmpty(inside)) return false;

            int colon = inside.IndexOf(':');
            if (colon < 0)
            {
                if (!TryInt(inside, out start)) return false;
                end = start;
                return true;
            }

            var a = inside.Substring(0, colon).Trim();
            var b = inside.Substring(colon + 1).Trim();

            if (!TryInt(a, out start)) return false;
            if (!TryInt(b, out end)) return false;

            if (end < start) (start, end) = (end, start);
            return true;
        }

        private static string GetSceneCacheKey(MayaSceneData scene)
        {
            if (!string.IsNullOrEmpty(scene.RawSha256)) return "sha256:" + scene.RawSha256;
            if (!string.IsNullOrEmpty(scene.SourcePath)) return "path:" + scene.SourcePath;
            return "scene@" + scene.GetHashCode();
        }

        private static string TrimQuotes(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            s = s.Trim();
            if (s.Length >= 2)
            {
                if ((s[0] == '"' && s[^1] == '"') || (s[0] == '\'' && s[^1] == '\''))
                    return s.Substring(1, s.Length - 2);
            }
            return s;
        }

        private static int IndexOf(List<string> tokens, string key)
        {
            for (int i = 0; i < tokens.Count; i++)
                if (string.Equals(tokens[i], key, StringComparison.Ordinal))
                    return i;
            return -1;
        }

        private static bool TryInt(string s, out int v)
        {
            return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v);
        }
    }
}
