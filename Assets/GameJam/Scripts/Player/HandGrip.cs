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

        public HandGrip(Transform hand, SlapHand slapHand)
        {
            _hand = hand;
            _slapHand = slapHand;
        }

        public IGrabbable Held { get; private set; }

        public SlapHand SlapHand => _slapHand;

        public void Update(bool isDown, bool canGrab, float grabRadius, float throwBoost)
        {
            bool wasDestroyed = Held is Object unityObject && unityObject == null;
            if (Held != null && (wasDestroyed || !Held.IsHeldBy(_hand)))
            {
                Held = null; // despawned, or someone took it out of our hand
            }

            if (isDown && !_wasDown && Held == null && canGrab)
            {
                Held = FindNearest(grabRadius);
                Held?.Grab(_hand, _slapHand);
            }
            else if (!isDown && _wasDown && Held != null)
            {
                Vector3 throwVelocity = _slapHand != null ? _slapHand.Velocity * throwBoost : Vector3.zero;
                Held.Release(throwVelocity);
                Held = null;
            }

            _wasDown = isDown;
        }

        /// <summary>The trigger on this hand: uses the held item (its effect runs while pressed).</summary>
        public void SetUse(bool isPressed) => Held?.Use(isPressed);

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
