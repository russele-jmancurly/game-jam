using UnityEngine;

namespace GameJam.Gameplay
{
    /// <summary>Trigger that blows the balloon (and players, more gently) along its forward axis.</summary>
    public sealed class WindVolume : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float _strength = 2.5f;
        [Tooltip("How strongly it pushes players compared with the balloon.")]
        [SerializeField, Range(0f, 1f)] private float _playerFactor = 0.4f;

        /// <summary>Acceleration applied to the balloon, in m/s².</summary>
        public Vector3 BalloonAcceleration => transform.forward * _strength;

        /// <summary>Acceleration applied to players, in m/s².</summary>
        public Vector3 PlayerAcceleration => BalloonAcceleration * _playerFactor;

        public void Configure(float strength) => _strength = strength;

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(transform.position, transform.forward * 2f);
        }
    }
}
