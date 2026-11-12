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

        // Recent hand poses for throws (see ThrowVelocity). 64 frames cover the throw window up to ~500 fps.
        private const int HistorySize = 64;
        private readonly Vector3[] _historyPositions = new Vector3[HistorySize];
        private readonly Quaternion[] _historyRotations = new Quaternion[HistorySize];
        private readonly float[] _historyTimes = new float[HistorySize];
        private int _historyCount;
        private int _historyHead; // index of the newest sample

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

            _historyHead = (_historyHead + 1) % HistorySize;
            _historyPositions[_historyHead] = position;
            _historyRotations[_historyHead] = transform.rotation;
            _historyTimes[_historyHead] = Time.time;
            _historyCount = Mathf.Min(_historyCount + 1, HistorySize);
        }

        /// <summary>
        /// The velocity to throw with: the fastest stretch (sampleSeconds long) of the last windowSeconds of the swing,
        /// measured at a point carried by the hand (localOffset: where the held item sits, so a wrist flick counts).
        /// People open their hand just after the swing's peak, when the hand is already slowing and curving away, so the
        /// velocity on the release frame alone throws short and off line. Measured from positions over time, so it doesn't
        /// depend on the frame rate. Separate from <see cref="Velocity"/>, which slaps are tuned on.
        /// </summary>
        public Vector3 ThrowVelocity(float windowSeconds, float sampleSeconds, Vector3 localOffset)
        {
            if (_historyCount < 2)
            {
                return Velocity;
            }

            float newestTime = _historyTimes[_historyHead];
            Vector3 best = Vector3.zero;
            float bestSpeed = -1f;
            // Walk back from the newest sample; each sample ends a stretch that starts sampleSeconds earlier.
            for (int end = 0; end < _historyCount - 1; end++)
            {
                int endIndex = (_historyHead - end + HistorySize) % HistorySize;
                float endTime = _historyTimes[endIndex];
                if (newestTime - endTime > windowSeconds)
                {
                    break;
                }

                int startIndex = endIndex;
                for (int back = end + 1; back < _historyCount; back++)
                {
                    startIndex = (_historyHead - back + HistorySize) % HistorySize;
                    if (endTime - _historyTimes[startIndex] >= sampleSeconds)
                    {
                        break;
                    }
                }

                float span = endTime - _historyTimes[startIndex];
                if (span <= 0.0001f)
                {
                    continue;
                }

                Vector3 from = _historyPositions[startIndex] + _historyRotations[startIndex] * localOffset;
                Vector3 to = _historyPositions[endIndex] + _historyRotations[endIndex] * localOffset;
                Vector3 velocity = (to - from) / span;
                float speed = velocity.sqrMagnitude;
                if (speed > bestSpeed)
                {
                    bestSpeed = speed;
                    best = velocity;
                }
            }

            return bestSpeed >= 0f ? best : Velocity;
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
