using UnityEngine;

namespace GameJam.Gameplay
{
    /// <summary>Placeholder commentator and start-line scoreboard: two big 3D texts in the world.</summary>
    public sealed class CommentatorBoard : MonoBehaviour
    {
        [SerializeField] private TextMesh _headline;
        [SerializeField] private TextMesh _scoreboard;

        public static CommentatorBoard Instance { get; private set; }

        public void Configure(TextMesh headline, TextMesh scoreboard)
        {
            _headline = headline;
            _scoreboard = scoreboard;
        }

        public void SetHeadline(string text) => SetText(_headline, text);

        public void SetScoreboard(string text) => SetText(_scoreboard, text);

        private static void SetText(TextMesh target, string text)
        {
            if (target != null && target.text != text)
            {
                target.text = text;
            }
        }

        private void Awake() => Instance = this;

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
