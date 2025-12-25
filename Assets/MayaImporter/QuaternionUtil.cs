using UnityEngine;

namespace MayaImporter.Utils
{
    /// <summary>
    /// Maya の rotationOrder（0..5）を考慮して Quaternion を生成する。
    /// Maya rotationOrder: 0=XYZ,1=YZX,2=ZXY,3=XZY,4=YXZ,5=ZYX
    /// </summary>
    public static class QuaternionUtil
    {
        public static Quaternion FromEulerDegrees(Vector3 eulerDeg, int mayaRotationOrder)
        {
            // Maya の各軸回転はローカルで積まれる。Unity の Quaternion は乗算順に注意。
            // ここでは「指定順で AngleAxis を掛ける」実装に統一する。
            Quaternion qx = Quaternion.AngleAxis(eulerDeg.x, Vector3.right);
            Quaternion qy = Quaternion.AngleAxis(eulerDeg.y, Vector3.up);
            Quaternion qz = Quaternion.AngleAxis(eulerDeg.z, Vector3.forward);

            switch (mayaRotationOrder)
            {
                case 0: // XYZ
                    return qx * qy * qz;
                case 1: // YZX
                    return qy * qz * qx;
                case 2: // ZXY
                    return qz * qx * qy;
                case 3: // XZY
                    return qx * qz * qy;
                case 4: // YXZ
                    return qy * qx * qz;
                case 5: // ZYX
                    return qz * qy * qx;
                default:
                    return qx * qy * qz;
            }
        }
    }
}
