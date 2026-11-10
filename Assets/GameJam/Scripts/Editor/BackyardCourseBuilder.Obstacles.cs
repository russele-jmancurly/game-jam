using System.Collections.Generic;
using GameJam.Core;
using GameJam.Gameplay;
using UnityEngine;

namespace GameJam.Editor
{
    /// <summary>
    /// The gap obstacles: one on the way to every hoop after the warm-up, so the long stretches between stations are part of
    /// the challenge (the user asked for a bigger yard, hoops further apart, more obstacles and more cactus). Each uses the
    /// potted cactus, the one thing in the yard that pops the balloon on touch, a different way:
    ///   to hoop 2: a cactus garden to weave through; to hoop 3: two cacti on a wagon rolling across the path;
    ///   to hoop 4: a sprinkler whose sweeping gust blows the balloon toward the cacti round it;
    ///   to hoop 5: two cactus pots swinging from frames; to hoop 6: a tall cactus wall to lob it over;
    ///   to the finish: a fenced pen whose only way in runs under two swinging pinatas, a cactus either side past them
    ///   (the user found the pinatas could simply be walked around: now they can't).
    /// Spots are computed from the hoops and checkpoints (the same numbers feed the giants' avoid circles).
    /// </summary>
    public static partial class BackyardCourseBuilder
    {
        private const float SwingBeamHeight = 5.8f;   // SwingFrame model: beam centre
        private const float SwingPostOffset = 3.2f;   // posts this far either side of the path
        private const float HangingPotDrop = 4.4f;    // PotHanger model: pot base below the pivot
        private const float HangingPotScale = 1.2f;
        private const float WagonHalfTravel = 5f;
        private const float WagonBedTop = 0.75f;      // Wagon model
        private const float SprinklerHub = 0.9f;      // SprinklerBase / SprinklerHead models
        private const float SprinklerReach = 6.7f;    // the gust ends here: short of the merry-go-round hoop (8 m away)
        // The finish pen: fenced with the yard's own 7 m pickets (a slapped balloon tops out near 6 m, so it can't be
        // lobbed in over them), open only at its south end. 7.6 m between the fence lines = two picket boards.
        private const float PenHalfWidth = 3.8f;
        private const float PenLength = 19f;          // five picket boards
        private const float PenPastPad = 3.8f;        // the back fence stands this far north of the pad's centre

        /// <summary>A cactus spot: where its pot stands, its yaw and its scale (1 = 1.7 m tall).</summary>
        private readonly struct CactusSpot
        {
            public readonly Vector3 Position;
            public readonly float Yaw;
            public readonly float Scale;

            public CactusSpot(Vector3 position, float yaw, float scale)
            {
                Position = position;
                Yaw = yaw;
                Scale = scale;
            }
        }

        // ---- Layout (shared by the builders below and RoamerAvoidCircles) --------------------------------------------------

        /// <summary>To hoop 2: six cacti staggered across the path in three rows, leaving a weaving line between them.</summary>
        private static List<CactusSpot> GardenCacti()
        {
            var spots = new List<CactusSpot>();
            (float t, float side, float scale)[] rows =
            {
                (0.36f, -3.0f, 1.3f), (0.36f, 1.6f, 1.6f), (0.54f, -0.8f, 1.4f), (0.54f, 3.6f, 1.7f), (0.72f, -3.4f, 1.5f), (0.72f, 0.9f, 1.3f),
            };
            for (int i = 0; i < rows.Length; i++)
            {
                Vector3 p = GapPoint(TrailHoops[0], TrailHoops[1], rows[i].t, out _, out Vector3 across, out _);
                spots.Add(new CactusSpot(p + across * rows[i].side, i * 57f, rows[i].scale));
            }

            return spots;
        }

        /// <summary>To hoop 3: the wagon's rails cross the way from checkpoint 2 to the diving board. Centre and direction.</summary>
        private static Vector3 WagonTrack(out Vector3 across)
        {
            Vector3 boardStand = PoolCenter + new Vector3(-12f, 0f, 0f);
            return GapPoint(TrailCheckpoints[1], boardStand, 0.55f, out _, out across, out _);
        }

