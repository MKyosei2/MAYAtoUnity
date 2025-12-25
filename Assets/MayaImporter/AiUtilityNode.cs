using UnityEngine;

namespace MayaImporter.Shader
{
    public class AiUtilityNode : MayaShader
    {
        public Color color = Color.white;

        public override Material BuildMaterial()
        {
            var mat = new Material(UnityEngine.Shader.Find("Standard"));
            mat.color = color;
            ApplyMaterialToRenderer(mat);
            return mat;
        }
    }
}
