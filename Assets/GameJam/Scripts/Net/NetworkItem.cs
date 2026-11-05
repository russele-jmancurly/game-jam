using Fusion;
using GameJam.Core;
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

        [SerializeField] private ItemDefinition _definition;
        [SerializeField] private KnockbackConfig _knockbackConfig;

        private Rigidbody _body;
        private Transform _hand;
        private IHandVelocity _handVelocity;
        private Vector3 _homePosition;
        private Quaternion _homeRotation;
        private int _defaultLayer;
        private bool _hasPendingThrow;
        private Vector3 _pendingThrowVelocity;
        private float _pendingThrowExpiry;
        private float _nextHitTime;

        [Networked] public PlayerRef Holder { get; private set; }

        [Networked] public PlayerRef Thrower { get; private set; }

        public ItemDefinition Definition => _definition;

        public bool IsHeld => _hand != null || (Object != null && Object.IsValid && Holder != PlayerRef.None);

        public Vector3 Position => transform.position;

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
            _body.mass = _definition.Mass;
            _homePosition = transform.position;
            _homeRotation = transform.rotation;
            _defaultLayer = gameObject.layer;
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

            // Fell off the course: put it back on its spot so the world never runs dry.
            if (HasStateAuthority && _hand == null && transform.position.y < _definition.KillHeight)
            {
                _body.linearVelocity = Vector3.zero;
                _body.angularVelocity = Vector3.zero;
                transform.SetPositionAndRotation(_homePosition, _homeRotation);
                Holder = PlayerRef.None;
                Thrower = PlayerRef.None;
            }

            ApplyPhysicsMode();
        }

        private void LateUpdate()
        {
            if (_hand != null)
            {
                transform.SetPositionAndRotation(_hand.position + _hand.forward * HoldDistance, _hand.rotation);
            }
        }

        private void OnTriggerEnter(Collider other) => HandleContact(other);

        private void OnCollisionEnter(Collision collision) => HandleContact(collision.collider);

        private void Throw(Vector3 velocity)
        {
            Holder = PlayerRef.None;
            Thrower = Runner.LocalPlayer;
            _body.isKinematic = false;
            _body.linearVelocity = velocity;
            _body.angularVelocity = Random.insideUnitSphere * RandomSpin;
        }

        private void ApplyPhysicsMode()
        {
            if (_body == null || Object == null || !Object.IsValid)
            {
                return;
            }

            bool shouldSimulate = HasStateAuthority && _hand == null;
            if (_body.isKinematic == shouldSimulate)
            {
                _body.isKinematic = !shouldSimulate;
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
                    balloon.RequestHit(hitter, velocity * _definition.BalloonPower);
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
