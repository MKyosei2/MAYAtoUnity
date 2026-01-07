// PATCH: ProductionImpl v6 (Unity-only, retention-first)
// Assets/MayaImporter/MayaAnimCurveWrapModeUtil.cs
// Maya animCurve infinity enum -> Unity WrapMode (best-effort).
// NOTE: Maya Linear extrapolation �� Unity WrapMode �ŕ\���ł��Ȃ����� ClampForever �Ɋ񂹂�B
// Evaluate() �� MayaAnimCurveNodeComponent ���Ő��`�O�}�����{����B

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
                case 4: // CycleRelative (offset�͕\���s�� -> Loop)
                    return WrapMode.Loop;

                case 5: // Oscillate
                    return WrapMode.PingPong;

                // 1 Linear -> WrapMode�ł͕\���s�iEvaluate�͐��`�O�}�j
                case 1:
                case 0:
                default:
                    return WrapMode.ClampForever;
            }
        }
    }
}
