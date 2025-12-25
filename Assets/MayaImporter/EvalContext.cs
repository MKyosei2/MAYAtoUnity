using System.Collections.Generic;

namespace MayaImporter.Phase3.Evaluation
{
    /// <summary>
    /// Evaluation 実行時のコンテキスト
    /// 「どの attribute が変わったか」を保持する
    /// </summary>
    public class EvalContext
    {
        public float Time;

        // ★ 変更された attribute（node.attr）
        private readonly HashSet<string> _dirtyAttributes = new();

        public void MarkAttributeDirty(string attrPath)
        {
            if (!string.IsNullOrEmpty(attrPath))
                _dirtyAttributes.Add(attrPath);
        }

        public bool IsAttributeDirty(string attrPath)
        {
            return _dirtyAttributes.Contains(attrPath);
        }

        public bool HasAnyDirtyAttributes()
        {
            return _dirtyAttributes.Count > 0;
        }

        public void ClearDirtyAttributes()
        {
            _dirtyAttributes.Clear();
        }
    }
}
