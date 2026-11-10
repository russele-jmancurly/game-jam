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
    /// Builds Scenes/Tunnel.unity, the mole tunnel between the yard and the tutorial: a head-tracked camera inside a shaft
    /// of soil rings (stones, roots, the odd worm and glowing mushroom) that <see cref="TunnelTransition"/> rushes up past
    /// the player while the destination loads. Fog hides both ends, so a dozen rings read as an endless fall.
    /// Menu: Game Jam > Build Tunnel Scene. CLI: <c>return GameJam.Editor.TunnelSceneBuilder.BuildFromCli();</c>
    /// </summary>
    public static class TunnelSceneBuilder
    {
        public const string ScenePath = PlaceholderAssets.SceneFolder + "/Tunnel.unity";

        private const int RingCount = 14;
        private const float RingSpacing = 1.5f;
        private const float ShaftRadius = 1.7f;
        private const int SlabsPerRing = 10;
        private static readonly Color Darkness = new Color(0.035f, 0.022f, 0.014f);

        [MenuItem("Game Jam/Build Tunnel Scene")]
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
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Transform trackingSpace = new GameObject("TrackingSpace").transform;
            var head = new GameObject("Head") { tag = "MainCamera" };
            head.transform.SetParent(trackingSpace, false);
            head.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            var camera = head.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Darkness;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 40f;
            head.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;
            head.AddComponent<AudioListener>();
            PrototypeSceneBuilder.BuildScreenFade(head.transform, true);

            // Light from the opening overhead; the fog swallows the shaft a few rings away in both directions.
            var sun = new GameObject("Daylight").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = ArtPalette.SunLight;
            sun.intensity = 0.9f;
            sun.shadows = LightShadows.None;
            sun.transform.rotation = Quaternion.Euler(62f, 25f, 0f);
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.5f, 0.4f, 0.31f);
            RenderSettings.skybox = null;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Darkness;
            RenderSettings.fogStartDistance = 2.5f;
            RenderSettings.fogEndDistance = 11f;

            Transform shaft = new GameObject("Shaft").transform;
            Transform[] rings = BuildRings(shaft);

            // The way in, overhead: a disc of daylight that shrinks as the player falls away from it.
            GameObject daylight = PlaceholderShapes.Primitive(PrimitiveType.Cylinder, "Opening", shaft, new Vector3(0f, 5.5f, 0f), new Vector3(2.6f, 0.01f, 2.6f),
                PlaceholderAssets.Unlit("TunnelDaylight", ArtPalette.SunLight), false);

            TextMesh label = PlaceholderShapes.Text("Label", shaft, new Vector3(0f, 1.75f, 1.35f), 0.0085f, "DOWN TO THE TUTORIAL...", ArtPalette.Accent);
            label.fontStyle = FontStyle.Bold;

            AmbientEffectsBuilder.TunnelStreaks(shaft, new Vector3(0f, -5f, 0f), ShaftRadius - 0.25f);

            var windHost = new GameObject("Wind");
            AudioSource wind = PlaceholderPrefabBuilder.Sound(windHost, GeneratedAudio.TunnelRush("Tunnel_Rush"), true, 0f, 0.9f);

            var transition = new GameObject("TunnelTransition").AddComponent<TunnelTransition>();
            transition.Configure(head.transform, shaft, rings, RingSpacing, daylight.transform, label, wind.GetComponent<SoundLevel>());

            EditorSceneManager.SaveScene(scene, ScenePath);
            PrototypeSceneBuilder.AppendToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            return $"Tunnel built: {ScenePath} ({RingCount} rings).";
        }

        /// <summary>
        /// The shaft: rings of soil slabs stacked along Y, every other ring a darker shade and turned half a slab (so the
        /// motion reads), each with a few things stuck in the wall.
        /// </summary>
        private static Transform[] BuildRings(Transform shaft)
        {
            Color soilDark = Color.Lerp(ArtPalette.TimberDark, ArtPalette.Charcoal, 0.55f);
            Color soilLight = Color.Lerp(ArtPalette.TimberDark, ArtPalette.Timber, 0.2f);
            Material[] soils =
            {
                PlaceholderAssets.Material("TunnelSoilA", Color.white, 0.05f, GeneratedTextures.Noise("T_TunnelSoilA", soilDark, soilLight, 9f), new Vector2(2f, 2f)),
                PlaceholderAssets.Material("TunnelSoilB", Color.white, 0.05f,
                    GeneratedTextures.Noise("T_TunnelSoilB", Color.Lerp(soilDark, Color.black, 0.55f), Color.Lerp(soilDark, Color.black, 0.15f), 9f), new Vector2(2f, 2f)),
            };
            Material stone = PlaceholderAssets.Material("BurrowStone", Color.Lerp(ArtPalette.Pebble, ArtPalette.Charcoal, 0.35f), 0.2f);
            Material root = PlaceholderAssets.Material("BurrowRoot", Color.Lerp(ArtPalette.TimberDark, ArtPalette.Timber, 0.35f), 0.1f);
            Material worm = PlaceholderAssets.Material("TunnelWorm", ArtPalette.CandyPink, 0.5f);
            Material glow = PlaceholderAssets.Emissive("GlowCapMint", ArtPalette.CandyMint, 1.6f);

            var random = new System.Random(5);
            float Range(float min, float max) => min + (float)random.NextDouble() * (max - min);

            var rings = new Transform[RingCount];
            float slabWidth = 2f * Mathf.PI * (ShaftRadius + 0.2f) / SlabsPerRing * 1.08f;
            for (int r = 0; r < RingCount; r++)
            {
                Transform ring = new GameObject($"Ring{r}").transform;
                ring.SetParent(shaft, false);
                ring.localPosition = new Vector3(0f, -9f + r * RingSpacing, 0f);
                ring.localRotation = Quaternion.Euler(0f, r % 2 * (180f / SlabsPerRing), 0f);
                rings[r] = ring;

                for (int s = 0; s < SlabsPerRing; s++)
                {
                    Quaternion turn = Quaternion.Euler(0f, s * 360f / SlabsPerRing, 0f);
                    PlaceholderShapes.Primitive(PrimitiveType.Cube, "Slab", ring, turn * new Vector3(0f, 0f, ShaftRadius + 0.2f),
                        new Vector3(slabWidth, RingSpacing + 0.02f, 0.4f), soils[r % 2], false, turn);
                }

                // Things stuck in the wall: a stone or two, sometimes a root across a corner, a worm, a glowing mushroom.
                int stones = random.Next(2, 5);
                for (int i = 0; i < stones; i++)
                {
                    Quaternion turn = Quaternion.Euler(0f, Range(0f, 360f), 0f);
                    float size = Range(0.3f, 0.65f);
                    PlaceholderShapes.Primitive(PrimitiveType.Sphere, "Stone", ring, turn * new Vector3(0f, Range(-0.5f, 0.5f), ShaftRadius - 0.02f),
                        new Vector3(size, size * 0.75f, size * 0.6f), stone, false, turn);
                }

                if (random.NextDouble() < 0.55)
                {
                    Quaternion turn = Quaternion.Euler(0f, Range(0f, 360f), 0f);
                    PlaceholderShapes.Primitive(PrimitiveType.Cylinder, "Root", ring, turn * new Vector3(0f, Range(-0.4f, 0.4f), ShaftRadius - 0.35f),
                        new Vector3(0.07f, Range(0.7f, 1.1f), 0.07f), root, false, turn * Quaternion.Euler(0f, 0f, 90f + Range(-25f, 25f)));
                }

                if (random.NextDouble() < 0.25)
                {
                    Quaternion turn = Quaternion.Euler(0f, Range(0f, 360f), 0f);
                    PlaceholderShapes.Primitive(PrimitiveType.Capsule, "Worm", ring, turn * new Vector3(0f, Range(-0.4f, 0.4f), ShaftRadius - 0.1f),
                        new Vector3(0.07f, 0.16f, 0.07f), worm, false, turn * Quaternion.Euler(0f, 0f, Range(40f, 140f)));
                }

                if (random.NextDouble() < 0.35)
                {
                    Quaternion turn = Quaternion.Euler(0f, Range(0f, 360f), 0f);
                    PlaceholderShapes.Primitive(PrimitiveType.Sphere, "GlowCap", ring, turn * new Vector3(0f, Range(-0.5f, 0.5f), ShaftRadius - 0.05f),
                        new Vector3(0.22f, 0.14f, 0.22f), glow, false);
                }
            }

            foreach (Renderer renderer in shaft.GetComponentsInChildren<Renderer>())
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            return rings;
        }
    }
}
