// MAYAIMPORTER_PATCH_V4: mb provenance/evidence + audit determinism (generated 2026-01-05)
namespace MayaImporter.Core.Connections
{
    /// <summary>
    /// Maya connectAttr Sێ
    /// SrcNode.SrcAttr -> DstNode.DstAttr
    ///
    ///  ݊RXgN^ێ
    /// </summary>
    public class MayaConnection
    {
        // -----------------------------
        // {ij
        // -----------------------------
        public string SrcNode;
        public string SrcAttr;

        public string DstNode;
        public string DstAttr;

        // -----------------------------
        // ݊vpeB
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
        // RXgN^
        // -----------------------------

        public MayaConnection() { }

        //   ConnectAttrParser pibool j
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

        //  R[h int nĂꍇ̕ی
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
