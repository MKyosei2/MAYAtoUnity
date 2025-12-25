using System.Collections.Generic;
using UnityEngine;

namespace MayaImporter.Shader
{
    /// <summary>
    /// ShaderBuildContext / Material を
    /// Unity 上で「エクスポート可能な形」にまとめるクラス。
    ///
    /// 現段階の役割:
    /// ・ShaderGraph を直接生成しない
    /// ・Material + Maya Shader Network の完全な関連情報を
    ///   GameObject / Component として保持
    ///
    /// 将来:
    /// ・ShaderGraph (.shadergraph) 生成
    /// ・JSON / 独自フォーマット書き出し
    /// ・URP/HDRP 切り替え
    /// すべてこのクラスから派生可能
    /// </summary>
    public static class ShaderGraphExporter
    {
        /// <summary>
        /// ShaderBuildContext を Unity シーン上にエクスポート（配置）する
        /// </summary>
        public static GameObject ExportToScene(
            ShaderBuildContext context,
            IEnumerable<Material> materials,
            Transform parent = null)
        {
            var root = new GameObject("MayaShaderExport");
            if (parent != null)
                root.transform.SetParent(parent, false);

            if (context == null)
                return root;

            // ===============================
            // 1. Context Holder
            // ===============================
            var contextHolder = root.AddComponent<ShaderBuildContextHolder>();
            contextHolder.Context = context;

            // ===============================
            // 2. Material 配置
            // ===============================
            if (materials != null)
            {
                foreach (var mat in materials)
                {
                    if (mat == null)
                        continue;

                    var matGO = new GameObject(mat.name);
                    matGO.transform.SetParent(root.transform, false);

                    var holder = matGO.AddComponent<ShaderMaterialReference>();
                    holder.Material = mat;
                }
            }

            // ===============================
            // 3. ノード可視化（デバッグ用途）
            // ===============================
            foreach (var pair in context.AllNodes)
            {
                var node = pair.Key;
                if (node == null)
                    continue;

                var nodeGO = new GameObject($"Node_{node.name}");
                nodeGO.transform.SetParent(root.transform, false);

                // 既存 Component をコピーする形で保持
                var proxy = nodeGO.AddComponent<ShaderNodeProxy>();
                proxy.SourceNode = node;
            }

            return root;
        }
    }

    #region Holder / Proxy Components

    /// <summary>
    /// ShaderBuildContext を保持する Component
    /// </summary>
    public sealed class ShaderBuildContextHolder : MonoBehaviour
    {
        public ShaderBuildContext Context;
    }

    /// <summary>
    /// Material 参照を保持する Component
    /// </summary>
    public sealed class ShaderMaterialReference : MonoBehaviour
    {
        public Material Material;
    }

    /// <summary>
    /// 元の ShaderNodeComponent を参照するプロキシ
    /// （可視化・デバッグ・再エクスポート用）
    /// </summary>
    public sealed class ShaderNodeProxy : MonoBehaviour
    {
        public ShaderNodeComponentBase SourceNode;
    }

    #endregion
}
