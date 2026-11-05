using GameJam.Core;
using UnityEngine;

namespace GameJam.Gameplay
{
    /// <summary>
    /// A glowing hoop somewhere in the yard. Guiding the balloon through it clears it (any order); clearing every hoop
    /// unlocks the finish. A cleared hoop is also where the balloon comes back after a pop.
    /// Detection is a trigger the balloon's contact check reads; the visual swaps colour once cleared.
    /// </summary>
    public sealed class HoopCheckpoint : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField, Min(0)] private int _index;
        [SerializeField] private Renderer[] _ringRenderers;
        [SerializeField] private float _spinDegreesPerSecond = 30f;
        [SerializeField] private Transform _spinner;

        private MaterialPropertyBlock _block;
        private bool _wasCleared;
        private bool _hasPainted;

        public int Index => _index;

        public void Configure(int index, Renderer[] ringRenderers, Transform spinner)
        {
            _index = index;
            _ringRenderers = ringRenderers;
            _spinner = spinner;
        }

        private void Awake() => _block = new MaterialPropertyBlock();

        private void Update()
        {
            if (_spinner != null)
            {
                _spinner.Rotate(0f, _spinDegreesPerSecond * Time.deltaTime, 0f, Space.World);
            }

            IRunStatus run = GameServices.Run;
            bool isCleared = run != null && run.IsHoopCleared(_index);
            if (_hasPainted && isCleared == _wasCleared)
            {
                return;
            }

            _hasPainted = true;
            _wasCleared = isCleared;
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
    }
}
