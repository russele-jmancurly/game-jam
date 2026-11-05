using UnityEngine;

namespace GameJam.Gameplay
{
    /// <summary>Trampoline trigger: launches players up, and the balloon a little less.</summary>
    public sealed class BouncePad : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float _launchSpeed = 9f;
        [SerializeField, Range(0f, 1f)] private float _balloonFactor = 0.6f;

        public float LaunchSpeed => _launchSpeed;

        public float BalloonLaunchSpeed => _launchSpeed * _balloonFactor;

        public void Configure(float launchSpeed) => _launchSpeed = launchSpeed;
    }
}
