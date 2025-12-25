using System.Collections.Generic;
using UnityEngine;

namespace MayaImporter.Shader
{
    /// <summary>
    /// ShaderBuildContext から Unity Material を再構築するビルダー。
    ///
    /// ※ クラス名に ShaderGraph とあるが、
    ///   この段階では ShaderGraph アセットを生成しない。
    /// </summary>
    public static class ShaderGraphBuilder
    {
        public static List<UnityEngine.Material> BuildMaterials(
            ShaderBuildContext context,
            Transform parent = null)
        {
            var materials = new List<UnityEngine.Material>();

            if (context == null || context.OutputNodes.Count == 0)
                return materials;

            foreach (var outputNode in context.OutputNodes)
            {
                var material = BuildSingleMaterial(outputNode, context, parent);
                if (material != null)
                    materials.Add(material);
            }

            return materials;
        }

        private static UnityEngine.Material BuildSingleMaterial(
            ShaderNodeComponentBase outputNode,
            ShaderBuildContext context,
            Transform parent)
        {
            var shader = SelectShader(outputNode);
            if (shader == null)
            {
                Debug.LogWarning(
                    $"[ShaderGraphBuilder] Shader not found for node: {outputNode.MayaNodeType}");
                shader = UnityEngine.Shader.Find("Standard");
            }

            var material = new UnityEngine.Material(shader)
            {
                name = $"MayaMaterial_{outputNode.name}"
            };

            var go = new GameObject(material.name);
            if (parent != null)
                go.transform.SetParent(parent, false);

            var holder = go.AddComponent<ShaderMaterialHolder>();
            holder.Material = material;
            holder.RootNode = outputNode;
            holder.Context = context;

            ApplyBasicMaterialProperties(material, outputNode, context);

            return material;
        }

        private static UnityEngine.Shader SelectShader(ShaderNodeComponentBase node)
        {
            switch (node.MayaNodeType)
            {
                case "lambert":
                    return UnityEngine.Shader.Find("Standard");

                case "phong":
                case "phongE":
                case "blinn":
                    return UnityEngine.Shader.Find("Standard (Specular setup)");

                case "surfaceShader":
                    return UnityEngine.Shader.Find("Unlit/Color");

                case "aiStandardSurface":
                    return UnityEngine.Shader.Find("Standard");
            }

            return null;
        }

        private static void ApplyBasicMaterialProperties(
            UnityEngine.Material material,
            ShaderNodeComponentBase node,
            ShaderBuildContext context)
        {
            if (node.Attributes != null &&
                node.Attributes.TryGetValue("color", out var colorObj))
            {
                if (TryConvertColor(colorObj, out var color) &&
                    material.HasProperty("_Color"))
                {
                    material.SetColor("_Color", color);
                }
            }

            if (node.Attributes != null &&
                node.Attributes.TryGetValue("incandescence", out var emisObj))
            {
                if (TryConvertColor(emisObj, out var emission) &&
                    material.HasProperty("_EmissionColor"))
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", emission);
                }
            }
        }

        private static bool TryConvertColor(object obj, out UnityEngine.Color color)
        {
            color = UnityEngine.Color.white;

            if (obj is UnityEngine.Color c)
            {
                color = c;
                return true;
            }

            if (obj is Vector3 v3)
            {
                color = new UnityEngine.Color(v3.x, v3.y, v3.z, 1f);
                return true;
            }

            if (obj is Vector4 v4)
            {
                color = new UnityEngine.Color(v4.x, v4.y, v4.z, v4.w);
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// 生成された Material と Maya Shader ネットワークを紐づける保持用 Component
    /// </summary>
    public sealed class ShaderMaterialHolder : MonoBehaviour
    {
        public UnityEngine.Material Material;
        public ShaderNodeComponentBase RootNode;
        public ShaderBuildContext Context;
    }
}
