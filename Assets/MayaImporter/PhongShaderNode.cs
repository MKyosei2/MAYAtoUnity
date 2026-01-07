// PATCH: ProductionImpl v6 (Unity-only, retention-first)
using UnityEngine;
using MayaImporter.Components;

namespace MayaImporter.Shader
{
    /// <summary>
    /// Phong -> Unity Lit/Standard best-effort conversion + lossless metadata retention.
    /// </summary>
    [DisallowMultipleComponent]
    public class PhongShaderNode : MayaShader
    {
        public Color diffuse = Color.white;
        [Range(0f, 1f)] public float shininess = 0.7f;

        public override Material BuildMaterial()
        {
            var shader = FindBestLitShader();
            var mat = new Material(shader);

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", diffuse);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", diffuse);

            float smooth = Mathf.Clamp01(shininess);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smooth);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smooth);

            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);

            var meta = GetComponent<MayaMaterialMetadata>() ?? gameObject.AddComponent<MayaMaterialMetadata>();
            meta.mayaShaderType = "phong";
            meta.baseColor = diffuse;
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
