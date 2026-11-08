using System;
using System.IO;
using GameJam.Core;
using UnityEditor;
using UnityEngine;

namespace GameJam.Editor
{
    /// <summary>
    /// Small palette-tinted textures generated in code (placeholder doctrine: subtle, palette-driven, cheap on Quest).
    /// Saved as PNGs under Art/Textures/Generated so materials reference real assets.
    /// </summary>
    public static class GeneratedTextures
    {
        public const string Folder = "Assets/GameJam/Art/Textures/Generated";

        public static Texture2D Checker(string name, Color a, Color b, int cells = 8, int size = 64) =>
            Create(name, size, FilterMode.Point, TextureWrapMode.Repeat, (x, y) =>
                ((x * cells / size) + (y * cells / size)) % 2 == 0 ? a : b);

        public static Texture2D Stripes(string name, Color a, Color b, int stripes = 4, int size = 64) =>
            Create(name, size, FilterMode.Bilinear, TextureWrapMode.Repeat, (x, y) =>
                (((x + y) * stripes * 2 / size) % 2) == 0 ? a : b);

        /// <summary>Soft round blob, white with alpha falloff; tinted by the material (balloon ground shadow).</summary>
        public static Texture2D RadialBlob(string name, int size = 64) =>
            Create(name, size, FilterMode.Bilinear, TextureWrapMode.Clamp, (x, y) =>
            {
                float dx = (x + 0.5f) / size * 2f - 1f;
                float dy = (y + 0.5f) / size * 2f - 1f;
                float alpha = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                return new Color(1f, 1f, 1f, alpha * alpha * (3f - 2f * alpha));
            });

        /// <summary>
        /// The "not your turn" icon: a white balloon (dark outline, knot and string) under a red no-sign (ring + slash), on a
        /// soft dark disc so it reads against a bright sky. Shapes are signed-distance fields, antialiased over ~1.5 px.
        /// </summary>
        public static Texture2D NotYourTurnIcon(string name, int size = 256) =>
            Create(name, size, FilterMode.Bilinear, TextureWrapMode.Clamp, (x, y) =>
            {
                float px = 2f / size;
                var p = new Vector2((x + 0.5f) / size * 2f - 1f, (y + 0.5f) / size * 2f - 1f);
                float Cover(float distance) => Mathf.Clamp01(0.5f - distance / (px * 1.5f));

                // Soft backing disc.
                Color result = new Color(ArtPalette.TextDark.r, ArtPalette.TextDark.g, ArtPalette.TextDark.b, 0f);
                result.a = Cover(p.magnitude - 0.96f) * 0.5f;

                // Balloon: ellipse body, little knot, wavy string.
                Vector2 q = (p - new Vector2(0f, 0.14f)) / new Vector2(0.4f, 0.48f);
                float body = (q.magnitude - 1f) * 0.4f;
                float knot = Mathf.Max(Mathf.Abs(p.x) - (0.07f - (p.y + 0.4f) * 0.6f), Mathf.Abs(p.y + 0.385f) - 0.05f);
                float stringX = 0.04f * Mathf.Sin((p.y + 0.45f) * 14f);
                float line = p.y < -0.42f && p.y > -0.8f ? Mathf.Abs(p.x - stringX) - 0.012f : 1f;
                float balloon = Mathf.Min(body, knot);
                result = Over(result, ArtPalette.TextDark, Cover(Mathf.Min(balloon - 0.035f, line)));
                result = Over(result, new Color(0.97f, 0.97f, 0.97f), Cover(balloon));

                // Red no-sign on top: a ring and a slash from top left to bottom right.
                float ring = Mathf.Abs(p.magnitude - 0.8f) - 0.075f;
                Vector2 axis = new Vector2(0.7071f, -0.7071f);
                float along = Mathf.Clamp(Vector2.Dot(p, axis), -0.8f, 0.8f);
                float slash = (p - axis * along).magnitude - 0.075f;
                result = Over(result, ArtPalette.Hazard, Cover(Mathf.Min(ring, slash)));
                return result;
            });

        private static Color Over(Color below, Color colour, float coverage)
        {
            Color c = Color.Lerp(below, colour, coverage);
            c.a = Mathf.Lerp(below.a, 1f, coverage);
            return c;
        }

        /// <summary>Low-octave value noise between two colours (lawn, pool ripples); 10-15% value variation.</summary>
        public static Texture2D Noise(string name, Color a, Color b, float scale, int size = 128) =>
            Create(name, size, FilterMode.Bilinear, TextureWrapMode.Repeat, (x, y) =>
            {
                float u = (float)x / size;
                float v = (float)y / size;
                float n = TileableNoise(u, v, scale) * 0.65f + TileableNoise(u, v, scale * 2.3f) * 0.35f;
                return Color.Lerp(a, b, n);
            });

        /// <summary>Pool tiles: light grout lines on a water colour.</summary>
        public static Texture2D Tiles(string name, Color tile, Color grout, int cells = 4, int size = 64) =>
            Create(name, size, FilterMode.Bilinear, TextureWrapMode.Repeat, (x, y) =>
            {
                int cell = size / cells;
                return x % cell == 0 || y % cell == 0 ? grout : tile;
            });

        private static float TileableNoise(float u, float v, float scale)
        {
            // Periodic sine-sum noise: tiles seamlessly and needs no random state.
            float s = Mathf.PI * 2f;
            float n = Mathf.Sin(u * s * Mathf.Round(scale)) * Mathf.Cos(v * s * Mathf.Round(scale * 0.7f + 1f))
                      + 0.5f * Mathf.Sin((u + v) * s * Mathf.Round(scale * 1.3f + 1f))
                      + 0.25f * Mathf.Cos((u - v) * s * Mathf.Round(scale * 2.1f + 1f));
            return Mathf.InverseLerp(-1.75f, 1.75f, n);
        }

        private static Texture2D Create(string name, int size, FilterMode filter, TextureWrapMode wrap, Func<int, int, Color> pixel)
        {
            string path = $"{Folder}/{name}.png";
            Directory.CreateDirectory(Folder);

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    texture.SetPixel(x, y, pixel(x, y));
                }
            }

            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.filterMode = filter;
            importer.wrapMode = wrap;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = size;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Keeps the palette as the source: generated textures only ever mix palette colours.</summary>
        public static Color Mix(Color a, Color b, float t) => Color.Lerp(a, b, t);

        internal static Color Darker(Color c, float amount = 0.12f) => Color.Lerp(c, ArtPalette.Shadow, amount);
    }
}
