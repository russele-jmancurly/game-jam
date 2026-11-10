using UnityEngine;

namespace GameJam.Core
{
    /// <summary>
    /// Shove tuning (GDD: Ragdoll and friendly fire). Nothing knocks a player down except a giant's kick (CharacterRoster
    /// Roamer Kick*): walking into a giant, moving obstacles, flames, other players' slaps and thrown or swung items only
    /// give a light shove, and you keep your hands and what you hold. The user's rule; there is no switch for it.
    /// </summary>
    [CreateAssetMenu(menuName = "Game Jam/Config/Knockback Config", fileName = "KnockbackConfig")]
    public sealed class KnockbackConfig : ScriptableObject
    {
        [Header("Shoves (obstacles, flames, a giant's body, other players' slaps and items)")]
        [Tooltip("Speed of every shove (m/s).")]
        [Min(0f)] public float PlayerPushForce = 2.5f;
        [Tooltip("How long the shoved player's hands let go of the ground so the shove can move them (seconds).")]
        [Range(0.05f, 0.5f)] public float PlayerPushReleaseSeconds = 0.15f;
        [Tooltip("A moving obstacle shoves only when its surface moves faster than this (m/s).")]
        [Min(0f)] public float ObstacleMinSpeed = 0.8f;

        [Header("Your own view (a straight shove, never a camera spin)")]
        [Tooltip("Minimum upward share of a shove or a kick, so you never get pushed into the floor.")]
        [Range(0f, 1f)] public float MinUpward = 0.15f;
        [Range(0f, 1f)] public float HapticAmplitude = 0.8f;
        [Min(0f)] public float HapticSeconds = 0.2f;
        [Tooltip("While you're knocked down (a giant's kick) the view dims this much (0 clear .. 1 black), then clears as " +
                 "you get up: a VR comfort cue for a flight you aren't steering.")]
        [Range(0f, 0.8f)] public float KnockdownDim = 0.35f;
    }
}
