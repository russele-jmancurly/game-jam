using System.Collections.Generic;
using Fusion;
using GameJam.Core;
using GameJam.Net;
using GameJam.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR;

namespace GameJam.Editor
{
    /// <summary>
    /// Menu: Game Jam > Build Prototype Scene. Builds the playable placeholder prototype: config assets
    /// (kept if they exist), placeholder materials, networked prefabs, the gorilla rig, the greybox course
    /// and the network runner, saved to Assets/GameJam/Scenes/Prototype.unity. Safe to run again.
    /// From the CLI: <c>return GameJam.Editor.PrototypeSceneBuilder.BuildFromCli();</c>
    /// </summary>
    public static class PrototypeSceneBuilder
    {
        public const string ScenePath = PlaceholderAssets.SceneFolder + "/Prototype.unity";

        private static readonly Vector3 RigStartPosition = new Vector3(0f, 0.45f, -9f);

        [MenuItem("Game Jam/Build Prototype Scene")]
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

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            foreach (Camera camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(camera.gameObject); // the rig brings its own camera
            }

            NetworkObject avatar = PlaceholderPrefabBuilder.BuildAvatar(configs);
            NetworkObject balloon = PlaceholderPrefabBuilder.BuildBalloon(configs);
            NetworkObject gameState = PlaceholderPrefabBuilder.BuildGameState(configs);
            NetworkObject[] items = PlaceholderPrefabBuilder.BuildItems(configs);
            NetworkObject roamer = PlaceholderPrefabBuilder.BuildRoamer(configs, BackyardCourseBuilder.RoamerWanderPoints,
                BackyardCourseBuilder.RoamerAvoidCircles, BackyardCourseBuilder.RoamerBounds);

            BackyardCourseBuilder.Build(configs.Roster);
            EnvironmentLightingBuilder.Build();
            EnvironmentDressingBuilder.Build();
            BuildRig(configs);
            BuildNetwork(avatar, balloon, gameState, items, roamer);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();

            // New or regenerated network prefabs must be in Fusion's prefab table, or Spawn fails at runtime.
            Fusion.Editor.NetworkProjectConfigUtilities.RebuildPrefabTable();
            return $"Prototype built: {ScenePath} with {items.Length} items. Press Play (desktop mode works without a headset).";
        }

        private static void BuildRig(PrototypeConfigSet configs)
        {
            var root = new GameObject("GorillaRig");
            root.transform.position = RigStartPosition;
            var body = root.AddComponent<Rigidbody>();
            body.mass = 1f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.constraints = RigidbodyConstraints.FreezeRotation;
            body.collisionDetectionMode = CollisionDetectionMode.Continuous;

            Transform trackingSpace = new GameObject("TrackingSpace").transform;
            trackingSpace.SetParent(root.transform, false);

            var head = new GameObject("Head") { tag = "MainCamera" };
            head.transform.SetParent(trackingSpace, false);
            head.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            head.AddComponent<Camera>().nearClipPlane = 0.02f;
            // Script-created cameras default to no post-processing; LocalRig turns it on for PC mode at runtime.
            head.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderPostProcessing = true;
            head.AddComponent<AudioListener>();
            var headCollider = head.AddComponent<SphereCollider>();
            headCollider.radius = 0.12f;

            Transform leftController = Child("LeftController", trackingSpace, new Vector3(-0.25f, 1.1f, 0.3f));
            Transform rightController = Child("RightController", trackingSpace, new Vector3(0.25f, 1.1f, 0.3f));

            Transform bodyColliderHolder = Child("BodyCollider", root.transform, new Vector3(0f, 1.2f, 0f));
            var bodyCollider = bodyColliderHolder.gameObject.AddComponent<CapsuleCollider>();
            bodyCollider.radius = 0.18f;
            bodyCollider.height = 0.5f;

            Material glove = PlaceholderAssets.Material("Glove", Color.white);
            SlapHand leftHand = HandFollower("LeftHandFollower", root.transform, leftController.localPosition, glove, XRNode.LeftHand, configs.Slap);
            SlapHand rightHand = HandFollower("RightHandFollower", root.transform, rightController.localPosition, glove, XRNode.RightHand, configs.Slap);
            TextMesh wrist = PlaceholderShapes.Text("WristTimer", leftHand.transform, new Vector3(0f, 0.08f, -0.05f), 0.01f, "Lobby", Color.white);

            var locomotion = root.AddComponent<GorillaLocomotion.Player>();
            locomotion.headCollider = headCollider;
            locomotion.bodyCollider = bodyCollider;
            locomotion.leftHandFollower = leftHand.transform;
            locomotion.rightHandFollower = rightHand.transform;
            locomotion.leftHandTransform = leftController;
            locomotion.rightHandTransform = rightController;
            locomotion.velocityHistorySize = 3;
            locomotion.maxArmLength = 1.5f;
            locomotion.unStickDistance = 1f;
            locomotion.velocityLimit = 0.4f;
            locomotion.maxJumpSpeed = 6.5f;
            locomotion.jumpMultiplier = 1.1f;
            locomotion.minimumRaycastDistance = 0.05f;
            locomotion.defaultSlideFactor = 0.03f;
            locomotion.defaultPrecision = 0.995f;
            locomotion.locomotionEnabledLayers = GameLayers.DefaultMask;

            var rig = root.AddComponent<LocalRig>();
            rig.Configure(configs.PlayerRig, configs.Knockback, locomotion, head.transform, leftController, rightController, leftHand, rightHand, wrist);
            root.AddComponent<RigHazardSensor>();
            root.AddComponent<DesktopRigInput>();

            // The whole rig is on Ignore Raycast so your own hands never climb your own body.
            PlaceholderShapes.SetLayerRecursively(root, GameLayers.IgnoreRaycast);
        }

        private static void BuildNetwork(NetworkObject avatar, NetworkObject balloon, NetworkObject gameState, NetworkObject[] items, NetworkObject roamer)
        {
            var network = new GameObject("Network");
            network.AddComponent<NetworkRunner>();
            network.AddComponent<NetworkSceneManagerDefault>();
            network.AddComponent<NetworkObjectProviderDefault>();
            network.AddComponent<Photon.Voice.Fusion.FusionVoiceClient>(); // follows the Fusion session; offline in Single mode
            network.AddComponent<NetworkBootstrap>().Configure(avatar, balloon, gameState, items, roamer, BackyardCourseBuilder.RoamerSpawn);
        }

        private static Transform Child(string name, Transform parent, Vector3 localPosition)
        {
            Transform child = new GameObject(name).transform;
            child.SetParent(parent, false);
            child.localPosition = localPosition;
            return child;
        }

        private static SlapHand HandFollower(string name, Transform parent, Vector3 localPosition, Material glove, XRNode node, SlapConfig slapConfig)
        {
            Transform follower = Child(name, parent, localPosition);
            PlaceholderShapes.Primitive(PrimitiveType.Sphere, "Glove", follower, Vector3.zero, Vector3.one * 0.1f, glove, false);
            var slapHand = follower.gameObject.AddComponent<SlapHand>();
            slapHand.Configure(node, slapConfig);
            return slapHand;
        }

        private static void AddToBuildSettings(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(entry => entry.path == path);
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
