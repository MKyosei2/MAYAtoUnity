using UnityEngine;

namespace MayaImporter.Geometry
{
    /// <summary>
    /// Optional helper to rebuild a NURBS curve into a polyline approximation.
    /// Does NOT destroy original NURBS data.
    /// </summary>
    [DisallowMultipleComponent]
    public class NurbsCurveRebuilder : MonoBehaviour
    {
        public NurbsCurveNode sourceCurve;
        public int sampleCount = 32;

        public LineRenderer BuildApproximation()
        {
            if (sourceCurve == null || sourceCurve.controlPoints == null)
                return null;

            var lr = gameObject.AddComponent<LineRenderer>();
            lr.positionCount = sourceCurve.controlPoints.Length;
            lr.useWorldSpace = false;

            for (int i = 0; i < sourceCurve.controlPoints.Length; i++)
            {
                lr.SetPosition(i, sourceCurve.controlPoints[i]);
            }

            return lr;
        }
    }
}
