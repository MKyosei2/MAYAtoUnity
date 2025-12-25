using UnityEngine;

namespace MayaImporter.Geometry
{
    /// <summary>
    /// Represents a joint influence for skinning.
    /// </summary>
    [DisallowMultipleComponent]
    public class JointInfluenceNode : MonoBehaviour
    {
        [Header("Joint")]
        public string jointName;

        [Header("Weight")]
        [Range(0f, 1f)]
        public float influenceWeight;

        /// <summary>
        /// Initialize joint influence.
        /// </summary>
        public void Initialize(string name, float weight)
        {
            jointName = name;
            influenceWeight = weight;
        }
    }
}