        /// <summary>To hoop 4: the sprinkler stands halfway from checkpoint 3 to the merry-go-round hoop.</summary>
        private static Vector3 SprinklerSpot(out Vector3 along, out Vector3 across) =>
            GapPoint(TrailCheckpoints[2], TrailHoops[3], 0.42f, out along, out across, out _);

        /// <summary>Four cacti just outside the sprinkler's reach, either side of the path: where its gust carries the balloon.</summary>
        private static List<CactusSpot> SprinklerCacti()
        {
            Vector3 c = SprinklerSpot(out Vector3 along, out Vector3 across);
            var spots = new List<CactusSpot>();
            foreach (float side in new[] { -1f, 1f })
            {
                foreach (float forward in new[] { -2.4f, 2.4f })
                {
                    spots.Add(new CactusSpot(c + across * (side * (SprinklerReach + 1.1f)) + along * forward, 30f + side * 40f + forward * 20f, 1.6f));
                }
            }

            return spots;
        }

        /// <summary>To hoop 5: two frames across the way from checkpoint 4 to the grill hoop.</summary>
        private static Vector3 HangingFrame(int index, out Quaternion facing) =>
            GapPoint(TrailCheckpoints[3], TrailHoops[4], index == 0 ? 0.25f : 0.5f, out _, out _, out facing);

        /// <summary>To hoop 6: five tall cacti in a row across the way from checkpoint 5 to the mushroom pad.</summary>
        private static List<CactusSpot> WallCacti()
        {
            Vector3 mushroom = TreeCenter + new Vector3(2.6f, 0f, -1.9f);
            Vector3 c = GapPoint(TrailCheckpoints[4], mushroom, 0.5f, out Vector3 along, out Vector3 across, out _);
            float yaw = Mathf.Atan2(across.x, across.z) * Mathf.Rad2Deg - 90f; // arms (the model's X) along the wall
            float[] scales = { 1.7f, 2f, 1.8f, 2f, 1.7f };
            var spots = new List<CactusSpot>();
            for (int i = 0; i < scales.Length; i++)
            {
                spots.Add(new CactusSpot(c + across * ((i - 2) * 2.5f) + along * (i % 2 == 0 ? 0f : 0.5f), yaw, scales[i]));
            }

            return spots;
        }

        /// <summary>The finish pen's south end (its way in) and north end (the back fence), on its centre line.</summary>
        private static float PenNorth => TrailFinish.z + PenPastPad;

        private static float PenSouth => PenNorth - PenLength;

        /// <summary>The two finish pinatas: how far south of the pad's centre each one swings, inside the pen.</summary>
        private static readonly float[] PinataBeforePad = { 12f, 7.5f };

        private static Vector3 PinataSpot(int index) => new Vector3(TrailFinish.x, 0f, TrailFinish.z - PinataBeforePad[index]);

        /// <summary>A cactus against each fence between the second pinata and the pad: where a swat can send the balloon.</summary>
        private static List<CactusSpot> FinishCacti() => new List<CactusSpot>
        {
            new CactusSpot(new Vector3(TrailFinish.x - 2.9f, 0f, TrailFinish.z - 5.2f), 15f, 1.2f),
            new CactusSpot(new Vector3(TrailFinish.x + 2.9f, 0f, TrailFinish.z - 5.2f), 200f, 1.2f),
        };

