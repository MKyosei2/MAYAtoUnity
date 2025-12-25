using System;
using System.Collections.Generic;
using UnityEngine;

namespace MayaImporter.Dynamics
{
    /// <summary>
    /// Unity-only best-effort runtime for Maya nParticleSystem / nParticle.
    /// Applies nucleus gravity (directional) + field forces by directly modifying particle velocities.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MayaNParticleRuntimeSystem : MonoBehaviour
    {
        public int Priority = 0;

        [Header("Source")]
        public string SourceNodeName;

        [Header("Bindings")]
        public ParticleSystem ParticleSystem;
        public MayaNucleusRuntimeWorld NucleusWorld;

        [Header("Connected Fields")]
        public List<string> FieldNodeNames = new List<string>();
        public List<MayaFieldRuntime> Fields = new List<MayaFieldRuntime>();

        [Header("Options")]
        public bool ApplyNucleusGravity = true;
        public bool ResolveFieldsEveryFrame = true;

        private ParticleSystem.Particle[] _buffer;

        private void Awake()
        {
            if (ParticleSystem == null) ParticleSystem = GetComponent<ParticleSystem>();
        }

        private void OnEnable()
        {
            if (ParticleSystem == null) ParticleSystem = GetComponent<ParticleSystem>();

            MayaParticleManager.EnsureExists();
            MayaParticleManager.Register(this);
        }

        private void OnDisable()
        {
            MayaParticleManager.Unregister(this);
        }

        internal void ApplyForcesInternal(float dt)
        {
            if (ParticleSystem == null) return;

            // Resolve field references lazily
            if (ResolveFieldsEveryFrame)
                ResolveFields();

            float timeScale = 1f;
            Vector3 g = Vector3.zero;

            if (ApplyNucleusGravity && NucleusWorld != null)
            {
                timeScale = Mathf.Max(0f, NucleusWorld.TimeScale);
                g = (NucleusWorld.GravityDirection.sqrMagnitude > 1e-12f ? NucleusWorld.GravityDirection.normalized : Vector3.down) * NucleusWorld.GravityMagnitude;
            }

            float scaledDt = dt * timeScale;
            if (scaledDt <= 0f) return;

            int max = 4096;
            var main = ParticleSystem.main;
            if (main.maxParticles > 0) max = Mathf.Max(64, main.maxParticles);

            if (_buffer == null || _buffer.Length < max)
                _buffer = new ParticleSystem.Particle[max];

            int count = ParticleSystem.GetParticles(_buffer);
            if (count <= 0) return;

            float t = Time.time;

            for (int i = 0; i < count; i++)
            {
                var p = _buffer[i];
                Vector3 pos = p.position;
                Vector3 vel = p.velocity;

                Vector3 acc = Vector3.zero;

                if (ApplyNucleusGravity && NucleusWorld != null)
                    acc += g;

                for (int k = 0; k < Fields.Count; k++)
                {
                    var f = Fields[k];
                    if (f == null) continue;
                    acc += f.ComputeAcceleration(pos, vel, t);
                }

                vel += acc * scaledDt;
                p.velocity = vel;

                _buffer[i] = p;
            }

            ParticleSystem.SetParticles(_buffer, count);
        }

        private void ResolveFields()
        {
            // Keep current resolved list in sync with names.
            Fields.Clear();

            for (int i = 0; i < FieldNodeNames.Count; i++)
            {
                var name = FieldNodeNames[i];
                if (string.IsNullOrEmpty(name)) continue;

                var tf = MayaImporter.Core.MayaNodeLookup.FindTransform(name);
                if (tf == null) tf = MayaImporter.Core.MayaNodeLookup.FindTransform(MayaImporter.Core.MayaPlugUtil.LeafName(name));
                if (tf == null) continue;

                var rt = tf.GetComponent<MayaFieldRuntime>();
                if (rt != null) Fields.Add(rt);
            }
        }
    }
}
