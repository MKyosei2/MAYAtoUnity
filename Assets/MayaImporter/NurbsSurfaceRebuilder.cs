using UnityEngine;

namespace MayaImporter.Geometry
{
    /// <summary>
    /// Optional helper to approximate a NURBS surface into a Unity Mesh.
    /// Original NURBS data remains intact.
    /// </summary>
    [DisallowMultipleComponent]
    public class NurbsSurfaceRebuilder : MonoBehaviour
    {
        public NurbsSurfaceNode sourceSurface;
        public int uResolution = 8;
        public int vResolution = 8;

        public Mesh BuildApproximation()
        {
            if (sourceSurface == null || sourceSurface.controlPoints == null)
                return null;

            // Placeholder approximation: control points only
            Mesh mesh = new Mesh();
            mesh.name = gameObject.name + "_NurbsApprox";

            int uCount = sourceSurface.controlPoints.GetLength(0);
            int vCount = sourceSurface.controlPoints.GetLength(1);

            Vector3[] vertices = new Vector3[uCount * vCount];
            int index = 0;

            for (int u = 0; u < uCount; u++)
            {
                for (int v = 0; v < vCount; v++)
                {
                    vertices[index++] = sourceSurface.controlPoints[u, v];
                }
            }

            mesh.vertices = vertices;
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();

            return mesh;
        }
    }
}
