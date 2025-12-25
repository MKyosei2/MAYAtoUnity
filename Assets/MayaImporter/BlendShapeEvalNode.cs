using MayaImporter.Core;

namespace MayaImporter.Phase3.Evaluation
{
    /// <summary>
    /// BlendShape Deformer EvalNode
    /// 実変形は Unity が行うため、評価は Dirty 伝播のみ
    /// </summary>
    public class BlendShapeEvalNode : EvalNode
    {
        private readonly MayaNode _mayaNode;

        public BlendShapeEvalNode(MayaNode node)
            : base(node.NodeName)
        {
            _mayaNode = node;
        }

        protected override void Evaluate(EvalContext ctx)
        {
            if (ctx == null)
                return;

            // Maya 的には outMesh が更新される
            ctx.MarkAttributeDirty($"{NodeName}.outMesh");
        }
    }
}
