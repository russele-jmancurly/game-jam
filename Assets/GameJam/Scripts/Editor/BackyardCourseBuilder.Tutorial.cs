using System.Collections.Generic;
using GameJam.Core;
using GameJam.Gameplay;
using GameJam.Net;
using UnityEditor;
using UnityEngine;

namespace GameJam.Editor
{
    /// <summary>
    /// The tutorial side of the course builder: the mole hole (the lobby's way down and the tutorial's way back) and the
    /// tutorial burrow, an underground practice room reached through it. Five rooms in a row, one lesson each (a sign
    /// with a goal that <see cref="TutorialGuide"/> watches) and a gate to the next that opens when the lesson is done:
    /// move, slap the balloon through a hoop, what pops it, items, and the giant. Everything is the real thing (the networked balloon, items and one giant, run offline), so what a player
    /// learns here is exactly how the yard behaves.
    /// </summary>
    public static partial class BackyardCourseBuilder
    {
        // The burrow: floor top at y = 0, x -12..12, z -6..66, 9 m to the roof. Five rooms in a row along z, a gate between
        // each pair; the player lands at the origin facing north.
        private const float BurrowHalfWidth = 12f;
        private const float BurrowSouth = -6f;
        private const float BurrowNorth = 66f;
        private const float BurrowHeight = 9f;
        private const float GateDoorWidth = 5f;
        private const float GateDoorHeight = 4.5f;

        /// <summary>Where the four gates stand (z): gate i closes section i off from section i + 1.</summary>
        public static readonly float[] TutorialGates = { 9f, 25f, 37f, 49f };

        /// <summary>Where a player stands at the start of each section (also where they respawn in it).</summary>
        public static readonly Vector3[] TutorialSectionStarts =
        {
            Vector3.zero, new Vector3(0f, 0f, 11f), new Vector3(0f, 0f, 27f), new Vector3(0f, 0f, 39f), new Vector3(0f, 0f, 51f),
        };

        /// <summary>Where the tutorial's giant walks: its den at the far end of the burrow (the first spot is its spawn).</summary>
        public static readonly Vector3[] TutorialGiantWander =
        {
            new Vector3(0f, 0f, 60f), new Vector3(-7f, 0f, 57f), new Vector3(7f, 0f, 57f), new Vector3(7f, 0f, 63f), new Vector3(-7f, 0f, 63f),
        };

        /// <summary>The den's bounds (min x, min z, max x, max z): the giant never leaves it.</summary>
        public static readonly Vector4 TutorialGiantBounds = new Vector4(-10f, 54f, 10f, 65f);

        /// <summary>Where the rig starts in the tutorial scene (the spawn point is at the origin).</summary>
        public static readonly Vector3 TutorialRigStart = new Vector3(0f, 0.05f, -1f);

        /// <summary>
        /// A mole hole at <paramref name="floorPoint"/> (on the surface it sits on): a mound of soil clumps round a dark
        /// opening, dust drifting out of it, a floating title with a bobbing arrow pointing down into it, and the trigger
        /// <see cref="MoleHole"/> listens on (found by the rig's hazard sensor). The mound has no colliders: a gorilla
        /// body floats above knee height anyway, so you get in by moving or hopping over the opening.
        /// <paramref name="labelYaw"/> turns the title toward where players come from (0 faces south, 90 west).
        /// </summary>
        private static GameObject BuildMoleHole(Transform parent, Vector3 floorPoint, string destination, string title, string subtitle, float labelYaw) =>
            BuildMoleHole(parent, floorPoint, destination, title, subtitle, labelYaw, out _);

