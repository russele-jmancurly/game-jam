using GameJam.Core;
using UnityEngine;

namespace GameJam.Gameplay
{
    /// <summary>
    /// Sets its AudioSource's volume from the game's mix (<see cref="AudioConfig"/> volume for its category x this sound's
    /// own level x a fade), live, so the balance can be tuned in Play mode. Loops start and stop through
    /// <see cref="FadeIn"/> / <see cref="FadeOut"/>: a fade instead of a hard start or cut, so they never click or stutter.
    /// Music (and any loop that plays on awake) fades in when it starts.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class SoundLevel : MonoBehaviour
    {
        [SerializeField] private AudioConfig _config;
        [SerializeField] private SoundCategory _category;
        [Tooltip("This sound's own level within its category.")]
        [Range(0f, 1f)] [SerializeField] private float _volume = 1f;

        private AudioSource _source;
        private float _fade = 1f;
        private float _fadeTarget = 1f;
        private bool _stopWhenSilent;

        public AudioSource Source => _source != null ? _source : _source = GetComponent<AudioSource>();

        public void Configure(AudioConfig config, SoundCategory category, float volume)
        {
            _config = config;
            _category = category;
            _volume = volume;
            Apply();
        }

        /// <summary>Starts the loop (if it isn't playing) and fades it up.</summary>
        public void FadeIn()
        {
            AudioSource source = Source;
            if (!source.isPlaying)
            {
                _fade = 0f;
                source.Play();
            }

            _fadeTarget = 1f;
            _stopWhenSilent = false;
        }

        /// <summary>Fades the loop down, then stops it.</summary>
        public void FadeOut()
        {
            _fadeTarget = 0f;
            _stopWhenSilent = true;
        }

        private void Awake()
        {
            if (Source.playOnAwake && Source.loop)
            {
                _fade = 0f; // fade in from silence instead of starting at full volume
            }

            Apply();
        }

        private void Update()
        {
            if (!Mathf.Approximately(_fade, _fadeTarget) && _config != null)
            {
                float seconds = _category == SoundCategory.Music && _fadeTarget > _fade ? _config.MusicFadeInSeconds : _config.LoopFadeSeconds;
                _fade = Mathf.MoveTowards(_fade, _fadeTarget, Time.deltaTime / seconds);
            }

            Apply();
            if (_stopWhenSilent && _fade <= 0f && Source.isPlaying)
            {
                Source.Stop();
                _stopWhenSilent = false;
            }
        }

        private void Apply()
        {
            float level = _config != null ? _config.Volume(_category) : 1f;
            Source.volume = _volume * level * _fade;
        }
    }
}
