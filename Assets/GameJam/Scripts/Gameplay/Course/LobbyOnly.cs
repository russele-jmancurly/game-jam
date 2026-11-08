using GameJam.Core;
using UnityEngine;

namespace GameJam.Gameplay
{
    /// <summary>
    /// Things that exist only while the crew waits in the lobby: the clear walls around the start stage (so nobody wanders
    /// off before the red button is slapped) and the "slap to start" sign. Everything under this object is switched on in
    /// the lobby and off from the moment the button is pressed until the run returns to the lobby. Reads the networked run
    /// state, so every player sees the same, at no network cost. With no run state yet (connecting) the lobby counts as on.
    /// </summary>
    public sealed class LobbyOnly : MonoBehaviour
    {
        [SerializeField] private GameObject[] _parts = System.Array.Empty<GameObject>();

        private int _shown = -1;

        public void Configure(GameObject[] parts) => _parts = parts;

        public static bool IsLobby => GameServices.Run == null || GameServices.Run.State == RunState.Lobby;

        private void Update()
        {
            int shown = IsLobby ? 1 : 0;
            if (shown == _shown)
            {
                return;
            }

            _shown = shown;
            foreach (GameObject part in _parts)
            {
                if (part != null)
                {
                    part.SetActive(shown == 1);
                }
            }
        }
    }
}
