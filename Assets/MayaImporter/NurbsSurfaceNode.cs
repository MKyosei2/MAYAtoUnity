using UnityEngine;

namespace MayaImporter.Geometry
{
    /// <summary>
    /// Maya NURBS surface node.
    /// Unity has no native NURBS surface concept, so data is stored as-is.
    /// </summary>
    [DisallowMultipleComponent]
    public class NurbsSurfaceNode : MonoBehaviour
    {
        [Header("Surface Definition")]
        public Vector3[,] controlPoints;

        [Header("Knot Vectors")]
        public float[] uKnots;
        public float[] vKnots;

        [Header("Degree")]
        public int uDegree;
        public int vDegree;

        [Header("Flags")]
        public bool uClosed;
        public bool vClosed;

        public void Initialize(
            Vector3[,] points,
            float[] uKnotVector,
            float[] vKnotVector,
            int uDeg,
            int vDeg,
            bool uIsClosed,
            bool vIsClosed)
        {
            controlPoints = points;
            uKnots = uKnotVector;
            vKnots = vKnotVector;
            uDegree = uDeg;
            vDegree = vDeg;
            uClosed = uIsClosed;
            vClosed = vIsClosed;
        }
    }
}
