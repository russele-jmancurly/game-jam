using UnityEngine;

namespace GameJam.Core
{
    /// <summary>An item a hand can pick up and throw.</summary>
    public interface IGrabbable
    {
        bool IsHeld { get; }

        Vector3 Position { get; }

        bool IsHeldBy(Transform hand);

        void Grab(Transform hand, IHandVelocity handVelocity);

        void Release(Vector3 throwVelocity);
    }
}
