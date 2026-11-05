using UnityEngine;

namespace GorillaLocomotion
{
    /// <summary>Tap clips played when a hand touches any of these materials (see <see cref="Sounds"/>).</summary>
    [System.Serializable]
    public class SurfaceSound
    {
        public Material[] Materials;
        public AudioClip[] Sounds;
    }
}
