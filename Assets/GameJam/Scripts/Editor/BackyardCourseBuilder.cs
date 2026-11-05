using System.Collections.Generic;
using GameJam.Core;
using GameJam.Gameplay;
using UnityEngine;

namespace GameJam.Editor
{
    /// <summary>
    /// The Giant Backyard as an open yard (GDD: Levels): walkable lawn the balloon must never touch, ringed by a climbable
    /// giant picket fence and the house. Five zones to roam between (start stage, paddling pool, washing line, playground,
    /// BBQ picnic, plus the big tree in the middle), six hoops to clear in any order, a finish pad on the porch, and things
    /// that move: local rhythm hazards in each zone plus passing objects that cross the whole yard (robot lawnmower, RC car,
    /// toy train, paper plane, bowling ball). Everything moves on the shared CourseClock, so it costs no network traffic.
    /// </summary>
    public static class BackyardCourseBuilder
    {
        // The yard: lawn top at y = 0, x -48..48, z -24..104.
        public const float HalfWidth = 48f;
        public const float SouthEdge = -24f;
        public const float NorthEdge = 104f;

        public static readonly Vector3 PoolCenter = new Vector3(24f, 0f, 28f);
        public const float PoolRadius = 9f;
        public static readonly Vector3 WashingLineCenter = new Vector3(-24f, 0f, 28f);
        public static readonly Vector3 TreeCenter = new Vector3(0f, 0f, 40f);
        public static readonly Vector3 PlaygroundCenter = new Vector3(-24f, 0f, 72f);
        public static readonly Vector3 BbqCenter = new Vector3(24f, 0f, 72f);
        public static readonly Vector3 PorchCenter = new Vector3(0f, 0f, 98f);
        public const float PicnicTableTop = 3.25f;

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

        /// <summary>Open lawn spots the roaming mischief maker wanders between.</summary>
        public static readonly Vector3[] RoamerWanderPoints =
        {
            new Vector3(-10f, 0f, 10f), new Vector3(10f, 0f, 8f), new Vector3(-34f, 0f, 6f), new Vector3(36f, 0f, 6f),
            new Vector3(-12f, 0f, 46f), new Vector3(8f, 0f, 54f), new Vector3(-36f, 0f, 50f), new Vector3(36f, 0f, 48f),
            new Vector3(-4f, 0f, 64f), new Vector3(6f, 0f, 78f), new Vector3(-30f, 0f, 90f), new Vector3(36f, 0f, 88f),
        };

        /// <summary>Set pieces the roamer steers around: x, z, radius.</summary>
        public static readonly Vector3[] RoamerAvoidCircles =
        {
            new Vector3(24f, 28f, 10.5f), new Vector3(0f, 40f, 2.5f), new Vector3(24f, 77f, 6f), new Vector3(24f, 66f, 2.5f),
            new Vector3(-24f, 28f, 4f), new Vector3(-38f, 16f, 3f), new Vector3(-38f, 28f, 3f), new Vector3(-38f, 40f, 3f),
            new Vector3(-20f, 70f, 3.5f), new Vector3(-33f, 68f, 4.5f), new Vector3(-28f, 80f, 4.5f), new Vector3(-14f, 80f, 3.5f),
            new Vector3(-34f, 81f, 4f), new Vector3(-12f, 66f, 2.5f), new Vector3(0f, -6f, 7f), new Vector3(6f, 22.5f, 0.8f),
            new Vector3(-6f, 22.5f, 0.8f), new Vector3(34f, 66f, 2.5f),
        };

        /// <summary>Where the roamer may walk: min x, min z, max x, max z (inside the fence, south of the porch).</summary>
        public static readonly Vector4 RoamerBounds = new Vector4(-44f, -20f, 44f, 86f);

