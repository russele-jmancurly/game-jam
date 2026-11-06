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
    public static class BackyardCourseBuilder
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
            new Vector3(-20f, 0f, -10f), new Vector3(-20f, 0f, 8f), new Vector3(-20f, 0f, 26f), new Vector3(-18f, 0f, 44f),
            new Vector3(-20f, 0f, 62f), new Vector3(-20f, 0f, 78f), new Vector3(12f, 0f, -10f), new Vector3(10f, 0f, 8f),
            new Vector3(18f, 0f, 38f), new Vector3(16f, 0f, 52f), new Vector3(0f, 0f, 74f), new Vector3(-10f, 0f, 34f),
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
            Texture2D lawn = GeneratedTextures.Noise("T_Lawn", ArtPalette.Lawn, ArtPalette.LawnDark, 3f);
            Texture2D deck = GeneratedTextures.Stripes("T_TimberDeck", ArtPalette.Timber, Color.Lerp(ArtPalette.Timber, ArtPalette.TimberDark, 0.25f), 3);
            Texture2D water = GeneratedTextures.Noise("T_PoolWater", poolWater, Color.Lerp(poolWater, ArtPalette.PoolTile, 0.25f), 4f);
            Texture2D checker = GeneratedTextures.Checker("T_FinishChecker", ArtPalette.Picket, ArtPalette.Charcoal, 8);
            Texture2D sand = GeneratedTextures.Noise("T_Sand", ArtPalette.HouseWall, Color.Lerp(ArtPalette.HouseWall, ArtPalette.Timber, 0.35f), 5f);

            return new Palette
            {
                Lawn = PlaceholderAssets.Material("Lawn", Color.white, 0.1f, lawn, new Vector2(13f, 25f)),
                Deck = PlaceholderAssets.Material("Platform", Color.white, PlaceholderAssets.MatteSmoothness, deck, new Vector2(2f, 2f)),
                Wood = PlaceholderAssets.Material("Wood", ArtPalette.TimberDark),
                Picket = PlaceholderAssets.Material("Picket", ArtPalette.Picket),
                Climb = PlaceholderAssets.Material("Climbable", ArtPalette.Climbable, 0.3f),
                Hazard = PlaceholderAssets.Material("Hazard", ArtPalette.Hazard, 0.35f),
                Cactus = PlaceholderAssets.Material("Cactus", ArtPalette.LawnDark),
                Water = PlaceholderAssets.Material("Water", Color.white, 0.55f, water, new Vector2(3f, 3f)),
                Gold = PlaceholderAssets.Material("Gate", ArtPalette.Accent, 0.4f),
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
            GameObject cap = Box("PicketCap", basePosition + Vector3.up * height, new Vector3(width * 0.71f, width * 0.71f, 0.4f), _palette.Picket, false);
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
                PlaceholderShapes.Text("HostName", _course, spot + new Vector3(0f, host.Height + 0.65f, 0f), 0.022f, $"{host.DisplayName}  ·  HOST", ArtPalette.Accent);
            }

            CharacterDefinition commentator = FindCharacter("Squoof");
            if (commentator != null)
            {
                // Booth: centre screen + (0, 2.4, 0.6), 1.6 m tall, so its roof is 3.2 m above the screen centre.
                Vector3 roof = screen + new Vector3(0f, 3.2f, 0.6f);
                float height = commentator.Height * 1.5f; // readable from across the yard
                CrewFigure("Commentator_Squoof", commentator, roof, 180f, height, fallback, false);
                PlaceholderShapes.Text("CommentatorName", _course, roof + new Vector3(0f, height + 0.5f, 0f), 0.05f, $"{commentator.DisplayName}  ·  COMMENTATOR", ArtPalette.Accent);
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
            Box("LineA", rotary.transform, Vector3.zero, new Vector3(7f, 0.1f, 0.1f), _palette.Picket);
            Box("LineB", rotary.transform, Vector3.zero, new Vector3(0.1f, 0.1f, 7f), _palette.Picket);

            // The washing line the hoop hangs on: its flapping sheets swing across the ring and swat the balloon.
            foreach (float z in new[] { -7f })
            {
                foreach (float x in new[] { -8f, 8f })
                {
                    Box("LinePost", c + new Vector3(x, 2.2f, z), new Vector3(0.25f, 4.4f, 0.25f), _palette.Wood);
                }

                Box("WashingLine", c + new Vector3(0f, 4.3f, z), new Vector3(16f, 0.05f, 0.05f), _palette.Picket, false);
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

            BuildFinishPad(TrailFinish);
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

        /// <summary>A low deck at the end of the trail: land the balloon on it once every hoop is cleared.</summary>
        private static void BuildFinishPad(Vector3 top)
        {
            Box("FinishPad", top + new Vector3(0f, -0.2f, 0f), new Vector3(8f, 0.4f, 6f), _palette.Deck);
            Box("FinishMat", top + new Vector3(0f, 0.02f, 0f), new Vector3(7f, 0.04f, 5f), _palette.Checker, false);
            GameObject beacon = PlaceholderShapes.Primitive(PrimitiveType.Cylinder, "FinishBeacon", _course, top + new Vector3(0f, 0.38f, 0f), new Vector3(1.4f, 0.35f, 1.4f),
                PlaceholderAssets.Emissive("FinishBeacon", ArtPalette.Hazard, 1.3f), false);
            // Locked (red beacon) until every hoop is cleared.
            GameObject finish = PlaceholderShapes.TriggerBox("FinishLine", _course, top + new Vector3(0f, 1.6f, 0f), new Vector3(7f, 3f, 5f));
            finish.AddComponent<FinishLine>().Configure(beacon.GetComponent<Renderer>());
            PlaceholderShapes.Text("FinishSign", _course, top + new Vector3(0f, 4.2f, 3.2f), 0.08f, "FINISH", ArtPalette.Accent);
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

            PlaceholderShapes.Text($"Hoop{index + 1}Label", root.transform, new Vector3(0f, HoopRingRadius + 0.5f, 0f), 0.04f, $"HOOP {index + 1}", ArtPalette.Accent);
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

        private static void CactusPot(Vector3 baseTop)
        {
            GameObject pot = PlaceholderShapes.Primitive(PrimitiveType.Cylinder, "CactusPot", _course, baseTop + Vector3.up * 0.4f, new Vector3(0.6f, 0.4f, 0.6f), _palette.Cactus, true);
            pot.AddComponent<Sharp>().Configure("Popped on a cactus!");
            PlaceholderShapes.Primitive(PrimitiveType.Cube, "CactusSpikes", pot.transform, new Vector3(0f, 1.05f, 0f), new Vector3(1.2f, 0.15f, 1.2f), _palette.Hazard, false);
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
