using System;
using System.Collections.Generic;
using UnityEngine;

namespace MayaImporter.Core
{
    /// <summary>
    /// Phase C-7:
    /// g100%•Û + Œœ”O“_‚Ì‰Â‹‰» + ƒxƒCƒNŒ‹‰Ê + ÄŒ»«ƒnƒbƒVƒ…h ‚ğ1‚Â‚ÉW–ñ‚·‚éB
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MayaAnimationAuditComponent : MonoBehaviour
    {
        [Header("Detected (from MayaAnimationManager)")]
        public bool hasAnimCurves;
        public bool hasConstraints;
        public bool hasExpressions;

        [Header("Bake Result")]
        public bool baked;
        public bool bakeFailed;
        public string bakedClipName;
        public float bakedStartFrame;
        public float bakedEndFrame;
        public float bakedFps;
        public int bakedCurveCount;
        public int bakedKeyCount;

        [Header("Determinism (Stable Clip Hash)")]
        public string clipStableHash;
        public string previousClipStableHash;
        public bool determinismOk;

        [Header("Limitations (Reporter Output)")]
        [SerializeField] private AnimRow[] rows = Array.Empty<AnimRow>();

        [Serializable]
        public struct AnimRow
        {
            public string scope;
            public string issueKey;
            public string severity;
            [TextArea] public string details;
        }

        public void SetFrom(List<MayaAnimationEvaluationLimitationsReporter.AnimLimitationRow> src)
        {
            if (src == null || src.Count == 0)
            {
                rows = Array.Empty<AnimRow>();
                return;
            }

            rows = new AnimRow[src.Count];
            for (int i = 0; i < src.Count; i++)
            {
                rows[i] = new AnimRow
                {
                    scope = src[i].Scope,
                    issueKey = src[i].IssueKey,
                    severity = src[i].Severity,
                    details = src[i].Details
                };
            }
        }

        public IReadOnlyList<AnimRow> GetRows() => rows;

        public void MarkNoBake()
        {
            baked = false;
            bakeFailed = false;
            bakedClipName = "";
            bakedCurveCount = 0;
            bakedKeyCount = 0;
        }

        public void MarkBakeFailed(float start, float end, float fps)
        {
            baked = false;
            bakeFailed = true;
            bakedStartFrame = start;
            bakedEndFrame = end;
            bakedFps = fps;
            bakedClipName = "";
            bakedCurveCount = 0;
            bakedKeyCount = 0;
        }

        public void SetBakeResult(float start, float end, float fps, string clipName, int curveCount, int keyCount, string stableHash)
        {
            // C-5: g‘O‰ñ‚Ìhashh ‚Æ”äŠr‚µ‚Ä determinism ‚ğ–¾¦
            previousClipStableHash = clipStableHash;

            baked = true;
            bakeFailed = false;
            bakedStartFrame = start;
            bakedEndFrame = end;
            bakedFps = fps;
            bakedClipName = clipName;
            bakedCurveCount = curveCount;
            bakedKeyCount = keyCount;

            clipStableHash = stableHash;

            if (string.IsNullOrEmpty(previousClipStableHash))
            {
                determinismOk = true; // ‰‰ñ‚Í”äŠr•s”\‚È‚Ì‚ÅOKˆµ‚¢
            }
            else
            {
                determinismOk = string.Equals(previousClipStableHash, clipStableHash, StringComparison.Ordinal);
            }
        }
    }
}
