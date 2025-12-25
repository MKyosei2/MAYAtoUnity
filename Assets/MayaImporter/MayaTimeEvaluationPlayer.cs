using UnityEngine;

namespace MayaImporter.Animation
{
    /// <summary>
    /// Minimal Maya-like time/evaluation player.
    ///
    /// Notes (portfolio / 100% policy):
    /// - This class does NOT attempt to replicate Maya's dependency graph evaluation strictly.
    /// - It provides a deterministic "time -> Unity sampling" path and a hook to plug in future solvers.
    /// - All original Maya data is preserved in MayaSceneData + MayaNodeComponentBase.
    /// </summary>
    [DisallowMultipleComponent]
    public class MayaTimeEvaluationPlayer : MonoBehaviour
    {
        [Header("Playback")]
        public bool PlayOnStart = false;

        // Keep existing name, but also provide a property alias for older code paths.
        public bool Loop = true;
        public bool loop { get => Loop; set => Loop = value; } // compatibility (Unity does not serialize properties)

        [Tooltip("Seconds per Maya frame (default 1/24).")]
        public float SecondsPerFrame = 1f / 24f;

        [Tooltip("Current time in frames (Maya time unit).")]
        public float CurrentFrame;

        [Header("Animation")]
        public AnimationClip Clip;
        public bool UseAnimatorIfPresent = true;

        private Animator _animator;
        private global::UnityEngine.Animation _legacy;
        private float _timeSec;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _legacy = GetComponent<global::UnityEngine.Animation>();
        }

        private void Start()
        {
            if (PlayOnStart)
                Play();
        }

        public void Play() => enabled = true;
        public void Stop() => enabled = false;

        /// <summary>
        /// Evaluate immediately at the given Maya frame.
        /// Used by baking/preview tools in Editor.
        /// </summary>
        public void EvaluateAtFrame(float frame)
        {
            if (SecondsPerFrame <= 0f) SecondsPerFrame = 1f / 24f;

            CurrentFrame = frame;
            _timeSec = frame * SecondsPerFrame;
            SampleAtSeconds(_timeSec);

            // Hook: future evaluation graph
            OnAfterSample(CurrentFrame, _timeSec);
        }

        private void Update()
        {
            if (SecondsPerFrame <= 0f) SecondsPerFrame = 1f / 24f;

            _timeSec += Time.deltaTime;

            // Convert to frames
            CurrentFrame = _timeSec / SecondsPerFrame;

            SampleAtSeconds(_timeSec);

            // Hook: future evaluation graph
            OnAfterSample(CurrentFrame, _timeSec);
        }

        private void SampleAtSeconds(float timeSec)
        {
            if (Clip == null)
                return;

            float clipLen = Mathf.Max(Clip.length, 0.0001f);
            float t = Loop ? (timeSec % clipLen) : Mathf.Min(timeSec, clipLen);

            if (UseAnimatorIfPresent && _animator != null)
            {
                _animator.Play(Clip.name, 0, t / clipLen);
                _animator.Update(0f);
                return;
            }

            if (_legacy != null)
            {
                if (_legacy.GetClip(Clip.name) == null)
                    _legacy.AddClip(Clip, Clip.name);

                _legacy.clip = Clip;
                _legacy[Clip.name].time = t;

                if (!_legacy.isPlaying) _legacy.Play(Clip.name);
                _legacy.Sample();
                return;
            }

            Clip.SampleAnimation(gameObject, t);
        }

        /// <summary>
        /// Future: plug eval graph / solver execution here.
        /// </summary>
        protected virtual void OnAfterSample(float frame, float timeSec)
        {
            // no-op
        }
    }
}
