// Assets/MayaImporter/Core/MayaFloatValue.cs
// Float scalar carrier (Core-side).
// 互換用に `.value` と `Set(float)` も提供する。

using UnityEngine;

namespace MayaImporter.Core
{
    [DisallowMultipleComponent]
    public sealed class MayaFloatValue : MonoBehaviour
    {
        public bool valid;

        [Header("Values")]
        public float mayaValue;
        public float unityValue;

        /// <summary>
        /// 互換: 旧コードが参照する `.value`（Unity側値として扱う）
        /// </summary>
        public float value
        {
            get => unityValue;
            set
            {
                unityValue = value;
                mayaValue = value;
                valid = true;
            }
        }

        /// <summary>
        /// Core標準: Maya/Unity 両方をセット
        /// </summary>
        public void Set(float maya, float unity)
        {
            mayaValue = maya;
            unityValue = unity;
            valid = true;
        }

        /// <summary>
        /// 互換: 単一値セット（maya=unity とみなす）
        /// </summary>
        public void Set(float v)
        {
            mayaValue = v;
            unityValue = v;
            valid = true;
        }
    }
}
