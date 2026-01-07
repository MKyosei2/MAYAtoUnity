// PATCH: ProductionImpl v6 (Unity-only, retention-first)
using UnityEngine;
using MayaImporter.Components;

namespace MayaImporter.Shader
{
    /// <summary>
    /// Blinn -> Unity Lit/Standard best-effort conversion + lossless metadata retention.
    /// </summary>
    [DisallowMultipleComponent]
    public class BlinnShaderNode : MayaShader
    {
        public Color diffuseColor = Color.gray;
        [Range(0f, 1f)] public float specular = 0.5f;

        public override Material BuildMaterial()
        {
            var shader = FindBestLitShader();
            var mat = new Material(shader);

            // Base color
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", diffuseColor);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", diffuseColor);

            // Treat specular as smoothness (best-effort)
            float smooth = Mathf.Clamp01(specular);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smooth);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smooth);

            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);

            // Retain metadata
            var meta = GetComponent<MayaMaterialMetadata>() ?? gameObject.AddComponent<MayaMaterialMetadata>();
            meta.mayaShaderType = "blinn";
            meta.baseColor = diffuseColor;
            meta.baseWeight = 1f;
            meta.metallic = 0f;
            meta.smoothness = smooth;
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
