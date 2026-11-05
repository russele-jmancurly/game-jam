using Fusion;
using GameJam.Core;
using UnityEngine;

namespace GameJam.Net
{
    public static class PlayerRefExtensions
    {
        /// <summary>The player's palette colour; PlayerRef.None (nobody) is neutral white.</summary>
        public static Color ToPaletteColor(this PlayerRef player) =>
            PlayerPalette.Get(player == PlayerRef.None ? -1 : player.PlayerId);
    }
}
