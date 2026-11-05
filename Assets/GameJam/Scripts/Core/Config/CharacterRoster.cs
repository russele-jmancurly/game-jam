using UnityEngine;

namespace GameJam.Core
{
    /// <summary>The characters players can be, and how the roaming mischief maker uses them.</summary>
    [CreateAssetMenu(menuName = "Game Jam/Config/Character Roster", fileName = "CharacterRoster")]
    public sealed class CharacterRoster : ScriptableObject
    {
        public CharacterDefinition[] Characters = System.Array.Empty<CharacterDefinition>();

        [Header("Roaming mischief maker")]
        [Tooltip("How many times taller than a player the roaming character is.")]
        [Min(1f)] public float RoamerScale = 2.4f;
        [Min(0.1f)] public float RoamerWalkSpeed = 2f;
        [Min(0.1f)] public float RoamerChaseSpeed = 3.6f;
        [Tooltip("The roamer notices the balloon within this distance (and below RoamerReachHeight).")]
        [Min(1f)] public float RoamerNoticeDistance = 10f;
        [Min(0.5f)] public float RoamerReachHeight = 6f;
        [Tooltip("Horizontal distance at which it stops and winds up a bonk.")]
        [Min(0.5f)] public float RoamerBonkDistance = 2.6f;
        [Tooltip("Telegraph before the bonk, so players can react (GDD rhythm rule).")]
        [Min(0.1f)] public float RoamerWindUpSeconds = 0.8f;
        [Min(0f)] public float RoamerBonkSpeed = 5.5f;
        [Min(0f)] public float RoamerBonkCooldown = 7f;
        [Tooltip("Knockback when it walks into a player.")]
        [Min(0f)] public float RoamerBumpForce = 4f;

        public int Count => Characters?.Length ?? 0;

        public CharacterDefinition Get(int index) =>
            Count == 0 ? null : Characters[((index % Count) + Count) % Count];
    }
}
