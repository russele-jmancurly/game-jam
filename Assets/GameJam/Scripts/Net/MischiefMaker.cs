using Fusion;
using GameJam.Core;
using GameJam.Gameplay;
using UnityEngine;

namespace GameJam.Net
{
    /// <summary>
    /// A giant version of a random character (re-rolled every run) loose in the yard. It wanders between open spots;
    /// when the balloon drifts close and low it waddles over, winds up (squashes, shows "!", so players can react) and
    /// bonks the balloon away as nobody's hit, then cools down. It bowls over players it walks into and you can climb it.
    /// The master client simulates it (Master Client Object); everyone else sees it through its NetworkTransform.
    /// </summary>
    public sealed class MischiefMaker : NetworkBehaviour, IKnockbackSource
    {
        private const float TargetReachedDistance = 1.5f;
        private const float TurnSpeed = 6f;

        [SerializeField] private CharacterRoster _roster;
        [SerializeField] private CharacterVisual _visual;
        [SerializeField] private Transform _visualRoot;
        [SerializeField] private TextMesh _alert;

        [Header("Yard (set by the builder)")]
        [Tooltip("Open lawn spots it wanders between.")]
        [SerializeField] private Vector3[] _wanderPoints = System.Array.Empty<Vector3>();
        [Tooltip("Circles it steers around: x, z, radius.")]
        [SerializeField] private Vector3[] _avoidCircles = System.Array.Empty<Vector3>();
        [Tooltip("Yard bounds it stays inside: min x, min z, max x, max z.")]
        [SerializeField] private Vector4 _bounds = new Vector4(-44f, -20f, 44f, 86f);

        private Vector3 _wanderTarget;
        private Vector3 _lastPosition;
        private Vector3 _velocity;
        private int _shownCharacter = -1;

        public static MischiefMaker Instance { get; private set; }

        [Networked] public int CharacterIndex { get; private set; }

        [Networked] public RoamerMood Mood { get; private set; }

        [Networked] private TickTimer StateTimer { get; set; }

        public bool KnocksPlayers => true;

        public string DisplayName => _roster != null && _roster.Get(CharacterIndex) != null ? _roster.Get(CharacterIndex).DisplayName : "The giant";

        public void Configure(CharacterRoster roster, CharacterVisual visual, Transform visualRoot, TextMesh alert, Vector3[] wanderPoints, Vector3[] avoidCircles, Vector4 bounds)
        {
            _roster = roster;
            _visual = visual;
            _visualRoot = visualRoot;
            _alert = alert;
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
                Mood = RoamerMood.Wander;
                PickWanderTarget();
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
                    WalkToward(_wanderTarget, _roster.RoamerWalkSpeed, dt);
                    if (HorizontalDistance(transform.position, _wanderTarget) < TargetReachedDistance)
                    {
                        PickWanderTarget();
                    }

                    if (canSeeBalloon)
                    {
                        Mood = RoamerMood.Chase;
                    }

                    break;

                case RoamerMood.Chase:
                    if (!canSeeBalloon)
                    {
                        Mood = RoamerMood.Wander;
                        PickWanderTarget();
                        break;
                    }

                    WalkToward(balloon.transform.position, _roster.RoamerChaseSpeed, dt);
                    if (HorizontalDistance(transform.position, balloon.transform.position) < _roster.RoamerBonkDistance)
                    {
                        Mood = RoamerMood.WindUp;
                        StateTimer = TickTimer.CreateFromSeconds(Runner, _roster.RoamerWindUpSeconds);
                    }

                    break;

                case RoamerMood.WindUp:
                    if (balloon != null)
                    {
                        Face(balloon.transform.position - transform.position, dt);
                    }

                    if (StateTimer.Expired(Runner))
                    {
                        Bonk(balloon);
                    }

                    break;

                case RoamerMood.Cooldown:
                    WalkToward(_wanderTarget, _roster.RoamerWalkSpeed, dt);
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

            if (_visualRoot != null)
            {
                float speed = new Vector2(_velocity.x, _velocity.z).magnitude;
                bool isWindingUp = Mood == RoamerMood.WindUp;
                float t = Time.time;
                Vector3 scale = Vector3.one;
                Vector3 offset = Vector3.zero;
                _visualRoot.localRotation = Quaternion.identity;
                if (isWindingUp)
                {
                    float squash = 0.12f + Mathf.Sin(t * 18f) * 0.04f; // telegraph: crouch and quiver before the bonk
                    scale = new Vector3(1f + squash, 1f - squash, 1f + squash);
                }
                else if (speed > 0.2f)
                {
                    offset.y = Mathf.Abs(Mathf.Sin(t * (4f + speed))) * 0.25f; // a waddling hop
                    _visualRoot.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * (4f + speed)) * 6f);
                }

