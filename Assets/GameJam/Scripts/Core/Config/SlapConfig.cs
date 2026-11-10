using UnityEngine;

namespace GameJam.Core
{
    /// <summary>Tuning for slapping with the gloves (GDD: Movement and controls, Ragdoll and friendly fire).</summary>
    [CreateAssetMenu(menuName = "Game Jam/Config/Slap Config", fileName = "SlapConfig")]
    public sealed class SlapConfig : ScriptableObject
    {
        [Header("Hand")]
        [Tooltip("Radius of the hit sweep around each glove.")]
        [Min(0.01f)] public float HandRadius = 0.08f;
        [Tooltip("How fast the tracked hand velocity follows the real one (0 = never, 1 = instantly).")]
        [Range(0.05f, 1f)] public float VelocitySmoothing = 0.6f;
        [Tooltip("Seconds before the same hand can hit again.")]
        [Min(0f)] public float Cooldown = 0.2f;

        [Header("Balloon")]
        [Tooltip("Slower hand speeds than this pass through without a hit.")]
        [Min(0f)] public float MinBalloonSlapSpeed = 0.6f;
        [Tooltip("Balloon speed = hand speed x this, then capped by BalloonConfig.MaxHitSpeed.")]
        [Min(0f)] public float BalloonVelocityScale = 0.9f;
        [Tooltip("After a hand hits something, neither hand can hit it again until both have left it and this many " +
                 "seconds have passed (a hand resting inside the balloon used to re-hit it: a double-hit pop).")]
        [Min(0f)] public float RehitSeconds = 0.3f;
        [Tooltip("A touch slower than MinBalloonSlapSpeed is a nudge: no hit, no turn used, the balloon just drifts off " +
                 "the hand at up to this speed (m/s), like pushing a real balloon. Sideways or down only, never up: a hand " +
                 "held under it must not keep it in the air without a turn.")]
        [Min(0f)] public float NudgeSpeed = 0.8f;
        [Tooltip("Seconds between nudges while a hand rests on the balloon (each one is a network message).")]
        [Min(0.02f)] public float NudgeInterval = 0.1f;
        [Tooltip("A nudge keeps the balloon off your quills this long: your body is right behind your hand.")]
        [Min(0f)] public float NudgeGraceSeconds = 0.5f;

        [Header("Friends")]
        [Tooltip("Hand speed needed to knock a friend over.")]
        [Min(0f)] public float FriendSlapSpeed = 2.5f;
        [Min(0f)] public float FriendForceScale = 1.2f;
        [Min(0f)] public float FriendMaxForce = 7f;
        [Range(0f, 2f)] public float FriendKnockdownSeconds = 1.5f;
    }
}
