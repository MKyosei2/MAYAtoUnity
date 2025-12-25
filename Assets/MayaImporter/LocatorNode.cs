using UnityEngine;

namespace MayaImporter.DAG
{
    /// <summary>
    /// Maya locator node.
    /// Represents a non-renderable reference point in space.
    /// </summary>
    [DisallowMultipleComponent]
    public class LocatorNode : MonoBehaviour
    {
        [Header("Locator Settings")]
        public float localScale = 1.0f;
        public Color locatorColor = Color.yellow;

        /// <summary>
        /// Initialize locator parameters.
        /// </summary>
        public void Initialize(float scale, Color color)
        {
            localScale = scale;
            locatorColor = color;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = locatorColor;
            Gizmos.DrawWireCube(transform.position, Vector3.one * localScale);
        }
#endif
    }
}
