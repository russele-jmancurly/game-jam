using UnityEngine;

namespace GameJam.Core
{
    /// <summary>One show character on the map (Jmancurly, Squoof): model, size and colour. Players are all the porcupine.</summary>
    [CreateAssetMenu(menuName = "Game Jam/Config/Character Definition", fileName = "Character_New")]
    public sealed class CharacterDefinition : ScriptableObject
    {
        public string DisplayName = "Character";

        [Tooltip("The character's model (an imported FBX). Leave empty to show a coloured placeholder.")]
        public GameObject Model;

        [Tooltip("Height the model is scaled to, in metres, for a player avatar.")]
        [Min(0.3f)] public float Height = 1.6f;

        [Tooltip("Extra yaw if the model doesn't face +Z after import.")]
        public float YawOffset;

        [Tooltip("Name tag and placeholder colour.")]
        public Color Tint = Color.white;

        [Tooltip("Optional: replaces every material on the model (e.g. while its real textures aren't imported yet).")]
        public Material MaterialOverride;

        [Header("Animation (clips from the rigged FBX, linked by the prototype builder)")]
        public AnimationClip Idle;
        public AnimationClip Walk;
        public AnimationClip WindUp;
        [Tooltip("Swing, hit, follow-through and back to idle; the hand meets the target at SlapHitTime.")]
        public AnimationClip Slap;
        public AnimationClip Wave;
        [Tooltip("Seconds into the Slap clip at which the hand hits.")]
        [Min(0.01f)] public float SlapHitTime = 7f / 30f;
        [Tooltip("Distance the Walk cycle covers per second at normal playback, in body heights (rig_character.py prints it; --measure re-measures).")]
        [Min(0.05f)] public float WalkStrideHeights = 1.0f;

        public bool IsAnimated => Model != null && Idle != null;
    }
}