        /// <summary>Avoid circles (x, z, radius) for every gap obstacle, so the giants walk round them.</summary>
        private static void AddObstacleAvoidCircles(List<Vector3> circles)
        {
            void Cacti(List<CactusSpot> spots)
            {
                foreach (CactusSpot spot in spots)
                {
                    circles.Add(new Vector3(spot.Position.x, spot.Position.z, 0.75f * spot.Scale));
                }
            }

            Cacti(GardenCacti());
            Cacti(SprinklerCacti());
            Cacti(WallCacti());

            Vector3 track = WagonTrack(out Vector3 trackAcross);
            for (float d = -WagonHalfTravel - 1f; d <= WagonHalfTravel + 1.01f; d += 2f)
            {
                Vector3 p = track + trackAcross * d;
                circles.Add(new Vector3(p.x, p.z, 1.6f));
            }

            Vector3 sprinkler = SprinklerSpot(out _, out _);
            circles.Add(new Vector3(sprinkler.x, sprinkler.z, 1f));
            for (int i = 0; i < 2; i++)
            {
                Vector3 frame = HangingFrame(i, out _);
                circles.Add(new Vector3(frame.x, frame.z, 3f));
            }

            // The finish pen, fences and all: the giants stay out of it (it is a dead end, and the crew's last stretch).
            for (float z = PenSouth; z <= PenNorth + 0.01f; z += 3.8f)
            {
                circles.Add(new Vector3(TrailFinish.x, z, PenHalfWidth + 0.8f));
            }
        }

        // ---- Builders --------------------------------------------------------------------------------------------------

        private static void BuildGapObstacles()
        {
            foreach (CactusSpot spot in GardenCacti())
            {
                CactusPot(spot.Position, spot.Yaw, spot.Scale);
            }

            BuildCactusWagon();
            BuildSprinkler();
            for (int i = 0; i < 2; i++)
            {
                BuildHangingCactus(i);
            }

            foreach (CactusSpot spot in WallCacti())
            {
                CactusPot(spot.Position, spot.Yaw, spot.Scale);
            }

            BuildFinishPen();
            for (int i = 0; i < PinataBeforePad.Length; i++)
            {
                BuildPinata(i);
            }

            foreach (CactusSpot spot in FinishCacti())
            {
                CactusPot(spot.Position, spot.Yaw, spot.Scale);
            }
        }

        /// <summary>
        /// Two potted cacti on a little red wagon that rolls back and forth on rails across the way to the diving board:
        /// a moving popper. Time the pass behind it, or lob the balloon over (the cacti reach 2.7 m).
        /// </summary>
        private static void BuildCactusWagon()
        {
            Vector3 centre = WagonTrack(out Vector3 across);
            Quaternion alongTrack = Quaternion.LookRotation(across, Vector3.up); // the wagon's length (Z) on the rails

            Material rail = PlaceholderAssets.Material("WagonRail", ArtPalette.Charcoal, 0.4f);
            foreach (float side in new[] { -0.76f, 0.76f })
            {
                GameObject strip = Box("WagonRail", centre + alongTrack * new Vector3(side, 0.03f, 0f), new Vector3(0.12f, 0.06f, WagonHalfTravel * 2f + 3f), rail, false);
                strip.transform.rotation = alongTrack;
            }

            for (float d = -WagonHalfTravel - 1f; d <= WagonHalfTravel + 1.01f; d += 1f)
            {
                GameObject sleeper = Box("WagonSleeper", centre + across * d + Vector3.up * 0.015f, new Vector3(1.9f, 0.03f, 0.22f), _palette.Wood, false);
                sleeper.transform.rotation = alongTrack;
            }

            MovingObstacle wagon = Mover("CactusWagon", centre - across * WagonHalfTravel);
            wagon.transform.rotation = alongTrack;
            wagon.ConfigurePingPong(across * (WagonHalfTravel * 2f), 11f, 0.2f);
            GameObject bed = Box("WagonBed", wagon.transform, new Vector3(0f, 0.5f, 0f), new Vector3(1.3f, 0.5f, 2.6f), _palette.Hazard);
            GameObject model = SetPieceModels.PlaceModel("Wagon", wagon.transform, wagon.transform.position, alongTrack, false);
            if (model != null)
            {
                bed.GetComponent<Renderer>().enabled = false;
            }

            foreach (float z in new[] { -0.62f, 0.62f })
            {
                CactusPot(wagon.transform.position + alongTrack * new Vector3(0f, WagonBedTop, z), z > 0f ? 90f : 250f, 1.15f, wagon.transform);
            }
        }

