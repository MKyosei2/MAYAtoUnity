using MayaImporter.Core;

namespace MayaImporter.Phase3.Evaluation
{
    /// <summary>
    /// SkinCluster Deformer EvalNode
    /// 実スキニングは Unity が行うため、Dirty 伝播のみ担当
    /// </summary>
    public class SkinClusterEvalNode : EvalNode
    {
        private readonly MayaNode _mayaNode;

        public SkinClusterEvalNode(MayaNode node)
            : base(node.NodeName)
        {
            _mayaNode = node;
        }

        protected override void Evaluate(EvalContext ctx)
        {
            if (ctx == null)
                return;

            // skinCluster も outMesh を更新する
            ctx.MarkAttributeDirty($"{NodeName}.outMesh");
        }
    }
}
