using UnityEngine;

namespace GameJam.Core
{
    /// <summary>Tuning for the local VR rig: comfort, grabbing, respawn and the desktop test mode.</summary>
    [CreateAssetMenu(menuName = "Game Jam/Config/Player Rig Config", fileName = "PlayerRigConfig")]
    public sealed class PlayerRigConfig : ScriptableObject
    {
        [Header("Comfort")]
        [Range(15f, 90f)] public float SnapTurnDegrees = 45f;

        [Header("Body")]
        [Tooltip("The body collider sits this far below the head.")]
        public float BodyDrop = 0.4f;
        [Tooltip("Falling below this height respawns you at the current section.")]
        public float KillHeight = -1f;
        [Tooltip("Radius around the body checked for hazards.")]
        [Min(0.05f)] public float HazardRadius = 0.35f;
        [Tooltip("Seconds of immunity after a hazard knocks you down.")]
        [Min(0f)] public float HazardCooldown = 1.5f;

        [Header("Items")]
        [Min(0.02f)] public float GrabRadius = 0.15f;
        [Tooltip("Throw velocity = hand velocity x this.")]
        [Min(0f)] public float ThrowBoost = 1.3f;

        [Header("PC test mode (keyboard and mouse)")]
        [Tooltip("Use keyboard and mouse even when a headset is connected. Without an active headset " +
                 "(Editor, MPPM virtual players, a Windows build) PC mode is always on.")]
        public bool ForceDesktopMode;
        [Tooltip("Show the controls panel in the corner of the screen while in PC mode.")]
        public bool ShowDesktopControlsHelp = true;
        [Min(0f)] public float DesktopMoveSpeed = 3f;
        [Min(0f)] public float DesktopJumpSpeed = 5f;
        [Min(0f)] public float DesktopMouseSensitivity = 0.15f;
    }
}
