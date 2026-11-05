using UnityEngine;

namespace GameJam.Gameplay
{
    /// <summary>
    /// Ground the balloon must never touch (the game's title rule): the lawn, the sandbox, paths. Players walk on it
    /// freely; the balloon pops the moment it lands.
    /// </summary>
    public sealed class BalloonGround : MonoBehaviour
    {
        [SerializeField] private string _popLine = "It touched the ground!";

        public string PopLine => _popLine;

        public void Configure(string popLine) => _popLine = popLine;
    }
}
