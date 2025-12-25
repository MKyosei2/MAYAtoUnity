namespace MayaImporter.Core.Connections
{
    /// <summary>
    /// Maya connectAttr 完全保持版
    /// SrcNode.SrcAttr -> DstNode.DstAttr
    ///
    /// ※ 旧実装互換コンストラクタを保持
    /// </summary>
    public class MayaConnection
    {
        // -----------------------------
        // 本実装（正）
        // -----------------------------
        public string SrcNode;
        public string SrcAttr;

        public string DstNode;
        public string DstAttr;

        // -----------------------------
        // 旧実装互換プロパティ
        // -----------------------------

        public string SrcAttrPath
        {
            get
            {
                if (string.IsNullOrEmpty(SrcAttr))
                    return SrcNode;
                return $"{SrcNode}.{SrcAttr}";
            }
        }

        public string DstAttrPath
        {
            get
            {
                if (string.IsNullOrEmpty(DstAttr))
                    return DstNode;
                return $"{DstNode}.{DstAttr}";
            }
        }

        // -----------------------------
        // コンストラクタ
        // -----------------------------

        public MayaConnection() { }

        // ★ 旧 ConnectAttrParser 用（bool 引数）
        public MayaConnection(
            string srcNode,
            string srcAttrPath,
            string dstNode,
            string dstAttrPath,
            bool unused)
        {
            SplitAttrPath(srcAttrPath, out SrcNode, out SrcAttr);
            SplitAttrPath(dstAttrPath, out DstNode, out DstAttr);
        }

        // ★ 旧コードが int を渡していた場合の保険
        public MayaConnection(
            string srcNode,
            string srcAttrPath,
            string dstNode,
            string dstAttrPath,
            int unused)
        {
            SplitAttrPath(srcAttrPath, out SrcNode, out SrcAttr);
            SplitAttrPath(dstAttrPath, out DstNode, out DstAttr);
        }

        // -----------------------------
        // helper
        // -----------------------------
        private void SplitAttrPath(
            string path,
            out string node,
            out string attr)
        {
            int idx = path.IndexOf('.');
            if (idx < 0)
            {
                node = path;
                attr = string.Empty;
            }
            else
            {
                node = path.Substring(0, idx);
                attr = path.Substring(idx + 1);
            }
        }

        public override string ToString()
        {
            return $"{SrcNode}.{SrcAttr} -> {DstNode}.{DstAttr}";
        }
    }
}
