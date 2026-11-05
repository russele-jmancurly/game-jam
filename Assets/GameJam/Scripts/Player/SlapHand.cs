using GameJam.Core;
using UnityEngine;
using UnityEngine.XR;

namespace GameJam.Player
{
    /// <summary>
    /// A glove. Tracks its own velocity and sweeps its whole path every frame, so even a very fast swing
    /// can't pass through the balloon. Anything implementing <see cref="ISlappable"/> can be hit.
    /// </summary>
    public sealed class SlapHand : MonoBehaviour, IHandVelocity
    {
        private const int MaxOverlaps = 16;
        private const int MaxSweepSteps = 8;

        [SerializeField] private XRNode _node = XRNode.RightHand;
        [SerializeField] private SlapConfig _config;

        private readonly Collider[] _overlaps = new Collider[MaxOverlaps];
        private Vector3 _lastPosition;
        private bool _hasLastPosition;
        private float _nextHitTime;

        public Vector3 Velocity { get; private set; }

        public XRNode Node => _node;

        public void Configure(XRNode node, SlapConfig config)
        {
            _node = node;
            _config = config;
        }

        private void LateUpdate()
        {
            Vector3 position = transform.position;
            if (!_hasLastPosition)
            {
                _lastPosition = position;
                _hasLastPosition = true;
            }

            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            Velocity = Vector3.Lerp(Velocity, (position - _lastPosition) / dt, _config.VelocitySmoothing);

            if (CanHit())
            {
                Sweep(_lastPosition, position);
            }

            _lastPosition = position;
        }

        private bool CanHit()
        {
            if (Time.time < _nextHitTime)
            {
                return false;
            }

            LocalRig rig = LocalRig.Instance;
            return rig == null || (!rig.IsKnockedDown && !rig.IsHolding(this));
        }

        private void Sweep(Vector3 from, Vector3 to)
        {
            float distance = Vector3.Distance(from, to);
            int steps = Mathf.Clamp(Mathf.CeilToInt(distance / _config.HandRadius), 1, MaxSweepSteps);

            for (int step = 1; step <= steps; step++)
            {
                Vector3 point = Vector3.Lerp(from, to, step / (float)steps);
                int count = Physics.OverlapSphereNonAlloc(point, _config.HandRadius, _overlaps, ~0, QueryTriggerInteraction.Collide);
                for (int i = 0; i < count; i++)
                {
                    if (TrySlap(_overlaps[i], point))
                    {
                        _nextHitTime = Time.time + _config.Cooldown;
                        return;
                    }
                }
            }
        }

        private bool TrySlap(Collider other, Vector3 point)
        {
            var target = other.GetComponentInParent<ISlappable>();
            if (target == null)
            {
                return false;
            }

            var slap = new SlapContext(Velocity, point);
            if (!target.TryReceiveSlap(slap, out HapticPulse feedback))
            {
                return false;
            }

            XRHaptics.Send(_node, feedback);
            return true;
        }
    }
}
