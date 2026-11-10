using System.Collections.Generic;
using GameJam.Core;
using GameJam.Gameplay;
using UnityEditor;
using UnityEngine;

namespace GameJam.Editor
{
    /// <summary>
    /// The Giant Backyard as a trail of stations (GDD: Levels): every hoop hangs at a set piece that is its obstacle, ~14 m
    /// apart and getting harder: open lawn (warm-up), the washing line (sheets swat the ring), the paddling pool (off the
    /// diving board, over the water), the merry-go-round (ride it or use the trampoline), the BBQ grill (flames on a beat,
    /// a fan blowing across) and the tree (bounce on the mushroom to the low branch), then the finish pad under a swinging
    /// pinata. Nothing is there that doesn't shape the way to a hoop (playtest: "what is the purpose of the other part of
    /// the map?"). Lawn the balloon must never touch, ringed by a climbable giant picket fence. Everything that moves runs
    /// on the shared CourseClock, so it costs no network traffic.
    /// </summary>
    public static partial class BackyardCourseBuilder
    {
        // The yard: lawn top at y = 0, x -26..26, z -16..84: the station trail plus a strip of lawn west for the giant.
        public const float HalfWidth = 26f;
        public const float SouthEdge = -16f;
        public const float NorthEdge = 84f;

        // Station centres (each zone keeps its internal layout; its hoop and checkpoint are offsets from the centre).
        // Order: washing line, pool, playground, BBQ, tree. Each is approached from its working side: the pool from the
        // west (the diving board), the tree from the south-east (the mushroom pad), the others from the south.
        public static readonly Vector3 WashingLineCenter = new Vector3(-4f, 0f, 21f);
        public static readonly Vector3 PoolCenter = new Vector3(13f, 0f, 24f);
        public const float PoolRadius = 9f;
        public static readonly Vector3 PlaygroundCenter = new Vector3(0f, 0f, 42f);
        public static readonly Vector3 BbqCenter = new Vector3(-6f, 0f, 56f);
        public static readonly Vector3 TreeCenter = new Vector3(2f, 0f, 60f);

        // Launch surfaces the hoop heights are measured from.
        private const float DiveBoardTop = 1.625f;  // BuildPool: board centre 1.5 m, 0.25 m thick; its tip is 6 m west of the pool centre
        private const float MerryTop = 0.35f;       // BuildPlayground: disc centre 0.25 m, 0.2 m thick
        private const float FlamesTop = 2.15f;      // BuildBbq: flame trigger centre 1.9 m, 0.5 m tall
        private const float BranchLowTop = 4.25f;   // BuildTree: BranchLow centre 4 m, 0.5 m thick, spans x 0.85..4.35 from the trunk

        /// <summary>Centre of the headline board (Squoof's booth sits on it), behind the finish pad.</summary>
        public static readonly Vector3 HeadlineScreen = new Vector3(-8f, 9f, 78f);

        private sealed class Palette
        {
            public Material Lawn;
            public Material Deck;
            public Material Wood;
            public Material Picket;
            public Material PicketCap;
            public Material Climb;
            public Material Hazard;
            public Material Cactus;
            public Material Water;
            public Material Gold;
            public Material Charcoal;
            public Material Inflatable;
            public Material Sand;
            public Material Soil;
            public Material Steel;
            public Material Checker;
            public Material Wall;
        }

        /// <summary>Open lawn spots the roaming mischief maker wanders between: the west strip, the south and the gaps.</summary>
        public static readonly Vector3[] RoamerWanderPoints =
        {
            // West lawn strip.
            new Vector3(-20f, 0f, -10f), new Vector3(-20f, 0f, 8f), new Vector3(-20f, 0f, 26f), new Vector3(-18f, 0f, 44f),
            new Vector3(-20f, 0f, 62f), new Vector3(-20f, 0f, 78f), new Vector3(-10f, 0f, 34f),
            // Middle and east. The pool walls off most of the east side, so these keep the giants from settling on the
            // west strip (from there every west spot is in a straight line, and few east ones are).
            new Vector3(12f, 0f, -10f), new Vector3(10f, 0f, 8f), new Vector3(18f, 0f, 38f), new Vector3(16f, 0f, 52f),
            new Vector3(0f, 0f, 74f), new Vector3(20f, 0f, -8f), new Vector3(22f, 0f, 10f), new Vector3(21f, 0f, 50f),
            new Vector3(20f, 0f, 66f), new Vector3(8f, 0f, 70f),
        };

        /// <summary>Set pieces the roamer steers around: x, z, radius (computed from the stations).</summary>
        public static Vector3[] RoamerAvoidCircles
        {
            get
            {
                Vector3 pinata = GapPoint(TrailHoops[TrailHoops.Length - 1], TrailFinish, PinataAlong, out _, out _, out _);
                return new[]
                {
                    new Vector3(PoolCenter.x, PoolCenter.z, PoolRadius + 1.5f),
                    new Vector3(PoolCenter.x - 12f, PoolCenter.z, 1.5f),                                   // diving board stand
                    new Vector3(WashingLineCenter.x, WashingLineCenter.z, 4f),                              // rotary clothesline
                    new Vector3(WashingLineCenter.x - 8f, WashingLineCenter.z - 7f, 0.8f), new Vector3(WashingLineCenter.x + 8f, WashingLineCenter.z - 7f, 0.8f),
                    new Vector3(TreeCenter.x, TreeCenter.z, 2.5f), new Vector3(TreeCenter.x + 2.6f, TreeCenter.z - 1.9f, 1.2f),
                    new Vector3(PlaygroundCenter.x + 4f, PlaygroundCenter.z - 2f, 3.5f), new Vector3(PlaygroundCenter.x, PlaygroundCenter.z - 6f, 2f),
                    new Vector3(BbqCenter.x, BbqCenter.z - 6f, 2.5f), new Vector3(BbqCenter.x + 10f, BbqCenter.z - 6f, 2f),
                    new Vector3(0f, -6f, 7f),                                                              // start stage
                    new Vector3(TrailFinish.x, TrailFinish.z, 5f),                                          // finish pad
                    new Vector3(HeadlineScreen.x - 6f, HeadlineScreen.z + 0.5f, 0.8f), new Vector3(HeadlineScreen.x + 6f, HeadlineScreen.z + 0.5f, 0.8f),
                    new Vector3(pinata.x, pinata.z, 3f),                                                   // pinata frame
                };
            }
        }

        /// <summary>Where the roamer may walk: min x, min z, max x, max z (4 m inside the fence).</summary>
        public static readonly Vector4 RoamerBounds = new Vector4(-HalfWidth + 4f, SouthEdge + 4f, HalfWidth - 4f, NorthEdge - 4f);

        /// <summary>Where the giants start: one per show character (west lawn strip, and east beyond the pool).</summary>
        public static readonly Vector3[] RoamerSpawns = { new Vector3(-20f, 0f, 30f), new Vector3(18f, 0f, 38f) };

        private static Palette _palette;
        private static Transform _course;
        private static CharacterRoster _roster;

        public static void Build(CharacterRoster roster)
        {
            _course = new GameObject("Course").transform;
            _palette = CreatePalette();
            _roster = roster;

            BuildGroundAndFence();
            BuildHouseAndFinish();
            BuildStartStage();
            BuildPool();
            BuildWashingLine();
            BuildTree();
            BuildPlayground();
            BuildBbq();
            BuildHoops();
            BuildItemStashes();
            SetPieceModels.Apply(_course); // real-looking models over the gameplay primitives (Art/Set), colliders unchanged
            BuildAmbience();
            DepthTestCourseText();
        }

        /// <summary>
        /// The built-in 3D text shader takes its depth test from a global the on-screen GUI sets (not from the material), so every
        /// label in the yard showed through whatever stood in front of it ("HOOP 1" and the headline across the lobby's
        /// how-to-play sign). Course labels use Shaders/WorldText (GameJam/WorldText, depth tested) instead; text meant to float
        /// on top (the time's-up banner) keeps its own material.
        /// </summary>
        private static void DepthTestCourseText()
        {
            const string path = PlaceholderAssets.MaterialFolder + "/WorldText.mat";
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("GameJam/WorldText");
            if (shader == null)
            {
                Debug.LogWarning("[Text] GameJam/WorldText shader missing: course labels stay on the built-in text shader.");
                return;
            }

            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            material.mainTexture = font.material.mainTexture;
            EditorUtility.SetDirty(material);
            foreach (TextMesh text in _course.GetComponentsInChildren<TextMesh>(true))
            {
                if (text.TryGetComponent(out MeshRenderer renderer) && renderer.sharedMaterial == font.material)
                {
                    renderer.sharedMaterial = material;
                }
            }
        }

        /// <summary>Ambient life: pollen drifting over the yard and leaves falling from the tree (no colliders).</summary>
        private static void BuildAmbience()
        {
            var ambience = new GameObject("Ambience").transform;
            ambience.SetParent(_course, false);
            float middleZ = (SouthEdge + NorthEdge) * 0.5f;
            AmbientEffectsBuilder.Pollen(ambience, new Vector3(0f, 3f, middleZ), new Vector3(HalfWidth * 2f, 5f, NorthEdge - SouthEdge));
            Color leafLight = Color.Lerp(ArtPalette.LawnDark, ArtPalette.Accent, 0.35f);
            AmbientEffectsBuilder.FallingLeaves(ambience, TreeCenter + new Vector3(0f, 11f, 0f), 4f, ArtPalette.LawnDark, leafLight);
            AmbientEffectsBuilder.Bursts(ambience); // pooled confetti (hoops) and pop shreds (balloon)
        }

