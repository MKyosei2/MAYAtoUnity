using UnityEngine;

namespace MayaImporter.Shader
{
    public class StingrayPBSShaderNode : MayaShader
    {
        public Color baseColor = Color.white;
        public float metallic = 0.0f;
        public float smoothness = 0.5f;
        public Texture2D baseColorTexture;
        public Texture2D normalTexture;

        public override Material BuildMaterial()
        {
            var material = new Material(UnityEngine.Shader.Find("Standard"));
            material.color = baseColor;
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Glossiness", smoothness);

            if (baseColorTexture != null)
                material.SetTexture("_MainTex", baseColorTexture);

            if (normalTexture != null)
            {
                material.EnableKeyword("_NORMALMAP");
                material.SetTexture("_BumpMap", normalTexture);
            }

            ApplyMaterialToRenderer(material);
            return material;
        }
    }
}
