// MAYAIMPORTER_PATCH_V4: mb provenance/evidence + audit determinism (generated 2026-01-05)
using System;
using System.Collections.Generic;

namespace MayaImporter.Core
{
    /// <summary>
    /// Phase2 Step5:
    /// Enrich .mb heuristic scene with:
    /// - Better node type guesses (camera/light/joint/mesh/transform)
    /// - Heuristic connections from extracted plug-like strings
    /// NOTE:
    /// This is best-effort and MUST NOT replace Raw. It's additive only.
    /// </summary>
    public static class MayaMbHeuristicGraphRebuilder
    {
        private static readonly string[] KnownTypes =
        {
            "transform","mesh","joint","camera",
            "directionalLight","pointLight","spotLight","areaLight",
            "shadingEngine","lambert","blinn","phong",
            "skinCluster","blendShape"
        };

        public static void Enrich(MayaSceneData scene, MayaImportLog log)
        {
            if (scene == null) return;
            if (scene.MbIndex == null) return;

            // 1) Improve node types if placeholder DAG exists
            if (scene.Nodes != null && scene.Nodes.Count > 0)
                GuessNodeTypesFromNearbyStrings(scene, log);

            // 2) Add heuristic connections (only if there are none yet)
            if (scene.Connections != null && scene.Connections.Count == 0)
                GuessConnectionsFromPlugPairs(scene, log);
        }

        private static void GuessNodeTypesFromNearbyStrings(MayaSceneData scene, MayaImportLog log)
        {
            var strings = scene.MbIndex.ExtractedStrings;
            if (strings == null || strings.Count == 0) return;

            // Build quick lookup: leaf -> full dag paths that end with that leaf
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

            int updated = 0;

            // Scan extracted strings in order. If we see a node-ish token and near it a type-ish token,
            // assign that type to matching DAG leaf nodes.
            for (int i = 0; i < strings.Count; i++)
            {
                var s = strings[i];
                if (string.IsNullOrEmpty(s)) continue;

                // Candidate node leaf name (no pipes, no dots)
                if (!LooksLikeNodeLeaf(s))
                    continue;

                // Look ahead within a small window for a known type token
                string foundType = null;
                for (int j = i + 1; j < Math.Min(strings.Count, i + 18); j++)
                {
                    var t = strings[j];
                    if (string.IsNullOrEmpty(t)) continue;

                    var guessed = GuessTypeToken(t);
                    if (guessed != null)
                    {
                        foundType = guessed;
                        break;
                    }
                }

                if (foundType == null)
                {
                    // Also try look-behind a little
                    for (int j = Math.Max(0, i - 10); j < i; j++)
                    {
                        var t = strings[j];
                        if (string.IsNullOrEmpty(t)) continue;

                        var guessed = GuessTypeToken(t);
                        if (guessed != null)
                        {
                            foundType = guessed;
                            break;
                        }
                    }
                }

                if (foundType == null) continue;

                // Apply to DAG nodes that match this leaf
                if (!leafToFull.TryGetValue(s, out var fullPaths)) continue;

                for (int k = 0; k < fullPaths.Count; k++)
                {
                    var full = fullPaths[k];
                    if (!scene.Nodes.TryGetValue(full, out var rec) || rec == null) continue;

                    // Only upgrade unknown/transform-ish placeholders; don't overwrite real ones later
                    if (string.IsNullOrEmpty(rec.NodeType) || rec.NodeType == "unknown" || rec.NodeType == "transform")
                    {
                        rec.NodeType = foundType;
                        updated++;

                        // Mark as heuristic provenance
                        if (!rec.Attributes.ContainsKey(".mbHeuristicType"))
                            rec.Attributes[".mbHeuristicType"] = new RawAttributeValue("string", new List<string> { foundType });

                        if (!rec.Attributes.ContainsKey(".mbHeuristicTypeFrom"))
                            rec.Attributes[".mbHeuristicTypeFrom"] = new RawAttributeValue("string", new List<string> { s });
                    }
                }
            }

            if (updated > 0)
                log?.Info($".mb heuristic: upgraded node types for {updated} nodes (best-effort).");
        }

