using GameJam.Core;
using GameJam.Gameplay;
using UnityEngine;

namespace GameJam.Editor
{
    /// <summary>
    /// Greybox of The Giant Backyard from primitives: start line, Flooded Lawn, Fence and Washing Line,
    /// Patio to the Roof. Swap pieces for real props once the art arrives; the gameplay components stay.
    /// </summary>
    public static class CourseGreyboxBuilder
    {
        private sealed class Palette
        {
            public Material Platform;
            public Material Stone;
            public Material Climb;
            public Material Hazard;
            public Material Cactus;
            public Material Water;
            public Material Gate;
            public Material Roof;
            public Material Wood;
            public Material Cloth;
            public Material Dark;
        }

        public static void Build()
        {
            Transform course = new GameObject("Course").transform;
            var palette = new Palette
            {
                Platform = PlaceholderAssets.Material("Platform", new Color(0.85f, 0.75f, 0.55f)),
                Stone = PlaceholderAssets.Material("Stone", new Color(0.6f, 0.6f, 0.65f)),
                Climb = PlaceholderAssets.Material("Climbable", new Color(1f, 0.55f, 0.1f)),
                Hazard = PlaceholderAssets.Material("Hazard", new Color(0.9f, 0.15f, 0.15f)),
                Cactus = PlaceholderAssets.Material("Cactus", new Color(0.2f, 0.55f, 0.25f)),
                Water = PlaceholderAssets.Material("Water", new Color(0.2f, 0.45f, 0.85f)),
                Gate = PlaceholderAssets.Material("Gate", new Color(1f, 0.9f, 0.2f)),
                Roof = PlaceholderAssets.Material("RoofTiles", new Color(0.65f, 0.25f, 0.2f)),
                Wood = PlaceholderAssets.Material("Wood", new Color(0.5f, 0.35f, 0.2f)),
                Cloth = PlaceholderAssets.Material("Laundry", new Color(0.95f, 0.95f, 1f)),
                Dark = PlaceholderAssets.Material("Dark", new Color(0.1f, 0.1f, 0.1f)),
            };

            BuildWaterFloor(course, palette);
            BuildStartLine(course, palette);
            BuildFloodedLawn(course, palette);
            BuildFenceAndWashingLine(course, palette);
            BuildPatioToRoof(course, palette);
        }

        private static void BuildWaterFloor(Transform course, Palette palette)
        {
            PlaceholderShapes.Box("WaterSurface", course, new Vector3(0f, 0f, 45f), new Vector3(80f, 0.1f, 140f), palette.Water, false);
            PlaceholderShapes.TriggerBox("WaterVolume", course, new Vector3(0f, -1f, 45f), new Vector3(80f, 2.6f, 140f)).AddComponent<Water>();
        }

        private static void BuildStartLine(Transform course, Palette palette)
        {
            PlaceholderShapes.Box("StartPlatform", course, new Vector3(0f, 0.75f, 0f), new Vector3(8f, 0.5f, 8f), palette.Platform);
            SpawnPoint(course, 0, new Vector3(0f, 1.05f, 0f));

            PlaceholderShapes.Box("ButtonPedestal", course, new Vector3(3f, 1.25f, 2f), new Vector3(0.5f, 0.5f, 0.5f), palette.Wood);
            GameObject button = PlaceholderShapes.Primitive(PrimitiveType.Cube, "StartButton", course, new Vector3(3f, 1.62f, 2f), new Vector3(0.4f, 0.25f, 0.4f), palette.Hazard, true);
            button.GetComponent<Collider>().isTrigger = true;
            button.AddComponent<StartButton>();
            button.layer = GameLayers.IgnoreRaycast;

            for (int i = 0; i < 6; i++)
            {
                ItemSpawn(course, i, new Vector3(-3f, 1.4f, -2.5f + i));
            }

            PlaceholderShapes.Box("CommentatorBooth", course, new Vector3(0f, 8f, 18f), new Vector3(5f, 1.5f, 1.5f), palette.Wood);
            var board = new GameObject("CommentatorBoard").AddComponent<CommentatorBoard>();
            board.transform.SetParent(course, false);
            TextMesh headline = PlaceholderShapes.Text("Headline", course, new Vector3(0f, 6.2f, 17f), 0.12f, "Connecting...", Color.white);
            TextMesh scoreboard = PlaceholderShapes.Text("Scoreboard", course, new Vector3(-3.2f, 2.6f, 3.9f), 0.03f, string.Empty, Color.yellow);
            board.Configure(headline, scoreboard);
        }

