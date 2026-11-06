using Fusion;
using GameJam.Core;
using GameJam.Gameplay;
using UnityEngine;

namespace GameJam.Net
{
    /// <summary>
    /// A giant version of a random character (re-rolled every run) loose in the yard. It strolls between open spots,
    /// easing in and out of its walk, turning before it sets off and pausing to look around. When the balloon drifts
    /// close and low it hurries over, raises its slap hand (the telegraph, with a "!"), swings and slaps the balloon away
    /// as nobody's hit, then cools down. It bowls over players it walks into and you can climb it.
    /// The master client simulates it (Master Client Object); everyone else sees it through its NetworkTransform, and
    /// the walk cycle and slap swing are animated locally from its speed and networked mood.
    /// </summary>
    public sealed class MischiefMaker : NetworkBehaviour, IKnockbackSource
    {
        private const float TargetReachedDistance = 1.2f;
        private const float FollowThroughSeconds = 0.45f;

        [SerializeField] private CharacterRoster _roster;
        [SerializeField] private CharacterVisual _visual;
        [SerializeField] private Transform _visualRoot;
        [SerializeField] private TextMesh _alert;
        [Tooltip("The giant's slap mitt and the arm joining it to the shoulder (visual only, no colliders).")]
        [SerializeField] private Transform _slapHand;
        [SerializeField] private Transform _slapArm;

        [Header("Yard (set by the builder)")]
        [Tooltip("Open lawn spots it wanders between.")]
        [SerializeField] private Vector3[] _wanderPoints = System.Array.Empty<Vector3>();
        [Tooltip("Circles it steers around: x, z, radius.")]
        [SerializeField] private Vector3[] _avoidCircles = System.Array.Empty<Vector3>();
        [Tooltip("Yard bounds it stays inside: min x, min z, max x, max z.")]
        [SerializeField] private Vector4 _bounds = new Vector4(-44f, -20f, 44f, 86f);

        // Authority-side simulation.
        private Vector3 _wanderTarget;
        private float _speed;

        // Local presentation.
        private Vector3 _lastPosition;
        private Vector3 _velocity;
        private float _shownSpeed;
        private float _stridePhase;
        private float _bodyHeight = 3.8f;
        private int _shownCharacter = -1;
        private RoamerMood _animatedMood = RoamerMood.Wander;

        public static MischiefMaker Instance { get; private set; }

        [Networked] public int CharacterIndex { get; private set; }

        [Networked] public RoamerMood Mood { get; private set; }

        [Networked] private TickTimer StateTimer { get; set; }

        public bool KnocksPlayers => true;

        public string DisplayName => _roster != null && _roster.Get(CharacterIndex) != null ? _roster.Get(CharacterIndex).DisplayName : "The giant";

        public void Configure(CharacterRoster roster, CharacterVisual visual, Transform visualRoot, TextMesh alert, Transform slapHand, Transform slapArm,
            Vector3[] wanderPoints, Vector3[] avoidCircles, Vector4 bounds)
        {
            _roster = roster;
            _visual = visual;
            _visualRoot = visualRoot;
            _alert = alert;
            _slapHand = slapHand;
            _slapArm = slapArm;
            _wanderPoints = wanderPoints;
            _avoidCircles = avoidCircles;
            _bounds = bounds;
        }

        public Vector3 PointVelocity(Vector3 worldPoint)
        {
            // Even standing still it shoves you a little, so walking into a giant always does something.
            Vector3 away = worldPoint - transform.position;
            away.y = 0f;
            Vector3 shove = away.sqrMagnitude > 0.001f ? away.normalized * 1.5f : Vector3.zero;
            return _velocity + shove;
        }

        /// <summary>Picks a new random character (called at every run start).</summary>
        public void Reroll() => RpcReroll();

        public override void Spawned()
        {
            Instance = this;
            _lastPosition = transform.position;
            if (HasStateAuthority)
            {
                CharacterIndex = RandomCharacter();
                StartWandering();
            }

            ApplyCharacter();
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority || _roster == null)
            {
                return;
            }

            float dt = Runner.DeltaTime;
            NetworkBalloon balloon = NetworkBalloon.Instance;
            bool canSeeBalloon = balloon != null && IsBalloonInReach(balloon);

