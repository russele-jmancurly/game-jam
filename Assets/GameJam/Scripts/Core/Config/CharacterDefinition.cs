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
    }
}
