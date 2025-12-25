using System.Collections.Generic;
using UnityEngine;

namespace MayaImporter.Animation
{
    /// <summary>
    /// Maya Animation ノード群から Unity AnimationClip を生成するクラス
    /// Maya API 非依存で動作する設計
    /// </summary>
    public class MayaAnimationClipGenerator
    {
        /// <summary>
        /// Mayaからパースされたキーフレーム情報（簡易表現）
        /// </summary>
        public class MayaKeyframe
        {
            public float time;
            public float value;
        }

        /// <summary>
        /// Mayaアニメーションカーブ情報
        /// </summary>
        public class MayaAnimCurve
        {
            public string unityPropertyPath;
            public List<MayaKeyframe> keys = new List<MayaKeyframe>();
        }

        /// <summary>
        /// Mayaアニメーション情報からUnity AnimationClipを生成
        /// </summary>
        public AnimationClip GenerateClip(
            string clipName,
            List<MayaAnimCurve> curves,
            float frameRate = 30.0f)
        {
            var clip = new AnimationClip
            {
                name = clipName,
                frameRate = frameRate
            };

            foreach (var curve in curves)
            {
                var unityCurve = new AnimationCurve();

                foreach (var key in curve.keys)
                {
                    unityCurve.AddKey(key.time, key.value);
                }

                clip.SetCurve(
                    "",
                    typeof(Transform),
                    curve.unityPropertyPath,
                    unityCurve
                );
            }

            clip.EnsureQuaternionContinuity();
            return clip;
        }
    }
}
