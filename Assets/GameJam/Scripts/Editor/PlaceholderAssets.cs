using System.Collections.Generic;
using System.IO;
using Fusion;
using UnityEditor;
using UnityEngine;

namespace GameJam.Editor
{
    /// <summary>Asset helpers for the prototype builder: folders, config assets, placeholder materials, prefabs.</summary>
    public static class PlaceholderAssets
    {
        public const string Root = "Assets/GameJam";
        public const string ConfigFolder = Root + "/Config";
        public const string ItemConfigFolder = ConfigFolder + "/Items";
        public const string MaterialFolder = Root + "/Materials/Placeholder";
        public const string PrefabFolder = Root + "/Prefabs/Net";
        public const string SceneFolder = Root + "/Scenes";

        private static readonly Dictionary<string, Material> MaterialCache = new Dictionary<string, Material>();

        public static void EnsureFolders()
        {
            MaterialCache.Clear();
            EnsureFolder(ItemConfigFolder);
            EnsureFolder(MaterialFolder);
            EnsureFolder(PrefabFolder);
            EnsureFolder(SceneFolder);
        }

        /// <summary>Loads a config asset, creating it with default values only if it does not exist yet (keeps tuning).</summary>
        public static T LoadOrCreateConfig<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        /// <summary>Matte "painted toy" finish by default; glossy only where the art bible wants shine.</summary>
        public const float MatteSmoothness = 0.18f;

        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        /// <summary>A URP Lit material in the placeholder folder, reused if it exists. Colours come from ArtPalette.</summary>
        public static Material Material(string name, Color color, float smoothness = MatteSmoothness, Texture2D texture = null, Vector2? tiling = null)
        {
            if (MaterialCache.TryGetValue(name, out Material cached) && cached != null)
            {
                return cached;
            }

            Material material = LoadOrCreate(name, "Universal Render Pipeline/Lit");
            material.color = color;
            material.SetFloat(SmoothnessId, smoothness);
            material.SetTexture(BaseMapId, texture);
            material.SetTextureScale(BaseMapId, tiling ?? Vector2.one);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            MaterialCache[name] = material;
            return material;
        }

        /// <summary>A lit material that also glows (studio lamps, LIVE light). No real light, so it costs nothing extra on Quest.</summary>
        public static Material Emissive(string name, Color color, float intensity = 1.5f)
        {
            Material material = Material(name, color, 0.5f);
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            material.SetColor(EmissionColorId, color * intensity);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>URP Unlit, opaque (a mirror surface: its texture is set at runtime).</summary>
        public static Material Unlit(string name, Color color)
        {
            if (MaterialCache.TryGetValue(name, out Material cached) && cached != null)
            {
                return cached;
            }

            Material material = LoadOrCreate(name, "Universal Render Pipeline/Unlit");
            material.color = color;
            EditorUtility.SetDirty(material);
            MaterialCache[name] = material;
            return material;
        }

        /// <summary>URP Unlit, alpha-blended (decal-style blob shadow). Tinted per renderer with a property block.</summary>
        public static Material TransparentUnlit(string name, Texture2D texture)
        {
            if (MaterialCache.TryGetValue(name, out Material cached) && cached != null)
            {
                return cached;
            }

            Material material = LoadOrCreate(name, "Universal Render Pipeline/Unlit");
            material.SetTexture(BaseMapId, texture);
            material.SetFloat("_Surface", 1f); // transparent
            material.SetFloat("_Blend", 0f);   // alpha
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            MaterialCache[name] = material;
            return material;
        }

        /// <summary>URP Unlit, alpha-blended, one colour (its alpha sets how see-through): clear glass walls.</summary>
        public static Material Glass(string name, Color color)
        {
            Material material = TransparentUnlit(name, null);
            material.color = color;
            material.SetFloat("_Cull", 0f); // both sides: seen from inside and outside the stage
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material LoadOrCreate(string name, string shaderName)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find(shaderName) ?? Shader.Find("Standard");
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            return material;
        }

        public static NetworkObject SavePrefab(GameObject root, string name)
        {
            string path = $"{PrefabFolder}/{name}.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<NetworkObject>();
        }

        public static void SetNetworkFlags(NetworkObject networkObject, NetworkObjectFlags add, NetworkObjectFlags remove)
        {
            networkObject.Flags = (networkObject.Flags | add) & ~remove;
            EditorUtility.SetDirty(networkObject);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
