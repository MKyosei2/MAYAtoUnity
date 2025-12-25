using UnityEngine;

namespace MayaImporter.Shader
{
    public class BlinnShaderNode : MayaShader
    {
        public Color diffuseColor = Color.gray;
        public float specular = 0.5f;

        public override Material BuildMaterial()
        {
            var mat = new Material(UnityEngine.Shader.Find("Standard"));
            mat.color = diffuseColor;
            mat.SetFloat("_Glossiness", specular);
            ApplyMaterialToRenderer(mat);
            return mat;
        }
    }
}
