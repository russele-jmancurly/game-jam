using UnityEngine;

namespace GameJam.Core
{
    /// <summary>Tuning for the local VR rig: comfort, grabbing, respawn and the desktop test mode.</summary>
    [CreateAssetMenu(menuName = "Game Jam/Config/Player Rig Config", fileName = "PlayerRigConfig")]
    public sealed class PlayerRigConfig : ScriptableObject
    {
        [Header("Comfort")]
        [Range(15f, 90f)] public float SnapTurnDegrees = 45f;

        [Header("Rendering")]
        [Tooltip("Post-processing (colour grading, vignette) costs a full-screen pass. Off in the headset by default " +
                 "to protect Quest frame time; always on in PC test mode.")]
        public bool PostProcessingInHeadset;

        [Header("Gorilla locomotion (GorillaPlus; applied every frame, so tune it live)")]
        [Tooltip("Hand speed (m/s, averaged) needed to launch off a surface. Lower = jumps trigger more easily.")]
        [Range(0.1f, 1.5f)] public float VelocityLimit = 0.4f;
        [Tooltip("Push-off speed needed to leave a slippery surface.")]
        [Range(0.1f, 1.5f)] public float SlideVelocityLimit = 0.7f;
        [Tooltip("Top launch speed (m/s).")]
        [Min(0f)] public float MaxJumpSpeed = 6.5f;
        [Tooltip("Launch speed = averaged hand speed x this.")]
        [Min(0f)] public float JumpMultiplier = 1.1f;
        [Tooltip("Hands further than this from the head are pulled back (stops stretched-arm launches).")]
        [Range(1f, 2.5f)] public float MaxArmLength = 1.5f;
        [Tooltip("Frames averaged for the launch velocity: more = smoother, steadier jumps; fewer = snappier. " +
                 "Read when the rig starts.")]
        [Range(2, 10)] public int VelocityHistorySize = 8;
        [Tooltip("Hand grip on ordinary surfaces (0 = sticky, higher = hands slide along walls).")]
        [Range(0.001f, 0.1f)] public float DefaultSlideFactor = 0.03f;
        [Range(0f, 1f)] public float TapVolume = 0.6f;
        [Range(0f, 1f)] public float TapHapticStrength = 0.5f;
        [Range(0f, 1f)] public float SlideHapticStrength = 0.075f;

        [Header("Body")]
        [Tooltip("The body collider's centre sits this far below the head.")]
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
