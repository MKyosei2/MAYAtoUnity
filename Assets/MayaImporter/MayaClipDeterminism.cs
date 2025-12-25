#if UNITY_EDITOR
using System;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MayaImporter.Core
{
    /// <summary>
    /// Phase C-5:
    /// AnimationClip の内容から “安定ハッシュ” を作る（再現性の証明）。
    /// - binding(path/type/property) と key(time/value/in/outTangent/weighted etc) を順序付きで直列化
    /// - Unityの内部IDや参照を使わないので、同じベイク結果なら同じhashになる
    /// </summary>
    public static class MayaClipDeterminism
    {
        public static string ComputeStableHash(AnimationClip clip)
        {
            if (clip == null) return "";

            try
            {
                var sb = new StringBuilder(1 << 16);

                var bindings = AnimationUtility.GetCurveBindings(clip);
                Array.Sort(bindings, CompareBinding);

                for (int i = 0; i < bindings.Length; i++)
                {
                    var b = bindings[i];
                    sb.Append("B|");
                    sb.Append(b.path).Append('|');
                    sb.Append(b.type != null ? b.type.FullName : "(null)").Append('|');
                    sb.Append(b.propertyName).Append('\n');

                    var curve = AnimationUtility.GetEditorCurve(clip, b);
                    if (curve == null)
                    {
                        sb.Append("C|null\n");
                        continue;
                    }

                    sb.Append("C|").Append(curve.length).Append('\n');

                    // Keys
                    var keys = curve.keys;
                    for (int k = 0; k < keys.Length; k++)
                    {
                        var key = keys[k];
                        sb.Append("K|")
                          .Append(key.time.ToString("R")).Append('|')
                          .Append(key.value.ToString("R")).Append('|')
                          .Append(key.inTangent.ToString("R")).Append('|')
                          .Append(key.outTangent.ToString("R")).Append('|')
                          .Append(key.inWeight.ToString("R")).Append('|')
                          .Append(key.outWeight.ToString("R")).Append('|')
                          .Append((int)key.weightedMode)
                          .Append('\n');
                    }
                }

                // Hash
                using var sha = SHA256.Create();
                var bytes = Encoding.UTF8.GetBytes(sb.ToString());
                var hash = sha.ComputeHash(bytes);
                return ToHex(hash);
            }
            catch
            {
                return "";
            }
        }

        private static int CompareBinding(EditorCurveBinding a, EditorCurveBinding b)
        {
            int c = string.CompareOrdinal(a.path, b.path);
            if (c != 0) return c;

            var at = a.type != null ? a.type.FullName : "";
            var bt = b.type != null ? b.type.FullName : "";
            c = string.CompareOrdinal(at, bt);
            if (c != 0) return c;

            return string.CompareOrdinal(a.propertyName, b.propertyName);
        }

        private static string ToHex(byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length * 2);
            for (int i = 0; i < bytes.Length; i++)
                sb.Append(bytes[i].ToString("x2"));
            return sb.ToString();
        }
    }
}
#endif
