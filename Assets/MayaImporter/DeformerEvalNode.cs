using MayaImporter.Core;

namespace MayaImporter.Phase3.Evaluation
{
    /// <summary>
    /// Deformer（skin / blendShape 等）の評価ノード
    /// 実変形は Unity 側が行うため、評価は「Dirty 伝播」だけを担う
    /// </summary>
    public class DeformerEvalNode : EvalNode
    {
        private readonly MayaNode _mayaNode;

        public DeformerEvalNode(MayaNode node)
            : base(node.NodeName)
        {
            _mayaNode = node;
        }

        protected override void Evaluate(EvalContext ctx)
        {
            // -----------------------------
            // Deformer 自体は CPU 評価しない
            // 役割は「出力 attribute を Dirty にする」だけ
            // -----------------------------

            if (ctx == null)
                return;

            // Maya 的には outMesh が更新される
            // attribute 解像度で Dirty を伝播
            ctx.MarkAttributeDirty($"{NodeName}.outMesh");
        }
    }
}