        /// <summary>
        /// A lawn sprinkler whose head turns slowly, sweeping a gust of spray round it: caught in it, the balloon is carried
        /// outward, toward the four cacti standing just past its reach. Cross while the arm points away.
        /// </summary>
        private static void BuildSprinkler()
        {
            Vector3 c = SprinklerSpot(out _, out _);
            GameObject stand = Box("SprinklerStand", c + new Vector3(0f, SprinklerHub * 0.5f, 0f), new Vector3(0.3f, SprinklerHub, 0.3f), _palette.Steel);
            if (SetPieceModels.PlaceModel("SprinklerBase", _course, c, Quaternion.identity, true) != null)
            {
                stand.GetComponent<Renderer>().enabled = false;
            }

            MovingObstacle head = Mover("SprinklerHead", c + Vector3.up * SprinklerHub);
            head.ConfigureSpin(32f, knocksPlayers: false); // the gust is a trigger under it: it must never count as a hit on a player
            GameObject arm = Box("SprinklerArm", head.transform, new Vector3(0f, 0f, 0.2f), new Vector3(0.12f, 0.12f, 1.3f), _palette.Climb, false);
            if (SetPieceModels.PlaceModel("SprinklerHead", head.transform, head.transform.position, Quaternion.identity, false) != null)
            {
                arm.GetComponent<Renderer>().enabled = false;
            }

            var gustSize = new Vector3(3f, 4f, SprinklerReach - 1f);
            GameObject gust = PlaceholderShapes.TriggerBox("SprinklerGust", head.transform, new Vector3(0f, 1.2f, 1f + gustSize.z * 0.5f), gustSize);
            PlaceholderPrefabBuilder.AirStream(gust.transform, gustSize, 5f);
            gust.AddComponent<WindVolume>().Configure(2.6f);

            foreach (CactusSpot spot in SprinklerCacti())
            {
                CactusPot(spot.Position, spot.Yaw, spot.Scale);
            }
        }

        /// <summary>
        /// A cactus pot hanging from a frame, swinging across the path like the pinata but popping what it touches. The two
        /// frames swing at different speeds, so the gap between them opens and closes.
        /// </summary>
        private static void BuildHangingCactus(int index)
        {
            Vector3 c = HangingFrame(index, out Quaternion facing);
            BuildSwingFrame("HangingFrame", c, facing);
            MovingObstacle swing = Mover($"HangingCactus{index + 1}", c + new Vector3(0f, SwingBeamHeight - 0.1f, 0f));
            swing.transform.rotation = facing;
            swing.ConfigureSwing(Vector3.forward, 30f, index == 0 ? 2.9f : 3.7f, phaseSeconds: index * 1.3f); // about the path: it sweeps across it
            GameObject rope = Box("HangingRope", swing.transform, new Vector3(0f, -(HangingPotDrop - 0.5f) * 0.5f, 0f), new Vector3(0.05f, HangingPotDrop - 0.5f, 0.05f), _palette.Wood, false);
            if (SetPieceModels.PlaceModel("PotHanger", swing.transform, swing.transform.position, facing, false) != null)
            {
                rope.GetComponent<Renderer>().enabled = false;
            }

            CactusPot(swing.transform.position + Vector3.down * HangingPotDrop, index * 110f, HangingPotScale, swing.transform);
        }

        /// <summary>
        /// A finish pinata (a paper donkey on a rope) swinging side to side across the way onto the finish pad: time the last
        /// slaps. Two of them now, the second faster and out of step with the first.
        /// </summary>
        private static void BuildPinata(int index)
        {
            Vector3 c = PinataSpot(index);
            Quaternion facing = Quaternion.identity; // the pen runs north: the frame stands across it
            BuildSwingFrame("PinataFrame", c, facing);
            MovingObstacle pinata = Mover($"FinishPinata{index + 1}", c + new Vector3(0f, SwingBeamHeight - 0.1f, 0f));
            pinata.transform.rotation = facing;
            pinata.ConfigureSwing(Vector3.forward, 38f, index == 0 ? 3.2f : 2.5f, phaseSeconds: index * 0.9f);
            GameObject rope = Box("PinataRope", pinata.transform, new Vector3(0f, -1.5f, 0f), new Vector3(0.05f, 3f, 0.05f), _palette.Wood, false);
            // Collider: the donkey, its length across the path (Pinata model: body centre 3.4 m under the pivot).
            GameObject body = Box("FinishPinataBody", pinata.transform, new Vector3(0f, -3.4f, 0f), new Vector3(1.3f, 1.1f, 0.8f),
                PlaceholderAssets.Material("Pinata", ArtPalette.CandyPink));
            if (SetPieceModels.PlaceModel("Pinata", pinata.transform, pinata.transform.position, facing * Quaternion.Euler(0f, index * 180f, 0f), false) != null)
            {
                rope.GetComponent<Renderer>().enabled = false;
                body.GetComponent<Renderer>().enabled = false;
            }
        }

