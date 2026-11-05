using UnityEngine;

namespace GameJam.Core
{
    /// <summary>Tuning for the balloon's floaty physics, damage and pop blast (GDD: The balloon).</summary>
    [CreateAssetMenu(menuName = "Game Jam/Config/Balloon Config", fileName = "BalloonConfig")]
    public sealed class BalloonConfig : ScriptableObject
    {
        [Header("Physics")]
        [Min(0.05f)] public float Radius = 0.25f;
        [Tooltip("Downward acceleration in m/s². Low, so players have time to react.")]
        [Min(0f)] public float Gravity = 1.0f;
        [Tooltip("Air drag. High, for a slow floaty fall.")]
        [Min(0f)] public float Drag = 1.2f;
        [Tooltip("Hard cap on the speed a single hit can give it, so wild swings cannot launch it off the map.")]
        [Min(0.1f)] public float MaxHitSpeed = 6f;
        [Tooltip("Free-flight speed cap, as a multiple of MaxHitSpeed.")]
        [Min(1f)] public float FreeFlightSpeedMultiplier = 1.5f;
        [Tooltip("Small upward kick added to every hit so the balloon stays near eye level.")]
        public float UpwardAssist = 0.6f;
        [Range(0f, 1f)] public float Bounciness = 0.6f;

        [Header("Damage")]
        [Tooltip("Touching a Jman's spikes faster than this pops it. Slower is a graze that leaks air.")]
        [Min(0f)] public float BodyPopSpeed = 2.2f;
        [Tooltip("Seconds between leak steps while grazing spikes.")]
        [Min(0f)] public float LeakCooldown = 1f;
        [Tooltip("Extra gravity per leak step (0.35 = 35% heavier each step).")]
        [Min(0f)] public float GravityPerLeakStep = 0.35f;
        [Tooltip("After a hit, the balloon ignores spikes for this long (the hitter's own body is right there).")]
        [Min(0f)] public float HitGraceSeconds = 0.25f;
        [Tooltip("Speed it bounces away from spikes after a graze.")]
        [Min(0f)] public float SpikePushSpeed = 1.5f;

        [Header("Pass rule")]
        [Tooltip("Solo testing: while only one player is in the session, the same player may hit the balloon " +
                 "twice in a row without popping it. Switches off automatically when a second player joins.")]
        public bool AllowSoloDoubleHits = true;

        [Header("Pop blast")]
        [Min(0f)] public float PopBlastRadius = 1.5f;
        [Min(0f)] public float PopBlastForce = 3f;
        [Min(0f)] public float PopBlastKnockdownSeconds = 0.8f;

        [Header("Visuals")]
        [Tooltip("Visual scale per condition: full, leaking, nearly flat.")]
        public float[] ConditionScales = { 1f, 0.85f, 0.7f };

        public float ScaleFor(BalloonCondition condition)
        {
            int index = (int)condition;
            return ConditionScales != null && index < ConditionScales.Length ? ConditionScales[index] : 1f;
        }
    }
}
