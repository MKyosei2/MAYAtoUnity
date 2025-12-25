using System;

namespace MayaImporter.Core
{
    /// <summary>
    /// Utilities for Maya plug strings:
    ///  - "node.attr"
    ///  - "|grp|node.attr"
    ///  - "ns:node.attr"
    ///  - "|grp|ns:node.attr"
    /// </summary>
    public static class MayaPlugUtil
    {
        /// <summary>
        /// Returns the node portion of a plug (best-effort).
        /// Example:
        ///  "|grp|pCube1Shape.outMesh" -> "|grp|pCube1Shape"
        ///  "pCube1.translateX" -> "pCube1"
        /// </summary>
        public static string ExtractNodePart(string plug)
        {
            if (string.IsNullOrEmpty(plug)) return null;

            var lastDot = plug.LastIndexOf('.');
            if (lastDot <= 0) return plug; // no attr, treat entire string as node part
            return plug.Substring(0, lastDot);
        }

        /// <summary>
        /// Returns the attribute portion of a plug (best-effort).
        /// Example:
        ///  "|grp|pCube1Shape.instObjGroups[0]" -> "instObjGroups[0]"
        ///  "lambert1.c" -> "c"
        /// </summary>
        public static string ExtractAttrPart(string plug)
        {
            if (string.IsNullOrEmpty(plug)) return null;
            var lastDot = plug.LastIndexOf('.');
            if (lastDot < 0 || lastDot == plug.Length - 1) return null;
            return plug.Substring(lastDot + 1);
        }

        /// <summary>
        /// Compares node identity robustly:
        /// - exact match of nodePart and nodeName
        /// - leaf match (after last '|') to handle DAG paths
        /// </summary>
        public static bool NodeMatches(string nodePart, string nodeName)
        {
            if (string.IsNullOrEmpty(nodePart) || string.IsNullOrEmpty(nodeName))
                return false;

            if (string.Equals(nodePart, nodeName, StringComparison.Ordinal))
                return true;

            var nodePartLeaf = LeafName(nodePart);
            var nodeNameLeaf = LeafName(nodeName);

            return string.Equals(nodePartLeaf, nodeNameLeaf, StringComparison.Ordinal);
        }

        /// <summary>
        /// Returns the leaf name after last '|' in a DAG path.
        /// "a|b|c" -> "c"
        /// "node" -> "node"
        /// </summary>
        public static string LeafName(string dagOrName)
        {
            if (string.IsNullOrEmpty(dagOrName)) return dagOrName;
            var idx = dagOrName.LastIndexOf('|');
            if (idx < 0) return dagOrName;
            if (idx == dagOrName.Length - 1) return dagOrName;
            return dagOrName.Substring(idx + 1);
        }
    }
}