        public static readonly Vector3 RoamerSpawn = new Vector3(0f, 0f, 60f);

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
            BuildPassingObjects();
            BuildHoops();
            BuildItemStashes();
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
                Lawn = PlaceholderAssets.Material("Lawn", Color.white, 0.1f, lawn, new Vector2(24f, 32f)),
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
            // The house closes the north side; its wall is climbable, its porch holds the finish pad.
            Box("HouseBody", new Vector3(0f, 12f, NorthEdge + 10f), new Vector3(32f, 24f, 20f), _palette.Wall);
            Material roof = PlaceholderAssets.Material("HouseRoof", ArtPalette.Roof);
            Box("RoofLeft", new Vector3(-8.5f, 29f, NorthEdge + 10f), new Vector3(20f, 2f, 23f), roof, false).transform.rotation = Quaternion.Euler(0f, 0f, 32f);
            Box("RoofRight", new Vector3(8.5f, 29f, NorthEdge + 10f), new Vector3(20f, 2f, 23f), roof, false).transform.rotation = Quaternion.Euler(0f, 0f, -32f);

            Vector3 porch = PorchCenter;
            Box("Porch", porch + new Vector3(0f, 0.6f, 0f), new Vector3(16f, 1.2f, 9f), _palette.Deck);
            Box("PorchStep", porch + new Vector3(0f, 0.3f, -5.4f), new Vector3(8f, 0.6f, 1.8f), _palette.Deck);
            foreach (float x in new[] { -7.3f, 7.3f })
            {
                Box("PorchPost", porch + new Vector3(x, 3.6f, -4f), new Vector3(0.5f, 4.8f, 0.5f), _palette.Picket, false);
            }

            Box("PorchRoof", porch + new Vector3(0f, 6.1f, -0.5f), new Vector3(17f, 0.4f, 10f), roof, false);

            // Finish pad: locked (red beacon) until every hoop is cleared.
            Box("FinishMat", porch + new Vector3(0f, 1.22f, 0.5f), new Vector3(7f, 0.04f, 5f), _palette.Checker, false);
            GameObject beacon = PlaceholderShapes.Primitive(PrimitiveType.Cylinder, "FinishBeacon", _course, porch + new Vector3(0f, 1.6f, 0.5f), new Vector3(1.4f, 0.35f, 1.4f),
                PlaceholderAssets.Emissive("FinishBeacon", ArtPalette.Hazard, 1.3f), false);
            GameObject finish = PlaceholderShapes.TriggerBox("FinishLine", _course, porch + new Vector3(0f, 2.8f, 0.5f), new Vector3(7f, 3f, 5f));
            finish.AddComponent<FinishLine>().Configure(beacon.GetComponent<Renderer>());
            PlaceholderShapes.Text("FinishSign", _course, porch + new Vector3(0f, 5.4f, -5.1f), 0.08f, "FINISH", ArtPalette.Accent);

            // A pinata swings across the porch steps: time the final slap between swings.
            MovingObstacle pinata = Mover("PorchPinata", porch + new Vector3(0f, 5.8f, -6.5f));
            pinata.ConfigureSwing(Vector3.forward, 50f, 3.2f);
            Box("Rope", pinata.transform, new Vector3(0f, -1.5f, 0f), new Vector3(0.05f, 3f, 0.05f), _palette.Wood, false);
            Box("Pinata", pinata.transform, new Vector3(0f, -3.4f, 0f), Vector3.one * 1.1f, PlaceholderAssets.Material("Pinata", ArtPalette.CandyPink));
        }

        // ------------------------------------------------------------------ zones