            switch (Mood)
            {
                case RoamerMood.Wander:
                    WalkToward(_wanderTarget, _roster.RoamerWalkSpeed, dt, true);
                    if (canSeeBalloon)
                    {
                        Mood = RoamerMood.Chase;
                    }
                    else if (HorizontalDistance(transform.position, _wanderTarget) < TargetReachedDistance)
                    {
                        Mood = RoamerMood.Idle; // stop and look around for a moment
                        StateTimer = TickTimer.CreateFromSeconds(Runner, Random.Range(_roster.RoamerIdleSeconds.x, _roster.RoamerIdleSeconds.y));
                    }

                    break;

                case RoamerMood.Idle:
                    SlowDown(dt);
                    if (canSeeBalloon)
                    {
                        Mood = RoamerMood.Chase;
                    }
                    else if (StateTimer.Expired(Runner))
                    {
                        StartWandering();
                    }

                    break;

                case RoamerMood.Chase:
                    if (!canSeeBalloon)
                    {
                        StartWandering();
                        break;
                    }

                    WalkToward(balloon.transform.position, _roster.RoamerChaseSpeed, dt, false);
                    if (HorizontalDistance(transform.position, balloon.transform.position) < _roster.RoamerBonkDistance)
                    {
                        Mood = RoamerMood.WindUp;
                        StateTimer = TickTimer.CreateFromSeconds(Runner, _roster.RoamerWindUpSeconds);
                    }

                    break;

                case RoamerMood.WindUp:
                    SlowDown(dt);
                    if (balloon != null)
                    {
                        TurnToward(balloon.transform.position - transform.position, dt);
                    }

                    if (StateTimer.Expired(Runner))
                    {
                        Mood = RoamerMood.Slap;
                        StateTimer = TickTimer.CreateFromSeconds(Runner, _roster.RoamerSlapSwingSeconds);
                    }

                    break;

                case RoamerMood.Slap:
                    SlowDown(dt);
                    if (StateTimer.Expired(Runner))
                    {
                        Slap(balloon); // the hand meets the balloon at the end of the swing
                    }

                    break;

                case RoamerMood.Cooldown:
                    WalkToward(_wanderTarget, _roster.RoamerWalkSpeed, dt, true);
                    if (HorizontalDistance(transform.position, _wanderTarget) < TargetReachedDistance)
                    {
                        PickWanderTarget();
                    }

                    if (StateTimer.Expired(Runner))
                    {
                        Mood = RoamerMood.Wander;
                    }

                    break;
            }
        }

        public override void Render()
        {
            if (CharacterIndex != _shownCharacter)
            {
                ApplyCharacter();
            }

            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            _velocity = (transform.position - _lastPosition) / dt;
            _lastPosition = transform.position;

            float rawSpeed = new Vector2(_velocity.x, _velocity.z).magnitude;
            _shownSpeed = Mathf.Lerp(_shownSpeed, rawSpeed, 1f - Mathf.Exp(-8f * dt)); // network interpolation is jittery
            CharacterAnimator rig = _visual != null ? _visual.Animation : null;
            if (rig != null)
            {
                AnimateRig(rig);
            }
            else
            {
                AnimateBody(dt);
            }

            if (_alert != null)
            {
                _alert.gameObject.SetActive(Mood == RoamerMood.WindUp || Mood == RoamerMood.Slap || Mood == RoamerMood.Chase);
                _alert.text = Mood == RoamerMood.Chase ? "?" : "!";
                Camera view = Camera.main;
                if (view != null)
                {
                    _alert.transform.rotation = Quaternion.LookRotation(_alert.transform.position - view.transform.position);
                }
            }
        }

        /// <summary>
        /// Rigged character: Idle/Walk from its ground speed, the WindUp clip (held) stretched over the wind-up timer, and
        /// the Slap clip timed so the hand lands when the hit is applied. The placeholder arm is hidden.
        /// </summary>
        private void AnimateRig(CharacterAnimator rig)
        {
            SetSlapArmVisible(false);
            if (_visualRoot != null)
            {
                _visualRoot.localPosition = Vector3.zero;
                _visualRoot.localRotation = Quaternion.identity;
                _visualRoot.localScale = Vector3.one;
            }

            rig.SetSpeed(_shownSpeed);
            if (Mood == _animatedMood)
            {
                return;
            }

            _animatedMood = Mood;
            switch (Mood)
            {
                case RoamerMood.WindUp:
                    rig.Play(CharacterAction.WindUp, _roster != null ? _roster.RoamerWindUpSeconds : 0.8f, true);
                    break;
                case RoamerMood.Slap:
                    rig.Play(CharacterAction.Slap, _roster != null ? _roster.RoamerSlapSwingSeconds : 0.22f);
                    break;
                default:
                    if (rig.CurrentAction == CharacterAction.WindUp)
                    {
                        rig.Stop(); // called off (new run): lower the hand
                    }

                    break;
            }
        }

