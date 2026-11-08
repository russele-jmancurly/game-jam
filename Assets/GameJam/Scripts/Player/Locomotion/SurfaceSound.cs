using UnityEngine;

namespace GorillaLocomotion
{
    /// <summary>Tap clips played when a hand touches any of these materials (see <see cref="Sounds"/>).</summary>
    [System.Serializable]
    public class SurfaceSound
    {
        [Tooltip("What the surface is (grass, wood, metal, plastic), for logs and effects.")]
        public string Name;

        [Tooltip("A hand tap here kicks up a puff of dirt and grass (HandTapEffects).")]
        public bool KicksUpDirt;

        public Material[] Materials;
        public AudioClip[] Sounds;
    }
}
