// PATCH: ProductionImpl v6 (Unity-only, retention-first)
using MayaImporter.Core;

namespace MayaImporter.Phase3.Evaluation
{
    /// <summary>
    /// Deformer�iskin / blendShape ���j�̕]���m�[�h
    /// ���ό`�� Unity �����s�����߁A�]���́uDirty �`�d�v������S��
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
            // Deformer ���̂� CPU �]�����Ȃ�
            // �����́u�o�� attribute �� Dirty �ɂ���v����
            // -----------------------------

            if (ctx == null)
                return;

            // Maya �I�ɂ� outMesh ���X�V�����
            // attribute �𑜓x�� Dirty ��`�d
            ctx.MarkAttributeDirty($"{NodeName}.outMesh");
        }
    }
}
