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
        [Tooltip("Sun and ambient light are multiplied by this in the headset only (applied live; 1 = as authored). The " +
                 "headset has no HDR and no tonemapping, so bright colours clip to white: at 1.35 (the old boost) the Quest build " +
                 "was washed out, and 0.8 keeps the deck, grass and bunting colourful. light_probe.cs compares values.")]
        [UnityEngine.Serialization.FormerlySerializedAs("HeadsetLightBoost")]
        [Range(0.5f, 1.5f)] public float HeadsetLightScale = 0.8f;

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
        [Tooltip("Volume of the hiss while a hand slides on a slippery surface (it fades in and out).")]
        [Range(0f, 1f)] public float SlideVolume = 0.35f;
        [Range(0f, 1f)] public float TapHapticStrength = 0.5f;
        [Range(0f, 1f)] public float SlideHapticStrength = 0.075f;

        [Header("Stick movement (left thumbstick, headset only)")]
        [Tooltip("Walk with the left thumbstick, in the direction you look. Arm swinging still works on top of it.")]
        public bool StickMovement = true;
        [Tooltip("Walking speed (m/s) with the stick pushed all the way.")]
        [Range(0f, 6f)] public float StickMoveSpeed = 2.5f;
        [Tooltip("Stick deflection ignored around the centre (drifting sticks).")]
        [Range(0f, 0.5f)] public float StickDeadZone = 0.15f;
        [Tooltip("Ledges and bumps up to this high are stepped over while walking with the stick (m).")]
        [Range(0f, 0.5f)] public float StickStepHeight = 0.2f;

        [Header("Body")]
        [Tooltip("See your own gorilla arms (shoulder to glove) in first person; off = floating glove spheres only.")]
        public bool ShowOwnArms = true;
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
        [Tooltip("Throw velocity = the swing's velocity x this.")]
        [Min(0f)] public float ThrowBoost = 1f;
        [Tooltip("A throw uses the fastest moment of the swing within this long before you let go (you open your hand a " +
                 "little after the swing's peak).")]
        [Range(0.02f, 0.3f)] public float ThrowWindowSeconds = 0.12f;
        [Tooltip("Length of the stretch the swing's velocity is measured over. Longer = steadier, shorter = snappier.")]
        [Range(0.01f, 0.1f)] public float ThrowSampleSeconds = 0.04f;
        [Tooltip("Fastest a thrown item can leave the hand, after the boost.")]
        [Min(1f)] public float ThrowMaxSpeed = 16f;

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
