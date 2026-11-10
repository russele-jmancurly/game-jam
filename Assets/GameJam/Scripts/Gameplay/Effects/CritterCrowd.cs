using UnityEngine;

namespace GameJam.Gameplay
{
    /// <summary>
    /// The backyard critter audience (frogs, snails, squirrels, ladybugs, songbirds, mice perched on logs along the fence
    /// and behind the start). One component animates all of them, with no per-critter MonoBehaviour and no allocations:
    /// a gentle idle bob, turning to watch the balloon (within LookDegrees of where they sit), and hopping when something
    /// good happens: a save or a rally milestone (the critters near it), a cleared hoop, the finish (everyone, longest).
    /// Local only: every client runs its own crowd from the events it already plays. Purely visual (no colliders).
    /// </summary>
    public sealed class CritterCrowd : MonoBehaviour
    {
        private const float CheerReach = 45f; // critters this close to a cheer hop; farther ones keep watching

        [SerializeField] private Transform[] _critters = System.Array.Empty<Transform>();
        [Tooltip("How far a critter turns from its seat to watch the balloon (degrees either way).")]
        [SerializeField] private float _lookDegrees = 55f;
        [Tooltip("How fast they turn to follow the balloon (per second).")]
        [SerializeField] private float _turnSpeed = 2.5f;
        [Tooltip("Idle bob height (metres) and speed (cycles per second).")]
        [SerializeField] private float _bobHeight = 0.025f;
        [SerializeField] private float _bobSpeed = 0.7f;
        [Tooltip("One cheering hop: height (metres) and length (seconds).")]
        [SerializeField] private float _hopHeight = 0.35f;
        [SerializeField] private float _hopSeconds = 0.36f;

        private Vector3[] _restPosition;
        private Quaternion[] _restRotation;
        private Vector3[] _restScale;
        private float[] _phase;
        private float[] _yaw;
        private float[] _hopStart;
        private int[] _hopsLeft;

        public static CritterCrowd Instance { get; private set; }

        /// <summary>Raised on every cheer (where, hops; CheerAll passes no spot), so others can join in (the MC).</summary>
        public static event System.Action<Vector3?, int> Cheered;

        /// <summary>What the critters watch (the balloon sets itself here). None: they face their seat's way.</summary>
        public static Transform LookTarget { get; set; }

        public int Count => _critters.Length;

        /// <summary>Builder wiring: every critter transform, as placed (their seat and facing are their rest pose).</summary>
        public void Configure(Transform[] critters) => _critters = critters;

        /// <summary>The critters within reach of this spot hop (1 hop = a small cheer, 4 = the finish).</summary>
        public static void Cheer(Vector3 where, int hops)
        {
            Cheered?.Invoke(where, hops);
            if (Instance != null)
            {
                Instance.StartHops(where, hops, CheerReach);
            }
        }

        /// <summary>Every critter in the yard hops.</summary>
        public static void CheerAll(int hops)
        {
            Cheered?.Invoke(null, hops);
            if (Instance != null)
            {
                Instance.StartHops(Vector3.zero, hops, float.PositiveInfinity);
            }
        }

        private void Awake()
        {
            Instance = this;
            int count = _critters.Length;
            _restPosition = new Vector3[count];
            _restRotation = new Quaternion[count];
            _restScale = new Vector3[count];
            _phase = new float[count];
            _yaw = new float[count];
            _hopStart = new float[count];
            _hopsLeft = new int[count];
            for (int i = 0; i < count; i++)
            {
                Transform critter = _critters[i];
                if (critter == null)
                {
                    continue;
                }

                _restPosition[i] = critter.localPosition;
                _restRotation[i] = critter.localRotation;
                _restScale[i] = critter.localScale;
                _phase[i] = i * 2.399f; // golden angle: neighbours never bob in step
                _hopStart[i] = -10f;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void StartHops(Vector3 where, int hops, float reach)
        {
            float now = Time.time;
            float reachSquared = reach * reach;
            for (int i = 0; i < _critters.Length; i++)
            {
                Transform critter = _critters[i];
                if (critter == null || (critter.position - where).sqrMagnitude > reachSquared)
                {
                    continue;
                }

                if (_hopsLeft[i] <= 0)
                {
                    _hopStart[i] = now + Mathf.Repeat(_phase[i], 1f) * 0.25f; // a ripple, not a drill team
                }

                _hopsLeft[i] = Mathf.Max(_hopsLeft[i], hops);
            }
        }

        private void Update()
        {
            float now = Time.time;
            float dt = Time.deltaTime;
            Transform target = LookTarget;
            bool hasTarget = target != null && target.gameObject.activeInHierarchy;
            Vector3 targetPosition = hasTarget ? target.position : Vector3.zero;
            float turn = 1f - Mathf.Exp(-_turnSpeed * dt);
            Transform parent = transform;

            for (int i = 0; i < _critters.Length; i++)
            {
                Transform critter = _critters[i];
                if (critter == null)
                {
                    continue;
                }

                // Watch the balloon: the yaw from its seat's facing, limited so nobody turns its back to the yard.
                float wantedYaw = 0f;
                if (hasTarget)
                {
                    Vector3 seat = parent.TransformPoint(_restPosition[i]);
                    Vector3 toTarget = targetPosition - seat;
                    toTarget.y = 0f;
                    if (toTarget.sqrMagnitude > 0.01f)
                    {
                        Vector3 forward = parent.rotation * (_restRotation[i] * Vector3.forward);
                        forward.y = 0f;
                        wantedYaw = Mathf.Clamp(Vector3.SignedAngle(forward, toTarget, Vector3.up), -_lookDegrees, _lookDegrees);
                    }
                }

                _yaw[i] = Mathf.Lerp(_yaw[i], wantedYaw, turn);

                float lift = _bobHeight * (0.5f + 0.5f * Mathf.Sin(now * _bobSpeed * 2f * Mathf.PI + _phase[i]));
                float stretch = 1f;
                if (_hopsLeft[i] > 0 && now >= _hopStart[i])
                {
                    float p = (now - _hopStart[i]) / _hopSeconds;
                    if (p >= 1f)
                    {
                        _hopsLeft[i]--;
                        _hopStart[i] = now + 0.05f;
                    }
                    else
                    {
                        lift += _hopHeight * 4f * p * (1f - p);
                        stretch = 1f + 0.12f * Mathf.Sin(p * Mathf.PI); // a little stretch in the air
                    }
                }

                critter.localPosition = _restPosition[i] + Vector3.up * lift;
                critter.localRotation = Quaternion.AngleAxis(_yaw[i], Vector3.up) * _restRotation[i];
                float thin = 1f / Mathf.Sqrt(stretch);
                critter.localScale = Vector3.Scale(_restScale[i], new Vector3(thin, stretch, thin));
            }
        }
    }
}
