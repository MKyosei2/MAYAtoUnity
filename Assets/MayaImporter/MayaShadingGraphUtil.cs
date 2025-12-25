using System;
using System.Collections.Generic;
using MayaImporter.Core;

namespace MayaImporter.Shading
{
    /// <summary>
    /// Utility to traverse Maya shading graph in MayaSceneData (Unity-only).
    /// Finds upstream nodes by following connections (src -> dst) where dst is current node.
    ///
    /// IMPORTANT:
    /// - We stop not only at "file" but also at nodes that bake a PNG and publish it via MayaTextureMetadata:
    ///   remapColor / remapValue / layeredTexture / remapHsv / reverse
    /// This enables shader nodes (aiStandardSurface etc.) to store those nodes in MayaMaterialMetadata,
    /// and MaterialResolver (metadata path) can load the baked PNG without touching Core.
    /// </summary>
    internal static class MayaShadingGraphUtil
    {
        // NodeTypes that can serve as "texture nodes" in Unity reconstruction
        private static readonly HashSet<string> s_terminalTextureLikeNodeTypes =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "file",
                "remapColor",
                "remapValue",
                "layeredTexture",
                "remapHsv",
                "reverse",
            };

        public static string ResolveToFirstUpstreamFile(MayaSceneData scene, string startNodeNameOrLeaf, int maxDepth = 12)
        {
            if (scene?.Nodes == null || scene.Connections == null) return null;
            if (string.IsNullOrEmpty(startNodeNameOrLeaf)) return null;

            var start = FindExactNodeName(scene, startNodeNameOrLeaf);
            if (string.IsNullOrEmpty(start)) return null;

            if (IsTerminalTextureLike(scene, start))
                return start;

            return FindUpstreamFirstTerminalTextureLike(scene, start, maxDepth);
        }

        private static string FindUpstreamFirstTerminalTextureLike(MayaSceneData scene, string startNodeExactName, int maxDepth)
        {
            var q = new Queue<(string node, int depth)>();
            var visited = new HashSet<string>(StringComparer.Ordinal);

            q.Enqueue((startNodeExactName, 0));
            visited.Add(startNodeExactName);

            while (q.Count > 0)
            {
                var (cur, depth) = q.Dequeue();

                if (IsTerminalTextureLike(scene, cur))
                    return cur;

                if (depth >= maxDepth)
                    continue;

                // Upstream edges: src -> dst where dst is current
                for (int i = 0; i < scene.Connections.Count; i++)
                {
                    var c = scene.Connections[i];
                    if (c == null) continue;

                    var dstNodePart = MayaPlugUtil.ExtractNodePart(c.DstPlug);
                    if (!MayaPlugUtil.NodeMatches(dstNodePart, cur))
                        continue;

                    var srcNodePart = MayaPlugUtil.ExtractNodePart(c.SrcPlug);
                    var srcExact = FindExactNodeName(scene, srcNodePart);
                    if (string.IsNullOrEmpty(srcExact))
                        continue;

                    if (visited.Add(srcExact))
                        q.Enqueue((srcExact, depth + 1));
                }
            }

            return null;
        }

        public static string FindExactNodeName(MayaSceneData scene, string nameOrLeaf)
        {
            if (scene?.Nodes == null || string.IsNullOrEmpty(nameOrLeaf)) return null;

            if (scene.Nodes.ContainsKey(nameOrLeaf))
                return nameOrLeaf;

            var leaf = MayaPlugUtil.LeafName(nameOrLeaf);

            foreach (var kv in scene.Nodes)
            {
                var r = kv.Value;
                if (r == null) continue;

                if (string.Equals(MayaPlugUtil.LeafName(r.Name), leaf, StringComparison.Ordinal))
                    return r.Name;
            }

            return null;
        }

        private static bool IsTerminalTextureLike(MayaSceneData scene, string exactName)
        {
            if (scene?.Nodes == null) return false;
            if (!scene.Nodes.TryGetValue(exactName, out var r) || r == null) return false;

            var t = r.NodeType ?? "";
            return s_terminalTextureLikeNodeTypes.Contains(t);
        }
    }
}
