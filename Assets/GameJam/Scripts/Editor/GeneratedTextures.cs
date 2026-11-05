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
