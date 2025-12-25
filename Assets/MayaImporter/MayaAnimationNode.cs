using System.Collections.Generic;
using UnityEngine;
using MayaImporter.Animation;

namespace MayaImporter.Animation
{
    /// <summary>
    /// Maya Animation ノード1つに対応するUnity側クラス
    /// AnimationClipGeneratorへデータを渡す責務を持つ
    /// </summary>
    public class MayaAnimationNode : MonoBehaviour
    {
        [Header("Maya Animation Node")]
        public string mayaNodeName;
        public string targetPropertyPath;

        [System.Serializable]
        public class Key
        {
            public float time;
            public float value;
        }

        public List<Key> keys = new List<Key>();

        /// <summary>
        /// 自身のデータを AnimationClipGenerator 用カーブへ変換
        /// </summary>
        public MayaAnimationClipGenerator.MayaAnimCurve ToAnimCurve()
        {
            var curve = new MayaAnimationClipGenerator.MayaAnimCurve
            {
                unityPropertyPath = targetPropertyPath
            };

            foreach (var key in keys)
            {
                curve.keys.Add(new MayaAnimationClipGenerator.MayaKeyframe
                {
                    time = key.time,
                    value = key.value
                });
            }

            return curve;
        }
    }
}