        private static void BuildStartStage()
        {
            var stage = new Vector3(0f, 0f, -6f);
            Box("StartStage", stage + new Vector3(0f, 0.2f, 0f), new Vector3(12f, 0.4f, 9f), _palette.Deck);
            SpawnPoint(0, new Vector3(0f, 0.4f, -8f), 0f, new Vector3(0f, 2.2f, 2.8f));

            Box("ButtonPedestal", stage + new Vector3(4f, 0.65f, 2.5f), new Vector3(0.5f, 0.5f, 0.5f), _palette.Wood);
            GameObject button = PlaceholderShapes.Primitive(PrimitiveType.Cube, "StartButton", _course, stage + new Vector3(4f, 1.02f, 2.5f), new Vector3(0.4f, 0.25f, 0.4f), _palette.Hazard, true);
            button.GetComponent<Collider>().isTrigger = true;
            button.AddComponent<StartButton>();
            button.layer = GameLayers.IgnoreRaycast;

            // Commentator: a big board on stilts in the middle of the yard, readable from everywhere south of the tree.
            Material board = PlaceholderAssets.Material("Backboard", ArtPalette.TextDark, 0.3f);
            var screen = new Vector3(0f, 9f, 22f);
            Box("HeadlineBoard", screen + new Vector3(0f, 0f, 0.35f), new Vector3(13f, 2.4f, 0.3f), board, false);
            Box("HeadlineTrim", screen + new Vector3(0f, 1.25f, 0.35f), new Vector3(13.2f, 0.2f, 0.4f), _palette.Gold, false);
            foreach (float x in new[] { -6f, 6f })
            {
                Box("HeadlineStilt", new Vector3(x, 4f, 22.5f), new Vector3(0.4f, 8f, 0.4f), _palette.Wood);
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

            BuildShowCrew(stage, screen);
        }

        /// <summary>
        /// The show's crew as map entities (players are all the porcupine): Jman, the host, greets the crew on the start
        /// stage; Squoof commentates from the top of the booth above the headline board. Static figures, purely visual.
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
            figure.ShowOnLoad(character, height);
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
            PlaceholderShapes.TriggerBox("DivingBoardSpring", _course, board + new Vector3(3.4f, 1.9f, 0f), new Vector3(1.4f, 0.6f, 1.4f))
                .AddComponent<BouncePad>().Configure(10f);

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

            // Two washing lines of flapping laundry (view blockers that swat the balloon).
            foreach (float z in new[] { -7f, 7f })
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

            // Flower beds with cactus pots along the west fence, and hose jets that blow the balloon toward the middle.
            for (int i = 0; i < 3; i++)
            {
                var bed = new Vector3(c.x - 14f, 0f, c.z - 12f + i * 12f);
                Box("FlowerBedFrame", bed + new Vector3(0f, 0.3f, 0f), new Vector3(5f, 0.6f, 8f), _palette.Wood);
                Box("FlowerBedSoil", bed + new Vector3(0f, 0.62f, 0f), new Vector3(4.4f, 0.06f, 7.4f), _palette.Soil).AddComponent<BalloonGround>().Configure("Lost in the flower bed!");
                CactusPot(bed + new Vector3(0f, 0.65f, -2f));
                CactusPot(bed + new Vector3(0.6f, 0.65f, 2.2f));
            }

            Box("HoseReel", c + new Vector3(-12f, 0.8f, 12f), new Vector3(1.2f, 1.6f, 1.2f), PlaceholderAssets.Material("HoseGreen", ArtPalette.LawnDark, 0.4f));
            PlaceholderShapes.TriggerBox("HoseJet", _course, c + new Vector3(-6f, 3f, 12f), new Vector3(12f, 4f, 3f), Quaternion.Euler(0f, 90f, 0f))
                .AddComponent<WindVolume>().Configure(3f);
        }

        private static void BuildTree()
        {
            // A climbable giant tree in the middle, with branch platforms and the highest hoop above them.
            Vector3 c = TreeCenter;
            Material bark = PlaceholderAssets.Material("Bark", ArtPalette.TimberDark, 0.1f);
            Box("TreeTrunk", c + new Vector3(0f, 5f, 0f), new Vector3(2.4f, 10f, 2.4f), bark);
            for (int i = 0; i < 8; i++)
            {
                float y = 1.2f + i * 1.1f;
                Quaternion around = Quaternion.Euler(0f, i * 90f + 45f, 0f);
                PlaceholderShapes.Primitive(PrimitiveType.Sphere, "ClimbHold", _course, c + new Vector3(0f, y, 0f) + around * Vector3.forward * 1.25f,
                    new Vector3(0.45f, 0.3f, 0.3f), _palette.Climb, true);
            }

            Box("BranchLow", c + new Vector3(2.6f, 4f, 0f), new Vector3(3.5f, 0.5f, 1.4f), bark);
            Box("BranchMid", c + new Vector3(-2.4f, 6.5f, 0.6f), new Vector3(3.2f, 0.5f, 1.4f), bark);
            Box("BranchHigh", c + new Vector3(0.4f, 9.2f, -2.4f), new Vector3(1.4f, 0.5f, 3.2f), bark);

            Material leaves = PlaceholderAssets.Material("Leaves", ArtPalette.LawnDark, 0.1f);
            foreach (Vector3 offset in new[] { new Vector3(0f, 13f, 0f), new Vector3(3.5f, 11.5f, 1.5f), new Vector3(-3.5f, 12f, -1f), new Vector3(1f, 12f, 3.5f), new Vector3(-1f, 12.5f, -3.5f) })
            {
                PlaceholderShapes.Primitive(PrimitiveType.Sphere, "Canopy", _course, c + offset, new Vector3(6f, 4.5f, 6f), leaves, false);
            }

            // Toy train track around the tree.
            Material track = PlaceholderAssets.Material("Track", ArtPalette.Steel, 0.5f);
            Vector3[] ring = Circle(c + Vector3.up * 0.03f, 11f, 28);
            for (int i = 0; i < ring.Length; i++)
            {
                Vector3 a = ring[i];
                Vector3 b = ring[(i + 1) % ring.Length];
                Box("TrackPiece", (a + b) / 2f, new Vector3(1.6f, 0.06f, Vector3.Distance(a, b)), track, false).transform.rotation = Quaternion.LookRotation(b - a);
            }
        }