        private static GameObject BuildMoleHole(Transform parent, Vector3 floorPoint, string destination, string title, string subtitle, float labelYaw,
            out TextMesh subtitleText)
        {
            var root = new GameObject("MoleHole_" + destination);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(floorPoint, Quaternion.identity);

            Color soilDark = Color.Lerp(ArtPalette.TimberDark, ArtPalette.Charcoal, 0.6f);
            Color soilLight = Color.Lerp(ArtPalette.TimberDark, ArtPalette.Timber, 0.15f);
            Material soil = PlaceholderAssets.Material("MoleSoil", Color.white, 0.05f, GeneratedTextures.Noise("T_MoleSoil", soilDark, soilLight, 5f), Vector2.one);
            Material soilDeep = PlaceholderAssets.Material("MoleSoilDeep", Color.Lerp(soilDark, Color.black, 0.35f), 0.05f);

            // The mound: an outer ring of big clumps and a darker inner lip, all a little uneven.
            var random = new System.Random(11);
            const int outer = 11;
            for (int i = 0; i < outer; i++)
            {
                float angle = (i + (float)random.NextDouble() * 0.4f) * Mathf.PI * 2f / outer;
                float radius = 0.78f + (float)random.NextDouble() * 0.12f;
                float size = 0.55f + (float)random.NextDouble() * 0.22f;
                PlaceholderShapes.Primitive(PrimitiveType.Sphere, "MoundClump", root.transform,
                    new Vector3(Mathf.Cos(angle) * radius, 0.08f, Mathf.Sin(angle) * radius), new Vector3(size, size * 0.62f, size), soil, false);
            }

            const int inner = 9;
            for (int i = 0; i < inner; i++)
            {
                float angle = (i + 0.5f) * Mathf.PI * 2f / inner;
                float size = 0.34f + (float)random.NextDouble() * 0.1f;
                PlaceholderShapes.Primitive(PrimitiveType.Sphere, "MoundLip", root.transform,
                    new Vector3(Mathf.Cos(angle) * 0.6f, 0.1f, Mathf.Sin(angle) * 0.6f), new Vector3(size, size * 0.6f, size), soilDeep, false);
            }

            // The opening: a dark earth ring round a black middle, so it reads as a shaft going down.
            PlaceholderShapes.Primitive(PrimitiveType.Cylinder, "HoleRim", root.transform, new Vector3(0f, 0.015f, 0f), new Vector3(1.3f, 0.01f, 1.3f),
                PlaceholderAssets.Unlit("MoleHoleRim", new Color(0.13f, 0.08f, 0.05f)), false);
            PlaceholderShapes.Primitive(PrimitiveType.Cylinder, "HoleDark", root.transform, new Vector3(0f, 0.03f, 0f), new Vector3(0.95f, 0.01f, 0.95f),
                PlaceholderAssets.Unlit("MoleHoleDark", new Color(0.015f, 0.01f, 0.008f)), false);

            // The trigger: a column over the opening, tall enough for a body floating above it.
            var trigger = new GameObject("DiveTrigger");
            trigger.transform.SetParent(root.transform, false);
            trigger.layer = GameLayers.IgnoreRaycast;
            var column = trigger.AddComponent<CapsuleCollider>();
            column.isTrigger = true;
            column.radius = 0.4f;
            column.height = 2.6f;
            column.center = new Vector3(0f, 1.3f, 0f);

            AudioSource dive = PlaceholderPrefabBuilder.Sound(root, GeneratedAudio.MoleDive("Mole_Dive"), false, 0f, 0.9f);
            (ParticleSystem clods, ParticleSystem dust) = AmbientEffectsBuilder.HandDirt(root.transform);
            AmbientEffectsBuilder.HoleDust(root.transform, floorPoint + Vector3.up * 0.1f);
            root.AddComponent<MoleHole>().Configure(destination, dive, clods, dust);

            // Title, a line under it, and an arrow pointing down into the hole (two crossed fins: readable from any side).
            var label = new GameObject("Label").transform;
            label.SetParent(root.transform, false);
            label.localRotation = Quaternion.Euler(0f, labelYaw, 0f); // the Edit-mode pose; in play it faces the player
            label.gameObject.AddComponent<FaceCamera>();
            TextMesh titleText = PlaceholderShapes.Text("Title", label, new Vector3(0f, 2.2f, 0f), 0.03f, title, ArtPalette.Accent);
            titleText.fontStyle = FontStyle.Bold;
            subtitleText = PlaceholderShapes.Text("Subtitle", label, new Vector3(0f, 1.9f, 0f), 0.015f, subtitle, ArtPalette.Picket);

            var arrow = new GameObject("DownArrow").transform;
            arrow.SetParent(root.transform, false);
            arrow.localPosition = new Vector3(0f, 1.35f, 0f);
            arrow.localRotation = Quaternion.Euler(0f, 0f, 180f);
            arrow.localScale = Vector3.one * 1.8f;
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

            arrow.gameObject.AddComponent<IdleMotion>().Configure(0.07f, 1.2f, 0f, 60f, 0f);
            return root;
        }