        /// <summary>
        /// The finish pen: giant pickets down both sides of the last stretch and across its far end, so the finish basket can
        /// only be reached up the pen, under both pinatas (they used to stand in open lawn and could be walked around).
        /// The same 7 m fence as the yard's: a slapped balloon tops out near 6 m, so it can't be lobbed in over it either.
        /// </summary>
        private static void BuildFinishPen()
        {
            const float plank = 3.6f, spacing = 3.8f, height = 7f;
            float x = TrailFinish.x;
            for (float z = PenSouth + spacing * 0.5f; z < PenNorth; z += spacing)
            {
                Picket(new Vector3(x - PenHalfWidth, 0f, z), Quaternion.Euler(0f, 90f, 0f), plank, height);
                Picket(new Vector3(x + PenHalfWidth, 0f, z), Quaternion.Euler(0f, 90f, 0f), plank, height);
            }

            foreach (float side in new[] { -spacing * 0.5f, spacing * 0.5f })
            {
                Picket(new Vector3(x + side, 0f, PenNorth), Quaternion.identity, plank, height);
            }

            // Over the way in: a banner between the two end pickets, so the pen reads as the finish from across the yard.
            Vector3 gate = new Vector3(x, 5.6f, PenSouth);
            Box("FinishGateBanner", gate, new Vector3(PenHalfWidth * 2f, 1f, 0.12f), _palette.Checker, false);
            Box("FinishGateTrim", gate + Vector3.up * 0.58f, new Vector3(PenHalfWidth * 2f + 0.2f, 0.16f, 0.2f), _palette.Gold, false);
            TextMesh sign = PlaceholderShapes.Text("FinishGateSign", _course, gate + new Vector3(0f, 1.3f, -0.1f), 0.07f, "FINISH", ArtPalette.Accent);
            sign.transform.rotation = Quaternion.identity; // on the banner, read from the south
        }

        /// <summary>
        /// Two posts either side of the path and a beam across their tops. Posts 3.2 m out: a pinata (0.65 m half-length on a
        /// 3.4 m rope, 38 degrees) reaches 2.95 m and a hanging cactus (30 degrees) 2.6 m, both clear of them (at 45 degrees
        /// the pinata touched its post).
        /// </summary>
        private static void BuildSwingFrame(string name, Vector3 c, Quaternion facing)
        {
            var parts = new List<GameObject>();
            foreach (float side in new[] { -SwingPostOffset, SwingPostOffset })
            {
                GameObject post = Box($"{name}Post", c + facing * new Vector3(side, SwingBeamHeight / 2f, 0f), new Vector3(0.25f, SwingBeamHeight, 0.25f), _palette.Wood);
                post.transform.rotation = facing;
                parts.Add(post);
            }

            GameObject beam = Box($"{name}Beam", c + new Vector3(0f, SwingBeamHeight, 0f), new Vector3(SwingPostOffset * 2f + 0.3f, 0.25f, 0.25f), _palette.Wood);
            beam.transform.rotation = facing;
            parts.Add(beam);
            if (SetPieceModels.PlaceModel("SwingFrame", _course, c, facing, true) != null)
            {
                foreach (GameObject part in parts)
                {
                    part.GetComponent<Renderer>().enabled = false;
                }
            }
        }
    }
}
