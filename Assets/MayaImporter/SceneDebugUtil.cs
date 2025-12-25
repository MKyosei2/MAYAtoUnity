using System.Text;
using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Utils
{
    /// <summary>
    /// “Unity上で再構築できているか” を確認するためのデバッグ補助。
    /// 実行環境は Unity のみ（Mayaなし）前提。
    /// </summary>
    public static class SceneDebugUtil
    {
        public static string SummarizeNode(MayaNodeComponentBase node)
        {
            if (node == null) return "(null)";

            var sb = new StringBuilder(256);
            sb.Append(node.NodeType).Append(" : ").Append(node.NodeName);

            if (!string.IsNullOrEmpty(node.ParentName))
                sb.Append("  parent=").Append(node.ParentName);

            if (!string.IsNullOrEmpty(node.Uuid))
                sb.Append("  uuid=").Append(node.Uuid);

            sb.Append("  attrs=").Append(node.Attributes != null ? node.Attributes.Count : 0);
            sb.Append("  conns=").Append(node.Connections != null ? node.Connections.Count : 0);

            return sb.ToString();
        }

        public static void LogNode(MayaNodeComponentBase node)
        {
            Debug.Log(SummarizeNode(node));
        }

        public static void MarkGameObject(GameObject go, string label)
        {
            if (go == null) return;
            go.name = string.IsNullOrEmpty(label) ? go.name : $"{go.name} [{label}]";
        }
    }
}