        private static void BuildPlayground()
        {
            Vector3 c = PlaygroundCenter;

            // Swing set: two seats on a swing rhythm that knock players over.
            Vector3 swings = c + new Vector3(-9f, 0f, -4f);
            foreach (float x in new[] { -3.5f, 3.5f })
            {
                Box("SwingFrame", swings + new Vector3(x, 2.5f, 0f), new Vector3(0.3f, 5f, 0.3f), _palette.Steel);
            }

            Box("SwingBar", swings + new Vector3(0f, 5f, 0f), new Vector3(7.4f, 0.3f, 0.3f), _palette.Steel);
            for (int i = 0; i < 2; i++)
            {
                MovingObstacle seat = Mover("SwingSeat", swings + new Vector3(-1.6f + i * 3.2f, 5f, 0f));
                seat.ConfigureSwing(Vector3.right, 45f, 2.6f + i * 0.4f);
                Box("Chain", seat.transform, new Vector3(0f, -1.7f, 0f), new Vector3(0.05f, 3.4f, 0.05f), _palette.Steel, false);
                Box("Seat", seat.transform, new Vector3(0f, -3.5f, 0f), new Vector3(1.2f, 0.2f, 0.7f), _palette.Hazard);
            }

            // Merry-go-round you can ride (a spinning platform), with the playground hoop above it.
            MovingObstacle merry = Mover("MerryGoRound", c + new Vector3(4f, 0.25f, -2f));
            merry.ConfigureSpin(28f);
            GameObject disc = PlaceholderShapes.Primitive(PrimitiveType.Cylinder, "MerryDisc", merry.transform, Vector3.zero, new Vector3(6f, 0.2f, 6f),
                PlaceholderAssets.Material("MerryDisc", Color.white, 0.3f, GeneratedTextures.Stripes("T_MerryStripes", ArtPalette.CandyPink, ArtPalette.Picket, 4), Vector2.one), true);
            Object.DestroyImmediate(disc.GetComponent<Collider>());
            disc.AddComponent<MeshCollider>().convex = true; // a squashed cylinder's capsule collider would be a dome
            for (int i = 0; i < 4; i++)
            {
                Quaternion around = Quaternion.Euler(0f, i * 90f, 0f);
                Box("MerryBar", merry.transform, around * new Vector3(0f, 0.8f, 2.3f), new Vector3(0.12f, 1.4f, 0.12f), _palette.Steel);
            }

            // Seesaw, slide tower and trampoline.
            MovingObstacle seesaw = Mover("Seesaw", c + new Vector3(-4f, 0.7f, 8f));
            seesaw.ConfigureSwing(Vector3.right, 14f, 3.5f, knocksPlayers: false);
            Box("SeesawPlank", seesaw.transform, Vector3.zero, new Vector3(1.2f, 0.25f, 8f), _palette.Climb);
            Box("SeesawPivot", c + new Vector3(-4f, 0.35f, 8f), new Vector3(0.8f, 0.7f, 0.8f), _palette.Steel, false);

            Vector3 slide = c + new Vector3(10f, 0f, 8f);
            Box("SlideTower", slide + new Vector3(0f, 3.2f, 0f), new Vector3(2.6f, 0.3f, 2.6f), _palette.Deck);
            foreach (Vector2 corner in new[] { new Vector2(-1.2f, -1.2f), new Vector2(1.2f, -1.2f), new Vector2(-1.2f, 1.2f), new Vector2(1.2f, 1.2f) })
            {
                Box("SlideLeg", slide + new Vector3(corner.x, 1.6f, corner.y), new Vector3(0.25f, 3.2f, 0.25f), _palette.Steel);
            }

            Box("SlideLadder", slide + new Vector3(0f, 1.6f, 1.45f), new Vector3(1.4f, 3.2f, 0.25f), _palette.Climb);
            Box("SlideChute", slide + new Vector3(0f, 1.65f, -3.6f), new Vector3(1.4f, 0.2f, 5.6f), PlaceholderAssets.Material("SlideChute", ArtPalette.CandyPurple, 0.7f))
                .transform.rotation = Quaternion.Euler(-34f, 0f, 0f);

            Vector3 trampoline = c + new Vector3(12f, 0f, -6f);
            PlaceholderShapes.Primitive(PrimitiveType.Cylinder, "Trampoline", _course, trampoline + new Vector3(0f, 0.5f, 0f), new Vector3(3f, 0.05f, 3f), _palette.Hazard, false);
            PlaceholderShapes.TriggerBox("TrampolinePad", _course, trampoline + new Vector3(0f, 0.7f, 0f), new Vector3(3f, 0.4f, 3f)).AddComponent<BouncePad>().Configure(9f);

            // Sandbox: the balloon pops in the sand too.
            Vector3 sandbox = c + new Vector3(-10f, 0f, 9f);
            Box("SandboxFrame", sandbox + new Vector3(0f, 0.25f, 0f), new Vector3(6f, 0.5f, 6f), _palette.Wood);
            Box("Sand", sandbox + new Vector3(0f, 0.52f, 0f), new Vector3(5.4f, 0.06f, 5.4f), _palette.Sand).AddComponent<BalloonGround>().Configure("Buried in the sandbox!");
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

            // Giant picnic table (the food sits on it), climbable benches.
            Vector3 table = c + new Vector3(0f, 0f, 5f);
            Box("PicnicTop", table + new Vector3(0f, PicnicTableTop - 0.25f, 0f), new Vector3(10f, 0.5f, 5f), _palette.Wood);
            foreach (float z in new[] { -4f, 4f })
            {
                Box("PicnicBench", table + new Vector3(0f, 1.5f, z), new Vector3(10f, 0.4f, 1.8f), _palette.Wood);
            }

            foreach (Vector2 corner in new[] { new Vector2(-4.4f, -2f), new Vector2(4.4f, -2f), new Vector2(-4.4f, 2f), new Vector2(4.4f, 2f) })
            {
                Box("PicnicLeg", table + new Vector3(corner.x, (PicnicTableTop - 0.5f) / 2f, corner.y), new Vector3(0.5f, PicnicTableTop - 0.5f, 0.5f), _palette.Wood);
            }

            Box("PicnicCloth", table + new Vector3(0f, PicnicTableTop + 0.01f, 0f), new Vector3(10.4f, 0.04f, 5.4f),
                PlaceholderAssets.Material("Gingham", Color.white, 0.1f, GeneratedTextures.Checker("T_Gingham", ArtPalette.Hazard, ArtPalette.Picket, 8), new Vector2(3f, 2f)), false);

            // Bug zapper (pops) and a box fan whose wind pushes the balloon west across the grill.
            Box("ZapperPole", c + new Vector3(-6f, 2f, -9f), new Vector3(0.2f, 4f, 0.2f), _palette.Steel);
            Box("BugZapper", c + new Vector3(-6f, 4.3f, -9f), new Vector3(0.6f, 0.9f, 0.6f), PlaceholderAssets.Emissive("Zapper", ArtPalette.CandyBlue, 1.5f))
                .AddComponent<Sharp>().Configure("Zapped by the bug zapper!");

            Box("BoxFan", c + new Vector3(10f, 1.8f, -6f), new Vector3(0.6f, 3.4f, 3.4f), PlaceholderAssets.Material("Fan", Color.Lerp(ArtPalette.Steel, ArtPalette.Picket, 0.5f)));
            PlaceholderShapes.TriggerBox("FanGust", _course, c + new Vector3(4f, 2.5f, -6f), new Vector3(4f, 4f, 11f), Quaternion.Euler(0f, -90f, 0f))
                .AddComponent<WindVolume>().Configure(2.2f);
        }

