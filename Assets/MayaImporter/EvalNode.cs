using System.Collections.Generic;

namespace MayaImporter.Phase3.Evaluation
{
    /// <summary>
    /// 評価単位（node）
    /// attribute 単位の依存を見て、必要なときだけ評価する
    /// </summary>
    public abstract class EvalNode
    {
        public string NodeName { get; }

        // -----------------------------
        // Dirty 管理（node 単位）
        // -----------------------------
        public bool Dirty { get; private set; } = true;

        // -----------------------------
        // 依存関係（node）
        // -----------------------------
        protected readonly List<EvalNode> _inputs = new();
        public IReadOnlyList<EvalNode> Inputs => _inputs;

        // -----------------------------
        // ★ attribute 単位の入力依存
        // -----------------------------
        private readonly HashSet<string> _inputAttributes = new();
        public IReadOnlyCollection<string> InputAttributes => _inputAttributes;

        protected EvalNode(string nodeName)
        {
            NodeName = nodeName;
        }

        // -----------------------------
        // Dependency 登録
        // -----------------------------
        public void AddInput(EvalNode node)
        {
            if (!_inputs.Contains(node))
                _inputs.Add(node);
        }

        public void AddInputAttribute(string attrPath)
        {
            if (!string.IsNullOrEmpty(attrPath))
                _inputAttributes.Add(attrPath);
        }

        // -----------------------------
        // Dirty 制御
        // -----------------------------
        public void MarkDirty()
        {
            if (Dirty)
                return;

            Dirty = true;

            // 下流へ伝播（node 単位）
            foreach (var n in _inputs)
                n.MarkDirty();
        }

        private void ClearDirty()
        {
            Dirty = false;
        }

        // -----------------------------
        // Evaluation（★本実装）
        // -----------------------------
        public void EvaluateIfNeeded(EvalContext ctx)
        {
            // 1. node が Dirty でない → 何もしない
            if (!Dirty)
                return;

            // 2. attribute 依存がある場合、
            //    自分が依存している attribute が変更されていなければ skip
            if (_inputAttributes.Count > 0 &&
                ctx != null &&
                ctx.HasAnyDirtyAttributes())
            {
                bool hit = false;

                foreach (var attr in _inputAttributes)
                {
                    if (ctx.IsAttributeDirty(attr))
                    {
                        hit = true;
                        break;
                    }
                }

                if (!hit)
                {
                    // attribute 的に無関係 → Dirty 解除して skip
                    ClearDirty();
                    return;
                }
            }

            // 3. 評価実行
            Evaluate(ctx);

            // 4. 自分の output attribute は dirty 扱い
            if (ctx != null)
                ctx.MarkAttributeDirty(NodeName);

            ClearDirty();
        }

        // -----------------------------
        // 実処理
        // -----------------------------
        protected abstract void Evaluate(EvalContext ctx);
    }
}
