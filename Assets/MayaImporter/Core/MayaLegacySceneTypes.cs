using System;
using System.Collections.Generic;
using System.Globalization;

namespace MayaImporter.Core
{
    /// <summary>
    /// Legacy/compat wrappers for old Evaluation code.
    ///
    /// Important:
    /// - In this project, NodeRecord / ConnectionRecord are NOT nested in MayaSceneData.
    ///   They exist as top-level types in namespace MayaImporter.Core.
    /// - This file provides Maya/API-free adapters that can be built from MayaSceneData.
    /// </summary>
    public sealed class MayaScene
    {
        private readonly MayaSceneData _data;

        private readonly Dictionary<string, MayaNode> _nodes =
            new Dictionary<string, MayaNode>(StringComparer.Ordinal);

        public MayaConnectionGraph ConnectionGraph { get; }

        public MayaScene(MayaSceneData data)
        {
            _data = data ?? new MayaSceneData();
            ConnectionGraph = MayaConnectionGraph.FromSceneData(_data);

            if (_data.Nodes != null)
            {
                foreach (var kv in _data.Nodes)
                {
                    var rec = kv.Value;
                    if (rec == null || string.IsNullOrEmpty(rec.Name)) continue;
                    _nodes[rec.Name] = MayaNode.FromRecord(rec);
                }
            }
        }

        public MayaNode GetNode(string nodeName)
        {
            if (string.IsNullOrEmpty(nodeName)) return null;
            _nodes.TryGetValue(nodeName, out var n);
            return n;
        }
    }

    public sealed class MayaConnectionGraph
    {
        public sealed class Connection
        {
            public string SrcNode;
            public string SrcAttr;
            public string DstNode;
            public string DstAttr;
        }

        public readonly List<Connection> Connections = new List<Connection>();

        public static MayaConnectionGraph FromSceneData(MayaSceneData data)
        {
            var g = new MayaConnectionGraph();
            if (data == null || data.Connections == null) return g;

            for (int i = 0; i < data.Connections.Count; i++)
            {
                var c = data.Connections[i];
                if (c == null) continue;

                var srcNode = MayaPlugUtil.ExtractNodePart(c.SrcPlug);
                var dstNode = MayaPlugUtil.ExtractNodePart(c.DstPlug);
                var srcAttr = MayaPlugUtil.ExtractAttrPart(c.SrcPlug);
                var dstAttr = MayaPlugUtil.ExtractAttrPart(c.DstPlug);

                if (string.IsNullOrEmpty(srcNode) || string.IsNullOrEmpty(dstNode))
                    continue;

                g.Connections.Add(new Connection
                {
                    SrcNode = srcNode,
                    DstNode = dstNode,
                    SrcAttr = srcAttr,
                    DstAttr = dstAttr
                });
            }

            return g;
        }
    }

    public sealed class MayaNode
    {
        public string NodeName { get; }
        public string NodeType { get; }

        public sealed class MayaAttrData
        {
            public object Value;
        }

        public sealed class MayaAttr
        {
            public MayaAttrData Data;
        }

        public readonly Dictionary<string, MayaAttr> Attributes =
            new Dictionary<string, MayaAttr>(StringComparer.Ordinal);

        private MayaNode(string name, string type)
        {
            NodeName = name;
            NodeType = type;
        }

        public static MayaNode FromRecord(NodeRecord rec)
        {
            var n = new MayaNode(rec.Name, rec.NodeType ?? "unknown");

            if (rec.Attributes != null)
            {
                foreach (var kv in rec.Attributes)
                {
                    var rawKey = kv.Key ?? "";
                    var key = rawKey.StartsWith(".", StringComparison.Ordinal) ? rawKey.Substring(1) : rawKey;

                    var rav = kv.Value;
                    object val = null;

                    if (rav != null)
                    {
                        // Prefer parsed value (best-effort parser)
                        if (rav.HasParsedValue)
                        {
                            val = rav.ParsedValue;
                        }
                        else
                        {
                            // Fallback: parse numeric tokens best-effort
                            val = ParseTokensBestEffort(rav.ValueTokens);
                        }
                    }

                    n.Attributes[key] = new MayaAttr { Data = new MayaAttrData { Value = val } };
                }
            }

            return n;
        }

        private static object ParseTokensBestEffort(List<string> tokens)
        {
            if (tokens == null || tokens.Count == 0) return null;

            // Collect numeric tokens
            var nums = new List<float>(16);
            for (int i = 0; i < tokens.Count; i++)
            {
                var s = (tokens[i] ?? "").Trim();
                if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
                    nums.Add(f);
            }

            // Scalar
            if (nums.Count == 1)
            {
                float f = nums[0];
                int fi = (int)Math.Round(f);
                if (Math.Abs(f - fi) < 1e-6f) return fi; // looks integral
                return f;
            }

            // Vec3 (keep old code happy: float[3])
            if (nums.Count >= 3 && nums.Count < 16)
            {
                return new[] { nums[0], nums[1], nums[2] };
            }

            // Array
            if (nums.Count >= 4)
            {
                return nums.ToArray();
            }

            return null;
        }
    }
}
