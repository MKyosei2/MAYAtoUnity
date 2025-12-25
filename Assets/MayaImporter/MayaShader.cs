using UnityEngine;

namespace MayaImporter.Shader
{
    /// <summary>
    /// MayaのShaderノード1つに対応するUnity側基底クラス
    /// Maya未インストール環境で .ma / .mb の解析結果から
    /// Unity Material を再構築するための責務を持つ
    /// </summary>
    public abstract class MayaShader : MonoBehaviour
    {
        [Header("Maya Shader Common")]
        public string mayaNodeName;
        public string mayaShaderType;

        /// <summary>
        /// Mayaノード情報を元にUnity Materialを生成・設定する
        /// </summary>
        public abstract Material BuildMaterial();

        /// <summary>
        /// Utility：RendererにMaterialを適用
        /// </summary>
        protected void ApplyMaterialToRenderer(Material material)
        {
            var renderer = GetComponent<Renderer>();
            if (renderer != null && material != null)
            {
                renderer.sharedMaterial = material;
            }
        }
    }
}
