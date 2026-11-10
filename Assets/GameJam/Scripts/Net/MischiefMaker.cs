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
    /// as nobody's hit, then cools down. When the balloon isn't in reach during a run it goes after a player standing near,
    /// draws its leg back (the telegraph, "!") and kicks them: the one knockdown left in easy mode, a real ragdoll. Walking
    /// into it only shoves you, and you can climb it. A thrown item knocks it
    /// down for RoamerKnockdownSeconds: it falls as a ragdoll (GiantRagdoll, local on every client) with dizzy stars over its
    /// head, gets up where it landed, then cools down (ignores the balloon) for RoamerBonkCooldown.
    /// The master client simulates it (Master Client Object); everyone else sees it through its NetworkTransform, and
    /// the walk cycle and slap swing are animated locally from its speed and networked mood.
    /// </summary>
    public sealed class MischiefMaker : NetworkBehaviour, IKnockbackSource
    {
        private const float TargetReachedDistance = 1.2f;
        private const float StallSeconds = 4f;       // no progress for this long = stuck against an avoid circle
        private const float StallProgress = 0.5f;   // metres closer that count as progress
        private const float PathMargin = 0.3f;      // extra clearance a wander path keeps from set pieces
        private const float FollowThroughSeconds = 0.45f;

        [SerializeField] private CharacterRoster _roster;
        [SerializeField] private CharacterVisual _visual;
        [SerializeField] private Transform _visualRoot;
        [SerializeField] private TextMesh _alert;
        [Tooltip("The giant's slap mitt and the arm joining it to the shoulder (visual only, no colliders).")]
        [SerializeField] private Transform _slapHand;
        [SerializeField] private Transform _slapArm;
        [Tooltip("Stars circling its head while it is knocked down.")]
        [SerializeField] private DizzyStars _stars;
        [Tooltip("Plays the kick (swish, then the thump as the foot lands) when the swing starts.")]
        [SerializeField] private AudioSource _kickAudio;

        [Header("Yard (set by the builder)")]
        [Tooltip("Open lawn spots it wanders between.")]
        [SerializeField] private Vector3[] _wanderPoints = System.Array.Empty<Vector3>();
        [Tooltip("Circles it steers around: x, z, radius.")]
        [SerializeField] private Vector3[] _avoidCircles = System.Array.Empty<Vector3>();
        [Tooltip("Yard bounds it stays inside: min x, min z, max x, max z.")]
        [SerializeField] private Vector4 _bounds = new Vector4(-44f, -20f, 44f, 86f);

        // Authority-side simulation.
        private Vector3 _wanderTarget;   // the spot it is walking to now (a hop on the way, or the destination)
        private Vector3 _wanderGoal;     // where the current route ends
        private readonly System.Collections.Generic.List<int> _route = new System.Collections.Generic.List<int>();
        private System.Collections.Generic.List<int>[] _links;
        private float _speed;
        private float _progressDeadline = -1f;
        private float _progressMark;
        private RoamerMood _progressMood;

        // Local presentation.
        private Vector3 _lastPosition;
        private Vector3 _velocity;
        private float _shownSpeed;
        private float _stridePhase;
        private float _bodyHeight = 3.8f;
        private int _shownCharacter = -1;
        private RoamerMood _animatedMood = RoamerMood.Wander;
        private RoamerMood _shownMood = RoamerMood.Wander;
        private RoamerMood _soundedMood = RoamerMood.Wander;
        private Collider _bodyCollider;
        private GiantRagdoll _ragdoll;
        private float _tipOver;  // 0 standing .. 1 lying (static-model fallback)

        /// <summary>Every giant in the yard (one per roster character: Jman and Squoof).</summary>
        public static readonly System.Collections.Generic.List<MischiefMaker> Active = new System.Collections.Generic.List<MischiefMaker>();

        /// <summary>The first giant (kept for tools and tests that only need one).</summary>
        public static MischiefMaker Instance => Active.Count > 0 ? Active[0] : null;

        /// <summary>How many characters the roster has (the bootstrap spawns one giant for each).</summary>
        public int CharacterCount => _roster != null ? _roster.Count : 0;

        [Networked] public int CharacterIndex { get; private set; }

        /// <summary>Fixed character for this giant, as roster index + 1 (0 = pick one at random every run).</summary>
        [Networked] private int AssignedCharacter { get; set; }

        [Networked] public RoamerMood Mood { get; private set; }

        [Networked] private TickTimer StateTimer { get; set; }

        /// <summary>The player it is going to kick (ChasePlayer, KickWindUp, Kick).</summary>
        [Networked] public PlayerRef KickTarget { get; private set; }

        /// <summary>The hit that knocked it down (the item's velocity), so every client flings its ragdoll the same way.</summary>
        [Networked] private Vector3 KnockVelocity { get; set; }

        public bool IsKnockedDown => Mood == RoamerMood.KnockedDown;

        public bool KnocksPlayers => !IsKnockedDown; // a giant lying on the lawn bowls nobody over

        public string DisplayName => _roster != null && _roster.Get(CharacterIndex) != null ? _roster.Get(CharacterIndex).DisplayName : "The giant";

        public void Configure(CharacterRoster roster, CharacterVisual visual, Transform visualRoot, TextMesh alert, Transform slapHand, Transform slapArm,
            DizzyStars stars, Vector3[] wanderPoints, Vector3[] avoidCircles, Vector4 bounds)
        {
            _stars = stars;
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

        public void ConfigureSounds(AudioSource kickAudio) => _kickAudio = kickAudio;

        public Vector3 PointVelocity(Vector3 worldPoint)
        {
            // Even standing still it shoves you a little, so walking into a giant always does something.
            Vector3 away = worldPoint - transform.position;
            away.y = 0f;
            Vector3 shove = away.sqrMagnitude > 0.001f ? away.normalized * 1.5f : Vector3.zero;
            return _velocity + shove;
        }

        /// <summary>A thrown item hit it: knock it down (any client; the master client decides).</summary>
        public void RequestKnockDown(Vector3 itemVelocity) => RpcKnockDown(itemVelocity);

        /// <summary>Picks a new random character (called at every run start).</summary>
        public void Reroll() => RpcReroll();

        /// <summary>
        /// Spawn-time wiring (onBeforeSpawned): a different yard from the one baked into the prefab (the tutorial's pen).
        /// </summary>
        public void ConfigureYard(Vector3[] wanderPoints, Vector3[] avoidCircles, Vector4 bounds)
        {
            _wanderPoints = wanderPoints;
            _avoidCircles = avoidCircles ?? System.Array.Empty<Vector3>();
            _bounds = bounds;
            _links = null;
        }

        /// <summary>Spawn-time wiring (call from Runner.Spawn's onBeforeSpawned): this giant is always that character.</summary>
        public void AssignCharacter(int rosterIndex) => AssignedCharacter = rosterIndex + 1;

        public override void Spawned()
        {
            if (!Active.Contains(this))
            {
                Active.Add(this);
            }

            _lastPosition = transform.position;
            _bodyCollider = GetComponent<Collider>();
            if (_stars != null)
            {
                _stars.Hide();
            }

            if (HasStateAuthority)
            {
                CharacterIndex = AssignedCharacter > 0 ? AssignedCharacter - 1 : RandomCharacter();
                StartWandering();
            }

            ApplyCharacter();
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            Active.Remove(this);
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
                    else if (TryPickKickTarget())
                    {
                        Mood = RoamerMood.ChasePlayer;
                    }
                    else if (HorizontalDistance(transform.position, _wanderTarget) < TargetReachedDistance && NextHop())
                    {
                        // a hop on the way: keep walking
                    }
                    else if (HorizontalDistance(transform.position, _wanderTarget) < TargetReachedDistance)
                    {
                        Mood = RoamerMood.Idle; // stop and look around for a moment
                        StateTimer = TickTimer.CreateFromSeconds(Runner, Random.Range(_roster.RoamerIdleSeconds.x, _roster.RoamerIdleSeconds.y));
                    }
                    else if (IsStalled(_wanderTarget))
                    {
                        PickWanderTarget(); // boxed in by set pieces: head somewhere else instead of walking on the spot
                    }

                    break;

                case RoamerMood.Idle:
                    SlowDown(dt);
                    if (canSeeBalloon)
                    {
                        Mood = RoamerMood.Chase;
                    }
                    else if (TryPickKickTarget())
                    {
                        Mood = RoamerMood.ChasePlayer;
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
                    else if (IsStalled(balloon.transform.position))
                    {
                        // Can't get to it from here: give up for a while (Cooldown ignores the balloon) and wander off.
                        Mood = RoamerMood.Cooldown;
                        StateTimer = TickTimer.CreateFromSeconds(Runner, _roster.RoamerBonkCooldown);
                        PickWanderTarget();
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

                case RoamerMood.ChasePlayer:
                {
                    NetworkAvatar target = FindAvatar(KickTarget);
                    if (canSeeBalloon)
                    {
                        Mood = RoamerMood.Chase; // the balloon always comes first
                        break;
                    }

                    if (!CanKick(target, _roster.RoamerKickNoticeDistance * 1.3f))
                    {
                        StartWandering(); // gone, out of reach, already down, or the run ended
                        break;
                    }

                    Vector3 at = target.BodyPosition;
                    WalkToward(at, _roster.RoamerChaseSpeed, dt, true);
                    if (HorizontalDistance(transform.position, at) < _roster.RoamerKickReach * 1.25f)
                    {
                        Mood = RoamerMood.KickWindUp;
                        StateTimer = TickTimer.CreateFromSeconds(Runner, _roster.RoamerKickWindUpSeconds);
                    }
                    else if (IsStalled(at))
                    {
                        Mood = RoamerMood.Cooldown; // can't get to them: leave them be for a while
                        StateTimer = TickTimer.CreateFromSeconds(Runner, _roster.RoamerKickCooldown);
                        PickWanderTarget();
                    }

                    break;
                }

                case RoamerMood.KickWindUp:
                {
                    NetworkAvatar target = FindAvatar(KickTarget);
                    if (target != null)
                    {
                        TurnToward(target.BodyPosition - transform.position, dt);
                        if (HorizontalDistance(transform.position, target.BodyPosition) < _roster.RoamerKickReach * 0.9f)
                        {
                            _speed = 0f; // plants its foot: never walks into them (that would be the bump's shove)
                        }
                    }

                    SlowDown(dt);
                    if (StateTimer.Expired(Runner))
                    {
                        Mood = RoamerMood.Kick;
                        StateTimer = TickTimer.CreateFromSeconds(Runner, _roster.RoamerKickSwingSeconds);
                    }

                    break;
                }

                case RoamerMood.Kick:
                    SlowDown(dt);
                    if (StateTimer.Expired(Runner))
                    {
                        Kick(FindAvatar(KickTarget)); // the foot lands at the end of the swing
                    }

                    break;

                case RoamerMood.KnockedDown:
                    _speed = 0f;
                    if (StateTimer.Expired(Runner))
                    {
                        GetUp();
                    }

                    break;

                case RoamerMood.Cooldown:
                    WalkToward(_wanderTarget, _roster.RoamerWalkSpeed, dt, true);
                    if (HorizontalDistance(transform.position, _wanderTarget) < TargetReachedDistance && NextHop())
                    {
                        // a hop on the way: keep walking
                    }
                    else if (HorizontalDistance(transform.position, _wanderTarget) < TargetReachedDistance || IsStalled(_wanderTarget))
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

            ShowKnockdown();
            if (Mood != _soundedMood)
            {
                if (Mood == RoamerMood.Kick && _kickAudio != null)
                {
                    _kickAudio.Play(); // everyone near hears it (3D), the swing lasts as long as the swish
                }

                _soundedMood = Mood;
            }

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
                bool chasing = Mood == RoamerMood.Chase || Mood == RoamerMood.ChasePlayer;
                bool striking = Mood == RoamerMood.WindUp || Mood == RoamerMood.Slap || Mood == RoamerMood.KickWindUp || Mood == RoamerMood.Kick;
                _alert.gameObject.SetActive(chasing || striking);
                _alert.text = chasing ? "?" : "!";
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

            rig.SetSpeed(IsKnockedDown ? 0f : _shownSpeed);
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
                case RoamerMood.KickWindUp:
                    if (rig.HasClip(CharacterAction.Kick))
                    {
                        // One clip for both: leg drawn back over the wind-up, swung through so the foot lands on the hit.
                        rig.Play(CharacterAction.Kick, _roster != null ? _roster.RoamerKickWindUpSeconds + _roster.RoamerKickSwingSeconds : 0.95f);
                    }
                    else
                    {
                        rig.Play(CharacterAction.WindUp, _roster != null ? _roster.RoamerKickWindUpSeconds : 0.7f, true);
                    }

                    break;
                case RoamerMood.Kick:
                    if (!rig.HasClip(CharacterAction.Kick))
                    {
                        rig.Play(CharacterAction.Slap, _roster != null ? _roster.RoamerKickSwingSeconds : 0.25f); // no Kick clip yet
                    }

                    break;
                default:
                    if (rig.CurrentAction == CharacterAction.WindUp)
                    {
                        rig.Stop(); // called off (new run): lower the hand or the leg
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

            SetSlapArmVisible(!IsKnockedDown);
            if (TipOver(dt))
            {
                return;
            }

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
            bool kicking = Mood == RoamerMood.KickWindUp || Mood == RoamerMood.Kick; // no leg rig here: the same lunge as the slap
            RoamerMood pose = Mood == RoamerMood.KickWindUp ? RoamerMood.WindUp : Mood == RoamerMood.Kick ? RoamerMood.Slap : Mood;
            switch (pose)
            {
                case RoamerMood.WindUp:
                {
                    float windUp = Mathf.Max(_roster == null ? 0.8f : kicking ? _roster.RoamerKickWindUpSeconds : _roster.RoamerWindUpSeconds, 0.01f);
                    float p = Smooth(Mathf.Clamp01((1f - remaining / windUp) * 2.5f)); // hand up in the first 40 %, then hold
                    hand = Vector3.Lerp(rest, raised, p) + Vector3.up * (Mathf.Sin(t * 22f) * 0.01f * h * p); // quiver
                    twist = -22f * p;
                    lean = Mathf.Lerp(lean, -4f, p);
                    break;
                }

                case RoamerMood.Slap:
                {
                    float swing = Mathf.Max(_roster == null ? 0.22f : kicking ? _roster.RoamerKickSwingSeconds : _roster.RoamerSlapSwingSeconds, 0.01f);
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
        private void RpcKnockDown(Vector3 itemVelocity)
        {
            if (IsKnockedDown || _roster == null)
            {
                return;
            }

            Mood = RoamerMood.KnockedDown;
            StateTimer = TickTimer.CreateFromSeconds(Runner, _roster.RoamerKnockdownSeconds);
            Vector3 along = new Vector3(itemVelocity.x, 0f, itemVelocity.z);
            along = along.sqrMagnitude > 0.0001f ? along.normalized : -transform.forward;
            KnockVelocity = along * _roster.RoamerKnockdownFling + Vector3.up * (_roster.RoamerKnockdownFling * 0.35f);
            _speed = 0f;

            GameState gameState = GameState.Instance;
            if (gameState != null)
            {
                gameState.Announce($"{DisplayName} is seeing stars!");
            }
        }

        /// <summary>
        /// Knockdown over: stand up where the ragdoll's hips landed (on this screen; the other screens' falls end close by
        /// and blend to it), then cool down so the crew gets a breather from it.
        /// </summary>
        private void GetUp()
        {
            if (_ragdoll != null && _ragdoll.IsDown)
            {
                Vector3 landed = Steer(_ragdoll.HipsGroundPoint(transform.position.y));
                if (HorizontalDistance(landed, transform.position) < 1.2f * _bodyHeight) // a fall can't carry it further than that
                {
                    transform.position = landed;
                }
            }

            Mood = RoamerMood.Cooldown;
            StateTimer = TickTimer.CreateFromSeconds(Runner, _roster.RoamerBonkCooldown);
            PickWanderTarget();
        }

        /// <summary>
        /// Every client: on the knockdown the ragdoll falls (rigged models) and the stars come out; on recovery it gets up.
        /// The body capsule is off while down, so nobody bumps into or climbs an invisible standing giant.
        /// </summary>
        private void ShowKnockdown()
        {
            bool isDown = IsKnockedDown;
            bool wasDown = _shownMood == RoamerMood.KnockedDown;
            _shownMood = Mood;
            if (_bodyCollider != null && _bodyCollider.enabled == isDown)
            {
                _bodyCollider.enabled = !isDown;
            }

            if (isDown == wasDown)
            {
                return;
            }

            CharacterAnimator rig = _visual != null ? _visual.Animation : null;
            if (isDown)
            {
                if (rig != null)
                {
                    rig.Stop(); // drop a raised slap hand
                    _ragdoll = GiantRagdoll.For(rig.gameObject, _bodyHeight);
                    _ragdoll.Fall(KnockVelocity);
                }

                if (_stars != null)
                {
                    bool onHead = _ragdoll != null && _ragdoll.IsValid && _ragdoll.Head != null;
                    _stars.Show(onHead ? _ragdoll.Head : _visualRoot, _bodyHeight, onHead ? 0.22f * _bodyHeight : 0.35f * _bodyHeight);
                }
            }
            else
            {
                if (_ragdoll != null)
                {
                    _ragdoll.GetUp(_roster != null ? _roster.RoamerGetUpSeconds : 0.8f);
                }

                if (_stars != null)
                {
                    _stars.Hide();
                }
            }
        }

        /// <summary>Static-model fallback for the knockdown: the whole model tips over along the hit and lies there.</summary>
        private bool TipOver(float dt)
        {
            float getUp = _roster != null ? _roster.RoamerGetUpSeconds : 0.8f;
            _tipOver = IsKnockedDown ? Mathf.Min(1f, _tipOver + dt / 0.45f) : Mathf.Max(0f, _tipOver - dt / getUp);
            if (_tipOver <= 0f)
            {
                return false;
            }

            Vector3 along = transform.InverseTransformDirection(new Vector3(KnockVelocity.x, 0f, KnockVelocity.z));
            Vector3 axis = Vector3.Cross(Vector3.up, along.sqrMagnitude > 0.0001f ? along.normalized : Vector3.back);
            float eased = IsKnockedDown ? 1f - (1f - _tipOver) * (1f - _tipOver) : Smooth(_tipOver);
            _visualRoot.localPosition = Vector3.zero;
            _visualRoot.localRotation = Quaternion.AngleAxis(85f * eased, axis);
            _visualRoot.localScale = Vector3.one;
            return true;
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RpcReroll()
        {
            if (AssignedCharacter > 0)
            {
                StartWandering(); // a fixed character keeps its face; just set off again
                return;
            }

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

        /// <summary>
        /// End of the kick swing: if the player is still within 1.4 x reach (a real dodge gets clear), they are launched
        /// away from the giant and ragdolled for everyone.
        /// </summary>
        private void Kick(NetworkAvatar target)
        {
            Mood = RoamerMood.Cooldown;
            StateTimer = TickTimer.CreateFromSeconds(Runner, _roster.RoamerKickCooldown);
            PickWanderTarget();
            KickTarget = PlayerRef.None;

            if (!CanKick(target, _roster.RoamerKickReach * 1.4f))
            {
                return; // dodged
            }

            Vector3 away = target.BodyPosition - transform.position;
            away.y = 0f;
            away = away.sqrMagnitude > 0.01f ? away.normalized : transform.forward;
            Vector3 direction = (away + Vector3.up * _roster.RoamerKickUpward).normalized;
            target.SendGiantKick(direction, _roster.RoamerKickForce, _roster.RoamerKickKnockdownSeconds);

            GameState gameState = GameState.Instance;
            if (gameState != null)
            {
                gameState.Announce($"{DisplayName} punted Player {Mathf.Max(1, target.Owner.PlayerId)}!");
            }
        }

        /// <summary>Wander or Idle: the nearest player it could kick, if any (sets KickTarget).</summary>
        private bool TryPickKickTarget()
        {
            NetworkAvatar best = null;
            float bestDistance = float.MaxValue;
            foreach (NetworkAvatar avatar in NetworkAvatar.All)
            {
                if (!CanKick(avatar, _roster.RoamerKickNoticeDistance))
                {
                    continue;
                }

                float distance = HorizontalDistance(transform.position, avatar.BodyPosition);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = avatar;
                }
            }

            if (best == null)
            {
                return false;
            }

            KickTarget = best.Owner;
            return true;
        }

        /// <summary>
        /// A player it may kick: during a run only (never in the lobby, the countdown or the tutorial), on their feet, low
        /// enough to reach, inside the yard, and not deep inside a no-go circle it can't walk into.
        /// </summary>
        private bool CanKick(NetworkAvatar avatar, float horizontalLimit)
        {
            if (avatar == null || avatar.Object == null || !avatar.Object.IsValid || avatar.IsDown || horizontalLimit <= 0f)
            {
                return false;
            }

            GameState gameState = GameState.Instance;
            if (gameState == null || !gameState.IsBalloonLive || GameState.IsPractice)
            {
                return false;
            }

            Vector3 body = avatar.BodyPosition;
            return body.y - transform.position.y < _roster.RoamerKickHeight
                   && HorizontalDistance(transform.position, body) < horizontalLimit
                   && InsideBounds(body, 0f) && CanStandNear(body, _roster.RoamerKickReach);
        }

        private static NetworkAvatar FindAvatar(PlayerRef player)
        {
            if (player == PlayerRef.None)
            {
                return null;
            }

            foreach (NetworkAvatar avatar in NetworkAvatar.All)
            {
                if (avatar != null && avatar.Owner == player)
                {
                    return avatar;
                }
            }

            return null;
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
                   && InsideBounds(balloonPosition, 0f) && CanStandNear(balloonPosition, _roster.RoamerBonkDistance);
        }

        /// <summary>
        /// False when the target (balloon or player) is so deep inside a no-go circle (start stage, pool, tree...) that the
        /// giant could never get within reach; otherwise it would walk on the spot against the circle's edge.
        /// </summary>
        private bool CanStandNear(Vector3 targetPosition, float reach)
        {
            float bodyRadius = 0.6f * _roster.RoamerScale * 0.5f;
            foreach (Vector3 circle in _avoidCircles)
            {
                float unreachable = circle.z + bodyRadius - reach * 0.9f;
                if (unreachable > 0f && HorizontalDistance(targetPosition, new Vector3(circle.x, 0f, circle.y)) < unreachable)
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

        /// <summary>
        /// True when the giant hasn't got StallProgress closer to its target for StallSeconds while trying to walk there:
        /// it is pressed against an avoid circle (set pieces can box a target in). Restarts when the mood changes.
        /// </summary>
        private bool IsStalled(Vector3 target)
        {
            float distance = HorizontalDistance(transform.position, target);
            float now = Time.time;
            if (_progressDeadline < 0f || Mood != _progressMood || distance < _progressMark - StallProgress)
            {
                _progressMood = Mood;
                _progressMark = distance;
                _progressDeadline = now + StallSeconds;
                return false;
            }

            if (now < _progressDeadline)
            {
                return false;
            }

            _progressDeadline = -1f;
            return true;
        }

        private void StartWandering()
        {
            Mood = RoamerMood.Wander;
            PickWanderTarget();
        }

        /// <summary>
        /// Picks a destination anywhere in the yard and the route there. Set pieces chain into walls (pool, diving board,
        /// clothesline), so it only ever walks straight lines that clear them (HasClearPath) and reaches far spots in hops
        /// over the wander spots, shortest way first. Picking only spots in a straight line kept a giant on whichever side it
        /// was on (the west strip, later the east once it had more spots); now every spot in the yard can be the destination,
        /// far ones and ones away from the other giant likelier, so both roam the whole yard.
        /// </summary>
        private void PickWanderTarget()
        {
            _progressDeadline = -1f;
            _route.Clear();
            if (_wanderPoints == null || _wanderPoints.Length == 0)
            {
                _wanderTarget = _wanderGoal = transform.position;
                return;
            }

            EnsureLinks();
            Vector3 here = transform.position;
            int goal = PickGoal(here);
            float[] toGoal = DistancesTo(goal);

            // First hop: a spot it can walk to straight from here, on the shortest way to the goal.
            int first = -1;
            float best = float.MaxValue;
            for (int i = 0; i < _wanderPoints.Length; i++)
            {
                if (float.IsPositiveInfinity(toGoal[i]) || !HasClearPath(here, _wanderPoints[i]))
                {
                    continue;
                }

                float cost = HorizontalDistance(here, _wanderPoints[i]) + toGoal[i];
                if (cost < best)
                {
                    best = cost;
                    first = i;
                }
            }

            if (first < 0)
            {
                first = Nearest(here); // boxed in: head for the nearest spot and route on from there
            }

            // The rest of the way: follow the shortest-distance links down to the goal.
            for (int at = first, guard = 0; at != goal && guard < _wanderPoints.Length; guard++)
            {
                int next = -1;
                float nextCost = float.MaxValue;
                foreach (int link in _links[at])
                {
                    float cost = HorizontalDistance(_wanderPoints[at], _wanderPoints[link]) + toGoal[link];
                    if (cost < nextCost)
                    {
                        nextCost = cost;
                        next = link;
                    }
                }

                if (next < 0)
                {
                    break;
                }

                _route.Add(next);
                at = next;
            }

            _wanderTarget = _wanderPoints[first];
            _wanderGoal = _wanderPoints[goal];
        }

        /// <summary>Walks on to the next spot of the route, if there is one left.</summary>
        private bool NextHop()
        {
            if (_route.Count == 0)
            {
                return false;
            }

            _wanderTarget = _wanderPoints[_route[0]];
            _route.RemoveAt(0);
            _progressDeadline = -1f;
            return true;
        }

        /// <summary>A destination: any spot but the one it stands at; far ones and ones away from the other giant likelier.</summary>
        private int PickGoal(Vector3 here)
        {
            var weights = new float[_wanderPoints.Length];
            float total = 0f;
            for (int i = 0; i < _wanderPoints.Length; i++)
            {
                float distance = HorizontalDistance(_wanderPoints[i], here);
                if (distance <= 3f)
                {
                    continue;
                }

                float weight = distance;
                foreach (MischiefMaker other in Active)
                {
                    if (other != this && (HorizontalDistance(_wanderPoints[i], other.transform.position) < 15f
                        || HorizontalDistance(_wanderPoints[i], other._wanderGoal) < 15f))
                    {
                        weight *= 0.25f; // the other giant is there or heading there
                    }
                }

                weights[i] = weight;
                total += weight;
            }

            if (total <= 0f)
            {
                return Random.Range(0, _wanderPoints.Length);
            }

            float pick = Random.value * total;
            for (int i = 0; i < weights.Length; i++)
            {
                pick -= weights[i];
                if (weights[i] > 0f && pick <= 0f)
                {
                    return i;
                }
            }

            return weights.Length - 1;
        }

        /// <summary>Which wander spots see each other in a straight line (built once, on the simulating client).</summary>
        private void EnsureLinks()
        {
            if (_links != null && _links.Length == _wanderPoints.Length)
            {
                return;
            }

            _links = new System.Collections.Generic.List<int>[_wanderPoints.Length];
            for (int i = 0; i < _wanderPoints.Length; i++)
            {
                _links[i] = new System.Collections.Generic.List<int>();
                for (int j = 0; j < _wanderPoints.Length; j++)
                {
                    if (i != j && HasClearPath(_wanderPoints[i], _wanderPoints[j]))
                    {
                        _links[i].Add(j);
                    }
                }
            }
        }

        /// <summary>Shortest walking distance from every spot to the goal over the links (Dijkstra; infinity = cut off).</summary>
        private float[] DistancesTo(int goal)
        {
            int n = _wanderPoints.Length;
            var distance = new float[n];
            var done = new bool[n];
            for (int i = 0; i < n; i++)
            {
                distance[i] = float.PositiveInfinity;
            }

            distance[goal] = 0f;
            for (int round = 0; round < n; round++)
            {
                int at = -1;
                for (int i = 0; i < n; i++)
                {
                    if (!done[i] && (at < 0 || distance[i] < distance[at]))
                    {
                        at = i;
                    }
                }

                if (at < 0 || float.IsPositiveInfinity(distance[at]))
                {
                    break;
                }

                done[at] = true;
                foreach (int link in _links[at])
                {
                    float through = distance[at] + HorizontalDistance(_wanderPoints[at], _wanderPoints[link]);
                    if (through < distance[link])
                    {
                        distance[link] = through;
                    }
                }
            }

            return distance;
        }

        private int Nearest(Vector3 position)
        {
            int nearest = 0;
            for (int i = 1; i < _wanderPoints.Length; i++)
            {
                if (HorizontalDistance(position, _wanderPoints[i]) < HorizontalDistance(position, _wanderPoints[nearest]))
                {
                    nearest = i;
                }
            }

            return nearest;
        }

        /// <summary>
        /// True when the straight walk from one spot to another stays outside every avoid circle (padded by the giant's
        /// body and PathMargin). A circle it is already pressed against doesn't count if the walk leads away from it.
        /// </summary>
        private bool HasClearPath(Vector3 from, Vector3 to)
        {
            float body = 0.6f * _roster.RoamerScale * 0.5f;
            var a = new Vector2(from.x, from.z);
            var ab = new Vector2(to.x, to.z) - a;
            float lengthSquared = Mathf.Max(ab.sqrMagnitude, 0.0001f);
            foreach (Vector3 circle in _avoidCircles)
            {
                var c = new Vector2(circle.x, circle.y);
                float limit = circle.z + body;
                bool startsAgainstIt = (a - c).magnitude < limit + PathMargin;
                if (startsAgainstIt && Vector2.Dot(ab, a - c) >= 0f)
                {
                    continue; // already touching it and heading away
                }

                float t = Mathf.Clamp01(Vector2.Dot(c - a, ab) / lengthSquared);
                if ((a + ab * t - c).magnitude < limit + (startsAgainstIt ? 0f : PathMargin))
                {
                    return false;
                }
            }

            return true;
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
