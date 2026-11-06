using GameJam.Core;
using UnityEditor;
using UnityEngine;

namespace GameJam.Editor
{
    /// <summary>Names the project layers the game uses, through the Editor's TagManager (never by hand-editing YAML).</summary>
    public static class ProjectLayers
    {
        public static void Ensure()
        {
            EnsureLayer(GameLayers.Balloon, "Balloon");
            EnsureLayer(GameLayers.Item, "Item");
        }

        private static void EnsureLayer(int index, string name)
        {
            Object tagManager = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0];
            var serialized = new SerializedObject(tagManager);
            SerializedProperty layer = serialized.FindProperty("layers").GetArrayElementAtIndex(index);

            if (layer.stringValue == name)
            {
                return;
            }

            if (!string.IsNullOrEmpty(layer.stringValue))
            {
                Debug.LogError($"[Layers] Layer {index} is already '{layer.stringValue}'; GameLayers expects '{name}' there.");
                return;
            }

            layer.stringValue = name;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"[Layers] Named layer {index} '{name}'.");
        }
    }
}
