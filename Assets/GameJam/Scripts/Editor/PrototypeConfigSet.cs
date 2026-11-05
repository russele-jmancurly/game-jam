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

            set.Roster.Characters = new[]
            {
                Character("Jmancurly", ArtPalette.Accent, "Jmancurly/Jmancurly_Game.fbx", null),
                Character("Squoof", ArtPalette.CandyMint, "Squoof/Squoof_Game.fbx", null),
                Character("Protobro", ArtPalette.CandyPurple, "Protobro/Protobro_Game.fbx",
                    PlaceholderAssets.Material("ProtobroSkin", Color.Lerp(ArtPalette.CandyPurple, ArtPalette.Picket, 0.35f), 0.35f)),
                Character("Elliot", ArtPalette.CandyBlue, "Elliot/Elliot_Game.fbx", null),
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

            character.Model = AssetDatabase.LoadAssetAtPath<GameObject>($"{CharacterFolder}/{modelPath}");
            if (materialOverride != null && character.MaterialOverride == null)
            {
                character.MaterialOverride = materialOverride;
            }

            EditorUtility.SetDirty(character);
            return character;
        }
    }
}
