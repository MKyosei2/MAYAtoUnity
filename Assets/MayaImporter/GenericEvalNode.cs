// PATCH: ProductionImpl v6 (Unity-only, retention-first)
using MayaImporter.Core;

namespace MayaImporter.Phase3.Evaluation
{
    /// <summary>
    /// Phase3 �����i�K�p�̔ėp�m�[�h
    /// ���p���艻�Ƃ��āu���ԋ쓮�m�[�h�ianimCurve�j�v���N�_�ɂ���@�\��ǉ�
    /// </summary>
    public class GenericEvalNode : EvalNode
    {
        // �� Time �ω��� Dirty �ɂ��ׂ���
        public bool IsTimeDriven { get; private set; }

        public GenericEvalNode(string nodeName)
            : base(nodeName)
        {
            // nodeName �����ł͖{������ł��Ȃ����A
            // Phase3 ���艻�̂��� EvalGraphBuilder �� SetTimeDriven ���ĂԑO��ɂ���
            IsTimeDriven = false;
        }

        public void SetTimeDriven(bool v)
        {
            IsTimeDriven = v;
        }

        protected override void Evaluate(EvalContext ctx)
        {
            // Generic �͉������Ȃ��i�ˑ��֌W�̓`�d�����S���j
        }
    }
}