        private static void GuessConnectionsFromPlugPairs(MayaSceneData scene, MayaImportLog log)
        {
            var strings = scene.MbIndex.ExtractedStrings;
            if (strings == null || strings.Count == 0) return;

            int added = 0;

            // Heuristic:
            // - find plug-like tokens "something.attr" (not numeric)
            // - pair consecutive plugs within a small distance => connection
            // This is noisy but useful as a starting point and keeps Raw intact.
            string pendingPlug = null;
            int pendingIndex = -1;

            for (int i = 0; i < strings.Count; i++)
            {
                var s = strings[i];
                if (!LooksLikePlugToken(s)) continue;

                if (pendingPlug == null)
                {
                    pendingPlug = s;
                    pendingIndex = i;
                    continue;
                }

                // If too far apart, reset pending
                if (i - pendingIndex > 6)
                {
                    pendingPlug = s;
                    pendingIndex = i;
                    continue;
                }

                // Create connection pendingPlug -> s
                scene.Connections.Add(new ConnectionRecord(pendingPlug, s, force: false));
                added++;

                // Also preserve a raw statement for audit/debug
                scene.RawStatements.Add(new RawStatement
                {
                    LineStart = -1,
                    LineEnd = -1,
                    Command = "mbConnectHeuristic",
                    Text = $"connectAttr \"{pendingPlug}\" \"{s}\";",
                    Tokens = new List<string> { "connectAttr", pendingPlug, s }
                });

                // Reset to look for next pair
                pendingPlug = null;
                pendingIndex = -1;

                if (added >= 20000) break; // safety
            }

            if (added > 0)
                log?.Info($".mb heuristic: added {added} connection candidates (best-effort).");
            else
                log?.Warn(".mb heuristic: no plug-pairs found for connections (still OK, raw preserved).");
        }

        // -----------------------
        // Token classification
        // -----------------------

        private static string GuessTypeToken(string t)
        {
            // direct match
            for (int i = 0; i < KnownTypes.Length; i++)
                if (string.Equals(t, KnownTypes[i], StringComparison.Ordinal))
                    return KnownTypes[i];

            // common variants / hints
            if (t.EndsWith("Light", StringComparison.Ordinal)) return t;          // directionalLight etc
            if (t.EndsWith("Shape", StringComparison.Ordinal)) return "mesh";    // shape => mesh placeholder
            if (t.IndexOf("camera", StringComparison.OrdinalIgnoreCase) >= 0) return "camera";
            if (t.IndexOf("joint", StringComparison.OrdinalIgnoreCase) >= 0) return "joint";

            return null;
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

        private static bool LooksLikePlugToken(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            if (s.Length < 4 || s.Length > 160) return false;

            // must contain a dot splitting node + attr
            int dot = s.IndexOf('.');
            if (dot <= 0 || dot >= s.Length - 1) return false;

            // reject numeric-like (0.5 etc)
            if (IsNumericLike(s)) return false;

            // reject file paths
            if (s.IndexOf('/') >= 0 || s.IndexOf('\\') >= 0) return false;

            // node part could be dag leaf or dag path
            var node = s.Substring(0, dot);
            var attr = s.Substring(dot + 1);

            if (!HasIdentifierChar(node)) return false;
            if (!HasIdentifierChar(attr)) return false;

            return true;
        }

        private static bool HasIdentifierChar(string s)
        {
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (char.IsLetter(c) || c == '_' || c == '|' || c == ':') return true;
            }
            return false;
        }

        private static bool IsNumericLike(string s)
        {
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (char.IsDigit(c)) continue;
                if (c == '+' || c == '-' || c == '.') continue;
                if (c == 'e' || c == 'E') continue;
                return false;
            }
            return true;
        }

        private static string LeafOfDag(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            int last = name.LastIndexOf('|');
            if (last >= 0 && last < name.Length - 1) return name.Substring(last + 1);
            return name;
        }
    }
}
