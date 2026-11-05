using Fusion;
using GameJam.Core;
using GameJam.Gameplay;
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
        private const float HoldDistance = 0.08f;
        private const float GustOvershoot = 1.5f; // a leaf-blower gust reaches this far past the hoop it aims at

        [SerializeField] private ItemDefinition _definition;
        [SerializeField] private KnockbackConfig _knockbackConfig;
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
        private bool _isEffectArmed;
        private float _effectArmTime;

        [Networked] public PlayerRef Holder { get; private set; }

        [Networked] public PlayerRef Thrower { get; private set; }

        /// <summary>Running while this item's landing effect (updraft, gust) blows; everyone shows the same zone.</summary>
        [Networked] private TickTimer EffectTimer { get; set; }

        [Networked] private Vector3 EffectOrigin { get; set; }

        [Networked] private Quaternion EffectFlow { get; set; }

        [Networked] private float EffectLength { get; set; }

        public ItemDefinition Definition => _definition;

        public bool IsHeld => _hand != null || (Object != null && Object.IsValid && Holder != PlayerRef.None);

        public Vector3 Position => transform.position;

        public void ConfigureEffect(ItemEffectZone effectZone) => _effectZone = effectZone;

        public void Configure(ItemDefinition definition, KnockbackConfig knockbackConfig)
        {
            _definition = definition;
            _knockbackConfig = knockbackConfig;
        }

        public bool IsHeldBy(Transform hand) => _hand != null && _hand == hand;

        public void Grab(Transform hand, IHandVelocity handVelocity)
        {
            _hand = hand;
            _handVelocity = handVelocity;
            _hasPendingThrow = false;
            SetLayer(GameLayers.IgnoreRaycast); // held items are not climbable and ignore your own rig

            if (HasStateAuthority)
            {
                Holder = Runner.LocalPlayer;
            }
            else
            {
                Object.RequestStateAuthority();
            }
        }

        public void Release(Vector3 throwVelocity)
        {
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
            _defaultLayer = gameObject.layer;
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
                _hand = null;
                _handVelocity = null;
                SetLayer(_defaultLayer);
            }

            // Fell off the course: put it back on its spot so the world never runs dry. Teleport, so no client lerps it
            // across the map.
            if (HasStateAuthority && _hand == null && transform.position.y < _definition.KillHeight)
            {
                _body.linearVelocity = Vector3.zero;
                _body.angularVelocity = Vector3.zero;
                if (_networkTransform != null)
                {
                    _networkTransform.Teleport(_homePosition, _homeRotation);
                }
                else
                {
                    transform.SetPositionAndRotation(_homePosition, _homeRotation);
                }

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
                transform.SetPositionAndRotation(_hand.position + _hand.forward * HoldDistance, _hand.rotation);
            }
        }

        private void OnTriggerEnter(Collider other) => HandleContact(other);

        private void OnCollisionEnter(Collision collision)
        {
            TriggerEffect();
            HandleContact(collision.collider);
        }

        public override void Render()
        {
            if (_effectZone == null)
            {
                return;
            }

            bool isOn = EffectTimer.IsRunning && !EffectTimer.Expired(Runner);
            Vector3? target = null;
            float lift = 0f;
            if (_definition.Effect == ItemEffect.HoopGust && EffectLength > GustOvershoot)
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
            _isEffectArmed = _definition.Effect != ItemEffect.None;
            _effectArmTime = Time.time;
        }

        /// <summary>
        /// First impact after a throw sets off the item's effect where it is: a bubble updraft straight up, or a gust that
        /// blows from here toward the nearest hoop not yet cleared (straight up when none is in range).
        /// </summary>
        private void TriggerEffect()
        {
            // Skip the first instant: a fresh throw can brush the thrower's own body.
            if (!_isEffectArmed || Time.time < _effectArmTime + 0.15f || Object == null || !Object.IsValid || !HasStateAuthority)
            {
                return;
            }

            _isEffectArmed = false;
            Vector3 origin = transform.position + Vector3.up * 0.1f;
            Quaternion flow = Quaternion.LookRotation(Vector3.up, Vector3.forward);
            float length = _definition.EffectLength;
            HoopCheckpoint hoop = _definition.Effect == ItemEffect.HoopGust ? HoopCheckpoint.NearestOpen(origin, _definition.EffectLength * 2f) : null;
            if (hoop != null)
            {
                Vector3 toHoop = hoop.transform.position - origin;
                flow = Quaternion.LookRotation(toHoop.normalized, Vector3.up);
                length = toHoop.magnitude + GustOvershoot; // blows just past the ring
            }

            EffectOrigin = origin;
            EffectFlow = flow;
            EffectLength = length;
            EffectTimer = TickTimer.CreateFromSeconds(Runner, _definition.EffectSeconds);

            if (GameState.Instance != null)
            {
                GameState.Instance.Announce(_definition.Effect == ItemEffect.Updraft ? "Bubbles! Ride them up!" : "The leaf blower's on, follow the wind!");
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

            bool isHeld = _hand != null;
            if (_body.isKinematic != isHeld)
            {
                _body.isKinematic = isHeld;
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

            var hitbox = other.GetComponentInParent<AvatarHitbox>();
            if (hitbox == null || hitbox.Avatar == null || hitbox.Avatar.Object == null)
            {
                return;
            }

            if (hitbox.Avatar.Object.StateAuthority == hitter)
            {
                return; // your own item never knocks you over
            }

            _nextHitTime = Time.time + FriendHitCooldown;
            bool isHeavy = _definition.Kind == ItemKind.Heavy;
            float force = isHeavy ? _definition.FriendForce : _knockbackConfig.SoftItemForce;
            float seconds = isHeavy ? _knockbackConfig.HeavyItemKnockdownSeconds : _knockbackConfig.SoftItemStunSeconds;
            hitbox.Avatar.SendKnockback(velocity.normalized, force, seconds);
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