        /// <summary>A trigger that launches players (with a boing).</summary>
        private static void BouncePadOn(GameObject trigger, float launchSpeed)
        {
            AudioSource boing = PlaceholderPrefabBuilder.Sound(trigger, GeneratedAudio.TrampolineBoing("Trampoline_Boing"), false, 20f, 0.9f);
            trigger.AddComponent<BouncePad>().Configure(launchSpeed, boing);
        }

        private static Palette CreatePalette()
        {
            Color poolWater = Color.Lerp(ArtPalette.Pool, ArtPalette.PoolDeep, 0.4f);
            // Mown stripes, 4 m wide, running down the yard (one texture = two stripes, tiled 8 m): the classic backyard
            // look, and the stripes give the lawn scale and depth at every distance for free.
            Texture2D lawn = GeneratedTextures.LawnStripes("T_LawnStripes", ArtPalette.Lawn, Color.Lerp(ArtPalette.Lawn, ArtPalette.LawnDark, 0.65f));
            // Deck boards ~30 cm wide (8 per 2.4 m tile) with dark seams: the stage, the first thing players see, reads as wood.
            Texture2D deck = GeneratedTextures.Planks("T_DeckPlanks", ArtPalette.Timber, Color.Lerp(ArtPalette.TimberDark, ArtPalette.Shadow, 0.25f));
            // Pickets darken toward the grass: baked ambient occlusion where the fence meets the lawn.
            Texture2D picket = GeneratedTextures.VerticalShade("T_PicketShade", ArtPalette.Picket, Color.Lerp(ArtPalette.Picket, ArtPalette.Shadow, 0.38f));
            Texture2D water = GeneratedTextures.Noise("T_PoolWater", poolWater, Color.Lerp(poolWater, ArtPalette.PoolTile, 0.25f), 4f);
            Texture2D checker = GeneratedTextures.Checker("T_FinishChecker", ArtPalette.Picket, ArtPalette.Charcoal, 8);
            Texture2D sand = GeneratedTextures.Noise("T_Sand", ArtPalette.HouseWall, Color.Lerp(ArtPalette.HouseWall, ArtPalette.Timber, 0.35f), 5f);

            return new Palette
            {
                Lawn = PlaceholderAssets.Material("Lawn", Color.white, 0.1f, lawn, new Vector2(HalfWidth * 2f / 8f, (NorthEdge - SouthEdge) / 8f)),
                Deck = PlaceholderAssets.Material("Platform", Color.white, 0.3f, deck, new Vector2(5f, 3.75f)),
                Wood = PlaceholderAssets.Material("Wood", ArtPalette.TimberDark),
                Picket = PlaceholderAssets.Material("Picket", Color.white, 0.22f, picket, Vector2.one),
                PicketCap = PlaceholderAssets.Material("PicketCap", ArtPalette.Picket, 0.22f),
                // Toys are glossy plastic (a highlight from the sky reads as "toy"); smoothness costs nothing extra.
                Climb = PlaceholderAssets.Material("Climbable", ArtPalette.Climbable, 0.5f),
                Hazard = PlaceholderAssets.Material("Hazard", ArtPalette.Hazard, 0.55f),
                Cactus = PlaceholderAssets.Material("Cactus", ArtPalette.LawnDark),
                Water = PlaceholderAssets.Material("Water", Color.white, 0.55f, water, new Vector2(3f, 3f)),
                Gold = PlaceholderAssets.Material("Gate", ArtPalette.Accent, 0.6f),
                Charcoal = PlaceholderAssets.Material("Charcoal", ArtPalette.Charcoal, 0.45f),
                Inflatable = PlaceholderAssets.Material("PoolInflatable", ArtPalette.CandyBlue, 0.6f),
                Sand = PlaceholderAssets.Material("Sand", Color.white, 0.05f, sand, new Vector2(2f, 2f)),
                Soil = PlaceholderAssets.Material("Soil", ArtPalette.TimberDark, 0.05f),
                Steel = PlaceholderAssets.Material("Steel", ArtPalette.Steel, 0.7f),
                Checker = PlaceholderAssets.Material("Dark", Color.white, 0.2f, checker, new Vector2(3f, 2f)),
                Wall = PlaceholderAssets.Material("HouseWall", ArtPalette.HouseWall),
            };
        }

        // ------------------------------------------------------------------ ground, fence, house

        private static void BuildGroundAndFence()
        {
            GameObject lawn = Box("Lawn", new Vector3(0f, -0.25f, (SouthEdge + NorthEdge) / 2f), new Vector3(HalfWidth * 2f, 0.5f, NorthEdge - SouthEdge), _palette.Lawn);
            lawn.AddComponent<BalloonGround>();

            // Giant picket fence: the yard's walls, climbable (Default layer) so a hero can scale it for the fence-top hoop.
            const float plank = 3.6f;
            const float spacing = 3.8f;
            const float height = 7f;
            for (float z = SouthEdge + 1.9f; z < NorthEdge; z += spacing)
            {
                Picket(new Vector3(-HalfWidth, 0f, z), Quaternion.Euler(0f, 90f, 0f), plank, height);
                Picket(new Vector3(HalfWidth, 0f, z), Quaternion.Euler(0f, 90f, 0f), plank, height);
            }

            for (float x = -HalfWidth + 1.9f; x < HalfWidth; x += spacing)
            {
                Picket(new Vector3(x, 0f, SouthEdge), Quaternion.identity, plank, height);
                if (Mathf.Abs(x) > 16f)
                {
                    Picket(new Vector3(x, 0f, NorthEdge), Quaternion.identity, plank, height); // the house fills the middle
                }
            }

            Material rail = PlaceholderAssets.Material("FenceRail", Color.Lerp(ArtPalette.Picket, ArtPalette.Shadow, 0.15f));
            foreach (float y in new[] { 1.8f, 5.2f })
            {
                Box("FenceRailWest", new Vector3(-HalfWidth - 0.45f, y, (SouthEdge + NorthEdge) / 2f), new Vector3(0.35f, 0.6f, NorthEdge - SouthEdge), rail, false);
                Box("FenceRailEast", new Vector3(HalfWidth + 0.45f, y, (SouthEdge + NorthEdge) / 2f), new Vector3(0.35f, 0.6f, NorthEdge - SouthEdge), rail, false);
                Box("FenceRailSouth", new Vector3(0f, y, SouthEdge - 0.45f), new Vector3(HalfWidth * 2f, 0.6f, 0.35f), rail, false);
            }
        }

        private static void Picket(Vector3 basePosition, Quaternion rotation, float width, float height)
        {
            GameObject board = Box("Picket", basePosition + Vector3.up * (height / 2f), new Vector3(width, height, 0.4f), _palette.Picket);
            board.transform.rotation = rotation;
            GameObject cap = Box("PicketCap", basePosition + Vector3.up * height, new Vector3(width * 0.71f, width * 0.71f, 0.4f), _palette.PicketCap, false);
            cap.transform.rotation = rotation * Quaternion.Euler(0f, 0f, 45f);
        }

        private static void BuildHouseAndFinish()
        {
            // The house closes the north side; its wall is climbable.
            Box("HouseBody", new Vector3(0f, 12f, NorthEdge + 10f), new Vector3(32f, 24f, 20f), _palette.Wall);
            Material roof = PlaceholderAssets.Material("HouseRoof", ArtPalette.Roof);
            Box("RoofLeft", new Vector3(-8.5f, 29f, NorthEdge + 10f), new Vector3(20f, 2f, 23f), roof, false).transform.rotation = Quaternion.Euler(0f, 0f, 32f);
            Box("RoofRight", new Vector3(8.5f, 29f, NorthEdge + 10f), new Vector3(20f, 2f, 23f), roof, false).transform.rotation = Quaternion.Euler(0f, 0f, -32f);
            // The house is the backdrop behind the headline board; the finish is at the end of the hoop trail (BuildHoops).
        }

        // ------------------------------------------------------------------ zones

