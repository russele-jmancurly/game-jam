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
            BuildMusic();
            BuildNetwork(avatar, balloon, gameState, items, roamer);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();

            // New or regenerated network prefabs must be in Fusion's prefab table, or Spawn fails at runtime.
            Fusion.Editor.NetworkProjectConfigUtilities.RebuildPrefabTable();
            return $"Prototype built: {ScenePath} with {items.Length} items. Press Play (desktop mode works without a headset).";
        }

        /// <summary>The background music: a seamless generated loop, 2D, fading in at the start (volume: AudioConfig.Music).</summary>
        private static void BuildMusic()
        {
            var music = new GameObject("Music");
            PlaceholderPrefabBuilder.Sound(music, GeneratedAudio.MusicLoop("Music_Backyard"), true, 0f, 1f, playOnAwake: true, category: SoundCategory.Music);
        }

        /// <summary>
        /// The "not your turn" icon on the head camera: low in the middle of the view (between the arms), drawn on top of
        /// everything, hidden until TurnIndicator shows it.
        /// </summary>
        private static void BuildTurnIndicator(Transform head)
        {
            var root = new GameObject("TurnIndicator").transform;
            root.SetParent(head, false);
            root.localPosition = new Vector3(0f, -0.22f, 0.55f);

            Material material = PlaceholderAssets.TransparentUnlit("NotYourTurnIcon", GeneratedTextures.NotYourTurnIcon("T_NotYourTurn"));
            material.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always); // never hidden by the arms
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Overlay;
            EditorUtility.SetDirty(material);
            GameObject icon = PlaceholderShapes.Primitive(PrimitiveType.Quad, "Icon", root, Vector3.zero, Vector3.one * 0.1f, material, false);
            if (icon.TryGetComponent(out Renderer iconRenderer))
            {
                iconRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                iconRenderer.receiveShadows = false;
            }

            TextMesh label = PlaceholderShapes.Text("Label", root, new Vector3(0f, -0.068f, 0f), 0.0045f, "NOT YOUR TURN", ArtPalette.Hazard);
            root.gameObject.AddComponent<TurnIndicator>().Configure(iconRenderer, label);
        }

        /// <summary>
        /// "TIME'S UP!" with a countdown back to the lobby, centred in the view (ResultBanner shows it). A dark panel and two
        /// lines of text, all drawn last and on top of everything, so arms and set pieces never cover them.
        /// </summary>
        private static void BuildResultBanner(Transform head)
        {
            var root = new GameObject("ResultBanner").transform;
            root.SetParent(head, false);
            root.localPosition = new Vector3(0f, 0.04f, 0.9f);

            Material panelMaterial = PlaceholderAssets.TransparentUnlit("ResultPanel", GeneratedTextures.RoundedPanel("T_RoundedPanel"));
            Color panelColor = ArtPalette.TextDark;
            panelColor.a = 0.82f;
            panelMaterial.color = panelColor;
            panelMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
            panelMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Overlay;
            EditorUtility.SetDirty(panelMaterial);
            GameObject panel = PlaceholderShapes.Primitive(PrimitiveType.Quad, "Panel", root, Vector3.zero, new Vector3(0.74f, 0.26f, 1f), panelMaterial, false);
            var panelRenderer = panel.GetComponent<Renderer>();
            panelRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            panelRenderer.receiveShadows = false;

            Material textMaterial = OnTopTextMaterial();
            TextMesh title = PlaceholderShapes.Text("Title", root, new Vector3(0f, 0.035f, -0.005f), 0.012f, "TIME'S UP!", ArtPalette.Accent);
            title.fontStyle = FontStyle.Bold;
            TextMesh subtitle = PlaceholderShapes.Text("Subtitle", root, new Vector3(0f, -0.065f, -0.005f), 0.0052f, "Back to the start", ArtPalette.Picket);
            title.GetComponent<MeshRenderer>().sharedMaterial = textMaterial;
            subtitle.GetComponent<MeshRenderer>().sharedMaterial = textMaterial;
            root.gameObject.AddComponent<ResultBanner>().Configure(panelRenderer, title, subtitle);
        }

        /// <summary>The built-in font's text material, drawn after the overlay panel and never hidden by depth.</summary>
        private static Material OnTopTextMaterial()
        {
            const string path = PlaceholderAssets.MaterialFolder + "/OnTopText.mat";
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(font.material);
                AssetDatabase.CreateAsset(material, path);
            }

            material.mainTexture = font.material.mainTexture;
            material.SetFloat("unity_GUIZTestMode", (float)UnityEngine.Rendering.CompareFunction.Always);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Overlay + 1;
            EditorUtility.SetDirty(material);
            return material;
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
            BuildTurnIndicator(head.transform);
            BuildResultBanner(head.transform);
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

            // GorillaPlus locomotion. Feel tuning (jump, slide, arm length, taps) comes from PlayerRigConfig at runtime
            // (LocalRig.ApplyLocomotionTuning); these are the structural values from GorillaPlus's rig prefab.
            var locomotion = root.AddComponent<GorillaLocomotion.Player>();
            locomotion.turnParent = trackingSpace.gameObject;
            locomotion.headFollower = head.transform;
            locomotion.headCollider = headCollider;
            locomotion.bodyCollider = bodyCollider;
            locomotion.leftFollower = leftHand.transform;
            locomotion.rightFollower = rightHand.transform;
            locomotion.leftController = leftController;
            locomotion.rightController = rightController;
            locomotion.velocityHistorySize = configs.PlayerRig.VelocityHistorySize;
            locomotion.unStickDistance = 1f;
            locomotion.minimumRaycastDistance = 0.05f;
            locomotion.defaultPrecision = 0.995f;
            locomotion.locomotionEnabledLayers = GameLayers.DefaultMask;
            locomotion.leftTapSource = HandAudio("TapAudio", leftHand.transform, false);
            locomotion.rightTapSource = HandAudio("TapAudio", rightHand.transform, false);
            locomotion.leftSlipSource = HandAudio("SlideAudio", leftHand.transform, true);
            locomotion.rightSlipSource = HandAudio("SlideAudio", rightHand.transform, true);
            locomotion.sounds = root.AddComponent<GorillaLocomotion.Sounds>();
            locomotion.sounds.DefaultSounds = new[]
            {
                GeneratedAudio.Tap("Tap_Low", 150f, 1),
                GeneratedAudio.Tap("Tap_Mid", 185f, 2),
                GeneratedAudio.Tap("Tap_High", 220f, 3),
            };
            locomotion.sounds.Surfaces = TapSurfaces();

            // Dirt and grass flicked up where a hand taps the lawn (surfaces marked KicksUpDirt).
            (ParticleSystem clods, ParticleSystem dust) = AmbientEffectsBuilder.HandDirt(root.transform);
            root.AddComponent<HandTapEffects>().Configure(locomotion, clods, dust);

            var rig = root.AddComponent<LocalRig>();
            rig.Configure(configs.PlayerRig, configs.Knockback, locomotion, head.transform, leftController, rightController, leftHand, rightHand, wrist);
            root.AddComponent<RigHazardSensor>();
            root.AddComponent<DesktopRigInput>();

            // The whole rig is on Ignore Raycast so your own hands never climb your own body.
            PlaceholderShapes.SetLayerRecursively(root, GameLayers.IgnoreRaycast);
        }

        /// <summary>Hand-tap surfaces, checked in this order; a material whose name has one of the words belongs to it.</summary>
        private static readonly (string name, bool kicksUpDirt, string[] keywords)[] SurfaceKinds =
        {
            ("Grass", true, new[] { "Lawn", "Grass", "Moss", "Turf" }),
            ("Wood", false, new[] { "Bark", "Picket", "Platform", "Wood", "HouseWall", "Deck", "Timber", "Plank", "Stand", "Picnic", "Branch", "Stage" }), // not "Table": inflaTABLE
            ("Metal", false, new[] { "Steel", "Charcoal", "Fan", "Gate", "Metal", "Truss", "Pole", "Grill", "Iron", "Chrome" }),
            ("Plastic", false, new[] { "Inflatable", "Merry", "Duck", "Climbable", "Pinata", "Laundry", "Glass", "Hazard", "Mushroom", "Trampoline", "Plastic", "Rubber", "Toy" }),
        };

        /// <summary>
        /// Sorts every material on the yard's solid colliders into grass, wood, metal and plastic by name, each with three
        /// generated tap variants (grass also kicks up dirt). Anything unmatched keeps the default taps.
        /// </summary>
        private static GorillaLocomotion.SurfaceSound[] TapSurfaces()
        {
            var materials = new List<Material>[SurfaceKinds.Length];
            for (int i = 0; i < materials.Length; i++)
            {
                materials[i] = new List<Material>();
            }

            var unmatched = new SortedSet<string>();
            var buffer = new List<Material>();
            foreach (Collider collider in Object.FindObjectsByType<Collider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (collider.isTrigger)
                {
                    continue;
                }

                Renderer renderer = collider.GetComponent<Renderer>(); // same lookup as Player.ResolveSurfaceMaterial
                if (renderer == null)
                {
                    renderer = collider.GetComponentInParent<Renderer>();
                }

                if (renderer == null)
                {
                    continue;
                }

                renderer.GetSharedMaterials(buffer);
                foreach (Material material in buffer)
                {
                    if (material == null)
                    {
                        continue;
                    }

                    int kind = System.Array.FindIndex(SurfaceKinds, k => System.Array.Exists(k.keywords,
                        word => material.name.IndexOf(word, System.StringComparison.OrdinalIgnoreCase) >= 0));
                    if (kind < 0)
                    {
                        unmatched.Add(material.name);
                    }
                    else if (!materials[kind].Contains(material))
                    {
                        materials[kind].Add(material);
                    }
                }
            }

            if (unmatched.Count > 0)
            {
                Debug.Log($"Hand taps: default sound for {string.Join(", ", unmatched)}");
            }

            var surfaces = new GorillaLocomotion.SurfaceSound[SurfaceKinds.Length];
            for (int i = 0; i < surfaces.Length; i++)
            {
                string name = SurfaceKinds[i].name;
                var sounds = new AudioClip[3];
                for (int v = 0; v < sounds.Length; v++)
                {
                    string clip = $"Tap_{name}_{v + 1}";
                    sounds[v] = name switch
                    {
                        "Grass" => GeneratedAudio.TapGrass(clip, v),
                        "Wood" => GeneratedAudio.TapWood(clip, v),
                        "Metal" => GeneratedAudio.TapMetal(clip, v),
                        _ => GeneratedAudio.TapPlastic(clip, v),
                    };
                }

                surfaces[i] = new GorillaLocomotion.SurfaceSound
                {
                    Name = name,
                    KicksUpDirt = SurfaceKinds[i].kicksUpDirt,
                    Materials = materials[i].ToArray(),
                    Sounds = sounds,
                };
            }

            return surfaces;
        }

        private static void BuildNetwork(NetworkObject avatar, NetworkObject balloon, NetworkObject gameState, NetworkObject[] items, NetworkObject roamer)
        {
            var network = new GameObject("Network");
            network.AddComponent<NetworkRunner>();
            network.AddComponent<NetworkSceneManagerDefault>();
            network.AddComponent<NetworkObjectProviderDefault>();
            network.AddComponent<Photon.Voice.Fusion.FusionVoiceClient>(); // follows the Fusion session; offline in Single mode
            network.AddComponent<NetworkBootstrap>().Configure(avatar, balloon, gameState, items, roamer, BackyardCourseBuilder.RoamerSpawns);
        }

        private static Transform Child(string name, Transform parent, Vector3 localPosition)
        {
            Transform child = new GameObject(name).transform;
            child.SetParent(parent, false);
            child.localPosition = localPosition;
            return child;
        }

        /// <summary>A quiet 3D source on a glove for hand taps (one-shots) or the looping slide hiss.</summary>
        private static AudioSource HandAudio(string name, Transform hand, bool isSlideLoop)
        {
            var source = new GameObject(name).AddComponent<AudioSource>();
            source.transform.SetParent(hand, false);
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.minDistance = 0.3f;
            source.maxDistance = 6f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.volume = 0.6f;
            if (isSlideLoop)
            {
                source.clip = GeneratedAudio.SlideLoop("Slide_Loop");
                source.loop = true;
                source.volume = 0.35f;
            }

            return source;
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
