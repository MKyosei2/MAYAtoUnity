using UnityEngine;

namespace MayaImporter.Core
{
    /// <summary>
    /// Maya(RH) -> Unity(LH) の座標変換ユーティリティ。
    /// 注意: 「完全な物理的等価」を保証するものではなく、ツール内で一貫した変換を提供する。
    /// </summary>
    internal static class MayaToUnityConversion
    {
        /// <summary>
        /// Position: MirrorZ の場合 (x, y, -z)
        /// </summary>
        public static Vector3 ConvertPosition(Vector3 mayaPos, CoordinateConversion conversion)
        {
            return conversion switch
            {
                CoordinateConversion.MayaToUnity_MirrorZ => new Vector3(mayaPos.x, mayaPos.y, -mayaPos.z),
                _ => mayaPos
            };
        }

        /// <summary>
        /// Direction/Vector: MirrorZ の場合 (x, y, -z)
        /// </summary>
        public static Vector3 ConvertDirection(Vector3 mayaDir, CoordinateConversion conversion)
        {
            return conversion switch
            {
                CoordinateConversion.MayaToUnity_MirrorZ => new Vector3(mayaDir.x, mayaDir.y, -mayaDir.z),
                _ => mayaDir
            };
        }

        /// <summary>
        /// Euler(deg) の簡易変換。
        /// MirrorZ の場合、経験則として (x, -y, -z)
        /// </summary>
        public static Vector3 ConvertEulerDegrees(Vector3 mayaEulerDeg, CoordinateConversion conversion)
        {
            return conversion switch
            {
                CoordinateConversion.MayaToUnity_MirrorZ => new Vector3(mayaEulerDeg.x, -mayaEulerDeg.y, -mayaEulerDeg.z),
                _ => mayaEulerDeg
            };
        }

        /// <summary>
        /// Quaternion を行列経由で変換（最も一貫性が出る）。
        /// </summary>
        public static Quaternion ConvertQuaternion(Quaternion mayaQ, CoordinateConversion conversion)
        {
            var m = Matrix4x4.Rotate(mayaQ);
            var mu = ConvertMatrix(m, conversion);
            return mu.rotation;
        }

        /// <summary>
        /// Matrix4x4 を変換。
        /// MirrorZ の場合: C * M * C, C = diag(1,1,-1,1)
        /// </summary>
        public static Matrix4x4 ConvertMatrix(Matrix4x4 mayaM, CoordinateConversion conversion)
        {
            return conversion switch
            {
                CoordinateConversion.MayaToUnity_MirrorZ => MirrorZMatrix(mayaM),
                _ => mayaM
            };
        }

        /// <summary>
        /// ローカルTRSを合成して Maya->Unity 変換後の行列も返す（監査/デバッグ向け）
        /// </summary>
        public static void BuildLocalMatrices(
            Vector3 mayaT,
            Quaternion mayaR,
            Vector3 mayaS,
            CoordinateConversion conversion,
            out Matrix4x4 localMaya,
            out Matrix4x4 localUnity)
        {
            localMaya = Matrix4x4.TRS(mayaT, mayaR, mayaS);
            localUnity = ConvertMatrix(localMaya, conversion);
        }

        private static Matrix4x4 MirrorZMatrix(Matrix4x4 m)
        {
            // C = diag(1,1,-1,1). For reflections, C^-1 = C.
            var C = Matrix4x4.identity;
            C.m22 = -1f;
            return C * m * C;
        }
    }
}