        private static void BuildStartStage()
        {
            var stage = new Vector3(0f, 0f, -6f);
            Box("StartStage", stage + new Vector3(0f, 0.2f, 0f), new Vector3(12f, 0.4f, 9f), _palette.Deck);
            // The balloon waits (stationed, bobbing) just above head height in front of the start: reachable with a raised arm.
            SpawnPoint(0, new Vector3(0f, 0.4f, -8f), 0f, new Vector3(0f, 1.4f, 1.8f));

            Box("ButtonPedestal", stage + new Vector3(4f, 0.65f, 2.5f), new Vector3(0.5f, 0.5f, 0.5f), _palette.Wood);
            GameObject button = PlaceholderShapes.Primitive(PrimitiveType.Cube, "StartButton", _course, stage + new Vector3(4f, 1.02f, 2.5f), new Vector3(0.4f, 0.25f, 0.4f), _palette.Hazard, true);
            button.GetComponent<Collider>().isTrigger = true;
            button.AddComponent<StartButton>();
            button.layer = GameLayers.IgnoreRaycast;
            BuildLobbyOnly(stage, new Vector3(12f, 0.4f, 9f), stage + new Vector3(4f, 0.4f, 2.5f));

            // Commentator: a big board on stilts behind the finish pad, facing down the trail.
            Material board = PlaceholderAssets.Material("Backboard", ArtPalette.TextDark, 0.3f);
            Vector3 screen = HeadlineScreen;
            Box("HeadlineBoard", screen + new Vector3(0f, 0f, 0.35f), new Vector3(13f, 2.4f, 0.3f), board, false);
            Box("HeadlineTrim", screen + new Vector3(0f, 1.25f, 0.35f), new Vector3(13.2f, 0.2f, 0.4f), _palette.Gold, false);
            foreach (float x in new[] { -6f, 6f })
            {
                Box("HeadlineStilt", new Vector3(screen.x + x, 4f, screen.z + 0.5f), new Vector3(0.4f, 8f, 0.4f), _palette.Wood);
            }

            Box("CommentatorBooth", screen + new Vector3(0f, 2.4f, 0.6f), new Vector3(5f, 1.6f, 1.6f), _palette.Wood, false);
            PlaceholderShapes.Primitive(PrimitiveType.Sphere, "LiveLight", _course, screen + new Vector3(2.8f, 3.6f, 0.4f), Vector3.one * 0.6f,
                PlaceholderAssets.Emissive("LiveLight", ArtPalette.Hazard, 2.5f), false);

            Box("ScoreboardBoard", new Vector3(-4.2f, 2.3f, -2.4f), new Vector3(3.4f, 1.7f, 0.12f), board, false);
            Box("ScoreboardTrim", new Vector3(-4.2f, 3.2f, -2.4f), new Vector3(3.5f, 0.12f, 0.16f), _palette.Gold, false);

            var boardComponent = new GameObject("CommentatorBoard").AddComponent<CommentatorBoard>();
            boardComponent.transform.SetParent(_course, false);
            TextMesh headline = PlaceholderShapes.Text("Headline", _course, screen, 0.12f, "Connecting...", ArtPalette.Accent);
            TextMesh scoreboard = PlaceholderShapes.Text("Scoreboard", _course, new Vector3(-4.2f, 2.3f, -2.5f), 0.019f, string.Empty, ArtPalette.Picket);
            boardComponent.Configure(headline, scoreboard);

            BuildMirror(stage);
            BuildShowCrew(stage, screen);
        }

        /// <summary>
        /// Lobby only (<see cref="LobbyOnly"/>, gone once the red button is slapped): clear glass walls round the start
        /// stage, so the crew stays on it but can still see the whole yard, and a "slap to start" sign on the button's
        /// pedestal with a bobbing arrow hovering over the button, pointing down at it. The walls are on Default (so gorilla hands meet them) but
        /// slippery like ice, so nobody climbs out, and tall enough that no jump clears them.
        /// </summary>
        private static void BuildLobbyOnly(Vector3 stageCenter, Vector3 stageSize, Vector3 pedestalBase)
        {
            var root = new GameObject("LobbyOnly");
            root.transform.SetParent(_course, false);
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); // children below are placed in world space

            // Walls: just outside the stage edges, from the ground up, corners overlapping.
            var fence = new GameObject("LobbyWalls");
            fence.transform.SetParent(root.transform, false);
            Material glass = PlaceholderAssets.Glass("LobbyGlass", new Color(0.75f, 0.9f, 1f, 0.07f));
            Material rail = PlaceholderAssets.Emissive("LobbyRail", ArtPalette.Accent, 1.2f);
            float top = stageSize.y + LobbyWallHeight;
            float halfX = stageSize.x * 0.5f + LobbyWallThickness * 0.5f;
            float halfZ = stageSize.z * 0.5f + LobbyWallThickness * 0.5f;
            var walls = new (string name, Vector3 offset, Vector3 size)[]
            {
                ("North", new Vector3(0f, 0f, halfZ), new Vector3(stageSize.x + LobbyWallThickness * 2f, top, LobbyWallThickness)),
                ("South", new Vector3(0f, 0f, -halfZ), new Vector3(stageSize.x + LobbyWallThickness * 2f, top, LobbyWallThickness)),
                ("East", new Vector3(halfX, 0f, 0f), new Vector3(LobbyWallThickness, top, stageSize.z)),
                ("West", new Vector3(-halfX, 0f, 0f), new Vector3(LobbyWallThickness, top, stageSize.z)),
            };
            Vector3 floor = new Vector3(stageCenter.x, 0f, stageCenter.z);
            foreach ((string name, Vector3 offset, Vector3 size) in walls)
            {
                GameObject wall = Box($"LobbyWall{name}", fence.transform, floor + offset + Vector3.up * (top * 0.5f), size, glass);
                wall.AddComponent<GorillaLocomotion.Surface>().slipPercentage = 0.99f; // ice: hands slide, can't climb out
                // Glowing rails at hand height and along the top, so the clear wall reads as a wall.
                var railSize = new Vector3(Mathf.Max(size.x, 0.04f), 0.04f, Mathf.Max(size.z, 0.04f));
                Box($"LobbyRail{name}", fence.transform, floor + offset + Vector3.up * (stageSize.y + 1.1f), railSize, rail, false);
                Box($"LobbyRailTop{name}", fence.transform, floor + offset + Vector3.up * top, railSize, rail, false);
            }

            // Sign on the pedestal's front (the side facing the spawn), below the button, and an arrow pointing up at it.
            var sign = new GameObject("LobbyStartSign");
            sign.transform.SetParent(root.transform, false);
            Vector3 front = pedestalBase + new Vector3(0f, 0f, -0.27f);
            Box("StartSignBoard", sign.transform, front + new Vector3(0f, 0.27f, -0.01f), new Vector3(1.5f, 0.48f, 0.03f),
                PlaceholderAssets.Material("SignBoard", ArtPalette.TextDark, 0.3f), false);
            PlaceholderShapes.Text("StartSignText", sign.transform, front + new Vector3(0f, 0.27f, -0.035f), 0.021f,
                "SLAP THE RED\nBUTTON TO START!", ArtPalette.Accent);
            // Arrow hovering over the button, pointing down at it: two crossed chevrons (readable from any side of the stage),
            // turned upside down, bobbing and slowly spinning. Button top: 0.745 m above the stage.
            var arrow = new GameObject("StartArrow").transform;
            arrow.SetParent(sign.transform, false);
            arrow.SetPositionAndRotation(pedestalBase + new Vector3(0f, 1.22f, 0f), Quaternion.Euler(0f, 0f, 180f));
            arrow.localScale = Vector3.one * 1.8f; // readable from the spawn, ~5.5 m away; tip ~0.25 m over the button
            Material glow = PlaceholderAssets.Emissive("StartArrowGlow", ArtPalette.Hazard, 1.6f);
            foreach (float yaw in new[] { 0f, 90f })
            {
                var fin = new GameObject("ArrowFin").transform;
                fin.SetParent(arrow, false);
                fin.localRotation = Quaternion.Euler(0f, yaw, 0f);
                Box("ArrowShaft", fin, new Vector3(0f, -0.07f, 0f), new Vector3(0.06f, 0.22f, 0.03f), glow, false);
                Box("ArrowHeadLeft", fin, new Vector3(-0.055f, 0.06f, 0f), new Vector3(0.06f, 0.17f, 0.03f), glow, false)
                    .transform.localRotation = Quaternion.Euler(0f, 0f, -45f);
                Box("ArrowHeadRight", fin, new Vector3(0.055f, 0.06f, 0f), new Vector3(0.06f, 0.17f, 0.03f), glow, false)
                    .transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            }

            arrow.gameObject.AddComponent<IdleMotion>().Configure(0.06f, 1.2f, 0f, 60f, 0f);

            GameObject howTo = BuildItemHowTo(root.transform, stageCenter, stageSize);
            GameObject howToPlay = BuildHowToPlay(root.transform, stageCenter, stageSize);

            // The mole hole to the tutorial, in the stage's south-east corner (clear of the spawn slots, the button and the
            // signs). Lobby only: once the button is slapped it is gone until the run is over.
            GameObject tutorialHole = BuildMoleHole(root.transform, stageCenter + new Vector3(3.9f, stageSize.y, -3.3f), SceneTravel.TutorialScene,
                "TUTORIAL", "JUMP IN THE HOLE!", 90f);

            root.AddComponent<LobbyOnly>().Configure(new[] { fence, sign, howTo, howToPlay, tutorialHole });
        }

        /// <summary>
        /// "How to play" picture sign on the inside of the lobby's north wall, straight ahead of the spawn: slap the balloon,
        /// knock it through a hoop, clear all six, fly it to the finish (a medal), with the two rules that pop it underneath.
        /// Lobby only, like the walls (the balloon is hidden in the lobby, so it never blocks a shot at hoop 1).
        /// </summary>
        private static GameObject BuildHowToPlay(Transform parent, Vector3 stageCenter, Vector3 stageSize)
        {
            float wallInside = stageCenter.z + stageSize.z * 0.5f - 0.08f;
            return BuildPictureSign("HowToPlaySign", parent, new Vector3(stageCenter.x, stageSize.y + 1.95f, wallInside), Quaternion.identity,
                "HOW TO PLAY", "DON'T LET IT TOUCH THE GROUND!      TAKE TURNS: NOBODY HITS IT TWICE IN A ROW!", 0.0115f,
                new[]
                {
                    ("Icon_Slap", "1. SLAP THE BALLOON"),
                    ("Icon_Hoop", "2. HIT IT THROUGH A HOOP"),
                    ("Icon_AllHoops", "3. CLEAR ALL 6 HOOPS"),
                    ("Icon_Win", "4. FLY IT TO THE FINISH!"),
                });
        }

