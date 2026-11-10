using UnityEngine;

namespace GameJam.Core
{
    /// <summary>
    /// The show characters on the map: the roaming giants (one per entry in Characters) and the MC on the headline board
    /// (not a giant, so not in Characters).
    /// </summary>
    [CreateAssetMenu(menuName = "Game Jam/Config/Character Roster", fileName = "CharacterRoster")]
    public sealed class CharacterRoster : ScriptableObject
    {
        [Tooltip("Each one roams the yard as a giant (the bootstrap spawns one per entry).")]
        public CharacterDefinition[] Characters = System.Array.Empty<CharacterDefinition>();
        [Tooltip("The MC standing on the headline board (Gonzales). Not a giant.")]
        public CharacterDefinition Mc;

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

        [Header("Kicking players (only when the balloon isn't in reach; the balloon always comes first)")]
        [Tooltip("During a run, it goes after a player standing this close (horizontal metres) when the balloon isn't in reach.")]
        [Min(0f)] public float RoamerKickNoticeDistance = 6f;
        [Tooltip("Only players this far above its feet at most (metres): a kick reaches the lawn, not a branch or its head.")]
        [Min(0.2f)] public float RoamerKickHeight = 1.6f;
        [Tooltip("Horizontal distance at which it plants its foot and winds up. Keep it over the body-bump contact (the " +
                 "giant's capsule, 0.45 x RoamerScale = 1.08 m at scale 2.4, plus the player's ~0.2 m), or walking into the " +
                 "player shoves them first.")]
        [Min(1f)] public float RoamerKickReach = 1.8f;
        [Tooltip("Telegraph (leg drawn back, \"!\") before the kick: time for the player to dodge.")]
        [Min(0.1f)] public float RoamerKickWindUpSeconds = 0.7f;
        [Tooltip("How long the kick swing takes; the foot lands at the end, if the player is still within 1.4 x reach.")]
        [Min(0.05f)] public float RoamerKickSwingSeconds = 0.25f;
        [Tooltip("Launch speed of a kicked player (m/s).")]
        [Min(0f)] public float RoamerKickForce = 7f;
        [Tooltip("Upward share of the kick direction (0 = flat along the lawn, 1 = 45 degrees up).")]
        [Range(0f, 2f)] public float RoamerKickUpward = 0.6f;
        [Tooltip("Seconds a kicked player is ragdolled: hands off, held items dropped, then they get up where they landed.")]
        [Min(0.2f)] public float RoamerKickKnockdownSeconds = 2f;
        [Tooltip("After a kick (hit or miss) it leaves players and the balloon alone for this long.")]
        [Min(0f)] public float RoamerKickCooldown = 6f;

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