        // ------------------------------------------------------------------ passing objects

        private static void BuildPassingObjects()
        {
            // Robot lawnmower: laps the whole yard inside the fence. Blades pop the balloon; it bowls players over.
            MovingObstacle mower = Mover("RobotLawnmower", new Vector3(-42f, 0.55f, -18f));
            mower.ConfigurePath(new[]
            {
                new Vector3(-42f, 0.55f, -18f), new Vector3(42f, 0.55f, -18f), new Vector3(42f, 0.55f, 88f), new Vector3(-42f, 0.55f, 88f),
            }, 3.5f, true, 0f, true, true);
            Box("MowerBody", mower.transform, Vector3.zero, new Vector3(2.4f, 1f, 3.2f), PlaceholderAssets.Material("MowerBody", ArtPalette.CandyMint, 0.5f));
            Box("MowerSkirt", mower.transform, new Vector3(0f, -0.4f, 0f), new Vector3(2.6f, 0.2f, 3.4f), _palette.Hazard);
            PlaceholderShapes.Primitive(PrimitiveType.Sphere, "MowerLight", mower.transform, new Vector3(0f, 0.65f, 0.6f), Vector3.one * 0.35f,
                PlaceholderAssets.Emissive("WarningLight", ArtPalette.Warning, 2.5f), false);

            // RC car: zig-zags between the zones and back. Swats the balloon, knocks players.
            MovingObstacle car = Mover("RcCar", new Vector3(-30f, 0.4f, 8f));
            car.ConfigurePath(new[]
            {
                new Vector3(-30f, 0.4f, 8f), new Vector3(30f, 0.4f, 12f), new Vector3(-14f, 0.4f, 16f), new Vector3(-12f, 0.4f, 52f),
                new Vector3(30f, 0.4f, 56f),
            }, 6f, false, 0f, true, false);
            Box("CarBody", car.transform, Vector3.zero, new Vector3(1.4f, 0.5f, 2.2f), PlaceholderAssets.Material("CarBody", ArtPalette.CandyPink, 0.6f));
            Box("CarCabin", car.transform, new Vector3(0f, 0.4f, -0.2f), new Vector3(1.1f, 0.4f, 1f), PlaceholderAssets.Material("Window", ArtPalette.Window, 0.8f), false);
            foreach (Vector2 wheel in new[] { new Vector2(-0.75f, 0.7f), new Vector2(0.75f, 0.7f), new Vector2(-0.75f, -0.7f), new Vector2(0.75f, -0.7f) })
            {
                PlaceholderShapes.Primitive(PrimitiveType.Cylinder, "Wheel", car.transform, new Vector3(wheel.x, -0.15f, wheel.y), new Vector3(0.5f, 0.12f, 0.5f), _palette.Charcoal, false)
                    .transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }

            // Toy train: an engine and two wagons circling the tree.
            Vector3[] loop = Circle(TreeCenter + Vector3.up * 0.55f, 11f, 28);
            Color[] wagonColours = { ArtPalette.Hazard, ArtPalette.CandyBlue, ArtPalette.Accent };
            for (int i = 0; i < 3; i++)
            {
                MovingObstacle wagon = Mover(i == 0 ? "TrainEngine" : "TrainWagon", loop[0]);
                wagon.ConfigurePath(loop, 3f, true, -i * 0.95f, true, false);
                Box("WagonBody", wagon.transform, Vector3.zero, new Vector3(1.3f, 0.9f, 2.4f),
                    PlaceholderAssets.Material($"Wagon_{ColorUtility.ToHtmlStringRGB(wagonColours[i])}", wagonColours[i], 0.5f));
                if (i == 0)
                {
                    PlaceholderShapes.Primitive(PrimitiveType.Cylinder, "Chimney", wagon.transform, new Vector3(0f, 0.75f, 0.6f), new Vector3(0.4f, 0.3f, 0.4f), _palette.Charcoal, false);
                }
            }

            // Paper plane gliding a loop over the yard: swats the balloon off course.
            MovingObstacle plane = Mover("PaperPlane", new Vector3(-30f, 6f, 4f));
            plane.ConfigurePath(new[]
            {
                new Vector3(-30f, 6f, 4f), new Vector3(28f, 7f, 14f), new Vector3(22f, 6.5f, 80f), new Vector3(-26f, 7.5f, 62f),
            }, 5f, true, 0f, false, false, new Vector3(0f, 0f, 12f));
            Material paper = PlaceholderAssets.Material("Paper", ArtPalette.Picket, 0.1f);
            Box("WingLeft", plane.transform, new Vector3(-0.5f, 0f, 0f), new Vector3(1.2f, 0.05f, 2.2f), paper).transform.localRotation = Quaternion.Euler(0f, 0f, 12f);
            Box("WingRight", plane.transform, new Vector3(0.5f, 0f, 0f), new Vector3(1.2f, 0.05f, 2.2f), paper).transform.localRotation = Quaternion.Euler(0f, 0f, -12f);

            // Bowling ball rolling up and down a lane from the porch steps to the start stage.
            // (x = 13 runs between the train track and the pool rim.)
            MovingObstacle ball = Mover("BowlingBall", new Vector3(13f, 0.9f, 84f));
            ball.ConfigurePath(new[] { new Vector3(13f, 0.9f, 84f), new Vector3(13f, 0.9f, 6f) }, 5f, false, 0f, true, false);
            PlaceholderShapes.Primitive(PrimitiveType.Sphere, "Ball", ball.transform, Vector3.zero, Vector3.one * 1.8f,
                PlaceholderAssets.Material("BowlingBall", ArtPalette.CandyPurple, 0.85f), true);
            Box("BowlingLane", new Vector3(13f, 0.01f, 45f), new Vector3(2.4f, 0.02f, 80f), PlaceholderAssets.Material("Lane", ArtPalette.Timber, 0.6f), false);
        }

