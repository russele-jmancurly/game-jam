using Fusion;
using GameJam.Core;
using GameJam.Net;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace GameJam.Editor
{
    /// <summary>
    /// Builds Scenes/Tutorial.unity: the tutorial burrow (<see cref="BackyardCourseBuilder.BuildTutorial"/>) with the same
    /// rig and networked prefabs as the game, run offline in practice mode (<see cref="NetworkBootstrap.ConfigureTutorial"/>),
    /// lit like a cave: warm flat light from the tunnel mouth, no shadows, glowing mushrooms, dark fog at the far end.
    /// Reached from the lobby's mole hole through Tunnel.unity. Rebuild after the prototype when prefabs or the rig change.
    /// Menu: Game Jam > Build Tutorial Scene. CLI: <c>return GameJam.Editor.TutorialSceneBuilder.BuildFromCli();</c>
    /// </summary>
    public static class TutorialSceneBuilder
    {
        public const string ScenePath = PlaceholderAssets.SceneFolder + "/Tutorial.unity";
        private static readonly Color CaveDark = new Color(0.07f, 0.045f, 0.03f);

        [MenuItem("Game Jam/Build Tutorial Scene")]
        public static void BuildFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log(Build());
            }
        }

        /// <summary>Non-interactive entry point. Refuses to discard unsaved scene changes.</summary>
        public static string BuildFromCli()
        {
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                if (EditorSceneManager.GetSceneAt(i).isDirty)
                {
                    return $"Not built: scene '{EditorSceneManager.GetSceneAt(i).path}' has unsaved changes. Save it first.";
                }
            }

            return Build();
        }

        private static string Build()
        {
            PlaceholderAssets.EnsureFolders();
            ProjectLayers.Ensure();
            PrototypeConfigSet configs = PrototypeConfigSet.LoadOrCreate();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // The same prefabs as the game (regenerated identically, like the prototype builder does).
            NetworkObject avatar = PlaceholderPrefabBuilder.BuildAvatar(configs);
            NetworkObject balloon = PlaceholderPrefabBuilder.BuildBalloon(configs);
            NetworkObject gameState = PlaceholderPrefabBuilder.BuildGameState(configs);
            NetworkObject[] items = PlaceholderPrefabBuilder.BuildItems(configs);
            NetworkObject roamer = PlaceholderPrefabBuilder.BuildRoamer(configs, BackyardCourseBuilder.RoamerWanderPoints,
                BackyardCourseBuilder.RoamerAvoidCircles, BackyardCourseBuilder.RoamerBounds);

            BackyardCourseBuilder.BuildTutorial(configs.Roster);
            BuildLighting();
            PrototypeSceneBuilder.BuildRig(configs, BackyardCourseBuilder.TutorialRigStart); // after the course: hand taps sort its colliders
            foreach (Camera camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                camera.clearFlags = CameraClearFlags.SolidColor; // a closed room: nothing to see behind the walls
                camera.backgroundColor = CaveDark;
            }

            PrototypeSceneBuilder.BuildMusic();
            NetworkBootstrap bootstrap = PrototypeSceneBuilder.BuildNetwork(avatar, balloon, gameState, items, roamer, BackyardCourseBuilder.RoamerSpawns);
            bootstrap.ConfigureTutorial(BackyardCourseBuilder.TutorialGiantWander, BackyardCourseBuilder.TutorialGiantBounds);

            EditorSceneManager.SaveScene(scene, ScenePath);
            PrototypeSceneBuilder.AppendToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            Fusion.Editor.NetworkProjectConfigUtilities.RebuildPrefabTable();
            return $"Tutorial built: {ScenePath} with {items.Length} item prefabs. Reach it from the lobby's mole hole, or open it and press Play.";
        }

        /// <summary>
        /// Cave light: one soft directional light from above (no shadows: none of the room's surfaces need them, and it
        /// keeps the Quest's shadow pass off), warm trilight ambient so nothing goes black, and dark fog that fades the far
        /// end. LocalRig scales the sun and ambient in the headset like it does in the yard.
        /// </summary>
        private static void BuildLighting()
        {
            var sun = new GameObject("CaveLight").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = ArtPalette.SunLight;
            sun.intensity = 0.85f;
            sun.shadows = LightShadows.None;
            sun.transform.rotation = Quaternion.Euler(68f, 20f, 0f);
            RenderSettings.sun = sun;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.52f, 0.42f);
            RenderSettings.ambientEquatorColor = new Color(0.5f, 0.4f, 0.32f);
            RenderSettings.ambientGroundColor = new Color(0.3f, 0.23f, 0.18f);
            RenderSettings.skybox = null;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = CaveDark;
            RenderSettings.fogStartDistance = 16f;
            RenderSettings.fogEndDistance = 70f;
        }
    }
}
