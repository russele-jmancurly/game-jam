using GameJam.Core;
using UnityEngine;

namespace GameJam.Gameplay
{
    /// <summary>
    /// The finish pad. The run is won when the balloon reaches it after every hoop is cleared; until then it shows as
    /// locked (red) and the commentator says how many hoops are left.
    /// </summary>
    public sealed class FinishLine : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private Renderer _beacon;

        private MaterialPropertyBlock _block;
        private int _paintedState = -1;

        public void Configure(Renderer beacon) => _beacon = beacon;

        private void Awake() => _block = new MaterialPropertyBlock();

        private void Update()
        {
            if (_beacon == null)
            {
                return;
            }

            IRunStatus run = GameServices.Run;
            int state = run != null && run.HoopCount > 0 && run.HoopsCleared >= run.HoopCount ? 1 : 0;
            if (state == _paintedState)
            {
                return;
            }

            _paintedState = state;
            Color color = state == 1 ? ArtPalette.CandyMint : ArtPalette.Hazard;
            _block.SetColor(BaseColorId, color);
            _block.SetColor(EmissionColorId, color * 1.3f);
            _beacon.SetPropertyBlock(_block);
        }
    }
}