        // ------------------------------------------------------------------ hoops, spawns, items

        // Hoop sizing, measured from the player and the balloon (see the numbers below) rather than eyeballed.
        // Player (gorilla locomotion): the body collider rests on the floor 0.4 m under the eyes, so eyes sit ~0.65 m up
        // (in VR too: you are gorilla-sized). Arms reach ~0.6 m above the eyes: standing reach ~1.25 m. A jump rises
        // v^2 / 2g: a casual 3 m/s push +0.46 m (reach ~1.7 m), a strong 4.5 m/s push +1.03 m (reach ~2.3 m).
        // Balloon (gravity 1 m/s^2, drag 1.2/s, slap = hand speed x 0.9 + 0.6): a medium slap (3 m/s hand) rises ~1.6 m,
        // a full one ~3.5 m. A hoop counts while the balloon (r 0.25) overlaps its trigger, i.e. inside the ring.
        private const float HoopRingRadius = 1f;            // 2 m across: ~2.5x a player, 4 balloons wide
        private const float HoopTriggerRadius = HoopRingRadius - 0.25f;
        private const float HoopAboveFloor = 2.4f;          // ring bottom 1.4 m: just above standing reach; centre within a good jump,
                                                            // and a medium slap from shoulder height carries the balloon through
        private const float HoopAbovePerch = 1.9f;          // on a high perch you can't jump safely: ring bottom ~0.9 m, a tap away

