using UnityEngine;

namespace GameJam.Gameplay
{
    /// <summary>
    /// A mole hole: a mound of soil round a dark opening. A player whose body gets into the opening (RigHazardSensor finds
    /// the trigger under this object) dives in: soil flies up, a dig-and-drop sound plays, the view fades and
    /// <see cref="SceneTravel"/> takes that player alone down the tunnel to <see cref="_destination"/>.
    /// The lobby's hole leads to the tutorial; the tutorial's leads back to the yard.
    /// </summary>
    public sealed class MoleHole : MonoBehaviour
    {
        [SerializeField] private string _destination = SceneTravel.TutorialScene;
        [SerializeField] private AudioSource _diveAudio;
        [Tooltip("Clods of soil thrown up as the player dives in (fired with Emit).")]
        [SerializeField] private ParticleSystem _clods;
        [SerializeField] private ParticleSystem _dust;
        [Range(0, 80)] [SerializeField] private int _clodCount = 40;
        [Range(0, 20)] [SerializeField] private int _dustCount = 8;

        public string Destination => _destination;

        public void Configure(string destination, AudioSource diveAudio, ParticleSystem clods, ParticleSystem dust)
        {
            _destination = destination;
            _diveAudio = diveAudio;
            _clods = clods;
            _dust = dust;
        }

        /// <summary>The local player dived in. Ignored while a trip is already under way.</summary>
        public void Enter()
        {
            if (SceneTravel.IsTravelling)
            {
                return;
            }

            if (_diveAudio != null)
            {
                _diveAudio.Play();
            }

            ThrowSoil();
            SceneTravel.Go(_destination);
        }

        private void ThrowSoil()
        {
            Vector3 mouth = transform.position + Vector3.up * 0.25f;
            if (_clods != null)
            {
                var emit = new ParticleSystem.EmitParams { applyShapeToPosition = false };
                for (int i = 0; i < _clodCount; i++)
                {
                    Vector2 ring = Random.insideUnitCircle;
                    emit.position = mouth + new Vector3(ring.x, 0f, ring.y) * 0.45f;
                    emit.velocity = new Vector3(ring.x * 2.2f, Random.Range(2.2f, 4.8f), ring.y * 2.2f);
                    emit.startSize = Random.Range(0.06f, 0.14f);
                    emit.startLifetime = Random.Range(0.7f, 1.1f);
                    _clods.Emit(emit, 1);
                }
            }

            if (_dust != null)
            {
                var emit = new ParticleSystem.EmitParams { applyShapeToPosition = false };
                for (int i = 0; i < _dustCount; i++)
                {
                    Vector2 ring = Random.insideUnitCircle;
                    emit.position = mouth + new Vector3(ring.x, 0.1f, ring.y) * 0.6f;
                    emit.velocity = new Vector3(ring.x * 0.8f, Random.Range(0.4f, 1.1f), ring.y * 0.8f);
                    emit.startSize = Random.Range(0.5f, 0.9f);
                    emit.startLifetime = Random.Range(0.9f, 1.4f);
                    _dust.Emit(emit, 1);
                }
            }
        }
    }
}
