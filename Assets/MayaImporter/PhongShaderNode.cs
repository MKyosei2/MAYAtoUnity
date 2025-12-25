using UnityEngine;

namespace MayaImporter.Shader
{
    public class PhongShaderNode : MayaShader
    {
        public Color diffuse = Color.white;
        public float shininess = 0.7f;

        public override Material BuildMaterial()
        {
            var mat = new Material(UnityEngine.Shader.Find("Standard"));
            mat.color = diffuse;
            mat.SetFloat("_Glossiness", shininess);
            ApplyMaterialToRenderer(mat);
            return mat;
        }
    }
}
