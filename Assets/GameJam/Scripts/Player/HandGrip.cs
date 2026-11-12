using GameJam.Core;
using UnityEngine;

namespace GameJam.Player
{
    /// <summary>One hand's grab-and-throw state: grip down near an item grabs it, letting go throws it.</summary>
    public sealed class HandGrip
    {
        private const int MaxOverlaps = 24;

        private readonly Transform _hand;
        private readonly SlapHand _slapHand;
        private readonly Collider[] _overlaps = new Collider[MaxOverlaps];
        private bool _wasDown;
        private float _nextPushTime;

        public HandGrip(Transform hand, SlapHand slapHand)
        {
            _hand = hand;
            _slapHand = slapHand;
        }

        public IGrabbable Held { get; private set; }

        /// <summary>The grip button is held on this hand.</summary>
        public bool IsDown => _wasDown;

        public SlapHand SlapHand => _slapHand;

        public void Update(bool isDown, bool canGrab, PlayerRigConfig config)
        {
            bool wasDestroyed = Held is Object unityObject && unityObject == null;
            if (Held != null && (wasDestroyed || !Held.IsHeldBy(_hand)))
            {
                Held = null; // despawned, or someone took it out of our hand
            }

            if (isDown && !_wasDown && Held == null && canGrab)
            {
                Held = FindNearest(config.GrabRadius);
                Held?.Grab(_hand, _slapHand);
            }
            else if (!isDown && _wasDown && Held != null)
            {
                Held.Release(ThrowVelocity(config));
                Held = null;
            }

            _wasDown = isDown;
        }

        /// <summary>
        /// The swing's best moment just before letting go (SlapHand.ThrowVelocity), measured where the item sits in the
        /// hand, boosted and capped.
        /// </summary>
        private Vector3 ThrowVelocity(PlayerRigConfig config)
        {
            if (_slapHand == null)
            {
                return Vector3.zero;
            }

            Vector3 itemInHand = _slapHand.transform.InverseTransformDirection(Held.Position - _slapHand.transform.position);
            Vector3 velocity = _slapHand.ThrowVelocity(config.ThrowWindowSeconds, config.ThrowSampleSeconds, itemInHand) * config.ThrowBoost;
            return Vector3.ClampMagnitude(velocity, config.ThrowMaxSpeed);
        }

        /// <summary>The trigger on this hand: uses the held item (its effect runs while pressed).</summary>
        public void SetUse(bool isPressed) => Held?.Use(isPressed);

        /// <summary>
        /// The other hand bumped the item this hand holds (a ball in the right hand hit with the left): true with the way
        /// the body is pushed, away from the item on the free hand's side. Needs closing speed, so hands resting together
        /// don't push, and waits out the cooldown between pushes.
        /// </summary>
        public bool IsBumpedBy(SlapHand freeHand, KnockbackConfig config, out Vector3 push)
        {
            push = Vector3.zero;
            if (Held == null || freeHand == null || Time.time < _nextPushTime
                || !Held.Touches(freeHand.transform.position, config.HeldItemTouchRadius, out Vector3 away))
            {
                return false;
            }

            Vector3 holdVelocity = _slapHand != null ? _slapHand.Velocity : Vector3.zero;
            float closingSpeed = Vector3.Dot(freeHand.Velocity - holdVelocity, -away);
            if (closingSpeed < config.HeldItemPushMinSpeed)
            {
                return false;
            }

            _nextPushTime = Time.time + config.HeldItemPushCooldown;
            push = away;
            return true;
        }

        public void Drop()
        {
            Held?.Release(Vector3.zero);
            Held = null;
            _wasDown = false;
        }

        private IGrabbable FindNearest(float radius)
        {
            Vector3 position = _hand.position;
            int count = Physics.OverlapSphereNonAlloc(position, radius, _overlaps, ~0, QueryTriggerInteraction.Collide);

            IGrabbable best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var item = _overlaps[i].GetComponentInParent<IGrabbable>();
                if (item == null || item.IsHeld)
                {
                    continue;
                }

                float distance = (item.Position - position).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = item;
                }
            }

            return best;
        }
    }
}
