using UnityEngine;

namespace MayaImporter.DAG
{
    /// <summary>
    /// Maya sceneSettings node.
    /// Holds global scene-level metadata not covered by Time or Unit.
    /// </summary>
    [DisallowMultipleComponent]
    public class SceneSettingsNode : MonoBehaviour
    {
        [Header("Scene Identification")]
        public string sceneName;
        public string author;
        public string comment;

        [Header("Up Axis")]
        public string upAxis = "Y"; // Y or Z

        [Header("Coordinate System")]
        public bool rightHanded = true;

        /// <summary>
        /// Initialize scene settings.
        /// </summary>
        public void Initialize(
            string sceneNameValue,
            string authorValue,
            string commentValue,
            string upAxisValue,
            bool isRightHanded)
        {
            sceneName = sceneNameValue;
            author = authorValue;
            comment = commentValue;
            upAxis = upAxisValue;
            rightHanded = isRightHanded;
        }
    }
}
