using UnityEngine;

namespace MayaImporter.Geometry
{
    /// <summary>
    /// Maya NURBS curve node.
    /// Stored as-is (Unity has no native NURBS concept).
    /// </summary>
    [DisallowMultipleComponent]
    public class NurbsCurveNode : MonoBehaviour
    {
        [Header("Curve Data")]
        public Vector3[] controlPoints;
        public float[] knots;
        public int degree;
        public bool closed;

        public void Initialize(
            Vector3[] points,
            float[] knotVector,
            int curveDegree,
            bool isClosed)
        {
            controlPoints = points;
            knots = knotVector;
            degree = curveDegree;
            closed = isClosed;
        }
    }
}
