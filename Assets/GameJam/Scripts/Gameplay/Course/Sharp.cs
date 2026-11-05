using UnityEngine;

namespace GameJam.Gameplay
{
    /// <summary>Pops the balloon on contact (cactus, dartboard, bug zapper). Harmless to players.</summary>
    public sealed class Sharp : MonoBehaviour
    {
        [SerializeField] private string _popLine = "Popped on something sharp!";

        public string PopLine => _popLine;

        public bool IsActive => isActiveAndEnabled;

        public void Configure(string popLine) => _popLine = popLine;
    }
}