        private void SetSlapArmVisible(bool isVisible)
        {
            if (_slapHand != null && _slapHand.gameObject.activeSelf != isVisible)
            {
                _slapHand.gameObject.SetActive(isVisible);
            }

            if (_slapArm != null && _slapArm.gameObject.activeSelf != isVisible)
            {
                _slapArm.gameObject.SetActive(isVisible);
            }
        }

        /// <summary>
        /// Static model fallback. Walk cycle (gentle stride bob, hip sway, forward lean, arm swing), idle breathing and look-around, and the
        /// slap: hand raised behind during the wind-up, swung across the front, then a follow-through back to rest.
        /// </summary>
        private void AnimateBody(float dt)
        {
            if (_visualRoot == null)
            {
                return;
            }

            SetSlapArmVisible(true);
            float h = _bodyHeight;
            float walkSpeed = _roster != null ? _roster.RoamerWalkSpeed : 2f;
            float gait = Mathf.Clamp01(_shownSpeed / Mathf.Max(walkSpeed, 0.1f)); // 0 standing .. 1 walking (more when chasing)
            float strideLength = 0.45f * h;
            _stridePhase = Mathf.Repeat(_stridePhase + _shownSpeed * dt / strideLength, 1f);
            float stride = _stridePhase * Mathf.PI * 2f;
            float t = Time.time;

            // Body: a soft two-beats-per-stride bob (no hard bounce), side sway, lean into the walk, idle breathing.
            float bob = (1f - Mathf.Cos(stride * 2f)) * 0.5f * 0.018f * h * Mathf.Min(gait, 1.3f);
            float sway = Mathf.Sin(stride) * 3f * Mathf.Min(gait, 1f);
            float lean = 5f * Mathf.Min(gait, 1.5f);
            float twist = 0f;
            float breathe = 1f + Mathf.Sin(t * 1.7f) * 0.012f * (1f - Mathf.Min(gait, 1f));
            if (Mood == RoamerMood.Idle)
            {
                twist = Mathf.Sin(t * 0.8f) * 18f; // looking around the yard
            }

            // Slap hand, in visual-root space (h = body height). Rest swings opposite the stride.
            var shoulder = new Vector3(0.24f * h, 0.72f * h, 0f);
            var rest = new Vector3(0.3f * h, 0.42f * h, Mathf.Sin(stride) * 0.09f * h * Mathf.Min(gait, 1.2f));
            var raised = new Vector3(0.36f * h, 0.98f * h, -0.22f * h);
            var strike = new Vector3(0.04f * h, 0.78f * h, 0.52f * h);
            var follow = new Vector3(-0.2f * h, 0.55f * h, 0.34f * h);
            Vector3 hand = rest;

            float remaining = StateTimer.RemainingTime(Runner) ?? 0f;
            switch (Mood)
            {
                case RoamerMood.WindUp:
                {
                    float windUp = Mathf.Max(_roster != null ? _roster.RoamerWindUpSeconds : 0.8f, 0.01f);
                    float p = Smooth(Mathf.Clamp01((1f - remaining / windUp) * 2.5f)); // hand up in the first 40 %, then hold
                    hand = Vector3.Lerp(rest, raised, p) + Vector3.up * (Mathf.Sin(t * 22f) * 0.01f * h * p); // quiver
                    twist = -22f * p;
                    lean = Mathf.Lerp(lean, -4f, p);
                    break;
                }

                case RoamerMood.Slap:
                {
                    float swing = Mathf.Max(_roster != null ? _roster.RoamerSlapSwingSeconds : 0.22f, 0.01f);
                    float p = Mathf.Clamp01(1f - remaining / swing);
                    p *= p; // accelerates into the hit
                    hand = Arc(raised, strike, p, 0.12f * h);
                    twist = Mathf.Lerp(-22f, 20f, p);
                    lean = Mathf.Lerp(-4f, 9f, p);
                    break;
                }

                case RoamerMood.Cooldown:
                {
                    float cooldown = _roster != null ? _roster.RoamerBonkCooldown : 7f;
                    float since = cooldown - remaining;
                    if (since < FollowThroughSeconds)
                    {
                        float p = since / FollowThroughSeconds;
                        hand = p < 0.35f ? Vector3.Lerp(strike, follow, Smooth(p / 0.35f)) : Vector3.Lerp(follow, rest, Smooth((p - 0.35f) / 0.65f));
                        twist = Mathf.Lerp(20f, 0f, Smooth(p));
                        lean = Mathf.Lerp(9f, lean, Smooth(p));
                    }

                    break;
                }
            }

            _visualRoot.localPosition = new Vector3(0f, bob, 0f);
            _visualRoot.localRotation = Quaternion.Euler(lean, twist, sway);
            _visualRoot.localScale = new Vector3(1f, breathe, 1f);

            if (_slapHand != null)
            {
                _slapHand.localPosition = hand;
                _slapHand.localScale = Vector3.one * (0.17f * h);
            }

            if (_slapArm != null)
            {
                Vector3 along = hand - shoulder;
                float length = along.magnitude;
                _slapArm.localPosition = (shoulder + hand) * 0.5f;
                _slapArm.localRotation = length > 0.001f ? Quaternion.FromToRotation(Vector3.up, along / length) : Quaternion.identity;
                float thickness = 0.07f * h;
                _slapArm.localScale = new Vector3(thickness, length * 0.5f, thickness); // the cylinder primitive is 2 m tall
            }
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RpcReroll()
        {
            int previous = CharacterIndex;
            int next = RandomCharacter();
            if (_roster != null && _roster.Count > 1 && next == previous)
            {
                next = (next + 1) % _roster.Count; // always a fresh face
            }

            CharacterIndex = next;
            StartWandering();
        }

        private void Slap(NetworkBalloon balloon)
        {
            Mood = RoamerMood.Cooldown;
            StateTimer = TickTimer.CreateFromSeconds(Runner, _roster.RoamerBonkCooldown);
            PickWanderTarget();

            if (balloon == null || !IsBalloonInReach(balloon, _roster.RoamerBonkDistance * 1.6f))
            {
                return; // the crew saved it during the wind-up
            }

            // Along the swing (forward and across) blended with straight away from the giant, plus lift.
            Vector3 away = balloon.transform.position - transform.position;
            away.y = 0f;
            away = away.sqrMagnitude > 0.01f ? away.normalized : transform.forward;
            Vector3 swingDirection = (transform.forward - transform.right * 0.35f).normalized;
            Vector3 horizontal = (away + swingDirection).normalized;
            Vector3 velocity = horizontal * _roster.RoamerBonkSpeed + Vector3.up * (_roster.RoamerBonkSpeed * _roster.RoamerSlapLift);
            balloon.RequestHit(PlayerRef.None, velocity); // nobody's hit: it doesn't count for the pass rule

            GameState gameState = GameState.Instance;
            if (gameState != null)
            {
                gameState.Announce($"{DisplayName} slapped the balloon!");
            }
        }

        private bool IsBalloonInReach(NetworkBalloon balloon, float? horizontalLimit = null)
        {
            GameState gameState = GameState.Instance;
            if (gameState != null && !gameState.IsBalloonLive)
            {
                return false;
            }

            Vector3 balloonPosition = balloon.transform.position;
            float limit = horizontalLimit ?? _roster.RoamerNoticeDistance;
            return balloonPosition.y < _roster.RoamerReachHeight && HorizontalDistance(transform.position, balloonPosition) < limit
                   && InsideBounds(balloonPosition, 0f) && CanStandNear(balloonPosition);
        }

        /// <summary>
        /// False when the balloon is so deep inside a no-go circle (start stage, pool, tree...) that the giant could
        /// never get within slap distance; otherwise it would walk on the spot against the circle's edge.
        /// </summary>
        private bool CanStandNear(Vector3 balloonPosition)
        {
            float bodyRadius = 0.6f * _roster.RoamerScale * 0.5f;
            foreach (Vector3 circle in _avoidCircles)
            {
                float unreachable = circle.z + bodyRadius - _roster.RoamerBonkDistance * 0.9f;
                if (unreachable > 0f && HorizontalDistance(balloonPosition, new Vector3(circle.x, 0f, circle.y)) < unreachable)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Turns toward the target and walks along its own facing, so it curves round instead of sliding sideways;
        /// it eases up to speed, turns on the spot when the target is behind it, and slows down as it arrives.
        /// </summary>
        private void WalkToward(Vector3 target, float maxSpeed, float dt, bool slowOnArrival)
        {
            Vector3 direction = target - transform.position;
            direction.y = 0f;
            float distance = direction.magnitude;
            if (distance < 0.05f)
            {
                SlowDown(dt);
                return;
            }

            direction /= distance;
            TurnToward(direction, dt);
            float facing = Vector3.Dot(transform.forward, direction);
            float wanted = maxSpeed * Mathf.Clamp01((facing - 0.3f) / 0.6f);
            if (slowOnArrival)
            {
                wanted *= Mathf.Clamp01(distance / 2.5f) * 0.8f + 0.2f;
            }

            _speed = Mathf.MoveTowards(_speed, wanted, _roster.RoamerAcceleration * dt);
            Move(dt);
        }

        private void SlowDown(float dt)
        {
            _speed = Mathf.MoveTowards(_speed, 0f, _roster.RoamerAcceleration * 1.5f * dt);
            Move(dt);
        }

        private void Move(float dt)
        {
            if (_speed <= 0.0001f)
            {
                return;
            }

            Vector3 next = transform.position + transform.forward * (_speed * dt);
            transform.position = Steer(next);
        }

        /// <summary>Pushes a position out of the obstacle circles and back inside the yard.</summary>
        private Vector3 Steer(Vector3 position)
        {
            float bodyRadius = 0.6f * _roster.RoamerScale * 0.5f;
            foreach (Vector3 circle in _avoidCircles)
            {
                var center = new Vector3(circle.x, position.y, circle.y);
                Vector3 offset = position - center;
                float minimum = circle.z + bodyRadius;
                if (offset.sqrMagnitude < minimum * minimum)
                {
                    offset = offset.sqrMagnitude > 0.0001f ? offset.normalized : Vector3.right;
                    position = center + offset * minimum;
                }
            }

            position.x = Mathf.Clamp(position.x, _bounds.x, _bounds.z);
            position.z = Mathf.Clamp(position.z, _bounds.y, _bounds.w);
            return position;
        }

        private void TurnToward(Vector3 direction, float dt)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }

            Quaternion target = Quaternion.LookRotation(direction.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, _roster.RoamerTurnSpeed * dt);
        }

        private void StartWandering()
        {
            Mood = RoamerMood.Wander;
            PickWanderTarget();
        }

        private void PickWanderTarget()
        {
            if (_wanderPoints == null || _wanderPoints.Length == 0)
            {
                _wanderTarget = transform.position;
                return;
            }

            Vector3 candidate = _wanderPoints[Random.Range(0, _wanderPoints.Length)];
            for (int attempt = 0; attempt < 3 && _wanderPoints.Length > 1 && HorizontalDistance(candidate, transform.position) < 3f; attempt++)
            {
                candidate = _wanderPoints[Random.Range(0, _wanderPoints.Length)];
            }

            _wanderTarget = candidate;
        }

        private void ApplyCharacter()
        {
            _shownCharacter = CharacterIndex;
            CharacterDefinition character = _roster != null ? _roster.Get(CharacterIndex) : null;
            if (character != null && _visual != null)
            {
                _bodyHeight = character.Height * _roster.RoamerScale;
                _visual.Show(character, _bodyHeight);
            }
        }

        private bool InsideBounds(Vector3 position, float margin) =>
            position.x > _bounds.x - margin && position.x < _bounds.z + margin && position.z > _bounds.y - margin && position.z < _bounds.w + margin;

        private int RandomCharacter() => _roster != null && _roster.Count > 0 ? Random.Range(0, _roster.Count) : 0;

        private static float Smooth(float x) => x * x * (3f - 2f * x);

        /// <summary>Straight line from a to b bowed outward (forward) by the given amount at the middle.</summary>
        private static Vector3 Arc(Vector3 a, Vector3 b, float p, float bow) =>
            Vector3.Lerp(a, b, p) + new Vector3(0.6f, 0f, 1f).normalized * (Mathf.Sin(p * Mathf.PI) * bow);

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
