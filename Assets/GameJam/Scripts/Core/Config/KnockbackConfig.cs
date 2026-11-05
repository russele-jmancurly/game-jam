using UnityEngine;

namespace GameJam.Core
{
    /// <summary>
    /// Knockback and ragdoll tuning (GDD: Ragdoll and friendly fire). A knocked player must be back
    /// in action within 2 seconds, so keep every duration under that.
    /// </summary>
    [CreateAssetMenu(menuName = "Game Jam/Config/Knockback Config", fileName = "KnockbackConfig")]
    public sealed class KnockbackConfig : ScriptableObject
    {
        [Header("Your own view (a straight shove, never a camera spin)")]
        [Tooltip("Minimum upward share of a knockback, so you never get pushed into the floor.")]
        [Range(0f, 1f)] public float MinUpward = 0.15f;
        [Range(0f, 1f)] public float HapticAmplitude = 0.8f;
        [Min(0f)] public float HapticSeconds = 0.2f;

        [Header("Moving obstacles")]
        [Tooltip("Obstacle surface speed below this does nothing.")]
        [Min(0f)] public float ObstacleMinSpeed = 0.8f;
        [Min(0f)] public float ObstacleForceScale = 1.2f;
        [Min(0f)] public float ObstacleMinForce = 3f;
        [Min(0f)] public float ObstacleMaxForce = 7f;
        [Range(0f, 2f)] public float ObstacleKnockdownSeconds = 1.2f;

        [Header("Flames and pulsing hazards")]
        [Min(0f)] public float FlameForce = 3f;
        [Range(0f, 2f)] public float FlameKnockdownSeconds = 0.8f;

        [Header("Thrown items")]
        [Tooltip("Soft items cause a mini-stun: a tiny shove.")]
        [Min(0f)] public float SoftItemForce = 1.5f;
        [Range(0f, 2f)] public float SoftItemStunSeconds = 0.4f;
        [Range(0f, 2f)] public float HeavyItemKnockdownSeconds = 1.5f;

        [Header("Debug")]
        [Min(0f)] public float SelfTestForce = 4f;
        [Range(0f, 2f)] public float SelfTestSeconds = 1.5f;
    }
}