        private static void BuildHoops()
        {
            // Six hoops, any order. Each has a checkpoint beside it where the balloon returns after a pop.
            const float diveBoardTop = 1.625f;  // BuildPool: board centre 1.5 m, 0.25 m thick; its tip is 6 m west of the pool centre
            const float merryTop = 0.35f;       // BuildPlayground: disc centre 0.25 m, 0.2 m thick
            const float flamesTop = 2.15f;      // BuildBbq: flame trigger centre 1.9 m, 0.5 m tall
            const float branchHighTop = 9.45f;  // BuildTree: BranchHigh centre 9.2 m, 0.5 m thick, spans z -4.0..-0.8 around the trunk
            const float fenceTop = 7f;          // BuildFence: picket height
            (Vector3 hoop, Vector3 checkpoint, float yaw)[] hoops =
            {
                // Over the pool, 1 m past the diving board's tip: slap it through from the board (or after a spring bounce).
                (PoolCenter + new Vector3(-5f, diveBoardTop + HoopAboveFloor, 0f), PoolCenter + new Vector3(-13f, 0f, -6f), 60f),
                // Through the laundry: the sheets hang from 2.3 m, so they brush the top of the ring and swat the balloon.
                (WashingLineCenter + new Vector3(0f, HoopAboveFloor, -7f), WashingLineCenter + new Vector3(8f, 0f, -12f), -20f),
                // Top of the tree, above the end of the highest branch.
                (TreeCenter + new Vector3(0.4f, branchHighTop + HoopAbovePerch, -3.2f), TreeCenter + new Vector3(0f, 0f, -14f), 0f),
                // Above the merry-go-round: ride it and slap the balloon up through as you spin past.
                (PlaygroundCenter + new Vector3(4f, merryTop + HoopAboveFloor, -2f), PlaygroundCenter + new Vector3(6f, 0f, -12f), 0f),
                // Over the grill: the ring's bottom clears the flames by 0.1 m, so a low balloon still gets burnt.
                (BbqCenter + new Vector3(0f, flamesTop + 0.1f + HoopRingRadius, -6f), BbqCenter + new Vector3(-8f, 0f, -14f), 0f),
                // On top of the west fence (pickets at x = -HalfWidth, 0.4 m thick): the ring hangs just inside it, so a player
                // clinging to the picket tops slaps the balloon up and slightly inward through it.
                (new Vector3(-HalfWidth + 0.2f + HoopRingRadius + 0.3f, fenceTop + HoopAbovePerch - 0.5f, 94f), new Vector3(-36f, 0f, 86f), -45f),
            };

            for (int i = 0; i < hoops.Length; i++)
            {
                Hoop(i, hoops[i].hoop);
                SpawnPoint(i + 1, hoops[i].checkpoint, hoops[i].yaw, new Vector3(0f, 2.4f, 1.8f));
            }
        }