        /// <summary>
        /// Picture sign on the inside of the lobby's east wall: grab an item, throw it, knock the giant down. Off the line of
        /// sight to hoop 1.
        /// </summary>
        private static GameObject BuildItemHowTo(Transform parent, Vector3 stageCenter, Vector3 stageSize)
        {
            float wallInside = stageCenter.x + stageSize.x * 0.5f - 0.08f;
            return BuildPictureSign("ItemHowToSign", parent, new Vector3(wallInside, stageSize.y + 1.75f, stageCenter.z - 1f), Quaternion.Euler(0f, 90f, 0f),
                "ITEMS KNOCK OUT THE GIANTS!", null, 0.0135f,
                new[]
                {
                    ("Icon_Item", "1. GRAB AN ITEM"),
                    ("Icon_Throw", "2. THROW IT"),
                    ("Icon_Knockdown", "3. BONK THE GIANT!"),
                });
        }

        /// <summary>
        /// A dark board with a title, an optional rules line, and picture steps left to right (light panels, icons from
        /// Art/UI rendered by tools/blender/scripts/build_lobby_icons.py, a caption under each, red chevrons between).
        /// Collider-free like the other set dressing. The root's local -Z faces the viewers and +X reads left to right.
        /// </summary>
        private static GameObject BuildPictureSign(string name, Transform parent, Vector3 position, Quaternion rotation, string titleText,
            string rulesText, float captionSize, (string icon, string caption)[] steps)
        {
            const float spacing = 1.15f;
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(position, rotation);

            bool hasRules = !string.IsNullOrEmpty(rulesText);
            float width = steps.Length * spacing + 0.05f;
            float top = hasRules ? 0.88f : 0.725f;
            float bottom = hasRules ? -0.78f : -0.725f;
            float panelY = hasRules ? -0.03f : 0.02f;
            float captionY = panelY - 0.55f;

            Material board = PlaceholderAssets.Material("SignBoard", ArtPalette.TextDark, 0.3f);
            Box($"{name}Board", root.transform, new Vector3(0f, (top + bottom) * 0.5f, 0.03f), new Vector3(width, top - bottom, 0.05f), board, false);
            Box($"{name}Trim", root.transform, new Vector3(0f, top + 0.015f, 0.03f), new Vector3(width + 0.1f, 0.06f, 0.07f), _palette.Gold, false);
            TextMesh title = PlaceholderShapes.Text($"{name}Title", root.transform, new Vector3(0f, top - 0.17f, -0.01f), hasRules ? 0.026f : 0.02f,
                titleText, ArtPalette.Accent);
            title.fontStyle = FontStyle.Bold;
            if (hasRules)
            {
                PlaceholderShapes.Text($"{name}Rules", root.transform, new Vector3(0f, top - 0.33f, -0.01f), 0.0125f, rulesText, ArtPalette.Hazard)
                    .fontStyle = FontStyle.Bold;
            }

            Material panel = PlaceholderAssets.TransparentUnlit("HowToPanel", GeneratedTextures.RoundedPanel("T_RoundedPanel"));
            panel.SetColor("_BaseColor", ArtPalette.Picket);
            EditorUtility.SetDirty(panel);
            Material arrow = PlaceholderAssets.Emissive("StartArrowGlow", ArtPalette.Hazard, 1.6f);
            for (int i = 0; i < steps.Length; i++)
            {
                float x = (i - (steps.Length - 1) * 0.5f) * spacing;
                GameObject back = PlaceholderShapes.Primitive(PrimitiveType.Quad, $"{name}Panel{i}", root.transform, new Vector3(x, panelY, -0.006f),
                    new Vector3(0.95f, 0.85f, 1f), panel, false);
                back.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                Texture2D texture = ImportIcon($"{PlaceholderAssets.Root}/Art/UI/{steps[i].icon}.png");
                if (texture != null)
                {
                    GameObject icon = PlaceholderShapes.Primitive(PrimitiveType.Quad, $"{name}Icon{i}", root.transform, new Vector3(x, panelY, -0.012f),
                        new Vector3(0.8f, 0.8f, 1f), PlaceholderAssets.TransparentUnlit($"HowTo{steps[i].icon}", texture), false);
                    icon.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }

                PlaceholderShapes.Text($"{name}Caption{i}", root.transform, new Vector3(x, captionY, -0.01f), captionSize, steps[i].caption, ArtPalette.Picket);
                if (i < steps.Length - 1)
                {
                    // A chevron pointing right, between this step and the next.
                    Transform chevron = new GameObject($"{name}Arrow{i}").transform;
                    chevron.SetParent(root.transform, false);
                    chevron.localPosition = new Vector3(x + spacing * 0.5f, panelY, -0.015f);
                    Box("ChevronTop", chevron, new Vector3(0f, 0.035f, 0f), new Vector3(0.11f, 0.035f, 0.02f), arrow, false).transform.localRotation = Quaternion.Euler(0f, 0f, -40f);
                    Box("ChevronBottom", chevron, new Vector3(0f, -0.035f, 0f), new Vector3(0.11f, 0.035f, 0.02f), arrow, false).transform.localRotation = Quaternion.Euler(0f, 0f, 40f);
                }
            }

            return root;
        }

        /// <summary>A UI icon PNG imported for the world: alpha as transparency, mipmapped (seen from metres away), clamped.</summary>
        private static Texture2D ImportIcon(string path)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            {
                Debug.LogWarning($"[HowTo] Missing icon {path}: run tools/blender/scripts/build_lobby_icons.py.");
                return null;
            }