                _visualRoot.localScale = scale;
                _visualRoot.localPosition = offset;
            }

            if (_alert != null)
            {
                _alert.gameObject.SetActive(Mood == RoamerMood.WindUp || Mood == RoamerMood.Chase);
                _alert.text = Mood == RoamerMood.WindUp ? "!" : "?";
                Camera view = Camera.main;
                if (view != null)
                {
                    _alert.transform.rotation = Quaternion.LookRotation(_alert.transform.position - view.transform.position);
                }
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
            Mood = RoamerMood.Wander;
            PickWanderTarget();
        }

        private void Bonk(NetworkBalloon balloon)
        {
            Mood = RoamerMood.Cooldown;
            StateTimer = TickTimer.CreateFromSeconds(Runner, _roster.RoamerBonkCooldown);
            PickWanderTarget();

            if (balloon == null || !IsBalloonInReach(balloon, _roster.RoamerBonkDistance * 1.6f))
            {
                return; // the crew saved it during the wind-up
            }

            Vector3 away = balloon.transform.position - transform.position;
            away.y = 0f;
            away = away.sqrMagnitude > 0.01f ? away.normalized : transform.forward;
            Vector3 velocity = away * _roster.RoamerBonkSpeed + Vector3.up * (_roster.RoamerBonkSpeed * 0.6f);
            balloon.RequestHit(PlayerRef.None, velocity); // nobody's hit: it doesn't count for the pass rule

            GameState gameState = GameState.Instance;
            if (gameState != null)
            {
                gameState.Announce($"{DisplayName} bonked the balloon!");
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
                   && InsideBounds(balloonPosition, 0f);
        }

        private void WalkToward(Vector3 target, float speed, float dt)
        {
            Vector3 position = transform.position;
            Vector3 direction = target - position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f)
            {
                return;
            }

            direction.Normalize();
            Vector3 next = position + direction * (speed * dt);
            next = Steer(next);
            Face(next - position, dt);
            transform.position = next;
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

        private void Face(Vector3 direction, float dt)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }

            Quaternion target = Quaternion.LookRotation(direction.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, Mathf.Clamp01(TurnSpeed * dt));
        }

        private void PickWanderTarget()
        {
            if (_wanderPoints == null || _wanderPoints.Length == 0)
            {
                _wanderTarget = transform.position;
                return;
            }

            Vector3 candidate = _wanderPoints[Random.Range(0, _wanderPoints.Length)];
            if (_wanderPoints.Length > 1 && HorizontalDistance(candidate, transform.position) < 3f)
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
                _visual.Show(character, character.Height * _roster.RoamerScale);
            }
        }

        private bool InsideBounds(Vector3 position, float margin) =>
            position.x > _bounds.x - margin && position.x < _bounds.z + margin && position.z > _bounds.y - margin && position.z < _bounds.w + margin;

        private int RandomCharacter() => _roster != null && _roster.Count > 0 ? Random.Range(0, _roster.Count) : 0;

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
