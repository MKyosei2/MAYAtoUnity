using System.Collections.Generic;
using UnityEngine;

namespace MayaImporter.DAG
{
    /// <summary>
    /// Maya displayLayer node.
    /// Holds visibility and membership information.
    /// </summary>
    [DisallowMultipleComponent]
    public class DisplayLayerNode : MonoBehaviour
    {
        [Header("Layer Settings")]
        public string layerName;
        public bool visibility = true;
        public Color layerColor = Color.white;

        [Header("Members")]
        [Tooltip("GameObjects that belong to this display layer")]
        public List<GameObject> members = new List<GameObject>();

        /// <summary>
        /// Adds a member GameObject to this layer.
        /// </summary>
        public void AddMember(GameObject go)
        {
            if (go == null) return;
            if (!members.Contains(go))
            {
                members.Add(go);
            }
        }

        /// <summary>
        /// Apply visibility to all members (optional helper).
        /// </summary>
        public void ApplyVisibility()
        {
            foreach (var go in members)
            {
                if (go != null)
                {
                    go.SetActive(visibility);
                }
            }
        }
    }
}
