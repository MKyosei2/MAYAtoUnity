using System.Globalization;
using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.DAG
{
    [MayaNodeType("pointLight")]
    [DisallowMultipleComponent]
    public sealed class PointLightNode : MayaNodeComponentBase
    {
        private bool _applied;

        public override void ApplyToUnity(MayaImportOptions options, MayaImportLog log)
        {
            if (_applied) return;
            _applied = true;

            if (options != null && !options.CreateUnityComponents) return;

            var l = GetComponent<global::UnityEngine.Light>() ?? gameObject.AddComponent<global::UnityEngine.Light>();
            l.type = global::UnityEngine.LightType.Point;

            l.color = ReadColor(new[] { ".color", "color", ".cl", "cl" }, global::UnityEngine.Color.white);
            l.intensity = global::UnityEngine.Mathf.Max(0f, ReadF(new[] { ".intensity", "intensity", ".i", "i" }, 1f));

            float range = ReadF(new[] { ".range", "range" }, -1f);
            if (range <= 0f)
            {
                float decay = ReadF(new[] { ".decayRate", "decayRate" }, -1f);
                range = decay >= 2f ? 10f : 30f;
            }
            l.range = global::UnityEngine.Mathf.Max(0.01f, range);

            l.shadows = global::UnityEngine.LightShadows.None;
        }

        private float ReadF(string[] keys, float def)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                if (TryGetAttr(keys[i], out var a) && a.Tokens != null && a.Tokens.Count > 0 &&
                    float.TryParse(a.Tokens[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
                    return f;
            }
            return def;
        }

        private global::UnityEngine.Color ReadColor(string[] keys, global::UnityEngine.Color def)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                if (!TryGetAttr(keys[i], out var a) || a.Tokens == null) continue;

                if (a.Tokens.Count >= 3 &&
                    float.TryParse(a.Tokens[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var r) &&
                    float.TryParse(a.Tokens[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var g) &&
                    float.TryParse(a.Tokens[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var b))
                    return new global::UnityEngine.Color(global::UnityEngine.Mathf.Clamp01(r), global::UnityEngine.Mathf.Clamp01(g), global::UnityEngine.Mathf.Clamp01(b), 1f);
            }
            return def;
        }
    }
}
