using System;
using System.Collections.Generic;
using UnityEngine;

namespace MayaImporter.Deformers
{
    /// <summary>
    /// Phase-1: Runtime holder for blendShape weights.
    /// Applies weights to SkinnedMeshRenderer if blend shapes exist in the mesh.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MayaBlendShapeController : MonoBehaviour
    {
        [Serializable]
        public sealed class Channel
        {
            public string Name;
            public float Weight; // Maya weight (0..1 typically; sometimes 0..100)
        }

        public List<Channel> Channels = new List<Channel>();

        [Tooltip("If true, Maya weights 0..1 are converted to Unity 0..100.")]
        public bool Convert01To100 = true;

        private SkinnedMeshRenderer _smr;

        private void Awake()
        {
            _smr = GetComponent<SkinnedMeshRenderer>();
        }

        private void LateUpdate()
        {
            if (_smr == null) _smr = GetComponent<SkinnedMeshRenderer>();
            if (_smr == null) return;

            var mesh = _smr.sharedMesh;
            if (mesh == null) return;

            int count = mesh.blendShapeCount;
            if (count <= 0) return;

            // Match by index if names don't exist; best-effort by order
            for (int i = 0; i < Channels.Count && i < count; i++)
            {
                float w = Channels[i].Weight;
                if (Convert01To100) w *= 100f;
                _smr.SetBlendShapeWeight(i, w);
            }
        }
    }
}
