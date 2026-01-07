// PATCH: ProductionImpl v6 (Unity-only, retention-first)
using UnityEngine;
using MayaImporter.Components;

namespace MayaImporter.Shader
{
    /// <summary>
    /// StingrayPBS -> Unity Lit/Standard best-effort conversion + lossless metadata retention.
    /// </summary>
    [DisallowMultipleComponent]
    public class StingrayPBSShaderNode : MayaShader
    {
        public Color baseColor = Color.white;
        [Range(0f, 1f)] public float metallic = 0.0f;
        [Range(0f, 1f)] public float smoothness = 0.5f;

        public Texture2D baseColorTexture;
        public Texture2D normalTexture;

        public override Material BuildMaterial()
        {
            var shader = FindBestLitShader();
            var mat = new Material(shader);

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", baseColor);

            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", Mathf.Clamp01(metallic));
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", Mathf.Clamp01(smoothness));
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", Mathf.Clamp01(smoothness));

            // BaseColor texture
            if (baseColorTexture != null)
            {
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", baseColorTexture);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", baseColorTexture);
            }

            // Normal map
            if (normalTexture != null)
            {
                if (mat.HasProperty("_BumpMap"))
                {
                    mat.EnableKeyword("_NORMALMAP");
                    mat.SetTexture("_BumpMap", normalTexture);
                }
            }

            // Retain metadata
            var meta = GetComponent<MayaMaterialMetadata>() ?? gameObject.AddComponent<MayaMaterialMetadata>();
            meta.mayaShaderType = "stingrayPBS";
            meta.baseColor = baseColor;
            meta.baseWeight = 1f;
            meta.metallic = Mathf.Clamp01(metallic);
            meta.smoothness = Mathf.Clamp01(smoothness);
            meta.baseColorTextureNode = baseColorTexture != null ? baseColorTexture.name : meta.baseColorTextureNode;
            meta.normalTextureNode = normalTexture != null ? normalTexture.name : meta.normalTextureNode;
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
