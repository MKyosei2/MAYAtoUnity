using System.Collections.Generic;
using UnityEngine;

namespace MayaImporter.Utils
{
    public static class MatrixUtil
    {
        /// <summary>
        /// tokens[start..start+15] を Matrix4x4 に詰める（row-major 想定）。
        /// Maya→Unity の座標変換は Core 側の MayaToUnityConversion に任せる方針。
        /// </summary>
        public static bool TryParseMatrix4x4(IReadOnlyList<string> tokens, int start, out Matrix4x4 m)
        {
            m = Matrix4x4.identity;
            if (tokens == null) return false;
            if (start < 0) return false;
            if (tokens.Count < start + 16) return false;

            float[] v = new float[16];
            for (int i = 0; i < 16; i++)
            {
                if (!MathUtil.TryParseFloat(tokens[start + i], out v[i]))
                    return false;
            }

            // Unity の Matrix4x4 は m[row,col] 的に m00..m33
            m.m00 = v[0]; m.m01 = v[1]; m.m02 = v[2]; m.m03 = v[3];
            m.m10 = v[4]; m.m11 = v[5]; m.m12 = v[6]; m.m13 = v[7];
            m.m20 = v[8]; m.m21 = v[9]; m.m22 = v[10]; m.m23 = v[11];
            m.m30 = v[12]; m.m31 = v[13]; m.m32 = v[14]; m.m33 = v[15];
            return true;
        }

        public static void DecomposeTRS(in Matrix4x4 m, out Vector3 t, out Quaternion r, out Vector3 s)
{
    t = m.GetColumn(3);

    // Scale = column magnitudes (preserve possible reflection by carrying sign on one axis)
    var col0 = (Vector3)m.GetColumn(0);
    var col1 = (Vector3)m.GetColumn(1);
    var col2 = (Vector3)m.GetColumn(2);

    float sx = col0.magnitude;
    float sy = col1.magnitude;
    float sz = col2.magnitude;

    // Avoid division by zero
    var n0 = (sx > 1e-12f) ? (col0 / sx) : Vector3.right;
    var n1 = (sy > 1e-12f) ? (col1 / sy) : Vector3.up;
    var n2 = (sz > 1e-12f) ? (col2 / sz) : Vector3.forward;

    // Detect handedness (reflection) via determinant sign
    float det = Vector3.Dot(Vector3.Cross(n0, n1), n2);

    // If reflected (det < 0), flip the axis with the largest scale to preserve the matrix sign
    if (det < 0f)
    {
        if (sx >= sy && sx >= sz) { sx = -sx; n0 = -n0; }
        else if (sy >= sx && sy >= sz) { sy = -sy; n1 = -n1; }
        else { sz = -sz; n2 = -n2; }
    }

    s = new Vector3(sx, sy, sz);

    // Rotation matrix from normalized columns
    var rm = Matrix4x4.identity;
    rm.SetColumn(0, new Vector4(n0.x, n0.y, n0.z, 0f));
    rm.SetColumn(1, new Vector4(n1.x, n1.y, n1.z, 0f));
    rm.SetColumn(2, new Vector4(n2.x, n2.y, n2.z, 0f));
    rm.m33 = 1f;

    r = rm.rotation;
}


        public static Matrix4x4 ComposeTRS(Vector3 t, Quaternion r, Vector3 s)
            => Matrix4x4.TRS(t, r, s);
    }
}
