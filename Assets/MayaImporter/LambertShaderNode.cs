// PATCH: ProductionImpl v6 (Unity-only, retention-first)
using UnityEngine;
using MayaImporter.Components;

namespace MayaImporter.Shader
{
    /// <summary>
    /// Lambert -> Unity Lit/Standard best-effort conversion + lossless metadata retention.
    /// </summary>
    [DisallowMultipleComponent]
    public class LambertShaderNode : MayaShader
    {
        public Color color = Color.white;

        public override Material BuildMaterial()
        {
            var shader = FindBestLitShader();
            var mat = new Material(shader);

            // Color
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);

            // Lambert: no specular by default
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0f);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0f);

            // Retain metadata for audit / reconstruction
            var meta = GetComponent<MayaMaterialMetadata>() ?? gameObject.AddComponent<MayaMaterialMetadata>();
            meta.mayaShaderType = "lambert";
            meta.baseColor = color;
            meta.baseWeight = 1f;
            meta.metallic = 0f;
            meta.smoothness = 0f;
            meta.opacity = 1f;

            ApplyMaterialToRenderer(mat);
            return mat;
        }

        private static UnityEngine.Shader FindBestLitShader()
        {
            var s = UnityEngine.Shader.Find("Universal Render Pipeline/Lit");
            if (s != null) return s;
            s = UnityEngine.Shader.Find("HDRP/Lit");
            if (s != null) return s;
            return UnityEngine.Shader.Find("Standard");
        }
    }
}
