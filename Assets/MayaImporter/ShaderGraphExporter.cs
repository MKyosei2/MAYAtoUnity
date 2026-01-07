// MAYAIMPORTER_PATCH_V4: mb provenance/evidence + audit determinism (generated 2026-01-05)
using System.Collections.Generic;
using UnityEngine;

namespace MayaImporter.Shader
{
    /// <summary>
    /// ShaderBuildContext / Material 
    /// Unity ŁuGNX|[g\Ȍ`vɂ܂Ƃ߂NXB
    ///
    /// iK̖:
    /// EShaderGraph 𒼐ڐȂ
    /// EMaterial + Maya Shader Network ̊SȊ֘A
    ///   GameObject / Component Ƃĕێ
    ///
    /// :
    /// EShaderGraph (.shadergraph) 
    /// EJSON / ƎtH[}bgo
    /// EURP/HDRP ؂ւ
    /// ׂẴNXh\
    /// </summary>
    public static class ShaderGraphExporter
    {
        /// <summary>
        /// ShaderBuildContext  Unity V[ɃGNX|[gizuj
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
            // 2. Material zu
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
            // 3. m[hifobOprj
            // ===============================
            foreach (var pair in context.AllNodes)
            {
                var node = pair.Key;
                if (node == null)
                    continue;

                var nodeGO = new GameObject($"Node_{node.name}");
                nodeGO.transform.SetParent(root.transform, false);

                //  Component Rs[`ŕێ
                var proxy = nodeGO.AddComponent<ShaderNodeProxy>();
                proxy.SourceNode = node;
            }

            return root;
        }
    }

    #region Holder / Proxy Components

    /// <summary>
    /// ShaderBuildContext ێ Component
    /// </summary>
    public sealed class ShaderBuildContextHolder : MonoBehaviour
    {
        public ShaderBuildContext Context;
    }

    /// <summary>
    /// Material QƂێ Component
    /// </summary>
    public sealed class ShaderMaterialReference : MonoBehaviour
    {
        public Material Material;
    }

    /// <summary>
    ///  ShaderNodeComponent QƂvLV
    /// iEfobOEăGNX|[gpj
    /// </summary>
    public sealed class ShaderNodeProxy : MonoBehaviour
    {
        public ShaderNodeComponentBase SourceNode;
    }

    #endregion
}
