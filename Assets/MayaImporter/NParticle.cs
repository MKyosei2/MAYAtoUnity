using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Dynamics
{
    [MayaNodeType("nParticle")]
    [DisallowMultipleComponent]
    public sealed class NParticle : MayaNodeComponentBase
    {
        [Header("Unity")]
        public ParticleSystem particleSystem;
        public MayaNParticleRuntimeSystem runtime;

        public override void ApplyToUnity(MayaImportOptions options, MayaImportLog log)
        {
            log ??= new MayaImportLog();

            // Ensure ParticleSystem exists (best-effort)
            particleSystem = GetComponent<ParticleSystem>();
            if (particleSystem == null) particleSystem = gameObject.AddComponent<ParticleSystem>();

            runtime = GetComponent<MayaNParticleRuntimeSystem>();
            if (runtime == null) runtime = gameObject.AddComponent<MayaNParticleRuntimeSystem>();

            runtime.SourceNodeName = NodeName;
            runtime.ParticleSystem = particleSystem;

            MayaParticleManager.EnsureExists();

            log.Info($"[nParticle] '{NodeName}' ParticleSystem ensured.");
        }
    }
}
