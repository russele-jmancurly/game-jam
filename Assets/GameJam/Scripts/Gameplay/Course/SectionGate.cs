using System.Collections;
using UnityEngine;

namespace GameJam.Gameplay
{
    /// <summary>
    /// The door between two sections of the tutorial burrow: a slab of roots that blocks the way until the lesson in
    /// front of it is done, then grinds down into the floor with a rumble and a line of dust, and stays open.
    /// The tutorial guide calls <see cref="Open"/>; nothing else moves it.
    /// </summary>
    public sealed class SectionGate : MonoBehaviour
    {
        [SerializeField] private Transform _door;
        [SerializeField] private float _drop = 4.7f;
        [SerializeField] private float _seconds = 1.6f;
        [SerializeField] private AudioSource _rumble;
        [SerializeField] private ParticleSystem _dust;
        [Tooltip("Half the doorway's width: the dust comes up along the door's foot.")]
        [SerializeField] private float _halfWidth = 2.5f;

        public bool IsOpen { get; private set; }

        public void Configure(Transform door, float drop, float halfWidth, AudioSource rumble, ParticleSystem dust)
        {
            _door = door;
            _drop = drop;
            _halfWidth = halfWidth;
            _rumble = rumble;
            _dust = dust;
        }

        public void Open()
        {
            if (IsOpen || _door == null)
            {
                return;
            }

            IsOpen = true;
            StartCoroutine(Sink());
        }

        private IEnumerator Sink()
        {
            if (_rumble != null)
            {
                _rumble.Play();
            }

            Vector3 start = _door.localPosition;
            float nextDust = 0f;
            for (float t = 0f; t < _seconds; t += Time.deltaTime)
            {
                float k = t / _seconds;
                // A slow start, then it drops away, with a little shake while it grinds.
                float shake = Mathf.Sin(t * 70f) * 0.012f * (1f - k);
                _door.localPosition = start + new Vector3(shake, -_drop * k * k, 0f);
                if (t >= nextDust)
                {
                    nextDust = t + 0.08f;
                    PuffDust();
                }

                yield return null;
            }

            _door.gameObject.SetActive(false); // out of the way for good: no collider left in the doorway
        }

        private void PuffDust()
        {
            if (_dust == null)
            {
                return;
            }

            var emit = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            for (int i = 0; i < 3; i++)
            {
                Vector3 foot = transform.position + transform.right * Random.Range(-_halfWidth, _halfWidth);
                foot.y = transform.position.y + 0.1f;
                emit.position = foot - transform.forward * Random.Range(-0.4f, 0.4f);
                emit.velocity = new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(0.5f, 1.4f), Random.Range(-0.6f, 0.6f));
                emit.startSize = Random.Range(0.5f, 0.9f);
                emit.startLifetime = Random.Range(0.9f, 1.5f);
                _dust.Emit(emit, 1);
            }
        }
    }
}
