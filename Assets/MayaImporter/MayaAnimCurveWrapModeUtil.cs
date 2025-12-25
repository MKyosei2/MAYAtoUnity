// Assets/MayaImporter/MayaAnimCurveWrapModeUtil.cs
// Maya animCurve infinity enum -> Unity WrapMode (best-effort).
// NOTE: Maya Linear extrapolation は Unity WrapMode で表現できないため ClampForever に寄せる。
// Evaluate() は MayaAnimCurveNodeComponent 側で線形外挿を実施する。

using UnityEngine;

namespace MayaImporter.Animation
{
    public static class MayaAnimCurveWrapModeUtil
    {
        public static WrapMode ToUnityWrapMode(int mayaInfinityEnum)
        {
            // Maya:
            // 0 Constant
            // 1 Linear
            // 3 Cycle
            // 4 CycleRelative
            // 5 Oscillate
            switch (mayaInfinityEnum)
            {
                case 3: // Cycle
                case 4: // CycleRelative (offsetは表現不可 -> Loop)
                    return WrapMode.Loop;

                case 5: // Oscillate
                    return WrapMode.PingPong;

                // 1 Linear -> WrapModeでは表現不可（Evaluateは線形外挿）
                case 1:
                case 0:
                default:
                    return WrapMode.ClampForever;
            }
        }
    }
}