        /// <summary>
        /// Builds the tutorial burrow into the open scene (the tutorial scene builder adds lighting, rig and network): five
        /// sections along z, one lesson each, with a gate that opens when the lesson is done.
        /// </summary>
        public static void BuildTutorial(CharacterRoster roster)
        {
            _course = new GameObject("Course").transform;
            _palette = CreatePalette();
            _roster = roster;

            BuildBurrowShell();
            BuildBurrowDressing();

            var status = new List<TextMesh>();
            var anchors = new List<Transform>();
            var exitHints = new List<TextMesh>();
            Vector3[] starts = TutorialSectionStarts;

            // Section 1: moving. A couple of crates to push off and climb, and the way back up for anyone who wants out.
            SpawnPoint(0, starts[0], 0f, new Vector3(0f, 1.4f, 14f)); // the balloon already waits in section 2
            TutorialBoard("Lesson1_Move", new Vector3(-5f, 2f, 5.5f), -20f, "1. MOVE",
                "PUSH THE GROUND WITH YOUR HANDS TO MOVE\nSWING DOWN AND BACK TO HOP FORWARD\nLEFT STICK: WALK THE WAY YOU LOOK\nGRAB WALLS AND CRATES TO CLIMB", null, status, anchors);
            Box("ClimbCrateLow", new Vector3(-8.2f, 0.6f, 1.5f), new Vector3(1.6f, 1.2f, 1.6f), _palette.Climb);
            Box("ClimbCrateHigh", new Vector3(-9.8f, 1.1f, 2.6f), new Vector3(1.5f, 2.2f, 1.5f), _palette.Climb);
            TextMesh welcome = PlaceholderShapes.Text("WelcomeTitle", _course, new Vector3(0f, 6.2f, 6f), 0.07f, "TUTORIAL", ArtPalette.Accent);
            welcome.fontStyle = FontStyle.Bold;
            Billboard(welcome);
            Billboard(PlaceholderShapes.Text("WelcomeHint", _course, new Vector3(0f, 5.45f, 6f), 0.032f, "ONE LESSON PER ROOM: FINISH IT TO OPEN THE GATE", ArtPalette.Picket));
            BuildMoleHole(_course, new Vector3(6.5f, 0f, 1.5f), SceneTravel.GameScene, "TO THE YARD", "JUMP IN TO SKIP THE TUTORIAL", 0f, out TextMesh exitHint);
            exitHints.Add(exitHint);

            // Section 2: the balloon and the hoop.
            SpawnPoint(1, starts[1], 0f, new Vector3(0f, 1.4f, 3f));
            Hoop(0, new Vector3(0f, 2.4f, 20f), 0f);
            TutorialBoard("Lesson2_Slap", new Vector3(5.6f, 2f, 16f), 22f, "2. SLAP THE BALLOON",
                "SWING YOUR HAND THROUGH IT: IT FLIES THE WAY\nYOUR HAND MOVES. KNOCK IT THROUGH THE HOOP!", new[] { "Icon_Slap", "Icon_Hoop" }, status, anchors);

            // Section 3: what pops it, with cactus pots and a puddle to try it on.
            SpawnPoint(2, starts[2], 0f, new Vector3(0f, 1.4f, 3.5f));
            TutorialBoard("Lesson3_Pop", new Vector3(-5.6f, 2f, 33.5f), -22f, "3. DON'T LET IT POP!",
                "THE BALLOON POPS IF IT TOUCHES\nTHE GROUND  ·  WATER  ·  SPIKES\nOR IF YOU HIT IT TWICE IN A ROW\n(WITH FRIENDS: TAKE TURNS!)\nA POPPED BALLOON COMES BACK", null, status, anchors);
            CactusPot(new Vector3(3.6f, 0f, 31.5f), 20f);
            CactusPot(new Vector3(5.8f, 0f, 33.2f), 200f, 0.8f);
            Vector3 puddle = new Vector3(5.2f, 0f, 28.6f);
            PlaceholderShapes.Primitive(PrimitiveType.Cylinder, "Puddle", _course, puddle + new Vector3(0f, 0.02f, 0f), new Vector3(2.8f, 0.01f, 2.8f), _palette.Water, false);
            PlaceholderShapes.TriggerBox("PuddleWaterVolume", _course, puddle + new Vector3(0f, 0.2f, 0f), new Vector3(2.2f, 0.4f, 2.2f)).AddComponent<Water>();
            Billboard(PlaceholderShapes.Text("PuddleLabel", _course, puddle + new Vector3(0f, 1f, 0f), 0.03f, "WATER", ArtPalette.Pool)).fontStyle = FontStyle.Bold;
            Billboard(PlaceholderShapes.Text("CactusLabel", _course, new Vector3(4.7f, 2.45f, 32.3f), 0.03f, "SPIKES", ArtPalette.Hazard)).fontStyle = FontStyle.Bold;

            // Section 4: items. The two hoop helpers and a pillow to throw.
            SpawnPoint(3, starts[3], 0f, new Vector3(0f, 1.4f, 3.5f));
            TutorialBoard("Lesson4_Items", new Vector3(5.6f, 2f, 45.5f), 22f, "4. ITEMS",
                "SQUEEZE THE GRIP TO GRAB  ·  TRIGGER TO USE\nBUBBLES LIFT THE BALLOON, THE BLOWER PUSHES IT\nLET GO MID-SWING TO THROW", new[] { "Icon_Item", "Icon_Throw" }, status, anchors);
            ItemSpawn(8, new Vector3(-3.6f, 0.6f, 41.5f)); // bubble wand
            ItemSpawn(9, new Vector3(-5.4f, 0.6f, 43f));   // leaf blower
            ItemSpawn(0, new Vector3(-3.8f, 0.6f, 44.6f)); // pillow

            // Section 5: the giant in its den, the balloon just inside it (so the giant shows what it does), and a row of
            // things to throw at it.
            SpawnPoint(4, starts[4], 0f, new Vector3(0f, 1.4f, 4.5f));
            TutorialBoard("Lesson5_Giant", new Vector3(-6.2f, 2f, 52.6f), -15f, "5. GIANTS",
                "GIANTS ROAM THE YARD. THEY SLAP THE BALLOON\nAWAY AND SHOVE YOU ASIDE.\nTHROW AN ITEM AT ONE TO KNOCK IT OUT!", new[] { "Icon_Throw", "Icon_Knockdown" }, status, anchors);
            int[] ammo = { 1, 2, 4, 6 }; // beach ball, rubber duck, watermelon, donut
            for (int i = 0; i < ammo.Length; i++)
            {
                ItemSpawn(ammo[i], new Vector3(2.6f + i * 1.5f, 0.6f, 52f));
            }

            Box("DenLine", new Vector3(0f, 0.01f, TutorialGiantBounds.y - 0.4f), new Vector3(BurrowHalfWidth * 2f - 1f, 0.02f, 0.25f), _palette.Gold, false);
            Billboard(PlaceholderShapes.Text("DenLabel", _course, new Vector3(0f, 5.6f, TutorialGiantBounds.y), 0.06f, "THE GIANT'S DEN", ArtPalette.Hazard))
                .fontStyle = FontStyle.Bold;
            BuildMoleHole(_course, new Vector3(10f, 0f, 50.8f), SceneTravel.GameScene, "BACK TO THE YARD", "JUMP IN WHEN YOU'RE DONE", 0f, out exitHint);
            exitHints.Add(exitHint);

            // The gates between the sections, each naming what is behind it.
            string[] next = { "NEXT: THE BALLOON", "NEXT: WHAT POPS IT", "NEXT: ITEMS", "NEXT: THE GIANT" };
            var gates = new SectionGate[TutorialGates.Length];
            for (int i = 0; i < gates.Length; i++)
            {
                gates[i] = BuildSectionGate(i, TutorialGates[i], next[i]);
            }

            var ambience = new GameObject("Ambience").transform;
            ambience.SetParent(_course, false);
            float middleZ = (BurrowSouth + BurrowNorth) * 0.5f;
            AmbientEffectsBuilder.Pollen(ambience, new Vector3(0f, 3.5f, middleZ), new Vector3(BurrowHalfWidth * 2f - 2f, 6f, BurrowNorth - BurrowSouth - 2f));
            AmbientEffectsBuilder.Bursts(ambience); // pooled confetti (lessons, the hoop) and pop shreds (the balloon)

            var guideHost = new GameObject("TutorialGuide");
            guideHost.transform.SetParent(_course, false);
            AudioSource chime = PlaceholderPrefabBuilder.Sound(guideHost, GeneratedAudio.LessonDone("Lesson_Done"), false, 0f, 0.8f);
            var fanfareHost = new GameObject("CompleteFanfare");
            fanfareHost.transform.SetParent(guideHost.transform, false);
            AudioSource fanfare = PlaceholderPrefabBuilder.Sound(fanfareHost, GeneratedAudio.WinFanfare("Win_Fanfare"), false, 0f, 0.8f);
            guideHost.AddComponent<TutorialGuide>().Configure(status.ToArray(), anchors.ToArray(), gates, chime, fanfare, exitHints.ToArray());

            DepthTestCourseText();
        }

