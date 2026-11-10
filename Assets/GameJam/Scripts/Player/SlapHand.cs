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

        private const float LeftGapSeconds = 0.06f;

        // Shared by both hands: the last thing either hand hit stays locked until no hand has touched it for a moment
        // and RehitSeconds have passed. A hand resting inside the balloon (on a client that doesn't own it, the balloon's
        // body only moves a round trip later), or the other hand pushing next to it, used to hit again: a double-hit pop.
        private static ISlappable LockedTarget;
        private static float LockedAt;
        private static float LockedLastTouch;

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
            LocalRig rig = LocalRig.Instance;
            return rig == null || (!rig.IsKnockedDown && !rig.IsHolding(this));
        }

        /// <summary>Releases the shared lock once no hand has touched the locked target for a moment and RehitSeconds passed.</summary>
        private void ReleaseLockIfLeft()
        {
            if (LockedTarget == null)
            {
                return;
            }

            float now = Time.time;
            if (now - LockedLastTouch > LeftGapSeconds && now - LockedAt > _config.RehitSeconds)
            {
                LockedTarget = null;
            }
        }

        private void Sweep(Vector3 from, Vector3 to)
        {
            ReleaseLockIfLeft();
            float distance = Vector3.Distance(from, to);
            int steps = Mathf.Clamp(Mathf.CeilToInt(distance / _config.HandRadius), 1, MaxSweepSteps);
            bool coolingDown = Time.time < _nextHitTime;

            for (int step = 1; step <= steps; step++)
            {
                Vector3 point = Vector3.Lerp(from, to, step / (float)steps);
                int count = Physics.OverlapSphereNonAlloc(point, _config.HandRadius, _overlaps, ~0, QueryTriggerInteraction.Collide);
                for (int i = 0; i < count; i++)
                {
                    var target = _overlaps[i].GetComponentInParent<ISlappable>();
                    if (target == null)
                    {
                        continue;
                    }

                    if (target == LockedTarget)
                    {
                        LockedLastTouch = Time.time; // still touching what was just hit: no new hit
                        continue;
                    }

                    if (!coolingDown && TrySlap(target, point))
                    {
                        _nextHitTime = Time.time + _config.Cooldown;
                        LockedTarget = target;
                        LockedAt = Time.time;
                        LockedLastTouch = Time.time;
                        return;
                    }
                }
            }
        }

        private bool TrySlap(ISlappable target, Vector3 point)
        {
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
