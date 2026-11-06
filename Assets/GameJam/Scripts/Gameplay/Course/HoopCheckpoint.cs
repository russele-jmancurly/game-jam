using GameJam.Core;
using UnityEngine;

namespace GameJam.Gameplay
{
    /// <summary>
    /// A glowing hoop somewhere in the yard. Guiding the balloon through it clears it (any order); clearing every hoop
    /// unlocks the finish. A cleared hoop is also where the balloon comes back after a pop.
    /// Detection is a trigger the balloon's contact check reads; once cleared the hoop shrinks away and stays hidden until
    /// the next run resets it.
    /// </summary>
    public sealed class HoopCheckpoint : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField, Min(0)] private int _index;
        [SerializeField] private Renderer[] _ringRenderers;
        [SerializeField] private float _spinDegreesPerSecond = 30f;
        [SerializeField] private Transform _spinner;
        [Tooltip("Plays the clear chime at the hoop.")]
        [SerializeField] private AudioSource _clearAudio;
        [Tooltip("Confetti burst material when cleared.")]
        [SerializeField] private Material _burstMaterial;

        private const float VanishSeconds = 0.4f;

        private MaterialPropertyBlock _block;
        private bool _wasCleared;
        private bool _hasPainted;
        private float _clearedAt = -1f;
        private Transform[] _parts;
        private Vector3[] _partScales;

        private static readonly System.Collections.Generic.List<HoopCheckpoint> ActiveHoops = new System.Collections.Generic.List<HoopCheckpoint>();

        /// <summary>Every hoop in the loaded yard (for items that aim the balloon at one).</summary>
        public static System.Collections.Generic.IReadOnlyList<HoopCheckpoint> All => ActiveHoops;

        public int Index => _index;

        public void Configure(int index, Renderer[] ringRenderers, Transform spinner, AudioSource clearAudio = null, Material burstMaterial = null)
        {
            _clearAudio = clearAudio;
            _burstMaterial = burstMaterial;
            _index = index;
            _ringRenderers = ringRenderers;
            _spinner = spinner;
        }

        /// <summary>The closest hoop not yet cleared within range of a point, or null.</summary>
        public static HoopCheckpoint NearestOpen(Vector3 from, float range)
        {
            IRunStatus run = GameServices.Run;
            HoopCheckpoint best = null;
            float bestDistance = range * range;
            foreach (HoopCheckpoint hoop in ActiveHoops)
            {
                if (run != null && run.IsHoopCleared(hoop._index))
                {
                    continue;
                }

                float distance = (hoop.transform.position - from).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = hoop;
                }
            }

            return best;
        }

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            var parts = new System.Collections.Generic.List<Transform>();
            foreach (Transform child in transform)
            {
                parts.Add(child); // the ring and its label
            }

            _parts = parts.ToArray();
            _partScales = new Vector3[_parts.Length];
            for (int i = 0; i < _parts.Length; i++)
            {
                _partScales[i] = _parts[i].localScale;
            }
        }

        private void OnEnable() => ActiveHoops.Add(this);

        private void OnDisable() => ActiveHoops.Remove(this);

        private void Update()
        {
            if (_spinner != null)
            {
                _spinner.Rotate(0f, _spinDegreesPerSecond * Time.deltaTime, 0f, Space.World);
            }

            IRunStatus run = GameServices.Run;
            bool isCleared = run != null && run.IsHoopCleared(_index);
            if (_clearedAt >= 0f)
            {
                Shrink();
            }

            if (_hasPainted && isCleared == _wasCleared)
            {
                return;
            }

            bool justCleared = isCleared && _hasPainted && !_wasCleared; // not on the first paint (late joiners)
            _hasPainted = true;
            _wasCleared = isCleared;
            if (justCleared)
            {
                if (_clearAudio != null)
                {
                    _clearAudio.Play();
                }

                // Confetti in the ring's two colours.
                PopBurstEffect.Play(transform.position, ArtPalette.Accent, _burstMaterial);
                PopBurstEffect.Play(transform.position, ArtPalette.CandyMint, _burstMaterial);
            }

            _clearedAt = isCleared ? Time.time : -1f;
            if (!isCleared)
            {
                ShowParts(true, 1f); // a new run: back again
            }

            Color color = isCleared ? ArtPalette.CandyMint : ArtPalette.Accent;
            _block.SetColor(BaseColorId, color);
            _block.SetColor(EmissionColorId, color * (isCleared ? 0.4f : 1.2f));
            foreach (Renderer ring in _ringRenderers)
            {
                if (ring != null)
                {
                    ring.SetPropertyBlock(_block);
                }
            }
        }

        /// <summary>Cleared: the ring and label shrink to nothing, then hide.</summary>
        private void Shrink()
        {
            float left = 1f - (Time.time - _clearedAt) / VanishSeconds;
            if (left <= 0f)
            {
                ShowParts(false, 1f);
                _clearedAt = -1f;
                return;
            }

            ShowParts(true, left * left);
        }

        private void ShowParts(bool isVisible, float scale)
        {
            for (int i = 0; i < _parts.Length; i++)
            {
                Transform part = _parts[i];
                if (part.gameObject.activeSelf != isVisible)
                {
                    part.gameObject.SetActive(isVisible);
                }

                part.localScale = _partScales[i] * scale;
            }
        }
    }
}
