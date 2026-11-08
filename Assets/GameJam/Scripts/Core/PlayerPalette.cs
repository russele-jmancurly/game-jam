using UnityEngine;

namespace GameJam.Core
{
    /// <summary>One colour per player, used for avatars and the balloon's last-hitter glow.</summary>
    public static class PlayerPalette
    {
        /// <summary>
        /// Colour for "nobody" (a fresh balloon, a hazard hit): party-balloon pink, which no player has. It was an
        /// off-white that shaded to a dull grey in the yard.
        /// </summary>
        public static readonly Color Neutral = ArtPalette.CandyPink;

        private static readonly Color[] Colors =
        {
            new Color(1f, 0.35f, 0.35f),
            new Color(0.3f, 0.6f, 1f),
            new Color(0.35f, 0.9f, 0.4f),
            new Color(1f, 0.8f, 0.2f),
            new Color(0.8f, 0.45f, 1f),
            new Color(1f, 0.55f, 0.15f),
        };

        /// <param name="playerId">Network player id; any negative value means nobody.</param>
        public static Color Get(int playerId)
        {
            if (playerId < 0)
            {
                return Neutral;
            }

            return Colors[playerId % Colors.Length];
        }
    }
}
