using UnityEngine;
using MayaImporter.Runtime;

namespace MayaImporter.Core
{
    /// <summary>
    /// Phase B:
    /// 全ノードに「Unityだけで検証できる100%の証拠」を付与する最終処理。
    /// - MayaOpaqueNodeRuntime: 再構築済みマーカー
    /// - MayaOpaqueAttributePreview: Raw属性プレビュー
    /// - MayaOpaqueConnectionPreview: 接続プレビュー
    /// - MayaDecodedAttributeSummary: 既存のカテゴリ別サマリ
    /// </summary>
    public static class MayaNodeRepresentationFinalizer
    {
        public static void FinalizeNode(MayaNodeComponentBase node, MayaImportOptions options)
        {
            if (node == null) return;
            options ??= new MayaImportOptions();

            // Marker
            if (options.AttachOpaqueRuntimeMarker)
            {
                var marker = node.GetComponent<MayaOpaqueNodeRuntime>();
                if (marker == null) marker = node.gameObject.AddComponent<MayaOpaqueNodeRuntime>();

                marker.mayaNodeType = node.NodeType ?? "";
                marker.mayaNodeName = node.NodeName ?? "";
                marker.mayaParentName = node.ParentName ?? "";
                marker.mayaUuid = node.Uuid ?? "";
                marker.attributeCount = node.Attributes != null ? node.Attributes.Count : 0;
                marker.connectionCount = node.Connections != null ? node.Connections.Count : 0;
                marker.gizmoSize = Mathf.Max(0.001f, options.OpaqueRuntimeGizmoSize);
            }

            // Raw attributes
            if (options.AttachOpaqueAttributePreview)
            {
                var ap = node.GetComponent<MayaOpaqueAttributePreview>();
                if (ap == null) ap = node.gameObject.AddComponent<MayaOpaqueAttributePreview>();

                ap.maxEntries = Mathf.Clamp(options.OpaquePreviewMaxEntries, 0, 2048);
                ap.BuildFrom(node);
            }

            // Connections
            if (options.AttachOpaqueConnectionPreview)
            {
                var cp = node.GetComponent<MayaOpaqueConnectionPreview>();
                if (cp == null) cp = node.gameObject.AddComponent<MayaOpaqueConnectionPreview>();

                cp.maxEntries = Mathf.Clamp(options.OpaquePreviewMaxEntries, 0, 4096);
                cp.BuildFrom(node);
            }

            // Typed summary (existing component)
            if (options.AttachDecodedAttributeSummary)
            {
                var sum = node.GetComponent<MayaDecodedAttributeSummary>();
                if (sum == null) sum = node.gameObject.AddComponent<MayaDecodedAttributeSummary>();

                sum.maxEntriesPerCategory = Mathf.Clamp(options.OpaquePreviewMaxEntries, 0, 4096);
                sum.BuildFrom(node);
            }
        }
    }
}