            if (!importer.alphaIsTransparency || !importer.mipmapEnabled || importer.wrapMode != TextureWrapMode.Clamp)
            {
                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private const float LobbyWallHeight = 5f;     // above the stage: no gorilla jump (~2.9 m) clears it
        private const float LobbyWallThickness = 0.2f;

        /// <summary>A tall wood-framed mirror at the stage's west edge, turned to the spawn so players see their porcupine.</summary>
        private static void BuildMirror(Vector3 stage)
        {
            var size = new Vector2(1.6f, 2.2f);
            var root = new GameObject("StageMirror").transform;
            root.SetParent(_course, false);
            // Visible face (quad -Z) toward +X, turned a little toward the spawn at the stage's south.
            root.SetPositionAndRotation(stage + new Vector3(-5.6f, 0.4f + 0.15f + size.y * 0.5f, -1.2f), Quaternion.Euler(0f, -80f, 0f));
            GameObject glass = PlaceholderShapes.Primitive(PrimitiveType.Quad, "MirrorGlass", root, Vector3.zero, new Vector3(size.x, size.y, 1f),
                PlaceholderAssets.Unlit("MirrorGlass", Color.white), false);
            glass.AddComponent<PlanarMirror>();
            const float frame = 0.12f;
            Material wood = _palette.Wood;
            Box("MirrorFrameTop", root, new Vector3(0f, size.y * 0.5f + frame * 0.5f, 0.04f), new Vector3(size.x + frame * 2f, frame, 0.1f), wood, false);
            Box("MirrorFrameBottom", root, new Vector3(0f, -size.y * 0.5f - frame * 0.5f, 0.04f), new Vector3(size.x + frame * 2f, frame, 0.1f), wood, false);
            Box("MirrorFrameLeft", root, new Vector3(-size.x * 0.5f - frame * 0.5f, 0f, 0.04f), new Vector3(frame, size.y, 0.1f), wood, false);
            Box("MirrorFrameRight", root, new Vector3(size.x * 0.5f + frame * 0.5f, 0f, 0.04f), new Vector3(frame, size.y, 0.1f), wood, false);
            Box("MirrorBack", root, new Vector3(0f, 0f, 0.07f), new Vector3(size.x, size.y, 0.04f), wood);
            Box("MirrorFoot", root, new Vector3(0f, -size.y * 0.5f - 0.1f, 0.25f), new Vector3(size.x + 0.3f, 0.1f, 0.6f), wood, false);
        }

        /// <summary>
        /// The show's crew as map entities (players are all the porcupine): Jman, the host, greets the crew on the start
        /// stage; Squoof commentates from the top of the booth above the headline board. Purely visual; rigged ones idle and wave.
        /// </summary>
        private static void BuildShowCrew(Vector3 stage, Vector3 screen)
        {
            if (_roster == null)
            {
                return;
            }

            Material fallback = PlaceholderAssets.Material("CharacterPlaceholder", Color.white, 0.3f);
            CharacterDefinition host = FindCharacter("Jmancurly");
            if (host != null)
            {
                Vector3 spot = stage + new Vector3(-3.5f, 0.4f, 2.8f);
                Box("HostPodium", spot + new Vector3(0f, 0.15f, 0f), new Vector3(1.1f, 0.3f, 1.1f), _palette.Gold);
                // Faces the spawn (south); a solid capsule so players bump into the host instead of walking through.
                CrewFigure("Host_Jman", host, spot + new Vector3(0f, 0.3f, 0f), 180f, host.Height, fallback, true);
                Billboard(PlaceholderShapes.Text("HostName", _course, spot + new Vector3(0f, host.Height + 0.65f, 0f), 0.022f, $"{host.DisplayName}  ·  HOST", ArtPalette.Accent));
            }

            CharacterDefinition commentator = FindCharacter("Squoof");
            if (commentator != null)
            {
                // Booth: centre screen + (0, 2.4, 0.6), 1.6 m tall, so its roof is 3.2 m above the screen centre.
                Vector3 roof = screen + new Vector3(0f, 3.2f, 0.6f);
                float height = commentator.Height * 1.5f; // readable from across the yard
                CrewFigure("Commentator_Squoof", commentator, roof, 180f, height, fallback, false);
                Billboard(PlaceholderShapes.Text("CommentatorName", _course, roof + new Vector3(0f, height + 0.5f, 0f), 0.05f, $"{commentator.DisplayName}  ·  COMMENTATOR", ArtPalette.Accent));
            }
        }

        private static CharacterDefinition FindCharacter(string displayName)
        {
            for (int i = 0; i < _roster.Count; i++)
            {
                if (_roster.Get(i).DisplayName == displayName)
                {
                    return _roster.Get(i);
                }
            }

            return null;
        }

        private static void CrewFigure(string name, CharacterDefinition character, Vector3 feet, float yaw, float height, Material fallback, bool solid)
        {
            var figure = new GameObject(name).AddComponent<CharacterVisual>();
            figure.transform.SetParent(_course, false);
            figure.transform.SetPositionAndRotation(feet, Quaternion.Euler(0f, yaw, 0f));
            figure.Configure(fallback);
            figure.ShowOnLoad(character, height, true); // rigged crew idle and wave at the crowd now and then
            if (solid)
            {
                var body = figure.gameObject.AddComponent<CapsuleCollider>();
                body.height = height;
                body.radius = height * 0.18f;
                body.center = new Vector3(0f, height * 0.5f, 0f);
            }
        }

        private static void BuildPool()
        {
            // A giant paddling pool: inflatable rim, water that pops the balloon and sends players back to their checkpoint.
            Vector3 c = PoolCenter;
            const int segments = 20;
            for (int i = 0; i < segments; i++)
            {
                float angle = i * 360f / segments;
                Quaternion rotation = Quaternion.Euler(0f, angle, 0f);
                Vector3 position = c + rotation * new Vector3(0f, 0.6f, PoolRadius);
                Box("PoolRim", position, new Vector3(3f, 1.2f, 0.9f), _palette.Inflatable).transform.rotation = rotation;
            }

            Box("PoolFloor", c + new Vector3(0f, 0.08f, 0f), new Vector3(PoolRadius * 2f - 1f, 0.16f, PoolRadius * 2f - 1f), _palette.Inflatable).GetComponent<Renderer>().enabled = false;
            PlaceholderShapes.Primitive(PrimitiveType.Cylinder, "PoolWater", _course, c + new Vector3(0f, 0.85f, 0f), new Vector3(PoolRadius * 2f - 0.6f, 0.02f, PoolRadius * 2f - 0.6f), _palette.Water, false);
            foreach (float yaw in new[] { 0f, 30f, 60f })
            {
                GameObject water = PlaceholderShapes.TriggerBox("PoolWaterVolume", _course, c + new Vector3(0f, 0.55f, 0f), new Vector3(12.6f, 0.75f, 12.6f), Quaternion.Euler(0f, yaw, 0f));
                water.AddComponent<Water>();
            }

            // Diving board out over the water: bounce up to slap the balloon through the pool hoop.
            Vector3 board = c + new Vector3(-PoolRadius - 1f, 0f, 0f);
            Box("DivingBoardStand", board + new Vector3(-2f, 0.7f, 0f), new Vector3(1.4f, 1.4f, 1.6f), _palette.Steel);
            Box("DivingBoard", board + new Vector3(1f, 1.5f, 0f), new Vector3(6f, 0.25f, 1.4f), _palette.Deck);
            BouncePadOn(PlaceholderShapes.TriggerBox("DivingBoardSpring", _course, board + new Vector3(3.4f, 1.9f, 0f), new Vector3(1.4f, 0.6f, 1.4f)), 10f);

            // A rubber duck float paddles around the pool (passing object: swats the balloon, harmless to players).
            MovingObstacle duck = Mover("PoolDuck", c + new Vector3(5f, 1.2f, 0f));
            duck.ConfigurePath(Circle(c + Vector3.up * 1.2f, 5f, 16), 1.6f, true, 0f, false, false);
            Material yellow = PlaceholderAssets.Material("DuckYellow", ArtPalette.Accent, 0.45f);
            Box("DuckBody", duck.transform, Vector3.zero, new Vector3(1.6f, 1.1f, 2f), yellow);
            PlaceholderShapes.Primitive(PrimitiveType.Sphere, "DuckHead", duck.transform, new Vector3(0f, 1f, 0.7f), Vector3.one, yellow, false);
            PlaceholderShapes.Primitive(PrimitiveType.Sphere, "DuckBeak", duck.transform, new Vector3(0f, 0.95f, 1.25f), new Vector3(0.5f, 0.22f, 0.45f),
                PlaceholderAssets.Material("DuckBeak", ArtPalette.Climbable), false);
        }

        private static void BuildWashingLine()
        {
            Vector3 c = WashingLineCenter;

            // Spinning rotary clothesline at head height: duck under it.
            Box("ClotheslinePole", c + new Vector3(0f, 1.3f, 0f), new Vector3(0.25f, 2.6f, 0.25f), _palette.Steel);
            MovingObstacle rotary = Mover("SpinningClothesline", c + new Vector3(0f, 2.4f, 0f));
            rotary.ConfigureSpin(40f);
            Box("LineA", rotary.transform, Vector3.zero, new Vector3(7f, 0.1f, 0.1f), _palette.PicketCap);
            Box("LineB", rotary.transform, Vector3.zero, new Vector3(0.1f, 0.1f, 7f), _palette.PicketCap);

            // The washing line the hoop hangs on: its flapping sheets swing across the ring and swat the balloon.
            foreach (float z in new[] { -7f })
            {
                foreach (float x in new[] { -8f, 8f })
                {
                    Box("LinePost", c + new Vector3(x, 2.2f, z), new Vector3(0.25f, 4.4f, 0.25f), _palette.Wood);
                }

                Box("WashingLine", c + new Vector3(0f, 4.3f, z), new Vector3(16f, 0.05f, 0.05f), _palette.PicketCap, false);
                for (int i = 0; i < 3; i++)
                {
                    MovingObstacle sheet = Mover("FlappingSheet", c + new Vector3(-5f + i * 5f, 4.3f, z));
                    sheet.ConfigureSwing(Vector3.right, 25f, 1.8f + i * 0.3f, knocksPlayers: false);
                    Color colour = ArtPalette.Candy[(i + (z > 0 ? 3 : 0)) % ArtPalette.Candy.Length];
                    Box("Sheet", sheet.transform, new Vector3(0f, -1f, 0f), new Vector3(3f, 2f, 0.04f), PlaceholderAssets.Material($"Laundry_{ColorUtility.ToHtmlStringRGB(colour)}", colour));
                }
            }

            // (The trail's cactus garden and sprinkler gust replaced the old flower beds and hose jet here.)
        }

        private static void BuildTree()
        {
            // A climbable giant tree in the middle, with branch platforms and the highest hoop above them.
            Vector3 c = TreeCenter;
            Material bark = PlaceholderAssets.Material("Bark", ArtPalette.TimberDark, 0.1f);
            // 3.2 m wide: matches the modelled bark (Art/Set/Tree), so climbing hands land on the trunk you see.
            Box("TreeTrunk", c + new Vector3(0f, 5f, 0f), new Vector3(3.2f, 10f, 3.2f), bark);
            for (int i = 0; i < 8; i++)
            {
                float y = 1.2f + i * 1.1f;
                Quaternion around = Quaternion.Euler(0f, i * 90f + 45f, 0f);
                PlaceholderShapes.Primitive(PrimitiveType.Sphere, "ClimbHold", _course, c + new Vector3(0f, y, 0f) + around * Vector3.forward * 1.85f,
                    new Vector3(0.45f, 0.3f, 0.3f), _palette.Climb, true);
            }

            Box("BranchLow", c + new Vector3(2.6f, 4f, 0f), new Vector3(3.5f, 0.5f, 1.4f), bark);
            Box("BranchMid", c + new Vector3(-2.4f, 6.5f, 0.6f), new Vector3(3.2f, 0.5f, 1.4f), bark);
            Box("BranchHigh", c + new Vector3(0.4f, 9.2f, -2.4f), new Vector3(1.4f, 0.5f, 3.2f), bark);

            // Bounce mushroom beside the low branch: launches players ~4.6 m (9.5 m/s), enough to grab the branch (top 4.25 m)
            // for the tree hoop above it. The balloon gets a gentler lift from it too.
            Vector3 mushroom = c + new Vector3(2.6f, 0f, -1.9f);
            Material stem = PlaceholderAssets.Material("MushroomStem", ArtPalette.Picket, 0.3f);
            Material cap = PlaceholderAssets.Material("MushroomCap", ArtPalette.Hazard, 0.45f);
            PlaceholderShapes.Primitive(PrimitiveType.Cylinder, "MushroomStem", _course, mushroom + new Vector3(0f, 0.2f, 0f), new Vector3(0.7f, 0.2f, 0.7f), stem, false);
            PlaceholderShapes.Primitive(PrimitiveType.Sphere, "MushroomCap", _course, mushroom + new Vector3(0f, 0.45f, 0f), new Vector3(1.8f, 0.5f, 1.8f), cap, false);
            for (int i = 0; i < 5; i++)
            {
                Quaternion around = Quaternion.Euler(0f, i * 72f, 0f);
                PlaceholderShapes.Primitive(PrimitiveType.Sphere, "MushroomSpot", _course, mushroom + new Vector3(0f, 0.62f, 0f) + around * new Vector3(0f, 0f, 0.5f),
                    new Vector3(0.28f, 0.1f, 0.28f), stem, false);
            }

            BouncePadOn(PlaceholderShapes.TriggerBox("MushroomPad", _course, mushroom + new Vector3(0f, 0.75f, 0f), new Vector3(1.6f, 0.4f, 1.6f)), 9.5f);

            Material leaves = PlaceholderAssets.Material("Leaves", ArtPalette.LawnDark, 0.1f);
            foreach (Vector3 offset in new[] { new Vector3(0f, 13f, 0f), new Vector3(3.5f, 11.5f, 1.5f), new Vector3(-3.5f, 12f, -1f), new Vector3(1f, 12f, 3.5f), new Vector3(-1f, 12.5f, -3.5f) })
            {
                PlaceholderShapes.Primitive(PrimitiveType.Sphere, "Canopy", _course, c + offset, new Vector3(6f, 4.5f, 6f), leaves, false);
            }

        }

        private static void BuildPlayground()
        {
            Vector3 c = PlaygroundCenter;

            // Merry-go-round you can ride (a spinning platform), with the playground hoop above it.
            MovingObstacle merry = Mover("MerryGoRound", c + new Vector3(4f, 0.25f, -2f));
            merry.ConfigureSpin(28f, knocksPlayers: false, carriesPlayers: true); // a ride: it carries you, never knocks you off
            GameObject disc = PlaceholderShapes.Primitive(PrimitiveType.Cylinder, "MerryDisc", merry.transform, Vector3.zero, new Vector3(6f, 0.2f, 6f),
                PlaceholderAssets.Material("MerryDisc", Color.white, 0.3f, GeneratedTextures.Stripes("T_MerryStripes", ArtPalette.CandyPink, ArtPalette.Picket, 4), Vector2.one), true);
            Object.DestroyImmediate(disc.GetComponent<Collider>());
            disc.AddComponent<MeshCollider>().convex = true; // a squashed cylinder's capsule collider would be a dome
            for (int i = 0; i < 4; i++)
            {
                Quaternion around = Quaternion.Euler(0f, i * 90f, 0f);
                // Decoration only (no collider): riders must be able to hop straight off in any direction.
                Box("MerryBar", merry.transform, around * new Vector3(0f, 0.8f, 2.3f), new Vector3(0.12f, 1.4f, 0.12f), _palette.Steel, false);
            }

            // Trampoline on the approach: bounce up beside the merry-go-round to slap the balloon through its hoop.
            Vector3 trampoline = c + new Vector3(0f, 0f, -6f);
            PlaceholderShapes.Primitive(PrimitiveType.Cylinder, "Trampoline", _course, trampoline + new Vector3(0f, 0.5f, 0f), new Vector3(3f, 0.05f, 3f), _palette.Hazard, false);
            BouncePadOn(PlaceholderShapes.TriggerBox("TrampolinePad", _course, trampoline + new Vector3(0f, 0.7f, 0f), new Vector3(3f, 0.4f, 3f)), 9f);
        }

        private static void BuildBbq()
        {
            Vector3 c = BbqCenter;

            // Grill with flames on a rhythm: the BBQ hoop sits right above it.
            Vector3 grill = c + new Vector3(0f, 0f, -6f);
            Box("Grill", grill + new Vector3(0f, 1.2f, 0f), new Vector3(3.4f, 0.9f, 2.2f), _palette.Charcoal);
            foreach (Vector2 corner in new[] { new Vector2(-1.4f, -0.9f), new Vector2(1.4f, -0.9f), new Vector2(-1.4f, 0.9f), new Vector2(1.4f, 0.9f) })
            {
                Box("GrillLeg", grill + new Vector3(corner.x, 0.4f, corner.y), new Vector3(0.2f, 0.8f, 0.2f), _palette.Steel);
            }

            GameObject flames = PlaceholderShapes.TriggerBox("GrillFlames", _course, grill + new Vector3(0f, 1.9f, 0f), new Vector3(3f, 0.5f, 1.8f));
            flames.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            var flameRenderer = flames.AddComponent<MeshRenderer>();
            flameRenderer.sharedMaterial = PlaceholderAssets.Material("Flame", ArtPalette.Flame);
            flames.AddComponent<PulsingHazard>().Configure(flameRenderer);

            // A box fan whose wind pushes the balloon west across the grill and past its hoop.
            Box("BoxFan", c + new Vector3(10f, 1.8f, -6f), new Vector3(0.6f, 3.4f, 3.4f), PlaceholderAssets.Material("Fan", Color.Lerp(ArtPalette.Steel, ArtPalette.Picket, 0.5f)));
            // Spinning blades on the fan's face (decoration, no colliders), so it reads as switched on.
            MovingObstacle blades = Mover("FanBlades", c + new Vector3(9.65f, 1.9f, -6f));
            blades.transform.rotation = Quaternion.Euler(0f, 0f, 90f); // spin axis (local up) along -X, the way it blows
            blades.ConfigureSpin(540f, knocksPlayers: false);
            Material bladeMaterial = PlaceholderAssets.Material("FanBlade", ArtPalette.Picket, 0.4f);
            Box("FanBladeA", blades.transform, Vector3.zero, new Vector3(2.9f, 0.06f, 0.45f), bladeMaterial, false);
            Box("FanBladeB", blades.transform, Vector3.zero, new Vector3(0.45f, 0.06f, 2.9f), bladeMaterial, false);

            // The wind itself, made visible: air streaks flowing from the fan across the grill and past its hoop.
            var gustSize = new Vector3(4f, 4f, 11f);
            GameObject gust = PlaceholderShapes.TriggerBox("FanGust", _course, c + new Vector3(4f, 2.5f, -6f), gustSize, Quaternion.Euler(0f, -90f, 0f));
            PlaceholderPrefabBuilder.AirStream(gust.transform, gustSize, 4f);
            gust
                .AddComponent<WindVolume>().Configure(2.2f);
        }


        // ------------------------------------------------------------------ hoops, spawns, items

        // Hoop sizing, measured from the player and the balloon rather than eyeballed.
        // Player (gorilla locomotion): eyes ~0.65 m above what you stand on; arms reach ~0.6 m above the eyes: standing reach
        // ~1.25 m; a strong jump adds ~1 m. Balloon: a slap launches it at the hand's speed (players measured 4.5-7.8 m/s), it
        // floats down at ~0.35 m/s. A hoop counts while the balloon (r 0.25) overlaps its trigger, i.e. inside the ring.
        private const float HoopRingRadius = 1f;            // 2 m across: ~2.5x a player, 4 balloons wide
        private const float HoopTriggerRadius = HoopRingRadius - 0.25f;
        private const float PinataAlong = 0.6f;             // how far along the last gap the finish pinata swings
        // Finish basket: a horizontal ring over the pad, scored only by dropping the balloon down through it. 2.9 m above
        // the pad is out of reach even with a jump (~2.25 m above what you stand on), so it takes a lofted slap that floats
        // down into it; the opening is 1.7 m across (3.4 balloons), a little smaller than a hoop.
        private const float BasketHeight = 2.9f;
        private const float BasketRadius = 0.85f;
        private const float BasketRimTube = 0.1f;
        private const float BasketOpening = BasketRadius - BasketRimTube / 2f - 0.18f; // balloon centre limit: it clears the rim

        /// <summary>
        /// The six hoops in trail order, each at its station (hoop 1 is the warm-up on open lawn after the start). Heights:
        /// 2.4 (lawn), 2.4 (1 m in front of the laundry line), 4.0 (over the pool, 2.4 above the diving board), 3.1 (2.75 above the merry-go-round),
        /// 3.25 (ring bottom 0.1 above the grill flames), 6.15 (1.9 above the low tree branch). Any order still counts.
        /// </summary>
        public static readonly Vector3[] TrailHoops =
        {
            new Vector3(0f, 2.4f, 3f),
            WashingLineCenter + new Vector3(0f, 2.4f, -8f),   // 1 m in front of the line: the sheets swing into its path
            PoolCenter + new Vector3(-5f, DiveBoardTop + 2.4f, 0f),
            PlaygroundCenter + new Vector3(4f, MerryTop + 2.75f, -2f),
            BbqCenter + new Vector3(0f, FlamesTop + 0.1f + HoopRingRadius, -6f),
            TreeCenter + new Vector3(2.8f, BranchLowTop + 1.9f, 0f),
        };

        /// <summary>
        /// Checkpoint k (k = 1..6, reached by clearing hoop k) stands on open lawn at the start of the approach to the next
        /// hoop (or the finish), on that station's working side, so nobody respawns in the pool or on the grill.
        /// </summary>
        public static readonly Vector3[] TrailCheckpoints =
        {
            WashingLineCenter + new Vector3(1f, 0f, -13f),   // toward the laundry hoop, from the south
            WashingLineCenter + new Vector3(3f, 0f, -3f),    // toward the diving board, from the west
            PlaygroundCenter + new Vector3(6f, 0f, -9f),     // toward the merry-go-round and trampoline
            BbqCenter + new Vector3(6f, 0f, -10f),           // toward the grill, from the south-east (the fan's wind blows across it)
            TreeCenter + new Vector3(6f, 0f, -6f),           // toward the mushroom pad and the low branch
            TreeCenter + new Vector3(-2f, 0f, 4f),           // toward the finish
        };

        /// <summary>Centre of the finish pad's top, at the end of the trail.</summary>
        public static readonly Vector3 TrailFinish = new Vector3(-8f, 0.4f, 68f);

        private static readonly Vector3 TrailStart = new Vector3(0f, 0f, -6.2f);

        private static void BuildHoops()
        {
            for (int i = 0; i < TrailHoops.Length; i++)
            {
                Vector3 previous = i == 0 ? TrailStart : TrailHoops[i - 1];
                Vector3 next = i + 1 < TrailHoops.Length ? TrailHoops[i + 1] : TrailFinish;
                Hoop(i, TrailHoops[i], YawToward(previous, next));

                Vector3 checkpoint = TrailCheckpoints[i];
                // The balloon waits 1 m ahead of the checkpoint, above head height, facing the next target.
                SpawnPoint(i + 1, checkpoint, YawToward(checkpoint, next), new Vector3(0f, 1.4f, 1.0f));
            }

            BuildFinishPad(TrailFinish, TrailHoops[TrailHoops.Length - 1]);
            FinishPinata(TrailHoops[TrailHoops.Length - 1], TrailFinish);
        }

        /// <summary>Ground point a fraction of the way along a gap, with the gap's direction and its right-hand side.</summary>
        private static Vector3 GapPoint(Vector3 from, Vector3 to, float t, out Vector3 along, out Vector3 across, out Quaternion facing)
        {
            along = Flat(to - from).normalized;
            across = Vector3.Cross(Vector3.up, along);
            facing = Quaternion.LookRotation(along, Vector3.up);
            return Flat(Vector3.Lerp(from, to, t));
        }

        /// <summary>A pinata swinging side to side across the way onto the finish pad: time the last slap.</summary>
        private static void FinishPinata(Vector3 from, Vector3 to)
        {
            Vector3 c = GapPoint(from, to, PinataAlong, out _, out Vector3 across, out Quaternion facing);
            const float beam = 5.8f;
            // Posts 3.2 m out: the pinata (0.55 m half-size on a 3.4 m rope, 45 degree swing) reaches 2.95 m, clear of them.
            foreach (float side in new[] { -3.2f, 3.2f })
            {
                Box("PinataPost", c + across * side + new Vector3(0f, beam / 2f, 0f), new Vector3(0.25f, beam, 0.25f), _palette.Wood);
            }

            Box("PinataBeam", c + new Vector3(0f, beam, 0f), new Vector3(6.7f, 0.25f, 0.25f), _palette.Wood).transform.rotation = facing;
            MovingObstacle pinata = Mover("FinishPinata", c + new Vector3(0f, beam - 0.1f, 0f));
            pinata.transform.rotation = facing;
            pinata.ConfigureSwing(Vector3.forward, 45f, 3.2f); // about the path: it sweeps across it
            Box("PinataRope", pinata.transform, new Vector3(0f, -1.5f, 0f), new Vector3(0.05f, 3f, 0.05f), _palette.Wood, false);
            Box("FinishPinataBody", pinata.transform, new Vector3(0f, -3.4f, 0f), Vector3.one * 1.1f, PlaceholderAssets.Material("Pinata", ArtPalette.CandyPink));
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        private static float YawToward(Vector3 from, Vector3 to)
        {
            Vector3 d = Flat(to - from);
            return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// A low deck at the end of the trail with the finish basket over it: a horizontal ring with a solid rim, a backboard
        /// on the far side (bank shots welcome) and a hanging net. Once every hoop is cleared, the balloon has to drop down
        /// through the ring (FinishLine.Accepts); flying in from the side doesn't count. The rim is red while locked.
        /// </summary>
        private static void BuildFinishPad(Vector3 top, Vector3 approachFrom)
        {
            Box("FinishPad", top + new Vector3(0f, -0.2f, 0f), new Vector3(8f, 0.4f, 6f), _palette.Deck);
            Box("FinishMat", top + new Vector3(0f, 0.02f, 0f), new Vector3(7f, 0.04f, 5f), _palette.Checker, false);

            Vector3 along = Flat(top - approachFrom).normalized; // the way players face as they arrive
            Quaternion facing = Quaternion.LookRotation(along, Vector3.up);
            Vector3 ring = top + Vector3.up * BasketHeight;
            var indicators = new List<Renderer>(); // the rim shows locked/unlocked (no beacon on the pad: it read as the target)

            // Rim: a closed ring of short boxes with colliders, so a balloon coming in from the side bounces off it.
            Material rimMaterial = PlaceholderAssets.Emissive("FinishBeacon", ArtPalette.Hazard, 1.3f);
            const int rimPieces = 24;
            float pieceLength = 2f * Mathf.PI * BasketRadius / rimPieces * 1.08f;
            for (int k = 0; k < rimPieces; k++)
            {
                float angle = k * 360f / rimPieces;
                Quaternion around = Quaternion.Euler(0f, angle, 0f);
                GameObject piece = Box($"FinishRim{k}", ring + around * Vector3.forward * BasketRadius,
                    new Vector3(pieceLength, BasketRimTube, BasketRimTube), rimMaterial);
                piece.transform.rotation = around;
                indicators.Add(piece.GetComponent<Renderer>());
            }

            // Net: strands hanging from the rim, gathering toward the bottom (visual only).
            Material netMaterial = PlaceholderAssets.Material("FinishNet", ArtPalette.Picket);
            const int strands = 12;
            const float netDepth = 0.75f, netBottomRadius = 0.5f;
            for (int k = 0; k < strands; k++)
            {
                Quaternion around = Quaternion.Euler(0f, k * 360f / strands, 0f);
                Vector3 upper = ring + around * Vector3.forward * (BasketRadius - 0.03f);
                Vector3 lower = ring + around * Vector3.forward * netBottomRadius + Vector3.down * netDepth;
                GameObject strand = Box($"FinishNet{k}", (upper + lower) / 2f, new Vector3(0.025f, (upper - lower).magnitude, 0.025f), netMaterial, false);
                strand.transform.rotation = Quaternion.FromToRotation(Vector3.up, upper - lower);
            }

            // Backboard behind the ring on the far side, a bracket to the rim, and a post down to the pad.
            const float boardGap = 0.25f, boardWidth = 2.2f, boardHeight = 1.4f;
            Vector3 boardCenter = ring + along * (BasketRadius + boardGap) + Vector3.up * (boardHeight / 2f - 0.2f);
            Box("FinishBackboard", boardCenter, new Vector3(boardWidth, boardHeight, 0.08f), _palette.Checker).transform.rotation = facing;
            Box("FinishBracket", ring + along * (BasketRadius + boardGap / 2f), new Vector3(0.1f, 0.1f, boardGap + 0.1f), _palette.Wood).transform.rotation = facing;
            Vector3 postBase = top + along * (BasketRadius + boardGap + 0.2f);
            float postHeight = BasketHeight + boardHeight - 0.2f;
            Box("FinishPost", postBase + Vector3.up * (postHeight / 2f), new Vector3(0.2f, postHeight, 0.2f), _palette.Wood);

            // Scoring trigger centred on the ring; FinishLine decides whether the balloon is dropping through it.
            GameObject finish = PlaceholderShapes.TriggerBox("FinishLine", _course, Vector3.zero,
                new Vector3(BasketOpening * 2f, 0.4f, BasketOpening * 2f));
            finish.transform.position = ring; // on the rim plane: FinishLine measures heights from it
            finish.AddComponent<FinishLine>().Configure(indicators.ToArray(), BasketOpening);

            TextMesh sign = PlaceholderShapes.Text("FinishSign", _course, Vector3.zero, 0.08f, "FINISH", ArtPalette.Accent);
            sign.transform.SetPositionAndRotation(boardCenter + Vector3.up * (boardHeight / 2f + 0.45f) - along * 0.05f, facing);
            TextMesh hint = PlaceholderShapes.Text("FinishHint", _course, Vector3.zero, 0.035f, "drop it in from above!", ArtPalette.Picket);
            hint.transform.SetPositionAndRotation(boardCenter + Vector3.up * (boardHeight / 2f + 0.12f) - along * 0.05f, facing);
        }

        private static void Hoop(int index, Vector3 center, float yaw)
        {
            var root = new GameObject($"Hoop{index + 1}");
            root.transform.SetParent(_course, false);
            root.transform.SetPositionAndRotation(center, Quaternion.Euler(0f, yaw, 0f));
            var trigger = root.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = HoopTriggerRadius;
            root.layer = GameLayers.IgnoreRaycast;

            var spinner = new GameObject("Ring").transform;
            spinner.SetParent(root.transform, false);
            Material ring = PlaceholderAssets.Emissive("HoopRing", ArtPalette.Accent, 1.2f);
            var renderers = new List<Renderer>();
            const int beads = 16;
            for (int b = 0; b < beads; b++)
            {
                float angle = b * Mathf.PI * 2f / beads;
                var local = new Vector3(Mathf.Cos(angle) * HoopRingRadius, Mathf.Sin(angle) * HoopRingRadius, 0f);
                GameObject bead = PlaceholderShapes.Primitive(PrimitiveType.Sphere, "Bead", spinner, local, Vector3.one * 0.26f, ring, false);
                renderers.Add(bead.GetComponent<Renderer>());
            }

            Billboard(PlaceholderShapes.Text($"Hoop{index + 1}Label", root.transform, new Vector3(0f, HoopRingRadius + 0.5f, 0f), 0.04f, $"HOOP {index + 1}", ArtPalette.Accent));
            var chimeHost = new GameObject("ClearChime");
            chimeHost.transform.SetParent(_course, false); // not under the hoop, which hides its children when cleared
            chimeHost.transform.position = center;
            AudioSource chime = PlaceholderPrefabBuilder.Sound(chimeHost, GeneratedAudio.HoopChime("Hoop_Chime"), false, 40f, 1f);
            root.AddComponent<HoopCheckpoint>().Configure(index, renderers.ToArray(), spinner, chime, PlaceholderAssets.Material("Confetti", Color.white, 0.3f));
        }

        private static void BuildItemStashes()
        {
            // Start pile: one of everything. Then two items beside every checkpoint: a hoop helper (bubble wand = updraft
            // for the high hoops: pool and tree; leaf blower = gust toward the nearest hoop for the rest) and one other.
            for (int i = 0; i < 10; i++)
            {
                ItemSpawn(i, new Vector3(-5f, 0.9f, -9.6f + i * 0.9f));
            }

            int[] helpers = { 9, 8, 9, 9, 8, 9 };
            for (int k = 0; k < TrailCheckpoints.Length; k++)
            {
                Vector3 cp = TrailCheckpoints[k];
                Vector3 next = k + 1 < TrailHoops.Length ? TrailHoops[k + 1] : TrailFinish;
                Vector3 across = Vector3.Cross(Vector3.up, Flat(next - cp).normalized);
                ItemSpawn(helpers[k], cp + across * 1.8f + Vector3.up * 0.6f);
                ItemSpawn(k % 8, cp - across * 1.8f + Vector3.up * 0.6f);
            }
        }

        // ------------------------------------------------------------------ helpers

        private const string CactusModelPath = "Assets/GameJam/Art/Set/Cactus.fbx";

        /// <summary>
        /// A potted cactus that pops the balloon (<see cref="Sharp"/>): the model from Art/Set/Cactus.fbx (a spiny saguaro
        /// with two arms in a terracotta pot, built by tools/blender/scripts/build_set_pieces.py) wrapped in colliders that
        /// follow it: the pot, the body out to its spine tips and each arm. Without the FBX it falls back to the old
        /// placeholder (a green cylinder with a red slab), which the user found unreadable as spikes.
        /// </summary>
        private static void CactusPot(Vector3 baseTop, float yaw = 0f, float scale = 1f)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(CactusModelPath);
            if (model == null)
            {
                GameObject pot = PlaceholderShapes.Primitive(PrimitiveType.Cylinder, "CactusPot", _course, baseTop + Vector3.up * 0.4f, new Vector3(0.6f, 0.4f, 0.6f), _palette.Cactus, true);
                pot.AddComponent<Sharp>().Configure("Popped on a cactus!");
                PlaceholderShapes.Primitive(PrimitiveType.Cube, "CactusSpikes", pot.transform, new Vector3(0f, 1.05f, 0f), new Vector3(1.2f, 0.15f, 1.2f), _palette.Hazard, false);
                return;
            }

            var root = new GameObject("CactusPot");
            root.transform.SetParent(_course, false);
            root.transform.SetPositionAndRotation(baseTop, Quaternion.Euler(0f, yaw, 0f));
            root.transform.localScale = Vector3.one * scale;
            root.AddComponent<Sharp>().Configure("Popped on a cactus!");

            var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true))
            {
                Object.DestroyImmediate(collider);
            }

            // Colliders in the model's own measurements (metres, origin at the pot's base): see build_cactus.
            CactusCollider(root.transform, "PotCollider", new Vector3(0f, 0.25f, 0f), 0.38f, 0.5f);
            CactusCollider(root.transform, "BodyCollider", new Vector3(0f, 1.08f, 0f), 0.4f, 1.24f);
            CactusCollider(root.transform, "ArmColliderWest", new Vector3(-0.5f, 1.17f, 0f), 0.22f, 0.66f);
            CactusCollider(root.transform, "ArmColliderEast", new Vector3(0.47f, 0.96f, 0.04f), 0.21f, 0.6f);
        }

