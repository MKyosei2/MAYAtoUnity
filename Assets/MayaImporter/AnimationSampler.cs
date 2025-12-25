using System.Collections.Generic;
using UnityEngine;

namespace MayaImporter.Animation
{
    /// <summary>
    /// Collects animation curves and builds Unity AnimationClips.
    /// </summary>
    [DisallowMultipleComponent]
    public class AnimationSampler : MonoBehaviour
    {
        [Header("Clip Settings")]
        public string clipName = "MayaAnimation";
        public float frameRate = 30.0f;

        [Header("Curves")]
        public List<AnimCurveNode> curves = new List<AnimCurveNode>();

        public AnimationClip BuildClip()
        {
            AnimationClip clip = new AnimationClip
            {
                name = clipName,
                frameRate = frameRate
            };

            foreach (var curve in curves)
            {
                if (curve != null)
                {
                    curve.ApplyToClip(clip);
                }
            }

            return clip;
        }

        public void CollectCurves()
        {
            curves.Clear();
            curves.AddRange(GetComponentsInChildren<AnimCurveNode>());
        }
    }
}
