// MAYAIMPORTER_PATCH_V4: mb provenance/evidence + audit determinism (generated 2026-01-05)
using System.Collections.Generic;
using UnityEngine;

namespace MayaImporter.Animation
{
    /// <summary>
    /// Maya Animation m[hQ Unity AnimationClip 𐶐NX
    /// Maya API ˑœ삷݌v
    /// </summary>
    public class MayaAnimationClipGenerator
    {
        /// <summary>
        /// Mayap[XꂽL[t[iȈՕ\j
        /// </summary>
        public class MayaKeyframe
        {
            public float time;
            public float value;
        }

        /// <summary>
        /// MayaAj[VJ[u
        /// </summary>
        public class MayaAnimCurve
        {
            public string unityPropertyPath;
            public List<MayaKeyframe> keys = new List<MayaKeyframe>();
        }

        /// <summary>
        /// MayaAj[V񂩂Unity AnimationClip𐶐
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