        private static void CactusCollider(Transform cactus, string name, Vector3 centre, float radius, float height)
        {
            var holder = new GameObject(name);
            holder.transform.SetParent(cactus, false);
            holder.transform.localPosition = centre;
            var capsule = holder.AddComponent<CapsuleCollider>();
            capsule.radius = radius;
            capsule.height = Mathf.Max(height, radius * 2f);
        }

        /// <summary>
        /// Makes a floating label (text that isn't on a sign or a wall) turn to face the player (<see cref="FaceCamera"/>),
        /// so it never reads edge-on or mirrored. Returns the text for chaining.
        /// </summary>
        private static TextMesh Billboard(TextMesh text)
        {
            text.gameObject.AddComponent<FaceCamera>();
            return text;
        }

        private static void SpawnPoint(int checkpoint, Vector3 position, float yaw, Vector3 balloonOffset)
        {
            var point = new GameObject($"SpawnPoint_Checkpoint{checkpoint}");
            point.transform.SetParent(_course, false);
            point.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            point.AddComponent<SpawnPoint>().Configure(checkpoint, balloonOffset);
        }

        private static void ItemSpawn(int itemIndex, Vector3 position)
        {
            var point = new GameObject($"ItemSpawn_{itemIndex}");
            point.transform.SetParent(_course, false);
            point.transform.position = position;
            point.AddComponent<ItemSpawnPoint>().Configure(itemIndex);
            ItemSpotRing(position);
        }

