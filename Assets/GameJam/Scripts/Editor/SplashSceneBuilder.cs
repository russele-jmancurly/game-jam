using System.Collections.Generic;
using GameJam.Core;
using GameJam.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GameJam.Editor
{
    /// <summary>
    /// Builds Scenes/Splash.unity: the Curly Blue logo on a dark backdrop with the game's background music
    /// (Music_Backyard), then the game. It is kept first in the build settings so the game opens on it. Its music level is
    /// AudioConfig.SplashMusic.
    /// </summary>
    public static class SplashSceneBuilder
    {
        public const string ScenePath = PlaceholderAssets.SceneFolder + "/Splash.unity";
        public const string LogoPath = PlaceholderAssets.Root + "/Art/UI/CurlyBlueLogo.png";
        private const float LogoWidth = 4f; // metres, at 5 m away (about 44 degrees): comfortably inside the view
        private static readonly Color Backdrop = new Color(0.035f, 0.05f, 0.13f);

        [MenuItem("Game Jam/Build Splash Scene")]
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
            Texture2D logo = ImportLogo();
            if (logo == null)
            {
                return $"Not built: logo texture missing at {LogoPath}.";
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Transform trackingSpace = new GameObject("TrackingSpace").transform;
            var head = new GameObject("Head") { tag = "MainCamera" };
            head.transform.SetParent(trackingSpace, false);
            head.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            var camera = head.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Backdrop;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 50f;
            head.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;
            head.AddComponent<AudioListener>();

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Logo";
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.position = new Vector3(0f, 1.6f, 5f);
            quad.transform.localScale = new Vector3(LogoWidth, LogoWidth * logo.height / logo.width, 1f);
            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = PlaceholderAssets.TransparentUnlit("SplashLogo", logo);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var musicHolder = new GameObject("Music");
            var music = musicHolder.AddComponent<AudioSource>();
            music.clip = GeneratedAudio.MusicLoop("Music_Backyard");
            music.playOnAwake = false;
            music.loop = true;
            music.spatialBlend = 0f; // 2D
            AudioConfig audio = PlaceholderAssets.LoadOrCreateConfig<AudioConfig>(PlaceholderPrefabBuilder.AudioConfigPath);

            var splash = new GameObject("Splash").AddComponent<StudioSplash>();
            // The game opens in the tutorial (one player, offline); leaving or finishing it leads to the lobby (Prototype).
            splash.Configure(head.transform, quad.transform, renderer, music, audio, SceneTravel.TutorialScene);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.black;
            RenderSettings.skybox = null;
            RenderSettings.fog = false;

            EditorSceneManager.SaveScene(scene, ScenePath);
            KeepFirstInBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            return $"Splash built: {ScenePath} (first in build settings; it loads the Tutorial, which leads to Prototype).";
        }

        private static Texture2D ImportLogo()
        {
            AssetDatabase.ImportAsset(LogoPath);
            if (AssetImporter.GetAtPath(LogoPath) is TextureImporter importer)
            {
                bool changed = importer.textureType != TextureImporterType.Default || !importer.alphaIsTransparency
                    || !importer.mipmapEnabled || importer.maxTextureSize != 2048 || importer.npotScale != TextureImporterNPOTScale.None
                    || importer.wrapMode != TextureWrapMode.Clamp;
                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = true; // it's viewed at a distance in the headset: mips keep it from shimmering
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.maxTextureSize = 2048;
                if (changed)
                {
                    importer.SaveAndReimport();
                }
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(LogoPath);
        }

        /// <summary>Puts the splash at index 0 and leaves every other scene in its order behind it.</summary>
        public static void KeepFirstInBuildSettings(string path = ScenePath)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
            {
                return;
            }

            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(entry => entry.path == path);
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
