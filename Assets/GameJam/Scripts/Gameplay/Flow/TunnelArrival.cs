using System.Collections;
using UnityEngine;

namespace GameJam.Gameplay
{
    /// <summary>
    /// The landing at the bottom (or top) of the mole tunnel, on the player's rig: when the scene was reached through the
    /// tunnel, a thud plays and soil and dust burst up round the player's feet as the view fades in. Does nothing on a
    /// plain scene load.
    /// </summary>
    public sealed class TunnelArrival : MonoBehaviour
    {
        [SerializeField] private Transform _head;
        [SerializeField] private AudioSource _thud;
        [SerializeField] private ParticleSystem _clods;
        [SerializeField] private ParticleSystem _dust;
        [SerializeField] private float _delay = 0.25f;

        public void Configure(Transform head, AudioSource thud, ParticleSystem clods, ParticleSystem dust)
        {
            _head = head;
            _thud = thud;
            _clods = clods;
            _dust = dust;
        }

        private IEnumerator Start()
        {
            if (!SceneTravel.ConsumeArrival())
            {
                yield break;
            }

            yield return new WaitForSeconds(_delay);
            if (_thud != null)
            {
                _thud.Play();
            }

            Vector3 feet = transform.position;
            if (_head != null)
            {
                feet = new Vector3(_head.position.x, transform.position.y, _head.position.z);
            }

            if (_clods != null)
            {
                var emit = new ParticleSystem.EmitParams { applyShapeToPosition = false };
                for (int i = 0; i < 36; i++)
                {
                    Vector2 ring = Random.insideUnitCircle.normalized * Random.Range(0.25f, 0.6f);
                    emit.position = feet + new Vector3(ring.x, 0.08f, ring.y);
                    emit.velocity = new Vector3(ring.x * 4f, Random.Range(1.4f, 3.2f), ring.y * 4f);
                    emit.startSize = Random.Range(0.05f, 0.11f);
                    emit.startLifetime = Random.Range(0.6f, 0.95f);
                    _clods.Emit(emit, 1);
                }
            }

            if (_dust != null)
            {
                var emit = new ParticleSystem.EmitParams { applyShapeToPosition = false };
                for (int i = 0; i < 10; i++)
                {
                    float angle = i * Mathf.PI * 2f / 10f;
                    var out2 = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    emit.position = feet + out2 * 0.4f + Vector3.up * 0.15f;
                    emit.velocity = out2 * 1.3f + Vector3.up * 0.25f;
                    emit.startSize = Random.Range(0.6f, 1f);
                    emit.startLifetime = Random.Range(1f, 1.5f);
                    _dust.Emit(emit, 1);
                }
            }
        }
    }
}
