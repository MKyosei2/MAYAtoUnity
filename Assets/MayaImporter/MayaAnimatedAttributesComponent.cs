using System;
using UnityEngine;

namespace MayaImporter.Animation
{
    [DisallowMultipleComponent]
    public sealed class MayaAnimatedAttributesComponent : MonoBehaviour
    {
        [Serializable]
        public sealed class Slot
        {
            public string MayaAttribute;   // e.g. ".myAttr"
            public string UnityFieldName;  // e.g. "A0"
        }

        public Slot[] Slots = new Slot[32];

        // 32 animatable fields (AnimationClip can target these)
        public float A0, A1, A2, A3, A4, A5, A6, A7;
        public float A8, A9, A10, A11, A12, A13, A14, A15;
        public float A16, A17, A18, A19, A20, A21, A22, A23;
        public float A24, A25, A26, A27, A28, A29, A30, A31;

        public string GetOrCreateFieldFor(string mayaAttr)
        {
            if (string.IsNullOrEmpty(mayaAttr)) return null;

            // find existing
            for (int i = 0; i < Slots.Length; i++)
            {
                var s = Slots[i];
                if (s != null && string.Equals(s.MayaAttribute, mayaAttr, StringComparison.Ordinal))
                    return s.UnityFieldName;
            }

            // allocate
            for (int i = 0; i < Slots.Length; i++)
            {
                if (Slots[i] == null || string.IsNullOrEmpty(Slots[i].MayaAttribute))
                {
                    Slots[i] = new Slot
                    {
                        MayaAttribute = mayaAttr,
                        UnityFieldName = "A" + i
                    };
                    return Slots[i].UnityFieldName;
                }
            }

            return null; // no slot left
        }
    }
}
