// MAYAIMPORTER_PATCH_V4: mb provenance/evidence + audit determinism (generated 2026-01-05)
using System.Collections.Generic;
using UnityEngine;

namespace MayaImporter.Shader
{
    /// <summary>
    /// Maya Hypershade Network 
    /// Unity ṕuShader / Material č\zp ԃf[^vɕϊNXB
    ///
    /// :
    /// EShaderNetwork 
    /// EŏII Material o̓m[h
    /// Em[hڑOtgo[X\ȍ\ɕϊ
    ///
    ///  ShaderGraph / Material ̎
    ///   ShaderGraphBuilder / ShaderGraphExporter Ɉς˂
    /// </summary>
    public static class HypershadeToShaderGraphConverter
    {
        /// <summary>
        /// ShaderNetwork  ShaderBuildContext 𐶐
        /// </summary>
        public static ShaderBuildContext Convert(ShaderNetwork network)
        {
            var context = new ShaderBuildContext();

            if (network == null || network.Nodes.Count == 0)
                return context;

            // ===============================
            // 1. o̓m[hiMaterialjo
            // ===============================
            foreach (var node in network.Nodes.Values)
            {
                if (IsMaterialOutputNode(node))
                {
                    context.OutputNodes.Add(node);
                }
            }

            // tH[obNiS Unknown ̏ꍇȂǁj
            if (context.OutputNodes.Count == 0)
            {
                foreach (var node in network.Nodes.Values)
                {
                    context.OutputNodes.Add(node);
                }
            }

            // ===============================
            // 2. Oto^
            // ===============================
            foreach (var node in network.Nodes.Values)
            {
                var info = new ShaderNodeInfo
                {
                    Node = node,
                    Inputs = node.InputConnections,
                    Outputs = node.OutputConnections
                };

                context.AllNodes.Add(node, info);
            }

            return context;
        }

        #region Utilities

        /// <summary>
        /// Maya IɁuMaterial óvƂ݂Ȃm[h
        /// </summary>
        private static bool IsMaterialOutputNode(ShaderNodeComponentBase node)
        {
            if (node == null)
                return false;

            var type = node.MayaNodeType;

            switch (type)
            {
                case "lambert":
                case "phong":
                case "phongE":
                case "blinn":
                case "surfaceShader":
                case "aiStandardSurface":
                    return true;
            }

            return false;
        }

        #endregion
    }

    #region Build Context Data Structures

    /// <summary>
    /// Shader / Material č\zp̑S̃ReLXg
    /// </summary>
    public sealed class ShaderBuildContext
    {
        /// <summary>
        /// Material o̓m[hij
        /// </summary>
        public readonly List<ShaderNodeComponentBase> OutputNodes =
            new List<ShaderNodeComponentBase>();

        /// <summary>
        /// lbg[N̑Sm[h
        /// </summary>
        public readonly Dictionary<ShaderNodeComponentBase, ShaderNodeInfo> AllNodes =
            new Dictionary<ShaderNodeComponentBase, ShaderNodeInfo>();
    }

    /// <summary>
    /// 1m[h̐ڑ
    /// </summary>
    public sealed class ShaderNodeInfo
    {
        public ShaderNodeComponentBase Node;

        /// <summary>
        /// InputPort  ڑm[h
        /// </summary>
        public Dictionary<string, ShaderNodeComponentBase> Inputs;

        /// <summary>
        /// OutputPort  ڑm[hꗗ
        /// </summary>
        public Dictionary<string, List<ShaderNodeComponentBase>> Outputs;
    }

    #endregion
}