        /// <summary>
        /// A slowly turning golden dashed ring on the ground under an item's spot, so pickups read at a glance and players
        /// know where a used-up item comes back. Visual only: no collider, no shadow, Ignore Raycast.
        /// </summary>
        private static void ItemSpotRing(Vector3 spot)
        {
            Physics.SyncTransforms();
            float groundY = Physics.Raycast(spot + Vector3.up * 0.2f, Vector3.down, out RaycastHit hit, 4f, GameLayers.DefaultMask, QueryTriggerInteraction.Ignore)
                ? hit.point.y
                : 0f;

            Material ring = PlaceholderAssets.TransparentUnlit("ItemSpotRing", GeneratedTextures.DashedRing("T_ItemSpotRing"));
            Color gold = ArtPalette.Accent;
            gold.a = 0.9f;
            ring.color = gold;
            EditorUtility.SetDirty(ring);

            var pivot = new GameObject("ItemSpotRing").transform;
            pivot.SetParent(_course, false);
            pivot.position = new Vector3(spot.x, groundY + 0.02f, spot.z);
            GameObject quad = PlaceholderShapes.Primitive(PrimitiveType.Quad, "Ring", pivot, Vector3.zero, Vector3.one * 0.85f, ring, false);
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var quadRenderer = quad.GetComponent<Renderer>();
            quadRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            quadRenderer.receiveShadows = false;
            PlaceholderShapes.SetLayerRecursively(pivot.gameObject, GameLayers.IgnoreRaycast);
            pivot.gameObject.AddComponent<IdleMotion>().Configure(0f, 0.5f, 0f, 35f, 0f);
        }

        private static MovingObstacle Mover(string name, Vector3 position)
        {
            var mover = new GameObject(name);
            mover.transform.SetParent(_course, false);
            mover.transform.position = position;
            mover.AddComponent<Rigidbody>().isKinematic = true;
            return mover.AddComponent<MovingObstacle>();
        }

        private static GameObject Box(string name, Vector3 worldPosition, Vector3 size, Material material, bool hasCollider = true)
        {
            GameObject box = PlaceholderShapes.Box(name, _course, Vector3.zero, size, material, hasCollider);
            box.transform.position = worldPosition;
            return box;
        }

        private static GameObject Box(string name, Transform parent, Vector3 localPosition, Vector3 size, Material material, bool hasCollider = true) =>
            PlaceholderShapes.Box(name, parent, localPosition, size, material, hasCollider);

        private static Vector3[] Circle(Vector3 center, float radius, int points)
        {
            var result = new Vector3[points];
            for (int i = 0; i < points; i++)
            {
                float angle = i * Mathf.PI * 2f / points;
                result[i] = center + new Vector3(Mathf.Sin(angle) * radius, 0f, Mathf.Cos(angle) * radius);
            }

            return result;
        }
    }
}
