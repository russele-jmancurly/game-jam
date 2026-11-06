using UnityEditor;
using UnityEngine;

namespace GameJam.Editor
{
    /// <summary>
    /// Import rules for the rigged show characters (Characters/&lt;Name&gt;/&lt;Name&gt;_Game.fbx from
    /// tools/blender/scripts/rig_character.py): Generic rig, clips named by their take ("Armature|Walk" becomes "Walk"),
    /// Idle and Walk loop, the one-shot gestures don't.
    /// </summary>
    public sealed class ShowCharacterImport : AssetPostprocessor
    {
        private static bool IsShowCharacter(string path) =>
            path.StartsWith("Assets/GameJam/Characters/") && path.EndsWith("_Game.fbx");

        private void OnPreprocessModel()
        {
            if (!IsShowCharacter(assetPath) || assetImporter is not ModelImporter importer)
            {
                return;
            }

            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.optimizeGameObjects = false;
        }

        private void OnPreprocessAnimation()
        {
            if (!IsShowCharacter(assetPath) || assetImporter is not ModelImporter importer)
            {
                return;
            }

            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            foreach (ModelImporterClipAnimation clip in clips)
            {
                int bar = clip.takeName.LastIndexOf('|');
                clip.name = bar >= 0 ? clip.takeName[(bar + 1)..] : clip.takeName;
                clip.loopTime = clip.name == "Idle" || clip.name == "Walk";
            }

            importer.clipAnimations = clips;
        }
    }
}
