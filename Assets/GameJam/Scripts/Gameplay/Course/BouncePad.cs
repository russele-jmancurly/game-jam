using UnityEngine;

namespace GameJam.Gameplay
{
    /// <summary>
    /// Trampoline trigger: launches players up. Balloons ignore it by default: the floaty balloon, relaunched every time it
    /// drifted back onto the pad, hovered over it forever (playtest: "the balloon is stuck on the trampoline").
    /// </summary>
    public sealed class BouncePad : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float _launchSpeed = 9f;
        [Tooltip("Share of the launch the balloon gets (0 = the balloon ignores the pad).")]
        [SerializeField, Range(0f, 1f)] private float _balloonFactor;

        [Tooltip("Boing when a player bounces (optional).")]
        [SerializeField] private AudioSource _bounceAudio;

        private float _nextSound;

        public float LaunchSpeed => _launchSpeed;

        public float BalloonLaunchSpeed => _launchSpeed * _balloonFactor;

        public bool LaunchesBalloon => _balloonFactor > 0f;

        public void Configure(float launchSpeed, AudioSource bounceAudio = null)
        {
            _launchSpeed = launchSpeed;
            _bounceAudio = bounceAudio;
        }

        /// <summary>A player just bounced: boing (at most every 0.3 s).</summary>
        public void PlayBounce()
        {
            if (_bounceAudio != null && Time.time >= _nextSound)
            {
                _nextSound = Time.time + 0.3f;
                _bounceAudio.pitch = Random.Range(0.92f, 1.08f);
                _bounceAudio.Play();
            }
        }
    }
}
