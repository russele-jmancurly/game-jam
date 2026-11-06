using GameJam.Core;
using UnityEditor;
using UnityEngine;

namespace GameJam.Editor
{
    /// <summary>All tuning assets the prototype uses, loaded (or created with defaults) from Assets/GameJam/Config.</summary>
    public sealed class PrototypeConfigSet
    {
        private const string CharacterFolder = "Assets/GameJam/Characters";

        public BalloonConfig Balloon { get; private set; }

        public SlapConfig Slap { get; private set; }

        public RunConfig Run { get; private set; }

        public PlayerRigConfig PlayerRig { get; private set; }

        public KnockbackConfig Knockback { get; private set; }

        public CharacterRoster Roster { get; private set; }

        public static PrototypeConfigSet LoadOrCreate()
        {
            const string folder = PlaceholderAssets.ConfigFolder;
            var set = new PrototypeConfigSet
            {
                Balloon = PlaceholderAssets.LoadOrCreateConfig<BalloonConfig>($"{folder}/BalloonConfig.asset"),
                Slap = PlaceholderAssets.LoadOrCreateConfig<SlapConfig>($"{folder}/SlapConfig.asset"),
                Run = PlaceholderAssets.LoadOrCreateConfig<RunConfig>($"{folder}/RunConfig.asset"),
                PlayerRig = PlaceholderAssets.LoadOrCreateConfig<PlayerRigConfig>($"{folder}/PlayerRigConfig.asset"),
                Knockback = PlaceholderAssets.LoadOrCreateConfig<KnockbackConfig>($"{folder}/KnockbackConfig.asset"),
                Roster = PlaceholderAssets.LoadOrCreateConfig<CharacterRoster>($"{folder}/CharacterRoster.asset"),
            };

            RemapSquoofClothes();
            set.Roster.Characters = new[]
            {
                Character("Jmancurly", ArtPalette.Accent, "Jmancurly/Jmancurly_Game.fbx", null),
                Character("Squoof", ArtPalette.CandyMint, "Squoof/Squoof_Game.fbx", null),
            };
            EditorUtility.SetDirty(set.Roster);
            return set;
        }

        /// <summary>
        /// Loads or creates a character, keeping its tuning, and (re)links its game model if the prepared FBX exists
        /// (made by tools/blender/scripts/prepare_character.py). Without the FBX it stays a coloured placeholder.
        /// </summary>
        private static CharacterDefinition Character(string name, Color tint, string modelPath, Material materialOverride)
        {
            string path = $"{PlaceholderAssets.ConfigFolder}/Characters/Character_{name}.asset";
            System.IO.Directory.CreateDirectory($"{PlaceholderAssets.ConfigFolder}/Characters");
            var character = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(path);
            if (character == null)
            {
                character = ScriptableObject.CreateInstance<CharacterDefinition>();
                character.DisplayName = name;
                character.Tint = tint;
                AssetDatabase.CreateAsset(character, path);
            }

            MarkNormalMaps($"{CharacterFolder}/{modelPath}");
            character.Model = AssetDatabase.LoadAssetAtPath<GameObject>($"{CharacterFolder}/{modelPath}");
            LinkClips(character, $"{CharacterFolder}/{modelPath}");
            if (materialOverride != null && character.MaterialOverride == null)
            {
                character.MaterialOverride = materialOverride;
            }

            EditorUtility.SetDirty(character);
            return character;
        }

        /// <summary>
        /// Links the clips of a rigged model (tools/blender/scripts/rig_character.py: Idle, Walk, WindUp, Slap, Wave;
        /// named by ShowCharacterImport). A static model leaves them empty and the character stays unanimated.
        /// </summary>
        private static void LinkClips(CharacterDefinition character, string modelPath)
        {
            AnimationClip Find(string name)
            {
                foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(modelPath))
                {
                    if (asset is AnimationClip clip && clip.name == name)
                    {
                        return clip;
                    }
                }

                return null;
            }

            character.Idle = Find("Idle");
            character.Walk = Find("Walk");
            character.WindUp = Find("WindUp");
            character.Slap = Find("Slap");
            character.Wave = Find("Wave");
        }

        /// <summary>
        /// Squoof's clothes have no albedo texture, and their base colours don't survive the FBX export, so the polo,
        /// shorts and sunglasses would import white and pink. Remap them to plain URP materials in the source's colours.
        /// </summary>
        private static void RemapSquoofClothes()
        {
            const string path = CharacterFolder + "/Squoof/Squoof_Game.fbx";
            if (AssetImporter.GetAtPath(path) is not ModelImporter importer)
            {
                return;
            }

            Material navy = PlaceholderAssets.Material("SquoofNavy", new Color(0.09f, 0.12f, 0.2f), 0.2f);
            Material lens = PlaceholderAssets.Material("SquoofLens", new Color(0.06f, 0.06f, 0.07f), 0.85f);
            bool changed = false;
            foreach ((string name, Material material) in new[] { ("outfit_top", navy), ("outfit_top.001", navy), ("Material_0.001", navy), ("MI_Pack1_003.001", lens) })
            {
                var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), name);
                if (!importer.GetExternalObjectMap().TryGetValue(id, out Object current) || current != material)
                {
                    importer.AddRemap(id, material);
                    changed = true;
                }
            }

            if (changed)
            {
                importer.SaveAndReimport();
            }
        }

        /// <summary>
        /// prepare_character.py writes a model's textures into &lt;model&gt;.fbm; Unity imports their normal maps as plain
        /// colour textures, which flattens the shading. Mark *_Normal / *_NRM images there as normal maps.
        /// </summary>
        private static void MarkNormalMaps(string modelPath)
        {
            string folder = System.IO.Path.ChangeExtension(modelPath, ".fbm");
            if (!AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = System.IO.Path.GetFileNameWithoutExtension(path);
                bool isNormal = name.EndsWith("_Normal") || name.EndsWith("_NRM");
                if (isNormal && AssetImporter.GetAtPath(path) is TextureImporter importer && importer.textureType != TextureImporterType.NormalMap)
                {
                    importer.textureType = TextureImporterType.NormalMap;
                    importer.SaveAndReimport();
                }
            }
        }
    }
}