        private static void Hoop(int index, Vector3 center)
        {
            var root = new GameObject($"Hoop{index + 1}");
            root.transform.SetParent(_course, false);
            root.transform.position = center;
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
            root.AddComponent<HoopCheckpoint>().Configure(index, renderers.ToArray(), spinner);
        }

        private static void BuildItemStashes()
        {
            // Start pile: one of everything. Zone stashes: a couple of items within reach everywhere (GDD: Items).
            for (int i = 0; i < 8; i++)
            {
                ItemSpawn(i, new Vector3(-5f, 0.9f, -9.6f + i * 0.9f));
            }

            (int item, Vector3 position)[] stashes =
            {
                (1, PoolCenter + new Vector3(-12f, 0.6f, 4f)), (2, PoolCenter + new Vector3(-12f, 0.6f, 6f)),
                (0, WashingLineCenter + new Vector3(6f, 0.6f, 10f)), (3, WashingLineCenter + new Vector3(8f, 0.6f, 10f)),
                (4, PlaygroundCenter + new Vector3(8f, 0.6f, -10f)), (5, PlaygroundCenter + new Vector3(10f, 0.6f, -10f)),
                (6, BbqCenter + new Vector3(-2f, PicnicTableTop + 0.5f, 5f)), (7, BbqCenter + new Vector3(2f, PicnicTableTop + 0.5f, 5f)),
                (1, TreeCenter + new Vector3(5f, 0.6f, -6f)), (3, TreeCenter + new Vector3(-5f, 0.6f, -6f)),
            };

            foreach ((int item, Vector3 position) in stashes)
            {
                ItemSpawn(item, position);
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
