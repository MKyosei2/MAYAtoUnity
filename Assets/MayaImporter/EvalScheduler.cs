using System.Collections.Generic;

namespace MayaImporter.Phase3.Evaluation
{
    public class EvalScheduler
    {
        private readonly EvaluationGraph _graph;
        private List<EvalNode> _order;

        private float _lastTime = float.NaN;

        public EvalScheduler(EvaluationGraph graph)
        {
            _graph = graph;
            _order = _graph.BuildEvaluationOrder();
        }

        public void Evaluate(EvalContext ctx)
        {
            // -----------------------------
            // Dirty起点：Time変化
            // -----------------------------
            if (ctx != null)
            {
                if (_lastTime != ctx.Time)
                {
                    _lastTime = ctx.Time;

                    // ★ animCurve 系だけ Dirty
                    foreach (var n in _graph.Nodes)
                    {
                        if (n is GenericEvalNode g && g.IsTimeDriven)
                            n.MarkDirty();
                    }
                }
            }

            // -----------------------------
            // 評価（Dirtyのみ実行）
            // -----------------------------
            foreach (var node in _order)
                node.EvaluateIfNeeded(ctx);
        }

        public void Rebuild()
        {
            _order = _graph.BuildEvaluationOrder();
        }
    }
}
