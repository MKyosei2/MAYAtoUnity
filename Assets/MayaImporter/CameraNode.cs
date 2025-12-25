using System.Globalization;
using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.DAG
{
    [MayaNodeType("camera")]
    [DisallowMultipleComponent]
    public sealed class CameraNode : MayaNodeComponentBase
    {
        private bool _applied;

        public override void ApplyToUnity(MayaImportOptions options, MayaImportLog log)
        {
            if (_applied) return;
            _applied = true;

            if (options != null && !options.CreateUnityComponents) return;

            var cam = GetComponent<global::UnityEngine.Camera>() ?? gameObject.AddComponent<global::UnityEngine.Camera>();

            float near = ReadF(new[] { ".nearClipPlane", "nearClipPlane", ".ncp", "ncp" }, cam.nearClipPlane);
            float far = ReadF(new[] { ".farClipPlane", "farClipPlane", ".fcp", "fcp" }, cam.farClipPlane);

            cam.nearClipPlane = global::UnityEngine.Mathf.Max(0.001f, near);
            cam.farClipPlane = global::UnityEngine.Mathf.Max(cam.nearClipPlane + 0.01f, far);

            bool ortho = ReadB(new[] { ".orthographic", "orthographic", ".ortho", "ortho" }, false);
            cam.orthographic = ortho;

            if (ortho)
            {
                float ow = ReadF(new[] { ".orthographicWidth", "orthographicWidth", ".ow", "ow" }, 10f);
                cam.orthographicSize = global::UnityEngine.Mathf.Max(0.0001f, ow * 0.5f);
            }
            else
            {
                float fov = ReadF(new[] { ".verticalFieldOfView", "verticalFieldOfView", ".fov", "fov" }, -1f);
                if (fov <= 0f)
                {
                    float focal = ReadF(new[] { ".focalLength", "focalLength", ".fl", "fl" }, 50f);
                    float vfaIn = ReadF(new[] { ".verticalFilmAperture", "verticalFilmAperture", ".vfa", "vfa" }, 0.94488f);
                    float vfaMm = vfaIn * 25.4f;
                    fov = 2f * global::UnityEngine.Mathf.Atan((vfaMm * 0.5f) / global::UnityEngine.Mathf.Max(0.0001f, focal)) * global::UnityEngine.Mathf.Rad2Deg;
                }
                cam.fieldOfView = global::UnityEngine.Mathf.Clamp(fov, 1f, 179f);
            }

            if (options == null || options.Conversion == CoordinateConversion.None)
                transform.localRotation = transform.localRotation * global::UnityEngine.Quaternion.Euler(0f, 180f, 0f);
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

        private bool ReadB(string[] keys, bool def)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                if (TryGetAttr(keys[i], out var a) && a.Tokens != null && a.Tokens.Count > 0)
                {
                    if (float.TryParse(a.Tokens[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
                        return f > 0.5f;
                    if (int.TryParse(a.Tokens[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
                        return v != 0;
                }
            }
            return def;
        }
    }
}
