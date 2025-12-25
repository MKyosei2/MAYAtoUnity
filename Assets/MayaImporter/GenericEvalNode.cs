using MayaImporter.Core;

namespace MayaImporter.Phase3.Evaluation
{
    /// <summary>
    /// Phase3 初期段階用の汎用ノード
    /// 実用安定化として「時間駆動ノード（animCurve）」を起点にする機能を追加
    /// </summary>
    public class GenericEvalNode : EvalNode
    {
        // ★ Time 変化で Dirty にすべきか
        public bool IsTimeDriven { get; private set; }

        public GenericEvalNode(string nodeName)
            : base(nodeName)
        {
            // nodeName だけでは本来判定できないが、
            // Phase3 安定化のため EvalGraphBuilder で SetTimeDriven を呼ぶ前提にする
            IsTimeDriven = false;
        }

        public void SetTimeDriven(bool v)
        {
            IsTimeDriven = v;
        }

        protected override void Evaluate(EvalContext ctx)
        {
            // Generic は何もしない（依存関係の伝播だけ担当）
        }
    }
}
