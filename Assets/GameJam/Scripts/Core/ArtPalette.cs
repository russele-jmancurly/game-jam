using UnityEngine;

namespace GameJam.Core
{
    /// <summary>
    /// The art bible's palette, the single source for every environment colour (materials, fog, lights, sky).
    /// Style: sunny toy-scale backyard game show; chunky rounded set pieces, bold candy colours on a soft
    /// sky, everything reads as a TV-studio prop. Mood: late-morning sun from front-left, cool sky bounce
    /// in the shadows, studio rigs ringing the set. Player colours live in <see cref="PlayerPalette"/>.
    /// </summary>
    public static class ArtPalette
    {
        // Ground and water
        public static readonly Color Lawn = Hex(0x7CC84B);
        public static readonly Color LawnDark = Hex(0x5FA83A);
        public static readonly Color Pool = Hex(0x4FC3E8);
        public static readonly Color PoolDeep = Hex(0x2E9CCB);
        public static readonly Color PoolTile = Hex(0xF2F7F9);

        // Set pieces
        public static readonly Color Timber = Hex(0xE8B66A);
        public static readonly Color TimberDark = Hex(0xB9823F);
        public static readonly Color Pebble = Hex(0xC9C2B8);
        public static readonly Color Moss = Hex(0x8FD16A);
        public static readonly Color Picket = Hex(0xF6F1E7);
        public static readonly Color Climbable = Hex(0xFF8A3D);
        public static readonly Color Roof = Hex(0xD9644A);
        public static readonly Color Brick = Hex(0xC2614F);
        public static readonly Color HouseWall = Hex(0xF3D9B1);
        public static readonly Color Window = Hex(0x9FD8F2);
        public static readonly Color Steel = Hex(0x8E97A6);
        public static readonly Color Charcoal = Hex(0x343A46);

        // Gameplay signals
        public static readonly Color Hazard = Hex(0xE8384F);
        public static readonly Color Warning = Hex(0xFFC93C);
        public static readonly Color Flame = Hex(0xFF6A1F);
        public static readonly Color Accent = Hex(0xFFD23F);
        public static readonly Color AccentDeep = Hex(0xF2A81D);

        // Candy colours for flowers, bunting and the crowd
        public static readonly Color CandyPink = Hex(0xFF7EB6);
        public static readonly Color CandyPurple = Hex(0xA77BF3);
        public static readonly Color CandyBlue = Hex(0x5AA9FF);
        public static readonly Color CandyMint = Hex(0x6FE3C1);

        // Light and atmosphere
        public static readonly Color SunLight = Hex(0xFFF0D6);
        public static readonly Color SkyTop = Hex(0x7EC8F0);
        public static readonly Color AmbientSky = Hex(0xA8D8F0);
        public static readonly Color AmbientEquator = Hex(0xE9E1CF);
        public static readonly Color AmbientGround = Hex(0x6F8A4E);
        public static readonly Color Fog = Hex(0xCFE6F0);
        public static readonly Color Shadow = Hex(0x5A6B8C);
        public static readonly Color Cloud = Hex(0xFFFFFF);
        public static readonly Color TextDark = Hex(0x1F2A44);

        public static readonly Color[] Candy = { CandyPink, CandyPurple, CandyBlue, CandyMint, Accent, Hazard, Climbable };

        public static Color Hex(int rgb) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);
    }
}