        private static void BuildFloodedLawn(Transform course, Palette palette)
        {
            Vector3[] stoneTops =
            {
                new Vector3(0f, 1.0f, 6f), new Vector3(1.2f, 1.1f, 8.6f), new Vector3(-0.8f, 1.2f, 11.2f),
                new Vector3(0.6f, 1.1f, 13.8f), new Vector3(-1f, 1.0f, 16.4f), new Vector3(0.8f, 1.2f, 19f),
                new Vector3(0f, 1.3f, 21.6f), new Vector3(0.6f, 1.3f, 26.8f),
            };
            foreach (Vector3 top in stoneTops)
            {
                PlaceholderShapes.Primitive(PrimitiveType.Cylinder, "SteppingStone", course, top - Vector3.up * 0.25f, new Vector3(1.6f, 0.25f, 1.6f), palette.Stone, true);
            }

            CactusPot(course, new Vector3(1.9f, 1.1f, 8.6f), palette);
            CactusPot(course, new Vector3(-1.6f, 1.0f, 16.6f), palette);
            CactusPot(course, new Vector3(1.5f, 1.2f, 19.2f), palette);

            // Sprinkler arm sweeping across the stones at head height: duck under it.
            PlaceholderShapes.Box("SprinklerPost", course, new Vector3(3.5f, 1.2f, 14f), new Vector3(0.3f, 2.4f, 0.3f), palette.Wood);
            MovingObstacle sprinkler = Mover("SprinklerArm", course, new Vector3(3.5f, 2.3f, 14f));
            sprinkler.ConfigureSpin(70f);
            PlaceholderShapes.Box("Arm", sprinkler.transform, Vector3.zero, new Vector3(9f, 0.15f, 0.15f), palette.Hazard);

            GameObject lilyPad = PlaceholderShapes.Primitive(PrimitiveType.Cylinder, "SinkingLilyPad", course, new Vector3(-0.2f, 1.15f, 24.2f), new Vector3(1.6f, 0.05f, 1.6f), palette.Cactus, true);
            lilyPad.AddComponent<CollapsingPlatform>().Configure(0f);

            PlaceholderShapes.Box("Gate1Platform", course, new Vector3(0f, 1.0f, 31f), new Vector3(6f, 0.6f, 5f), palette.Platform);
            Gate(course, 1, new Vector3(0f, 2.8f, 29.2f), palette);
            SpawnPoint(course, 1, new Vector3(0f, 1.35f, 30.5f));
            ItemSpawn(course, 3, new Vector3(2.2f, 1.7f, 32.5f));
            ItemSpawn(course, 0, new Vector3(-2.2f, 1.7f, 32.5f));
            ItemSpawn(course, 5, new Vector3(2.2f, 1.7f, 30f));
        }

        private static void BuildFenceAndWashingLine(Transform course, Palette palette)
        {
            PlaceholderShapes.Box("FenceWall", course, new Vector3(0f, 2.3f, 34.2f), new Vector3(6f, 4f, 0.4f), palette.Climb);
            PlaceholderShapes.Box("FenceTop", course, new Vector3(0f, 4.1f, 35.4f), new Vector3(6f, 0.4f, 2f), palette.Wood);
            PlaceholderShapes.Box("ClotheslinePlatform", course, new Vector3(0f, 4.0f, 39.5f), new Vector3(5f, 0.4f, 4f), palette.Platform);

            PlaceholderShapes.Box("ClotheslinePole", course, new Vector3(0f, 4.75f, 39.5f), new Vector3(0.15f, 1.1f, 0.15f), palette.Wood);
            MovingObstacle clothesline = Mover("SpinningClothesline", course, new Vector3(0f, 5.35f, 39.5f));
            clothesline.ConfigureSpin(45f);
            PlaceholderShapes.Box("LineA", clothesline.transform, Vector3.zero, new Vector3(4.4f, 0.08f, 0.08f), palette.Cloth);
            PlaceholderShapes.Box("LineB", clothesline.transform, Vector3.zero, new Vector3(0.08f, 0.08f, 4.4f), palette.Cloth);

            PlaceholderShapes.Box("BarPostLeft", course, new Vector3(-1.6f, 4.5f, 41.5f), new Vector3(0.2f, 3.5f, 0.2f), palette.Wood);
            PlaceholderShapes.Box("BarPostRight", course, new Vector3(1.6f, 4.5f, 41.5f), new Vector3(0.2f, 3.5f, 0.2f), palette.Wood);
            for (int i = 0; i < 5; i++)
            {
                PlaceholderShapes.Primitive(PrimitiveType.Cylinder, "MonkeyBar", course, new Vector3(0f, 6.1f, 42.2f + i * 1.2f),
                    new Vector3(0.08f, 1.6f, 0.08f), palette.Climb, true, Quaternion.Euler(0f, 0f, 90f));
            }

            MovingObstacle basket = Mover("SwingingBasket", course, new Vector3(0f, 8.5f, 44.5f));
            basket.ConfigureSwing(Vector3.right, 40f, 3f);
            PlaceholderShapes.Box("Rope", basket.transform, new Vector3(0f, -1.2f, 0f), new Vector3(0.05f, 2.4f, 0.05f), palette.Wood, false);
            PlaceholderShapes.Box("Basket", basket.transform, new Vector3(0f, -2.8f, 0f), new Vector3(1f, 0.8f, 1f), palette.Wood);

            MovingObstacle sheet = Mover("FlappingSheet", course, new Vector3(0f, 6.6f, 50.8f));
            sheet.ConfigureSwing(Vector3.right, 25f, 2f, knocksPlayers: false);
            PlaceholderShapes.Box("Sheet", sheet.transform, new Vector3(0f, -0.7f, 0f), new Vector3(2.5f, 1.4f, 0.02f), palette.Cloth, false);

            PlaceholderShapes.Box("CardboardBox1", course, new Vector3(0f, 4.2f, 48.8f), new Vector3(2.5f, 0.4f, 2.5f), palette.Wood)
                .AddComponent<CollapsingPlatform>().Configure(0f);
            PlaceholderShapes.Box("CardboardBox2", course, new Vector3(1.2f, 4.4f, 51.8f), new Vector3(2.5f, 0.4f, 2.5f), palette.Wood)
                .AddComponent<CollapsingPlatform>().Configure(2.5f);

            PlaceholderShapes.Box("Gate2Platform", course, new Vector3(0f, 4.3f, 56f), new Vector3(6f, 0.6f, 5f), palette.Platform);
            Gate(course, 2, new Vector3(0f, 6.1f, 54.2f), palette);
            SpawnPoint(course, 2, new Vector3(0f, 4.65f, 55.5f));
            ItemSpawn(course, 4, new Vector3(2.2f, 5f, 57.5f));
            ItemSpawn(course, 1, new Vector3(-2.2f, 5f, 57.5f));
            ItemSpawn(course, 3, new Vector3(-2.2f, 5f, 55f));
        }