        /// <summary>
        /// A wall of soil across the burrow at <paramref name="z"/> with a doorway in the middle, closed by a slab of roots
        /// (<see cref="SectionGate"/>) that sinks into the floor when the lesson in front of it is done. The lintel names
        /// the room behind it; the door says it is locked.
        /// </summary>
        private static SectionGate BuildSectionGate(int index, float z, string nextLabel)
        {
            var root = new GameObject($"SectionGate{index + 1}");
            root.transform.SetParent(_course, false);
            root.transform.position = new Vector3(0f, 0f, z);

            Material wall = PlaceholderAssets.Material("BurrowWall", Color.white);
            float sideWidth = BurrowHalfWidth - GateDoorWidth * 0.5f;
            float sideX = GateDoorWidth * 0.5f + sideWidth * 0.5f;
            Box("GateWallWest", root.transform, new Vector3(-sideX, BurrowHeight * 0.5f, 0f), new Vector3(sideWidth, BurrowHeight, 1f), wall);
            Box("GateWallEast", root.transform, new Vector3(sideX, BurrowHeight * 0.5f, 0f), new Vector3(sideWidth, BurrowHeight, 1f), wall);
            float lintelHeight = BurrowHeight - GateDoorHeight;
            Box("GateLintel", root.transform, new Vector3(0f, GateDoorHeight + lintelHeight * 0.5f, 0f), new Vector3(GateDoorWidth, lintelHeight, 1f), wall);

            // Frame: two posts and a beam, so the doorway reads from across the room.
            Material frame = PlaceholderAssets.Material("BurrowRoot", Color.white);
            Box("GatePostWest", root.transform, new Vector3(-GateDoorWidth * 0.5f - 0.15f, GateDoorHeight * 0.5f, -0.5f), new Vector3(0.3f, GateDoorHeight, 0.3f), frame, false);
            Box("GatePostEast", root.transform, new Vector3(GateDoorWidth * 0.5f + 0.15f, GateDoorHeight * 0.5f, -0.5f), new Vector3(0.3f, GateDoorHeight, 0.3f), frame, false);
            Box("GateBeam", root.transform, new Vector3(0f, GateDoorHeight + 0.15f, -0.5f), new Vector3(GateDoorWidth + 0.9f, 0.3f, 0.3f), frame, false);
            TextMesh label = PlaceholderShapes.Text("GateLabel", root.transform, new Vector3(0f, GateDoorHeight + 0.95f, -0.52f), 0.05f, nextLabel, ArtPalette.Accent);
            label.fontStyle = FontStyle.Bold;

            // The door: a dark slab with root bars across it, under one unscaled parent that sinks away. Solid until then.
            Transform door = new GameObject("GateDoor").transform;
            door.SetParent(root.transform, false);
            door.localPosition = new Vector3(0f, GateDoorHeight * 0.5f, 0f);
            Material slab = PlaceholderAssets.Material("GateSlab", Color.Lerp(ArtPalette.TimberDark, ArtPalette.Charcoal, 0.55f), 0.1f);
            Box("GateSlab", door, Vector3.zero, new Vector3(GateDoorWidth, GateDoorHeight, 0.5f), slab);
            for (int bar = 0; bar < 5; bar++)
            {
                float x = (-0.4f + bar * 0.2f) * GateDoorWidth;
                Box("GateBar", door, new Vector3(x, 0f, -0.3f), new Vector3(0.22f, GateDoorHeight, 0.16f), frame, false);
            }

            Box("GateCrossBar", door, new Vector3(0f, 1.25f, -0.36f), new Vector3(GateDoorWidth, 0.24f, 0.16f), frame, false);
            Box("GateCrossBarLow", door, new Vector3(0f, -1.45f, -0.36f), new Vector3(GateDoorWidth, 0.24f, 0.16f), frame, false);
            TextMesh lockedText = PlaceholderShapes.Text("Locked", door, new Vector3(0f, -0.4f, -0.46f), 0.042f, "LOCKED\nFINISH THIS ROOM'S LESSON", ArtPalette.Warning);
            lockedText.fontStyle = FontStyle.Bold;

            AudioSource rumble = PlaceholderPrefabBuilder.Sound(root, GeneratedAudio.GateRumble("Gate_Rumble"), false, 30f, 1f);
            ParticleSystem dust = AmbientEffectsBuilder.DustPuffs(root.transform);
            var gate = root.AddComponent<SectionGate>();
            gate.Configure(door, GateDoorHeight + 0.3f, GateDoorWidth * 0.5f, rumble, dust);
            return gate;
        }

