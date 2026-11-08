using UnityEngine;

namespace GameJam.Player
{
    /// <summary>
    /// A small puff of dirt and grass where the local player's hand taps the lawn (only surfaces marked KicksUpDirt in
    /// the rig's Sounds list: grass). Two pooled particle systems on the rig, fired with Emit (no instantiation, no
    /// garbage): clods and grass bits flicked up off the surface, and a faint dust puff. Purely local and visual.
    /// </summary>
    public sealed class HandTapEffects : MonoBehaviour
    {
        [SerializeField] private GorillaLocomotion.Player _locomotion;
        [SerializeField] private ParticleSystem _clods;
        [SerializeField] private ParticleSystem _dust;
        [Tooltip("Clods and grass bits per tap.")]
        [Range(1, 30)] [SerializeField] private int _clodsPerTap = 9;
        [Tooltip("How fast the bits fly off the ground (m/s).")]
        [Min(0f)] [SerializeField] private float _clodSpeed = 1.6f;

        public void Configure(GorillaLocomotion.Player locomotion, ParticleSystem clods, ParticleSystem dust)
        {
            _locomotion = locomotion;
            _clods = clods;
            _dust = dust;
        }

        private void OnEnable()
        {
            if (_locomotion != null)
            {
                _locomotion.HandTapped += OnHandTapped;
            }
        }

        private void OnDisable()
        {
            if (_locomotion != null)
            {
                _locomotion.HandTapped -= OnHandTapped;
            }
        }

        private void OnHandTapped(Vector3 point, Vector3 normal, Material material)
        {
            GorillaLocomotion.SurfaceSound surface = _locomotion.sounds != null ? _locomotion.sounds.SurfaceFor(material) : null;
            if (surface == null || !surface.KicksUpDirt)
            {
                return;
            }

            Vector3 up = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.up;
            Vector3 start = point + up * 0.01f;
            if (_clods != null)
            {
                var emit = new ParticleSystem.EmitParams { position = start, applyShapeToPosition = false };
                for (int i = 0; i < _clodsPerTap; i++)
                {
                    // A cone around the surface normal: mostly up and out, the way dirt flicks off a slapped lawn.
                    Vector3 direction = (up + Random.insideUnitSphere * 0.75f).normalized;
                    emit.velocity = direction * (_clodSpeed * Random.Range(0.5f, 1.2f));
                    _clods.Emit(emit, 1);
                }
            }

            if (_dust != null)
            {
                var dust = new ParticleSystem.EmitParams { position = start + up * 0.03f, applyShapeToPosition = false, velocity = up * 0.25f };
                _dust.Emit(dust, 2);
            }
        }
    }
}
