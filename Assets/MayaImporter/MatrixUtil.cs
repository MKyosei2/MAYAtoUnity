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

            // scale は列ベクトルの長さ
            var col0 = m.GetColumn(0);
            var col1 = m.GetColumn(1);
            var col2 = m.GetColumn(2);

            s = new Vector3(col0.magnitude, col1.magnitude, col2.magnitude);

            // 回転行列を正規化
            var rm = Matrix4x4.identity;
            if (s.x != 0f) rm.SetColumn(0, col0 / s.x);
            if (s.y != 0f) rm.SetColumn(1, col1 / s.y);
            if (s.z != 0f) rm.SetColumn(2, col2 / s.z);
            rm.m33 = 1f;

            r = rm.rotation;
        }

        public static Matrix4x4 ComposeTRS(Vector3 t, Quaternion r, Vector3 s)
            => Matrix4x4.TRS(t, r, s);
    }
}
