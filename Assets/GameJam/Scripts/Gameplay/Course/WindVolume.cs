using UnityEngine;

namespace GameJam.Gameplay
{
    /// <summary>Trigger that blows the balloon (and players, more gently) along its forward axis.</summary>
    public sealed class WindVolume : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float _strength = 2.5f;
        [Tooltip("How strongly it pushes players compared with the balloon.")]
        [SerializeField, Range(0f, 1f)] private float _playerFactor = 0.4f;

        private bool _hasTarget;
        private Vector3 _target;
        private float _lift;

        /// <summary>Acceleration applied to the balloon, in m/s² (along the wind; see <see cref="BalloonAccelerationAt"/>).</summary>
        public Vector3 BalloonAcceleration => transform.forward * _strength;

        /// <summary>Acceleration applied to players, in m/s².</summary>
        public Vector3 PlayerAcceleration => BalloonAcceleration * _playerFactor;

        public void Configure(float strength) => _strength = strength;

        /// <summary>
        /// Makes the wind steer the balloon toward a point (a leaf blower aimed at a hoop) instead of blowing straight, and
        /// adds <paramref name="lift"/> upward so the balloon's gravity doesn't drag it under the target. Null = straight.
        /// </summary>
        public void SetTarget(Vector3? target, float lift)
        {
            _hasTarget = target.HasValue;
            _target = target ?? Vector3.zero;
            _lift = lift;
        }

        /// <summary>Acceleration on a balloon at this position, in m/s².</summary>
        public Vector3 BalloonAccelerationAt(Vector3 position)
        {
            if (!_hasTarget)
            {
                return BalloonAcceleration;
            }

            Vector3 toTarget = _target - position;
            Vector3 steer = toTarget.sqrMagnitude > 0.01f ? toTarget.normalized * _strength : Vector3.zero;
            return steer + Vector3.up * _lift;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(transform.position, transform.forward * 2f);
        }
    }
}
