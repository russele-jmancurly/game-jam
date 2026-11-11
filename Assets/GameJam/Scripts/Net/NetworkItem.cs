using Fusion;
using GameJam.Core;
using GameJam.Gameplay;
using GameJam.Player;
using UnityEngine;

namespace GameJam.Net
{
    /// <summary>
    /// A grabbable, throwable item. Grabbing requests state authority, so its physics runs on the holder's
    /// headset. A thrown item's hit counts as the thrower's hit for the balloon's pass rule.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class NetworkItem : NetworkBehaviour, IGrabbable
    {
        private const float PendingThrowTimeout = 1f;
        private const float FriendHitCooldown = 0.5f;
        private const float RandomSpin = 5f;
        private const float GustAimCone = 25f;     // a hoop within this many degrees of where the blower points gets the gust
        private const float GustOvershoot = 1.5f; // a leaf-blower gust reaches this far past the hoop it aims at
        private const float SolidAfterReleaseSeconds = 0.5f; // a let-go effect item clears your body before it turns solid again

        [SerializeField] private ItemDefinition _definition;
        [Tooltip("Wind zone this item sets off where it lands (bubble wand, leaf blower); null for plain items.")]
        [SerializeField] private ItemEffectZone _effectZone;

        private const int MaxSwingOverlaps = 12;

        private readonly Collider[] _swingOverlaps = new Collider[MaxSwingOverlaps];
        private Rigidbody _body;
        private NetworkTransform _networkTransform;
        private Collider _collider;
        private Transform _hand;
        private IHandVelocity _handVelocity;
        private Vector3 _homePosition;
        private Quaternion _homeRotation;
        private int _defaultLayer;
        private bool _hasPendingThrow;
        private Vector3 _pendingThrowVelocity;
        private float _pendingThrowExpiry;
        private float _nextHitTime;
        private bool _isUsing;
        private float _nextUseTime;
        private bool _shownSpent;
        private bool _ignoresRig;
        private bool _hasRigRule;
        private float _solidFromTime;

        [Networked] public PlayerRef Holder { get; private set; }

        [Networked] public PlayerRef Thrower { get; private set; }

        /// <summary>Running while this item's landing effect (updraft, gust) blows; everyone shows the same zone.</summary>
        [Networked] private TickTimer EffectTimer { get; set; }

        /// <summary>Used up (single-use effect items): hidden at its spot until RespawnTimer runs out.</summary>
        [Networked] public NetworkBool IsSpent { get; private set; }

        [Networked] private TickTimer RespawnTimer { get; set; }

        /// <summary>
        /// The item's spot, set once by whoever spawned it, so every client (late joiners included) sends it to the same
        /// place when it falls off the map, is used up, or the run returns to the lobby.
        /// </summary>
        [Networked] private Vector3 HomePosition { get; set; }

        [Networked] private Quaternion HomeRotation { get; set; }

        [Networked] private NetworkBool HasHome { get; set; }

        [Networked] private Vector3 EffectOrigin { get; set; }

        [Networked] private Quaternion EffectFlow { get; set; }

        [Networked] private float EffectLength { get; set; }

        /// <summary>The leaf blower's gust found a hoop in its aim cone: home the balloon onto it.</summary>
        [Networked] private NetworkBool EffectHomes { get; set; }

        public ItemDefinition Definition => _definition;

        public bool IsHeld => _hand != null || (Object != null && Object.IsValid && Holder != PlayerRef.None);

        public Vector3 Position => transform.position;

        public void ConfigureEffect(ItemEffectZone effectZone) => _effectZone = effectZone;

        public void Configure(ItemDefinition definition) => _definition = definition;

        public bool IsHeldBy(Transform hand) => _hand != null && _hand == hand;

        public bool Touches(Vector3 point, float radius, out Vector3 away)
        {
            away = Vector3.zero;
            if (_collider == null || !_collider.enabled)
            {
                return false;
            }

            Vector3 surface = _collider.ClosestPoint(point);
            if ((surface - point).sqrMagnitude > radius * radius)
            {
                return false;
            }

            away = point - _collider.bounds.center;
            away = away.sqrMagnitude > 0.0001f ? away.normalized : Vector3.up;
            return true;
        }

        public void Grab(Transform hand, IHandVelocity handVelocity)
        {
            if (Object != null && Object.IsValid && IsSpent)
            {
                return; // used up and hidden: nothing to grab until it respawns
            }

            _hand = hand;
            _handVelocity = handVelocity;
            _hasPendingThrow = false;
            SetLayer(GameLayers.IgnoreRaycast); // held items are not climbable and ignore your own rig
            UpdateRigCollision(false);           // at once: a solid wand or blower in the hand would ram its holder

            if (HasStateAuthority)
            {
                Holder = Runner.LocalPlayer;
            }
            else
            {
                Object.RequestStateAuthority();
            }
        }

        /// <summary>
        /// A trigger press starts the effect (bubble updraft from the wand's ring, a gust from the blower's nozzle where you
        /// aim); it runs its full EffectSeconds while you hold the item, following your aim, then the item is used up.
        /// Letting go of the trigger doesn't end it (a press that flickered used to end it at once and the item vanished).
        /// </summary>
        public void Use(bool isPressed)
        {
            if (_definition.Effect == ItemEffect.None || _hand == null || Object == null || !Object.IsValid || !HasStateAuthority || IsSpent)
            {
                return;
            }

            if (isPressed && !_isUsing && Time.time >= _nextUseTime)
            {
                _isUsing = true;
                EffectTimer = TickTimer.CreateFromSeconds(Runner, _definition.EffectSeconds);
                AimEffect();
                if (GameState.Instance != null)
                {
                    GameState.Instance.Announce(_definition.Effect == ItemEffect.Updraft ? "Bubbles! Ride them up!" : "The leaf blower's on, follow the wind!");
                }
            }

        }

        public void Release(Vector3 throwVelocity)
        {
            if (_isUsing && HasStateAuthority)
            {
                StopEffect(); // its one use is over: it vanishes instead of being thrown
                return;
            }

            _hand = null;
            _handVelocity = null;
            SetLayer(_defaultLayer);

            if (HasStateAuthority)
            {
                Throw(throwVelocity);
                return;
            }

            // Authority hasn't arrived yet: throw as soon as it does.
            _hasPendingThrow = true;
            _pendingThrowVelocity = throwVelocity;
            _pendingThrowExpiry = Time.time + PendingThrowTimeout;
        }

        public override void Spawned()
        {
            _body = GetComponent<Rigidbody>();
            _networkTransform = GetComponent<NetworkTransform>();
            _collider = GetComponent<Collider>();
            _body.mass = _definition.Mass;
            _homePosition = transform.position;
            _homeRotation = transform.rotation;
            if (HasStateAuthority && !HasHome)
            {
                HomePosition = transform.position;
                HomeRotation = transform.rotation;
                HasHome = true;
            }

            _defaultLayer = gameObject.layer;
            UpdateRigCollision(true);
            if (_effectZone != null)
            {
                _effectZone.transform.SetParent(null, true); // the zone stays where the item landed, not on the item
            }
            ApplyPhysicsMode();
        }

        public override void FixedUpdateNetwork()
        {
            if (_hand != null && HasStateAuthority)
            {
                Holder = Runner.LocalPlayer;
            }

            if (_isUsing && HasStateAuthority)
            {
                if (EffectTimer.Expired(Runner) || _hand == null)
                {
                    StopEffect();
                }
                else
                {
                    AimEffect(); // the effect follows the item while the trigger is held
                }
            }

            // Used up: comes back at its spot once the respawn timer runs out.
            if (HasStateAuthority && IsSpent && RespawnTimer.ExpiredOrNotRunning(Runner))
            {
                IsSpent = false;
            }

            if (_hasPendingThrow)
            {
                if (HasStateAuthority)
                {
                    _hasPendingThrow = false;
                    Throw(_pendingThrowVelocity);
                }
                else if (Time.time > _pendingThrowExpiry)
                {
                    _hasPendingThrow = false;
                }
            }

            // Someone else grabbed it out of our hand.
            if (_hand != null && !HasStateAuthority && Holder != PlayerRef.None && Holder != Runner.LocalPlayer)
            {
                _isUsing = false; // the new holder owns the effect now; a stale flag would spend the item later
                _hand = null;
                _handVelocity = null;
                SetLayer(_defaultLayer);
            }

            // Fell off the course: put it back on its spot so the world never runs dry. Teleport, so no client lerps it
            // across the map.
            if (HasStateAuthority && _hand == null && transform.position.y < _definition.KillHeight)
            {
                TeleportHome();
                Holder = PlayerRef.None;
                Thrower = PlayerRef.None;
            }

            ApplyPhysicsMode();
        }

        /// <summary>
        /// A held item swung into the balloon or a friend: detected by overlap, since held items sit on the Ignore Raycast
        /// layer that the balloon doesn't collide with (GDD: hits come from velocity, not collider contact alone).
        /// </summary>
        private void FixedUpdate()
        {
            if (_hand == null || !HasStateAuthority || _collider == null || Time.time < _nextHitTime)
            {
                return;
            }

            Bounds bounds = _collider.bounds;
            int count = Physics.OverlapBoxNonAlloc(bounds.center, bounds.extents, _swingOverlaps, Quaternion.identity, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                Collider other = _swingOverlaps[i];
                if (!other.transform.IsChildOf(transform))
                {
                    HandleContact(other);
                }
            }
        }

        private void LateUpdate()
        {
            if (_hand != null)
            {
                // Each item's hold pose puts its business end where the fist points (nozzle, wand ring, pan, dart tip).
                transform.SetPositionAndRotation(_hand.TransformPoint(_definition.HoldOffset), _hand.rotation * Quaternion.Euler(_definition.HoldEuler));
            }
        }

        private void OnTriggerEnter(Collider other) => HandleContact(other);

        private void OnCollisionEnter(Collision collision) => HandleContact(collision.collider);

        public override void Render()
        {
            UpdateRigCollision(false); // here, not in FixedUpdateNetwork: it must also run on clients that don't own the item

            if (IsSpent != _shownSpent)
            {
                ShowSpent(IsSpent);
            }

            if (_effectZone == null)
            {
                return;
            }

            bool isOn = EffectTimer.IsRunning && !EffectTimer.Expired(Runner);
            Vector3? target = null;
            float lift = 0f;
            if (_definition.Effect == ItemEffect.HoopGust && EffectHomes && EffectLength > GustOvershoot)
            {
                // The gust was aimed at a hoop GustOvershoot short of its far end: home the balloon onto the ring and hold it
                // up against its own gravity (a flat wind alone carried it under the hoop in testing).
                target = EffectOrigin + EffectFlow * Vector3.forward * (EffectLength - GustOvershoot);
                lift = NetworkBalloon.Instance != null && NetworkBalloon.Instance.Config != null ? NetworkBalloon.Instance.Config.Gravity : 1f;
            }

            _effectZone.Show(isOn, EffectOrigin, EffectFlow, EffectLength, _definition.EffectWidth, _definition.EffectStrength, target, lift);
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (_effectZone != null)
            {
                Destroy(_effectZone.gameObject);
            }
        }

        private void Throw(Vector3 velocity)
        {
            Holder = PlayerRef.None;
            Thrower = Runner.LocalPlayer;
            _body.isKinematic = false;
            _body.linearVelocity = velocity;
            _body.angularVelocity = Random.insideUnitSphere * RandomSpin;
        }

        /// <summary>
        /// Points the running effect from the item: bubbles rise straight up from the wand's ring; the blower's gust goes
        /// where its nozzle points, or homes onto a hoop not yet cleared that lies within GustAimCone of the aim.
        /// </summary>
        private void AimEffect()
        {
            Vector3 origin = transform.TransformPoint(_definition.EffectEmitter);
            Quaternion flow = Quaternion.LookRotation(Vector3.up, transform.forward);
            float length = _definition.EffectLength;
            bool homes = false;
            if (_definition.Effect == ItemEffect.HoopGust)
            {
                Vector3 aim = transform.forward;
                flow = Quaternion.LookRotation(aim, Vector3.up);
                HoopCheckpoint hoop = HoopCheckpoint.NearestOpen(origin, _definition.EffectLength * 2f);
                if (hoop != null)
                {
                    Vector3 toHoop = hoop.transform.position - origin;
                    if (Vector3.Angle(aim, toHoop) < GustAimCone)
                    {
                        flow = Quaternion.LookRotation(toHoop.normalized, Vector3.up);
                        length = toHoop.magnitude + GustOvershoot; // blows just past the ring
                        homes = true;
                    }
                }
            }

            EffectOrigin = origin;
            EffectFlow = flow;
            EffectLength = length;
            EffectHomes = homes; // aimed away from every hoop: a plain gust along the aim
        }

        private void StopEffect()
        {
            _isUsing = false;
            EffectTimer = TickTimer.None;
            _nextUseTime = Time.time + _definition.EffectCooldown;
            Spend();
        }

        /// <summary>
        /// Single use: the effect has run, so the item leaves the hand, vanishes and waits at its spot until it respawns.
        /// </summary>
        private void Spend()
        {
            IsSpent = true;
            RespawnTimer = TickTimer.CreateFromSeconds(Runner, _definition.UsedUpRespawnSeconds);
            _hand = null;
            _handVelocity = null;
            _hasPendingThrow = false;
            SetLayer(_defaultLayer);
            Holder = PlayerRef.None;
            Thrower = PlayerRef.None;
            TeleportHome();

            if (GameState.Instance != null)
            {
                GameState.Instance.Announce($"The {_definition.DisplayName.ToLowerInvariant()} is used up!");
            }
        }

        /// <summary>Back to its spot without a lerp across the map (NetworkTransform.Teleport), at rest.</summary>
        private void TeleportHome()
        {
            Vector3 position = HasHome ? HomePosition : _homePosition;
            Quaternion rotation = HasHome ? HomeRotation : _homeRotation;
            _body.linearVelocity = Vector3.zero;
            _body.angularVelocity = Vector3.zero;
            if (_networkTransform != null)
            {
                _networkTransform.Teleport(position, rotation);
            }
            else
            {
                transform.SetPositionAndRotation(position, rotation);
            }
        }

        /// <summary>
        /// The run went back to the lobby: wherever this item is (held, thrown, used up), it goes back on its spot, ready.
        /// Called by the game state (master client); the item's owner carries it out.
        /// </summary>
        public void ReturnHome()
        {
            if (Object != null && Object.IsValid)
            {
                RpcReturnHome();
            }
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RpcReturnHome()
        {
            _isUsing = false;
            EffectTimer = TickTimer.None;
            IsSpent = false;
            RespawnTimer = TickTimer.None;
            _hand = null; // the holder's HandGrip lets go when it sees the item is no longer in its hand
            _handVelocity = null;
            _hasPendingThrow = false;
            SetLayer(_defaultLayer);
            Holder = PlayerRef.None;
            Thrower = PlayerRef.None;
            TeleportHome();
            ApplyPhysicsMode();
        }

        /// <summary>Hides a used-up item (and turns its colliders off, so nobody can grab or bump it) on every client.</summary>
        private void ShowSpent(bool isSpent)
        {
            _shownSpent = isSpent;
            foreach (Renderer itemRenderer in GetComponentsInChildren<Renderer>(true))
            {
                itemRenderer.enabled = !isSpent;
            }

            foreach (Collider itemCollider in GetComponentsInChildren<Collider>(true))
            {
                itemCollider.enabled = !isSpent;
            }

            if (!isSpent)
            {
                UpdateRigCollision(true); // disabling a collider drops its IgnoreCollision pairs: set the rule again
            }
        }

        /// <summary>
        /// Fusion 2.1 Forecast Physics owns proxy rigidbodies (it extrapolates them into local time), so only the state
        /// authority toggles isKinematic: on while held, off otherwise. Fusion syncs the flag to everyone else.
        /// </summary>
        private void ApplyPhysicsMode()
        {
            if (_body == null || Object == null || !Object.IsValid || !HasStateAuthority)
            {
                return;
            }

            bool isFrozen = _hand != null || IsSpent; // held, or used up and waiting at its spot
            if (_body.isKinematic != isFrozen)
            {
                _body.isKinematic = isFrozen;
            }
        }

        private void HandleContact(Collider other)
        {
            if (Object == null || !Object.IsValid || !HasStateAuthority || Time.time < _nextHitTime)
            {
                return;
            }

            // Swinging a held item (umbrella, pan) uses the hand's speed; a thrown one uses its own.
            Vector3 velocity = _hand != null ? _handVelocity?.Velocity ?? Vector3.zero : _body.linearVelocity;
            if (velocity.magnitude < _definition.MinHitSpeed)
            {
                return;
            }

            PlayerRef hitter = _hand != null ? Runner.LocalPlayer : Thrower;

            var balloon = other.GetComponentInParent<NetworkBalloon>();
            if (balloon != null)
            {
                _nextHitTime = Time.time + _definition.HitCooldown;
                if (_definition.Kind == ItemKind.Trap)
                {
                    balloon.RequestPop($"Popped by the {_definition.DisplayName.ToLowerInvariant()}!");
                }
                else
                {
                    // Helpful items steer the knock toward the nearest open hoop (GDD: most items help with the balloon).
                    Vector3 hit = HoopAim.Assist(balloon.transform.position, velocity * _definition.BalloonPower,
                        _definition.HoopAssist, _definition.HoopAssistRange, balloon.Config);
                    balloon.RequestHit(hitter, hit);
                }

                return;
            }

            // A thrown item (not a swung or loose one) knocks a giant down: ragdoll and dizzy stars.
            var giant = other.GetComponentInParent<MischiefMaker>();
            if (giant != null)
            {
                if (_hand == null && Thrower != PlayerRef.None && !giant.IsKnockedDown)
                {
                    _nextHitTime = Time.time + FriendHitCooldown;
                    giant.RequestKnockDown(velocity);
                }

                return;
            }

            var hitbox = other.GetComponentInParent<AvatarHitbox>();
            if (hitbox == null || hitbox.Avatar == null || hitbox.Avatar.Object == null)
            {
                return;
            }

            if (hitter == PlayerRef.None || hitbox.Avatar.Object.StateAuthority == hitter)
            {
                return; // only an item another player threw or swung pushes you: never a loose one, never your own
            }

            _nextHitTime = Time.time + FriendHitCooldown;
            hitbox.Avatar.SendShove(velocity.normalized); // a light shove whatever the item: only a giant's kick knocks players down
        }

        /// <summary>
        /// Plain items never physically push your own body or head (a ball rolling into you, a held pan against your chest).
        /// Items with a trigger effect (bubble wand, leaf blower) are solid against your body while loose or thrown (the
        /// user's call), but not in anyone's hand or just after being let go: a held item follows the hand, so it would
        /// shove its holder around. Only the local rig's solid colliders are involved: other players' hitboxes are triggers, so
        /// throwing an item at a friend still registers.
        /// </summary>
        private void UpdateRigCollision(bool force)
        {
            bool isHeld = IsHeld; // by anyone: a kinematic item in another player's hand would ram you, not bump you
            if (isHeld)
            {
                _solidFromTime = Time.time + SolidAfterReleaseSeconds;
            }

            bool isSolid = _definition.Effect != ItemEffect.None && !isHeld && Time.time >= _solidFromTime;
            bool ignores = !isSolid;
            if (!force && _hasRigRule && ignores == _ignoresRig)
            {
                return;
            }

            LocalRig rig = LocalRig.Instance;
            if (rig == null)
            {
                return;
            }

            _ignoresRig = ignores;
            _hasRigRule = true;

            Collider[] mine = GetComponentsInChildren<Collider>(true);
            foreach (Collider part in rig.transform.root.GetComponentsInChildren<Collider>(true))
            {
                if (part.isTrigger)
                {
                    continue;
                }

                foreach (Collider own in mine)
                {
                    Physics.IgnoreCollision(own, part, ignores);
                }
            }
        }

        private void SetLayer(int layer)
        {
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = layer;
            }
        }
    }
}
