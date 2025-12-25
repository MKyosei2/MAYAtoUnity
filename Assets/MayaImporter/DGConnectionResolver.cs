using System;
using System.Collections.Generic;
using MayaImporter.Core;

namespace MayaImporter.Utils
{
    /// <summary>
    /// MayaSceneData.Connections を「引きやすい形」に索引化する。
    /// 100点条件（再構築）では、Unity側で “接続” の概念を保持できることが重要なので、
    /// まずは lossless に参照できるインデックスを作る。
    /// </summary>
    public sealed class DGConnectionResolver
    {
        private readonly Dictionary<string, List<string>> _incomingByDstPlug = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> _outgoingBySrcPlug = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        private readonly Dictionary<string, List<ConnectionRecord>> _incomingByDstNode = new Dictionary<string, List<ConnectionRecord>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<ConnectionRecord>> _outgoingBySrcNode = new Dictionary<string, List<ConnectionRecord>>(StringComparer.Ordinal);

        public DGConnectionResolver(MayaSceneData scene)
        {
            if (scene == null || scene.Connections == null) return;

            foreach (var c in scene.Connections)
            {
                if (c == null) continue;
                if (!string.IsNullOrEmpty(c.DstPlug))
                {
                    if (!_incomingByDstPlug.TryGetValue(c.DstPlug, out var list))
                    {
                        list = new List<string>();
                        _incomingByDstPlug[c.DstPlug] = list;
                    }
                    list.Add(c.SrcPlug);
                }

                if (!string.IsNullOrEmpty(c.SrcPlug))
                {
                    if (!_outgoingBySrcPlug.TryGetValue(c.SrcPlug, out var list))
                    {
                        list = new List<string>();
                        _outgoingBySrcPlug[c.SrcPlug] = list;
                    }
                    list.Add(c.DstPlug);
                }

                if (StringParsingUtil.TrySplitPlug(c.DstPlug, out var dstNode, out _))
                {
                    if (!_incomingByDstNode.TryGetValue(dstNode, out var list))
                    {
                        list = new List<ConnectionRecord>();
                        _incomingByDstNode[dstNode] = list;
                    }
                    list.Add(c);
                }

                if (StringParsingUtil.TrySplitPlug(c.SrcPlug, out var srcNode, out _))
                {
                    if (!_outgoingBySrcNode.TryGetValue(srcNode, out var list))
                    {
                        list = new List<ConnectionRecord>();
                        _outgoingBySrcNode[srcNode] = list;
                    }
                    list.Add(c);
                }
            }
        }

        public IReadOnlyList<string> GetSourcesOf(string dstPlug)
        {
            if (string.IsNullOrEmpty(dstPlug)) return Array.Empty<string>();
            return _incomingByDstPlug.TryGetValue(dstPlug, out var list) ? list : Array.Empty<string>();
        }

        public IReadOnlyList<string> GetDestinationsOf(string srcPlug)
        {
            if (string.IsNullOrEmpty(srcPlug)) return Array.Empty<string>();
            return _outgoingBySrcPlug.TryGetValue(srcPlug, out var list) ? list : Array.Empty<string>();
        }

        public IReadOnlyList<ConnectionRecord> GetIncomingToNode(string nodeName)
        {
            if (string.IsNullOrEmpty(nodeName)) return Array.Empty<ConnectionRecord>();
            return _incomingByDstNode.TryGetValue(nodeName, out var list) ? list : Array.Empty<ConnectionRecord>();
        }

        public IReadOnlyList<ConnectionRecord> GetOutgoingFromNode(string nodeName)
        {
            if (string.IsNullOrEmpty(nodeName)) return Array.Empty<ConnectionRecord>();
            return _outgoingBySrcNode.TryGetValue(nodeName, out var list) ? list : Array.Empty<ConnectionRecord>();
        }

        public bool TryGetSingleSource(string dstPlug, out string srcPlug)
        {
            srcPlug = null;
            var list = GetSourcesOf(dstPlug);
            if (list == null || list.Count == 0) return false;
            srcPlug = list[0];
            return true;
        }
    }
}