        private static void BuildPatioToRoof(Transform course, Palette palette)
        {
            PlaceholderShapes.Box("PatioTable", course, new Vector3(0f, 4.3f, 60.5f), new Vector3(4f, 0.4f, 3f), palette.Wood);
            PlaceholderShapes.Box("Grill", course, new Vector3(0f, 4.0f, 64f), new Vector3(3f, 0.8f, 2f), palette.Dark);
            PlaceholderShapes.Box("GrillLedgeLeft", course, new Vector3(-2.1f, 4.2f, 64f), new Vector3(0.6f, 0.4f, 3f), palette.Platform);
            PlaceholderShapes.Box("GrillLedgeRight", course, new Vector3(2.1f, 4.2f, 64f), new Vector3(0.6f, 0.4f, 3f), palette.Platform);

            GameObject flames = PlaceholderShapes.TriggerBox("GrillFlames", course, new Vector3(0f, 4.7f, 64f), new Vector3(2.6f, 0.5f, 1.6f));
            flames.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            var flameRenderer = flames.AddComponent<MeshRenderer>();
            flameRenderer.sharedMaterial = PlaceholderAssets.Material("Flame", new Color(1f, 0.3f, 0f));
            flames.AddComponent<PulsingHazard>().Configure(flameRenderer);

            PlaceholderShapes.Box("BugZapper", course, new Vector3(1.6f, 5.9f, 66.8f), new Vector3(0.4f, 0.6f, 0.4f), PlaceholderAssets.Material("Zapper", new Color(0.3f, 0.5f, 1f)))
                .AddComponent<Sharp>().Configure("Zapped by the bug zapper!");

            PlaceholderShapes.Box("LandingPlatform", course, new Vector3(0f, 4.3f, 68.5f), new Vector3(4f, 0.4f, 4f), palette.Platform);
            ItemSpawn(course, 4, new Vector3(-1.5f, 5f, 68f));
            ItemSpawn(course, 2, new Vector3(1.5f, 5f, 68f));

            PlaceholderShapes.Primitive(PrimitiveType.Cylinder, "Trampoline", course, new Vector3(0f, 4.55f, 70.2f), new Vector3(2f, 0.05f, 2f), palette.Hazard, false);
            PlaceholderShapes.TriggerBox("TrampolinePad", course, new Vector3(0f, 4.8f, 70.2f), new Vector3(2f, 0.4f, 2f))
                .AddComponent<BouncePad>().Configure(9f);

            GameObject roof = PlaceholderShapes.Box("Roof", course, new Vector3(0f, 8f, 75.5f), new Vector3(6f, 0.4f, 7f), palette.Roof);
            roof.AddComponent<GorillaLocomotion.Surface>().slipPercentage = 0.3f; // slippery tiles
            PlaceholderShapes.Box("Chimney", course, new Vector3(-2f, 9.2f, 73.5f), new Vector3(1f, 2f, 1f), PlaceholderAssets.Material("Brick", new Color(0.6f, 0.3f, 0.25f)));

            PlaceholderShapes.TriggerBox("WindFanGust", course, new Vector3(0f, 9.6f, 76.5f), new Vector3(6f, 3f, 5f), Quaternion.Euler(0f, 90f, 0f))
                .AddComponent<WindVolume>().Configure(2.5f);
            PlaceholderShapes.Box("WindFan", course, new Vector3(-3.6f, 9.4f, 76.5f), new Vector3(0.3f, 1.4f, 1.4f), PlaceholderAssets.Material("Fan", new Color(0.8f, 0.8f, 0.85f)));
            PlaceholderShapes.Box("Dartboard", course, new Vector3(3.2f, 9.6f, 77.5f), new Vector3(0.2f, 1f, 1f), palette.Hazard)
                .AddComponent<Sharp>().Configure("Darted!");

            MovingObstacle pinata = Mover("WreckingPinata", course, new Vector3(0f, 13.5f, 80.6f));
            pinata.ConfigureSwing(Vector3.forward, 55f, 2.6f);
            PlaceholderShapes.Box("Rope", pinata.transform, new Vector3(0f, -2f, 0f), new Vector3(0.05f, 4f, 0.05f), palette.Wood, false);
            PlaceholderShapes.Box("Pinata", pinata.transform, new Vector3(0f, -4.3f, 0f), Vector3.one, PlaceholderAssets.Material("Pinata", new Color(1f, 0.4f, 0.8f)));

            PlaceholderShapes.Box("FinishPlatform", course, new Vector3(0f, 8f, 84.5f), new Vector3(6f, 0.4f, 5f), palette.Dark);
            PlaceholderShapes.TriggerBox("FinishLine", course, new Vector3(0f, 9.6f, 84.5f), new Vector3(6f, 3f, 4f)).AddComponent<FinishLine>();
            PlaceholderShapes.Box("FinishBanner", course, new Vector3(0f, 11.5f, 82.5f), new Vector3(6f, 0.6f, 0.1f), palette.Gate, false);
        }

