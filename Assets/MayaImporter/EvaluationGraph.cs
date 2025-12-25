using System.Collections.Generic;
using System.Linq;

namespace MayaImporter.Phase3.Evaluation
{
    /// <summary>
    /// EvalNode の評価グラフ
    /// attribute 単位の依存を node Dirty に昇格させる
    /// </summary>
    public class EvaluationGraph
    {
        private readonly Dictionary<string, EvalNode> _nodes = new();

        public IEnumerable<EvalNode> Nodes => _nodes.Values;

        public void AddNode(EvalNode node)
        {
            _nodes[node.NodeName] = node;
        }

        public EvalNode GetNode(string name)
        {
            _nodes.TryGetValue(name, out var n);
            return n;
        }

        // -----------------------------
        // ★ 本実装 API
        // -----------------------------
        public void AddDependency(
            string srcNode,
            string srcAttr,
            string dstNode,
            string dstAttr)
        {
            var src = GetNode(srcNode);
            var dst = GetNode(dstNode);

            if (src == null || dst == null)
                return;

            // node dependency
            dst.AddInput(src);

            // attribute dependency（識別のみ）
            if (!string.IsNullOrEmpty(dstAttr))
                dst.AddInputAttribute($"{dstNode}.{dstAttr}");
        }

        // -----------------------------
        // 旧 API（互換）
        // -----------------------------
        public void AddDependency(string srcNode, string dstNode)
        {
            var src = GetNode(srcNode);
            var dst = GetNode(dstNode);
            if (src == null || dst == null) return;

            dst.AddInput(src);
        }

        // -----------------------------
        // Evaluation order
        // -----------------------------
        public List<EvalNode> BuildEvaluationOrder()
        {
            var result = new List<EvalNode>();
            var visited = new HashSet<EvalNode>();

            foreach (var node in _nodes.Values)
                Visit(node, visited, result);

            return result;
        }

        private void Visit(
            EvalNode node,
            HashSet<EvalNode> visited,
            List<EvalNode> result)
        {
            if (visited.Contains(node))
                return;

            visited.Add(node);

            foreach (var input in node.Inputs)
                Visit(input, visited, result);

            result.Add(node);
        }

        // -----------------------------
        // Dirty
        // -----------------------------
        public void MarkAllDirty()
        {
            foreach (var n in _nodes.Values)
                n.MarkDirty();
        }
    }
}
