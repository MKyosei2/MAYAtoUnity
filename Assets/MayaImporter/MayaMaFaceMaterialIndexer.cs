using System;
using System.Collections.Generic;

namespace MayaImporter.Core
{
    /// <summary>
    /// Phase2 Step19:
    /// Parse .ma "sets -e -forceElement <shadingEngine> <mesh.f[...]> ..." statements
    /// and build per-face SG assignment ranges.
    /// </summary>
    public static class MayaMaFaceMaterialIndexer
    {
        public sealed class Assignment
        {
            public readonly Dictionary<string, List<(int start, int end)>> SgToFaceRanges
                = new Dictionary<string, List<(int start, int end)>>(StringComparer.Ordinal);

            public bool HasAny => SgToFaceRanges.Count > 0;
        }

        /// <summary>
        /// Returns: meshShapeName -> Assignment
        /// meshShapeName uses leaf name (strip dag path) to match NodeRecord.Name.
        /// </summary>
        public static Dictionary<string, Assignment> Build(MayaSceneData scene, MayaImportLog log)
        {
            var map = new Dictionary<string, Assignment>(StringComparer.Ordinal);
            if (scene?.RawStatements == null || scene.RawStatements.Count == 0) return map;

            int hit = 0;

            foreach (var st in scene.RawStatements)
            {
                if (st == null) continue;
                if (!string.Equals(st.Command, "sets", StringComparison.Ordinal)) continue;
                var tokens = st.Tokens;
                if (tokens == null || tokens.Count == 0) continue;

                int fe = FindForceElement(tokens);
                if (fe < 0 || fe + 1 >= tokens.Count) continue;

                var sg = tokens[fe + 1];
                if (string.IsNullOrEmpty(sg)) continue;

                for (int i = fe + 2; i < tokens.Count; i++)
                {
                    var m = tokens[i];
                    if (string.IsNullOrEmpty(m)) continue;
                    if (m.StartsWith("-", StringComparison.Ordinal)) continue;

                    if (!TryParseMember(m, out var mesh, out var start, out var end, out var wholeObject))
                        continue;

                    if (string.IsNullOrEmpty(mesh)) continue;

                    if (!map.TryGetValue(mesh, out var a))
                    {
                        a = new Assignment();
                        map[mesh] = a;
                    }

                    if (!a.SgToFaceRanges.TryGetValue(sg, out var ranges))
                    {
                        ranges = new List<(int start, int end)>(8);
                        a.SgToFaceRanges[sg] = ranges;
                    }

                    if (wholeObject)
                    {
                        // store sentinel: (-1,-1) => whole object (resolved later once faceCount known)
                        ranges.Add((-1, -1));
                    }
                    else
                    {
                        if (start < 0) start = 0;
                        if (end < start) end = start;
                        ranges.Add((start, end));
                    }

                    hit++;
                }
            }

            if (hit > 0) log?.Info($".ma sets(forceElement) parsed: {hit} members (Step19 index built).");
            else log?.Info(".ma sets(forceElement) parsed: 0 (per-face submesh may be unavailable, OK).");

            return map;
        }

        private static int FindForceElement(List<string> tokens)
        {
            for (int i = 0; i < tokens.Count; i++)
            {
                if (tokens[i] == "-forceElement" || tokens[i] == "-fe")
                    return i;
            }
            return -1;
        }

        private static bool TryParseMember(string token, out string mesh, out int start, out int end, out bool wholeObject)
        {
            mesh = null;
            start = 0;
            end = 0;
            wholeObject = false;

            // strip dag path leaf
            token = Leaf(token);

            // face component: mesh.f[0:12] or mesh.f[3]
            int f = token.IndexOf(".f[", StringComparison.Ordinal);
            if (f >= 0)
            {
                mesh = token.Substring(0, f);
                int lb = token.IndexOf('[', f);
                int rb = token.IndexOf(']', f);
                if (lb < 0 || rb < 0 || rb <= lb + 1) return false;

                var inside = token.Substring(lb + 1, rb - lb - 1);
                int colon = inside.IndexOf(':');
                if (colon >= 0)
                {
                    if (!int.TryParse(inside.Substring(0, colon), out start)) return false;
                    if (!int.TryParse(inside.Substring(colon + 1), out end)) return false;
                    wholeObject = false;
                    return true;
                }
                else
                {
                    if (!int.TryParse(inside, out start)) return false;
                    end = start;
                    wholeObject = false;
                    return true;
                }
            }

            // whole object assignment (no .f[])
            mesh = token;
            wholeObject = true;
            return true;
        }

        private static string Leaf(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            int last = s.LastIndexOf('|');
            if (last >= 0 && last < s.Length - 1) return s.Substring(last + 1);
            return s;
        }
    }
}
