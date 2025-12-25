using System.Collections.Generic;
using UnityEngine;

namespace MayaImporter.DAG
{
    /// <summary>
    /// Maya renderLayer node.
    /// Holds render-specific grouping and overrides.
    /// </summary>
    [DisallowMultipleComponent]
    public class RenderLayerNode : MonoBehaviour
    {
        [Header("Layer Settings")]
        public string layerName;
        public bool isRenderable = true;

        [Header("Members")]
        [Tooltip("GameObjects that belong to this render layer")]
        public List<GameObject> members = new List<GameObject>();

        /// <summary>
        /// Adds a member GameObject to this render layer.
        /// </summary>
        public void AddMember(GameObject go)
        {
            if (go == null) return;
            if (!members.Contains(go))
            {
                members.Add(go);
            }
        }
    }
}
