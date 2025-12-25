using UnityEngine;

namespace MayaImporter.Animation
{
    /// <summary>
    /// Maya spline IK handle node.
    /// </summary>
    [DisallowMultipleComponent]
    public class SplineIKHandleNode : MonoBehaviour
    {
        [Header("Spline IK")]
        public Transform startJoint;
        public Transform endJoint;
        public Transform curve;

        public int subdivisions = 5;

        public void Initialize(
            Transform start,
            Transform end,
            Transform curveTransform,
            int subdiv)
        {
            startJoint = start;
            endJoint = end;
            curve = curveTransform;
            subdivisions = subdiv;
        }
    }
}