        /// <summary>Packed-earth floor (the balloon pops on it, like the lawn), soil walls and roof. All solid and climbable.</summary>
        private static void BuildBurrowShell()
        {
            Color earth = Color.Lerp(ArtPalette.TimberDark, ArtPalette.Charcoal, 0.42f);
            Color earthLight = Color.Lerp(ArtPalette.TimberDark, ArtPalette.Timber, 0.3f);
            float length = BurrowNorth - BurrowSouth;
            float middleZ = (BurrowSouth + BurrowNorth) * 0.5f;
            float width = BurrowHalfWidth * 2f;

            // "Turf" in the name sorts the floor with the grass taps, which kick up clods of soil.
            Material floor = PlaceholderAssets.Material("BurrowTurf", Color.white, 0.05f,
                GeneratedTextures.Noise("T_BurrowFloor", Color.Lerp(earth, Color.black, 0.1f), Color.Lerp(earth, earthLight, 0.45f), 7f), new Vector2(width / 4f, length / 4f));
            Material wall = PlaceholderAssets.Material("BurrowWall", Color.white, 0.05f,
                GeneratedTextures.Noise("T_BurrowWall", Color.Lerp(earth, Color.black, 0.2f), Color.Lerp(earth, earthLight, 0.5f), 5f), new Vector2(8f, 2f));
            Material roof = PlaceholderAssets.Material("BurrowRoof", Color.Lerp(earth, Color.black, 0.35f), 0.05f);

            Box("BurrowFloor", new Vector3(0f, -0.25f, middleZ), new Vector3(width + 2f, 0.5f, length + 2f), floor).AddComponent<BalloonGround>();
            Box("BurrowWallWest", new Vector3(-BurrowHalfWidth - 0.5f, BurrowHeight * 0.5f, middleZ), new Vector3(1f, BurrowHeight, length + 2f), wall);
            Box("BurrowWallEast", new Vector3(BurrowHalfWidth + 0.5f, BurrowHeight * 0.5f, middleZ), new Vector3(1f, BurrowHeight, length + 2f), wall);
            Box("BurrowWallSouth", new Vector3(0f, BurrowHeight * 0.5f, BurrowSouth - 0.5f), new Vector3(width, BurrowHeight, 1f), wall);
            Box("BurrowWallNorth", new Vector3(0f, BurrowHeight * 0.5f, BurrowNorth + 0.5f), new Vector3(width, BurrowHeight, 1f), wall);
            Box("BurrowRoof", new Vector3(0f, BurrowHeight + 0.5f, middleZ), new Vector3(width + 2f, 1f, length + 2f), roof);
        }

