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

        /// <summary>A URP Lit material in the placeholder folder, reused if it exists.</summary>
        public static Material Material(string name, Color color)
        {
            if (MaterialCache.TryGetValue(name, out Material cached) && cached != null)
            {
                return cached;
            }

            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = color;
            EditorUtility.SetDirty(material);
            MaterialCache[name] = material;
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