        private static void CactusPot(Transform parent, Vector3 baseTop, Palette palette)
        {
            GameObject pot = PlaceholderShapes.Primitive(PrimitiveType.Cylinder, "CactusPot", parent, baseTop + Vector3.up * 0.3f, new Vector3(0.3f, 0.3f, 0.3f), palette.Cactus, true);
            pot.AddComponent<Sharp>().Configure("Popped on a cactus!");
            PlaceholderShapes.Primitive(PrimitiveType.Cube, "CactusSpikes", pot.transform, new Vector3(0f, 1.05f, 0f), new Vector3(1.2f, 0.15f, 1.2f), palette.Hazard, false);
        }

        private static void Gate(Transform parent, int sectionIndex, Vector3 position, Palette palette)
        {
            PlaceholderShapes.TriggerBox($"SectionGate{sectionIndex}", parent, position, new Vector3(6f, 4f, 1f))
                .AddComponent<SectionGate>().Configure(sectionIndex);
            PlaceholderShapes.Box($"Gate{sectionIndex}PostLeft", parent, position + new Vector3(-3.1f, 0f, 0f), new Vector3(0.2f, 4f, 0.2f), palette.Gate);
            PlaceholderShapes.Box($"Gate{sectionIndex}PostRight", parent, position + new Vector3(3.1f, 0f, 0f), new Vector3(0.2f, 4f, 0.2f), palette.Gate);
        }

        private static void SpawnPoint(Transform parent, int section, Vector3 position)
        {
            var point = new GameObject($"SpawnPoint_Section{section}");
            point.transform.SetParent(parent, false);
            point.transform.localPosition = position;
            point.AddComponent<SpawnPoint>().Configure(section, new Vector3(0f, 1.6f, 1.2f));
        }

        private static void ItemSpawn(Transform parent, int itemIndex, Vector3 position)
        {
            var point = new GameObject($"ItemSpawn_{itemIndex}");
            point.transform.SetParent(parent, false);
            point.transform.localPosition = position;
            point.AddComponent<ItemSpawnPoint>().Configure(itemIndex);
        }

        private static MovingObstacle Mover(string name, Transform parent, Vector3 position)
        {
            var mover = new GameObject(name);
            mover.transform.SetParent(parent, false);
            mover.transform.localPosition = position;
            mover.AddComponent<Rigidbody>().isKinematic = true;
            return mover.AddComponent<MovingObstacle>();
        }
    }
}