        /// <summary>
        /// Collider-free dressing so the room reads as a burrow: roots hanging from the roof and creeping down the walls,
        /// stones along the wall bases, clusters of glowing mushrooms (the room's "lamps": emissive, no real lights), and
        /// the bright mouth of the tunnel in the roof over the landing spot.
        /// </summary>
        private static void BuildBurrowDressing()
        {
            var dressing = new GameObject("BurrowDressing").transform;
            dressing.SetParent(_course, false);
            var random = new System.Random(23);
            float Range(float min, float max) => min + (float)random.NextDouble() * (max - min);

            Material root = PlaceholderAssets.Material("BurrowRoot", Color.Lerp(ArtPalette.TimberDark, ArtPalette.Timber, 0.35f), 0.1f);
            Material stone = PlaceholderAssets.Material("BurrowStone", Color.Lerp(ArtPalette.Pebble, ArtPalette.Charcoal, 0.35f), 0.2f);
            Material stem = PlaceholderAssets.Material("GlowStem", ArtPalette.Picket, 0.2f);
            Material[] caps =
            {
                PlaceholderAssets.Emissive("GlowCapMint", ArtPalette.CandyMint, 1.6f),
                PlaceholderAssets.Emissive("GlowCapPink", ArtPalette.CandyPink, 1.5f),
                PlaceholderAssets.Emissive("GlowCapBlue", ArtPalette.CandyBlue, 1.6f),
            };

            // Roots hanging from the roof (kept above 5.5 m and away from the middle line the balloon flies along).
            for (int i = 0; i < 26; i++)
            {
                float x = Range(3.5f, BurrowHalfWidth - 0.8f) * (i % 2 == 0 ? 1f : -1f);
                float z = Range(BurrowSouth + 1f, BurrowNorth - 1f);
                float lengthDown = Range(1.2f, 3.2f);
                PlaceholderShapes.Primitive(PrimitiveType.Cylinder, "HangingRoot", dressing, new Vector3(x, BurrowHeight - lengthDown * 0.5f, z),
                    new Vector3(Range(0.05f, 0.1f), lengthDown * 0.5f, Range(0.05f, 0.1f)), root, false,
                    Quaternion.Euler(Range(-9f, 9f), 0f, Range(-9f, 9f)));
            }

            // Roots creeping down the side walls, and stones and mushrooms along their bases.
            for (int side = -1; side <= 1; side += 2)
            {
                float wallX = side * (BurrowHalfWidth - 0.08f);
                for (float z = BurrowSouth + 2f; z < BurrowNorth - 1f; z += Range(3.2f, 5f))
                {
                    float rootLength = Range(2.5f, 6f);
                    PlaceholderShapes.Primitive(PrimitiveType.Cylinder, "WallRoot", dressing, new Vector3(wallX, BurrowHeight - rootLength * 0.5f, z),
                        new Vector3(0.14f, rootLength * 0.5f, 0.14f), root, false, Quaternion.Euler(Range(-14f, 14f), 0f, 0f));

                    float stoneSize = Range(0.4f, 0.9f);
                    PlaceholderShapes.Primitive(PrimitiveType.Sphere, "Stone", dressing, new Vector3(side * (BurrowHalfWidth - Range(0.3f, 0.8f)), stoneSize * 0.25f, z + Range(-1f, 1f)),
                        new Vector3(stoneSize, stoneSize * 0.6f, stoneSize * Range(0.8f, 1.3f)), stone, false);

                    Vector3 cluster = new Vector3(side * (BurrowHalfWidth - Range(0.7f, 1.3f)), 0f, z + Range(1.2f, 2.2f));
                    int count = random.Next(2, 5);
                    for (int m = 0; m < count; m++)
                    {
                        float height = Range(0.3f, 0.9f);
                        float capSize = height * Range(0.7f, 1f);
                        Vector3 at = cluster + new Vector3(Range(-0.45f, 0.45f), 0f, Range(-0.45f, 0.45f));
                        PlaceholderShapes.Primitive(PrimitiveType.Cylinder, "MushroomStem", dressing, at + Vector3.up * (height * 0.5f),
                            new Vector3(height * 0.16f, height * 0.5f, height * 0.16f), stem, false);
                        PlaceholderShapes.Primitive(PrimitiveType.Sphere, "MushroomCap", dressing, at + Vector3.up * height,
                            new Vector3(capSize, capSize * 0.5f, capSize), caps[random.Next(caps.Length)], false);
                    }
                }
            }

            // The tunnel mouth in the roof over the landing spot: daylight coming down, with dust falling through it.
            PlaceholderShapes.Primitive(PrimitiveType.Cylinder, "TunnelMouth", dressing, new Vector3(0f, BurrowHeight - 0.01f, 0f), new Vector3(3f, 0.01f, 3f),
                PlaceholderAssets.Unlit("TunnelDaylight", ArtPalette.SunLight), false);
            for (int i = 0; i < 9; i++)
            {
                float angle = i * Mathf.PI * 2f / 9f;
                PlaceholderShapes.Primitive(PrimitiveType.Sphere, "MouthClump", dressing,
                    new Vector3(Mathf.Cos(angle) * 1.5f, BurrowHeight - 0.02f, Mathf.Sin(angle) * 1.5f), new Vector3(0.7f, 0.35f, 0.7f),
                    PlaceholderAssets.Material("BurrowRoof", Color.black, 0.05f), false);
            }

            foreach (Renderer renderer in dressing.GetComponentsInChildren<Renderer>())
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        /// <summary>
        /// A lesson sign on two posts: title, a few lines of text, optional picture icons (Art/UI, the lobby signs' set) and
        /// a goal line at the bottom that <see cref="TutorialGuide"/> rewrites as the player makes progress. Collider-free.
        /// The board's local -Z faces the viewers; positive <paramref name="yaw"/> turns it toward the west.
        /// </summary>
        private static void TutorialBoard(string name, Vector3 center, float yaw, string title, string body, string[] icons, List<TextMesh> status,
            List<Transform> anchors)
        {
            const float width = 3.5f;
            const float height = 2.1f;
            var root = new GameObject(name);
            root.transform.SetParent(_course, false);
            root.transform.SetPositionAndRotation(center, Quaternion.Euler(0f, yaw, 0f));

            Material board = PlaceholderAssets.Material("SignBoard", ArtPalette.TextDark, 0.3f);
            Box($"{name}Board", root.transform, new Vector3(0f, 0f, 0.03f), new Vector3(width, height, 0.05f), board, false);
            Box($"{name}Trim", root.transform, new Vector3(0f, height * 0.5f + 0.015f, 0.03f), new Vector3(width + 0.1f, 0.06f, 0.07f), _palette.Gold, false);
            float postLength = center.y - height * 0.5f;
            foreach (float x in new[] { -width * 0.5f + 0.25f, width * 0.5f - 0.25f })
            {
                Box($"{name}Post", root.transform, new Vector3(x, -height * 0.5f - postLength * 0.5f, 0.08f), new Vector3(0.12f, postLength, 0.12f), _palette.Wood, false);
            }

            TextMesh titleText = PlaceholderShapes.Text($"{name}Title", root.transform, new Vector3(0f, height * 0.5f - 0.2f, -0.01f), 0.034f, title, ArtPalette.Accent);
            titleText.fontStyle = FontStyle.Bold;

            bool hasIcons = icons != null && icons.Length > 0;
            if (hasIcons)
            {
                Material panel = PlaceholderAssets.TransparentUnlit("HowToPanel", GeneratedTextures.RoundedPanel("T_RoundedPanel"));
                panel.SetColor("_BaseColor", ArtPalette.Picket);
                EditorUtility.SetDirty(panel);
                for (int i = 0; i < icons.Length; i++)
                {
                    float x = (i - (icons.Length - 1) * 0.5f) * 0.95f;
                    GameObject back = PlaceholderShapes.Primitive(PrimitiveType.Quad, $"{name}Panel{i}", root.transform, new Vector3(x, 0.3f, -0.006f),
                        new Vector3(0.7f, 0.62f, 1f), panel, false);
                    back.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    Texture2D texture = ImportIcon($"{PlaceholderAssets.Root}/Art/UI/{icons[i]}.png");
                    if (texture != null)
                    {
                        GameObject icon = PlaceholderShapes.Primitive(PrimitiveType.Quad, $"{name}Icon{i}", root.transform, new Vector3(x, 0.3f, -0.012f),
                            new Vector3(0.58f, 0.58f, 1f), PlaceholderAssets.TransparentUnlit($"HowTo{icons[i]}", texture), false);
                        icon.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    }
                }
            }

            PlaceholderShapes.Text($"{name}Body", root.transform, new Vector3(0f, hasIcons ? -0.43f : 0.06f, -0.01f), hasIcons ? 0.019f : 0.0205f, body, ArtPalette.Picket);

            TextMesh goal = PlaceholderShapes.Text($"{name}Goal", root.transform, new Vector3(0f, -height * 0.5f + 0.17f, -0.01f), 0.022f, "...", ArtPalette.Warning);
            goal.fontStyle = FontStyle.Bold;
            status.Add(goal);
            anchors.Add(root.transform);
        }
    }
}
