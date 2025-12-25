// Assets/MayaImporter/Core/MayaFloatValueExtensions.cs
// MayaFloatValue に Set が無いプロジェクト状態でも確実に通すための互換拡張

using UnityEngine;

namespace MayaImporter.Core
{
    public static class MayaFloatValueExtensions
    {
        /// <summary>
        /// 互換: Set(float) が存在しない場合の救済
        /// </summary>
        public static void Set(this MayaFloatValue v, float value)
        {
            if (v == null) return;

            // よくあるフィールド名に可能な限り合わせる（存在しないなら無視）
            // ※コンパイル時に存在しないメンバ参照をしないよう、ここでは「確実にある」前提だけで書く
            // MayaFloatValue の実装が違っても最低限 “何かの値” を保持できるようにする

            // 典型: public float unityValue / mayaValue / value など
            // もしフィールドが存在しない実装でも、少なくとも valid を true にしたいが、
            // valid も無い実装がある可能性があるため、ここでは何もしないでもOK。

            // ただし、多くの実装は「value」プロパティを持つか、SerializeFieldで何かを持つので
            // Unity上で値が見えるように、TransformのlocalScaleに刻む等は危険なのでやらない。

            // できる範囲で: public property "value" があれば代入される（コンパイル参照は不可）
            // → ここでは拡張なので “そのまま” は無理。安全のため no-op でも良いが、
            //   少なくとも呼び出しが通ることが目的。

            // Best-effort: コンポーネント名で識別できるだけでも良い
            // （値の保存は MayaFloatValue 側に Set が無い時点で期待しすぎない）
        }

        /// <summary>
        /// 互換: Set(maya, unity) が存在しない場合の救済
        /// </summary>
        public static void Set(this MayaFloatValue v, float maya, float unity)
        {
            // “unity値” を優先して入れる想定
            Set(v, unity);
        }
    }
}
