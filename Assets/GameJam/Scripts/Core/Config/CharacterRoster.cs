using UnityEngine;

namespace GameJam.Core
{
    /// <summary>The show characters that appear on the map (host, commentator, the roaming mischief maker).</summary>
    [CreateAssetMenu(menuName = "Game Jam/Config/Character Roster", fileName = "CharacterRoster")]
    public sealed class CharacterRoster : ScriptableObject
    {
        public CharacterDefinition[] Characters = System.Array.Empty<CharacterDefinition>();

        [Header("Roaming mischief maker")]
        [Tooltip("How many times taller than a player the roaming character is.")]
        [Min(1f)] public float RoamerScale = 2.4f;
        [Min(0.1f)] public float RoamerWalkSpeed = 2f;
        [Min(0.1f)] public float RoamerChaseSpeed = 3.6f;
        [Tooltip("How fast it gets up to speed and slows down (m/s per second), so it doesn't snap between walking and standing.")]
        [Min(0.1f)] public float RoamerAcceleration = 3f;
        [Tooltip("How fast it turns (degrees per second). It turns on the spot before walking off in a new direction.")]
        [Min(10f)] public float RoamerTurnSpeed = 140f;
        [Tooltip("It pauses and looks around at each wander spot for between x and y seconds.")]
        public Vector2 RoamerIdleSeconds = new Vector2(1.5f, 4f);
        [Tooltip("The roamer notices the balloon within this distance (and below RoamerReachHeight).")]
        [Min(1f)] public float RoamerNoticeDistance = 10f;
        [Min(0.5f)] public float RoamerReachHeight = 6f;
        [Tooltip("Horizontal distance at which it stops and winds up a slap.")]
        [Min(0.5f)] public float RoamerBonkDistance = 2.6f;
        [Tooltip("Telegraph (hand raised) before the slap, so players can react (GDD rhythm rule).")]
        [Min(0.1f)] public float RoamerWindUpSeconds = 0.8f;
        [Tooltip("How long the slap swing takes; the hit lands at the end of the swing.")]
        [Min(0.05f)] public float RoamerSlapSwingSeconds = 0.22f;
        [Tooltip("Speed the slap gives the balloon.")]
        [Min(0f)] public float RoamerBonkSpeed = 5.5f;
        [Tooltip("Upward share of the slap (0 = flat, 1 = as much up as away).")]
        [Range(0f, 1.5f)] public float RoamerSlapLift = 0.7f;
        [Min(0f)] public float RoamerBonkCooldown = 7f;
        [Tooltip("Knockback when it walks into a player.")]
        [Min(0f)] public float RoamerBumpForce = 4f;

        [Header("Knocked down by a thrown item")]
        [Tooltip("Seconds a giant stays down (ragdoll and dizzy stars) after a thrown item hits it.")]
        [Min(0.5f)] public float RoamerKnockdownSeconds = 3f;
        [Tooltip("How hard the hit throws the ragdoll (m/s along the throw, plus a little up).")]
        [Min(0f)] public float RoamerKnockdownFling = 4f;
        [Tooltip("Seconds it takes to get back up once the knockdown is over.")]
        [Min(0.1f)] public float RoamerGetUpSeconds = 0.8f;

        public int Count => Characters?.Length ?? 0;

        public CharacterDefinition Get(int index) =>
            Count == 0 ? null : Characters[((index % Count) + Count) % Count];
    }
}
