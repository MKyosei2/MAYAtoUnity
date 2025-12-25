using UnityEngine;

namespace MayaImporter.Animation
{
    /// <summary>
    /// Unity-side “reconstructable” curve container.
    /// MayaAnimCurveNodeComponent が自動生成/更新する。
    /// </summary>
    [DisallowMultipleComponent]
    public class AnimCurveNode : AnimCurveBase
    {
        [Header("Target (optional)")]
        public string targetPath;
        public string propertyName;

        /// <summary>
        /// Applies this curve to a Unity AnimationClip (optional workflow).
        /// MayaImporter の runtime evaluation 方式では必須ではない。
        /// </summary>
        public void ApplyToClip(AnimationClip clip)
        {
            if (clip == null) return;
            if (string.IsNullOrEmpty(propertyName)) return;

            var curve = ToUnityCurve();
            if (curve == null) return;

            // NOTE:
            // binding type / property mapping はプロジェクト方針次第。
            // ここは最低限 Transform への SetCurve を提供する。
            clip.SetCurve(targetPath ?? string.Empty, typeof(Transform), propertyName, curve);
        }
    }
}
