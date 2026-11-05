namespace GorillaLocomotion
{
    using UnityEngine;

    /// <summary>
    /// Per-collider override for gorilla locomotion. Without one, slip comes from the collider's PhysicsMaterial
    /// (static friction below <see cref="Player.slipperyFrictionThreshold"/>) and bounce from its bounciness.
    /// </summary>
    public class Surface : MonoBehaviour
    {
        [Tooltip("0 = grippy (normal ground), above the player's ice threshold (0.95) = hands slide like on ice.")]
        [Range(0f, 1f)]
        public float slipPercentage = 0.03f;

        [Tooltip("0 = no bounce, 1 = hands rebound at full speed (trampoline).")]
        [Range(0f, 1f)]
        public float bouncePercentage;
    }
}
