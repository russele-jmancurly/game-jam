using UnityEngine;

namespace GameJam.Core
{
    /// <summary>
    /// Knocks the local player over through the network, so friends see the ragdoll while the local
    /// player gets a comfortable straight-line shove.
    /// </summary>
    public interface IKnockbackRelay
    {
        void SendKnockback(Vector3 direction, float force, float seconds);
    }
}
