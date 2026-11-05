using GameJam.Core;
using UnityEngine;

namespace GameJam.Gameplay
{
    /// <summary>
    /// A character statue on the start stage. Slap it to become that character (synced to everyone). The statue glows
    /// for the character you are currently playing.
    /// </summary>
    public sealed class CharacterPedestal : MonoBehaviour, ISlappable
    {
        private const float StatueHeight = 0.9f;
        private static readonly HapticPulse PickFeedback = new HapticPulse(0.4f, 0.06f);
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private CharacterRoster _roster;
        [SerializeField, Min(0)] private int _rosterIndex;
        [SerializeField] private CharacterVisual _statue;
        [SerializeField] private TextMesh _nameTag;
        [SerializeField] private Renderer _plinth;

        private MaterialPropertyBlock _block;
        private int _paintedSelected = -1;

        public void Configure(CharacterRoster roster, int rosterIndex, CharacterVisual statue, TextMesh nameTag, Renderer plinth)
        {
            _roster = roster;
            _rosterIndex = rosterIndex;
            _statue = statue;
            _nameTag = nameTag;
            _plinth = plinth;
        }

        public bool TryReceiveSlap(in SlapContext slap, out HapticPulse feedback)
        {
            feedback = PickFeedback;
            ICharacterSelector selector = GameServices.LocalCharacter;
            if (selector == null)
            {
                return false;
            }

            selector.SelectCharacter(_rosterIndex);
            return true;
        }

        private void Start()
        {
            _block = new MaterialPropertyBlock();
            CharacterDefinition character = _roster != null ? _roster.Get(_rosterIndex) : null;
            if (character == null)
            {
                return;
            }

            if (_statue != null)
            {
                _statue.Show(character, StatueHeight);
            }

            if (_nameTag != null)
            {
                _nameTag.text = character.DisplayName;
                _nameTag.color = character.Tint;
            }
        }

        private void Update()
        {
            ICharacterSelector selector = GameServices.LocalCharacter;
            int selected = selector != null && selector.CurrentCharacter == _rosterIndex ? 1 : 0;
            if (_plinth == null || selected == _paintedSelected)
            {
                return;
            }

            _paintedSelected = selected;
            Color color = selected == 1 ? ArtPalette.Accent : ArtPalette.TextDark;
            _block.SetColor(BaseColorId, color);
            _block.SetColor(EmissionColorId, selected == 1 ? color : Color.black);
            _plinth.SetPropertyBlock(_block);
        }
    }
}
