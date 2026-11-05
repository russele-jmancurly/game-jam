using System.Collections.Generic;
using UnityEngine;

namespace GorillaLocomotion
{
    /// <summary>
    /// Hand tap sounds per surface material (from GorillaPlus). Materials are matched by reference, then by name
    /// (for instanced copies); the match is cached so a tap allocates nothing.
    /// </summary>
    public class Sounds : MonoBehaviour
    {
        [Header("Surface Sounds")]
        public SurfaceSound[] Surfaces;

        [Header("Default Sounds")]
        public AudioClip[] DefaultSounds;

        private readonly Dictionary<Material, SurfaceSound> _matches = new Dictionary<Material, SurfaceSound>();

        public AudioClip GetSound(Material material)
        {
            SurfaceSound surface = material != null ? Match(material) : null;
            if (surface != null)
            {
                return surface.Sounds[Random.Range(0, surface.Sounds.Length)];
            }

            if (DefaultSounds != null && DefaultSounds.Length > 0)
            {
                return DefaultSounds[Random.Range(0, DefaultSounds.Length)];
            }

            return null;
        }

        private SurfaceSound Match(Material material)
        {
            if (_matches.TryGetValue(material, out SurfaceSound cached))
            {
                return cached;
            }

            SurfaceSound found = null;
            if (Surfaces != null)
            {
                string name = material.name.Replace(" (Instance)", string.Empty);
                foreach (SurfaceSound surface in Surfaces)
                {
                    if (surface?.Materials == null || surface.Sounds == null || surface.Sounds.Length == 0)
                    {
                        continue;
                    }

                    foreach (Material candidate in surface.Materials)
                    {
                        if (candidate != null && (candidate == material || candidate.name == name))
                        {
                            found = surface;
                            break;
                        }
                    }

                    if (found != null)
                    {
                        break;
                    }
                }
            }

            _matches[material] = found;
            return found;
        }
    }
}
