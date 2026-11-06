using GameJam.Core;
using GameJam.Gameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace GameJam.Editor
{
    /// <summary>
    /// Set dressing for the open Giant Backyard (GDD: Levels): lawn beyond the fence, house windows, TV-studio light rigs
    /// and stands with a bobbing crowd peeking over the fence, clouds, giant flowers and grass, pool floats, the studio's own
    /// food props (Drive: Shiesty Assets/Props/Food) on the picnic table and as ice-cream landmarks, and the START arch.
    /// Purely visual: no colliders, so it never changes how the yard plays. Static where it doesn't move, so URP can batch it
    /// on Quest. Seeded randomness keeps every rebuild identical. Layout constants come from BackyardCourseBuilder.
    /// </summary>
    public static class EnvironmentDressingBuilder
    {
        private const string FoodFolder = "Assets/GameJam/Art/Props/Food";
        private const string AtlasPath = "Assets/GameJam/Art/Textures/T_ColorAtlas_v1.png";

        private static System.Random _random;
        private static Material _atlas;

        public static void Build()
        {
            _random = new System.Random(2026);
            Transform root = new GameObject("Dressing").transform;
            Transform staticRoot = new GameObject("Static").transform;
            staticRoot.SetParent(root, false);
            Transform movingRoot = new GameObject("Moving").transform;
            movingRoot.SetParent(root, false);

            _atlas = PlaceholderAssets.Material("FoodAtlas", Color.white, 0.25f, AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath));

            BuildOutsideLawn(staticRoot);
            BuildHouseDetails(staticRoot);
            BuildFlowersAndGrass(staticRoot);
            BuildStudio(staticRoot, movingRoot);
            BuildClouds(staticRoot);
            BuildPoolFloats(movingRoot);
            BuildPicnicFood(staticRoot);
            BuildStartArch(staticRoot);

            MarkStatic(staticRoot.gameObject);
        }

        private static void BuildOutsideLawn(Transform parent)
        {
            Material lawn = PlaceholderAssets.Material("Lawn", ArtPalette.Lawn);
            const float far = 260f;
            float w = BackyardCourseBuilder.HalfWidth;
            float south = BackyardCourseBuilder.SouthEdge;
            float north = BackyardCourseBuilder.NorthEdge;
            float middle = (south + north) / 2f;

            Decor(PrimitiveType.Cube, "LawnOutsideWest", parent, new Vector3(-(w + 0.5f + far / 2f), -0.05f, middle), new Vector3(far, 0.1f, far * 2f), lawn);
            Decor(PrimitiveType.Cube, "LawnOutsideEast", parent, new Vector3(w + 0.5f + far / 2f, -0.05f, middle), new Vector3(far, 0.1f, far * 2f), lawn);
            Decor(PrimitiveType.Cube, "LawnOutsideSouth", parent, new Vector3(0f, -0.05f, south - 0.5f - far / 2f), new Vector3(w * 2f + 1f, 0.1f, far), lawn);
            Decor(PrimitiveType.Cube, "LawnOutsideNorth", parent, new Vector3(0f, -0.05f, north + 0.5f + far / 2f), new Vector3(w * 2f + 1f, 0.1f, far), lawn);
        }

        private static void BuildHouseDetails(Transform parent)
        {
            Material frame = PlaceholderAssets.Material("WindowFrame", ArtPalette.Picket);
            Material glass = PlaceholderAssets.Emissive("WindowGlass", ArtPalette.Window, 0.6f);
            float front = BackyardCourseBuilder.NorthEdge - 0.1f;

            Decor(PrimitiveType.Cube, "HouseDoor", parent, new Vector3(0f, 3.6f, front - 0.1f), new Vector3(3.6f, 4.8f, 0.3f), PlaceholderAssets.Material("HouseDoor", ArtPalette.CandyBlue));
            foreach (float y in new[] { 11f, 18.5f })
            {
                for (int column = -2; column <= 2; column++)
                {
                    var center = new Vector3(column * 6f, y, front);
                    Decor(PrimitiveType.Cube, "WindowFrame", parent, center, new Vector3(4f, 4.4f, 0.3f), frame);
                    Decor(PrimitiveType.Cube, "WindowGlass", parent, center + new Vector3(0f, 0f, -0.12f), new Vector3(3.3f, 3.7f, 0.2f), glass);
                    Decor(PrimitiveType.Cube, "WindowBar", parent, center + new Vector3(0f, 0f, -0.25f), new Vector3(0.25f, 3.7f, 0.12f), frame);
                }
            }
        }

        private static void BuildFlowersAndGrass(Transform parent)
        {
            Material stem = PlaceholderAssets.Material("FlowerStem", ArtPalette.LawnDark);
            Material[] blades =
            {
                PlaceholderAssets.Material("GrassBlade", ArtPalette.Lawn),
                PlaceholderAssets.Material("GrassBladeDark", ArtPalette.LawnDark),
                PlaceholderAssets.Material("GrassBladeLight", ArtPalette.Moss),
            };

            float w = BackyardCourseBuilder.HalfWidth;

            // Giant flowers just outside the fence, tall enough to bloom over it and frame the yard.
            for (int i = 0; i < 18; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                var basePosition = new Vector3(side * Range(w + 3f, w + 12f), 0f, Range(-20f, 100f));
                Flower(parent, basePosition, Range(9f, 15f), ArtPalette.Candy[i % ArtPalette.Candy.Length], stem, facingInward: side);
            }

            // Grass tufts along the inside of the fence (no colliders: players brush through them).
            for (int i = 0; i < 50; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                Tuft(parent, new Vector3(side * Range(w - 2.5f, w - 0.8f), 0f, Range(-20f, 100f)), blades);
            }

            for (int i = 0; i < 16; i++)
            {
                Tuft(parent, new Vector3(Range(-w + 2f, w - 2f), 0f, Range(BackyardCourseBuilder.SouthEdge + 0.8f, BackyardCourseBuilder.SouthEdge + 2.5f)), blades);
            }
        }

        private static void Flower(Transform parent, Vector3 basePosition, float height, Color petalColor, Material stem, float facingInward)
        {
            Decor(PrimitiveType.Cylinder, "FlowerStem", parent, basePosition + Vector3.up * (height / 2f), new Vector3(0.6f, height / 2f, 0.6f), stem,
                Quaternion.Euler(Range(-4f, 4f), 0f, Range(-4f, 4f)));
            Decor(PrimitiveType.Sphere, "FlowerLeaf", parent, basePosition + new Vector3(1.4f, height * 0.35f, 0f), new Vector3(3f, 0.4f, 1.4f), stem,
                Quaternion.Euler(0f, Range(0f, 360f), 20f));

            Material petals = PlaceholderAssets.Material($"Petal_{ColorUtility.ToHtmlStringRGB(petalColor)}", petalColor);
            Material center = PlaceholderAssets.Material("FlowerCenter", ArtPalette.Accent);
            Vector3 head = basePosition + Vector3.up * height;
            Quaternion facing = Quaternion.Euler(-60f, facingInward < 0f ? 90f : -90f, 0f); // face into the yard
            for (int p = 0; p < 6; p++)
            {
                Vector3 offset = facing * (Quaternion.Euler(0f, 0f, p * 60f) * Vector3.up * 1.6f);
                Decor(PrimitiveType.Sphere, "Petal", parent, head + offset, new Vector3(1.8f, 1.8f, 0.6f), petals, facing * Quaternion.Euler(0f, 0f, p * 60f));
            }

            Decor(PrimitiveType.Sphere, "FlowerCenter", parent, head, new Vector3(1.6f, 1.6f, 1f), center, facing);
        }

        private static void Tuft(Transform parent, Vector3 basePosition, Material[] blades)
        {
            for (int b = 0; b < 4; b++)
            {
                float height = Range(1.5f, 3.5f);
                Vector3 offset = new Vector3(Range(-0.6f, 0.6f), height / 2f, Range(-0.6f, 0.6f));
                GameObject blade = Decor(PrimitiveType.Cube, "GrassBlade", parent, basePosition + offset, new Vector3(0.3f, height, 0.1f), blades[b % blades.Length],
                    Quaternion.Euler(Range(-14f, 14f), Range(0f, 180f), Range(-14f, 14f)));
                blade.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        private static void BuildStudio(Transform staticParent, Transform movingParent)
        {
            // TV-studio set around the yard: light towers above the fence and stands of bobbing fans looking in.
            Material truss = PlaceholderAssets.Material("Truss", ArtPalette.Charcoal, 0.5f);
            Material lampBody = PlaceholderAssets.Material("LampBody", ArtPalette.Charcoal, 0.6f);
            Material lampGlow = PlaceholderAssets.Emissive("LampGlow", ArtPalette.SunLight, 2f);
            float w = BackyardCourseBuilder.HalfWidth;

            float south = BackyardCourseBuilder.SouthEdge;
            float north = BackyardCourseBuilder.NorthEdge;
            foreach (float z in new[] { south + 16f, (south + north) / 2f, north - 10f })
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    var basePosition = new Vector3(side * (w + 6f), 0f, z);
                    Decor(PrimitiveType.Cube, "TrussTower", staticParent, basePosition + Vector3.up * 11f, new Vector3(0.8f, 22f, 0.8f), truss);
                    Decor(PrimitiveType.Cube, "TrussArm", staticParent, basePosition + new Vector3(-side * 1.5f, 21.5f, 0f), new Vector3(3.5f, 0.5f, 0.5f), truss);

                    Quaternion aim = Quaternion.LookRotation(new Vector3(side * 12f, 0f, z + 8f) - (basePosition + Vector3.up * 21f));
                    for (int lamp = -1; lamp <= 1; lamp += 2)
                    {
                        Vector3 lampPosition = basePosition + new Vector3(-side * 2.5f, 21f, lamp * 1.2f);
                        Decor(PrimitiveType.Cylinder, "Lamp", staticParent, lampPosition, new Vector3(1.1f, 0.7f, 1.1f), lampBody, aim * Quaternion.Euler(90f, 0f, 0f));
                        Decor(PrimitiveType.Cylinder, "LampLens", staticParent, lampPosition + aim * Vector3.forward * 0.72f, new Vector3(0.95f, 0.05f, 0.95f), lampGlow,
                            aim * Quaternion.Euler(90f, 0f, 0f));
                    }
                }
            }

            Stand(staticParent, movingParent, new Vector3(-(w + 16f), 0f, (south + north) / 2f - 8f), 90f);
            Stand(staticParent, movingParent, new Vector3(w + 16f, 0f, (south + north) / 2f + 8f), -90f);
            Stand(staticParent, movingParent, new Vector3(0f, 0f, BackyardCourseBuilder.SouthEdge - 14f), 0f);
        }

        private static void Stand(Transform staticParent, Transform movingParent, Vector3 front, float yaw)
        {
            // Tall stands so the crowd sees over the 7 m fence.
            Material seats = PlaceholderAssets.Material("StandSeats", ArtPalette.CandyBlue);
            Material frame = PlaceholderAssets.Material("StandFrame", ArtPalette.Charcoal);
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
            const int tiers = 4;

            Decor(PrimitiveType.Cube, "StandBase", staticParent, front + rotation * new Vector3(0f, 3f, -3.6f), new Vector3(22f, 6f, 9.6f), frame, rotation);
            for (int tier = 0; tier < tiers; tier++)
            {
                Vector3 tierCenter = front + rotation * new Vector3(0f, 7f + tier * 2f, -tier * 2.4f);
                Decor(PrimitiveType.Cube, "StandTier", staticParent, tierCenter, new Vector3(22f, 2f, 2.4f), tier % 2 == 0 ? seats : frame, rotation);

                for (int fan = 0; fan < 8; fan++)
                {
                    Vector3 seat = tierCenter + rotation * new Vector3(-9.5f + fan * 2.7f + Range(-0.3f, 0.3f), 1.9f, 0f);
                    Color shirt = ArtPalette.Candy[_random.Next(ArtPalette.Candy.Length)];
                    var fanRoot = new GameObject("Fan");
                    fanRoot.transform.SetParent(movingParent, false);
                    fanRoot.transform.SetPositionAndRotation(seat, rotation);
                    Decor(PrimitiveType.Capsule, "FanBody", fanRoot.transform, seat, new Vector3(1.1f, 0.9f, 1f),
                        PlaceholderAssets.Material($"Shirt_{ColorUtility.ToHtmlStringRGB(shirt)}", shirt), rotation);
                    Decor(PrimitiveType.Sphere, "FanHead", fanRoot.transform, seat + Vector3.up * 1.5f, Vector3.one * 0.95f,
                        PlaceholderAssets.Material("FanSkin", ArtPalette.HouseWall), rotation);
                    fanRoot.AddComponent<IdleMotion>().Configure(Range(0.08f, 0.25f), Range(0.8f, 1.6f), 3f, 0f, Range(0f, 6.3f));
                }
            }
        }

        private static void BuildClouds(Transform parent)
        {
            Material cloud = PlaceholderAssets.Material("Cloud", ArtPalette.Cloud, 0f);
            for (int i = 0; i < 9; i++)
            {
                var center = new Vector3(Range(-140f, 140f), Range(55f, 85f), Range(-60f, 240f));
                int puffs = 3 + _random.Next(3);
                for (int p = 0; p < puffs; p++)
                {
                    float size = Range(10f, 20f);
                    GameObject puff = Decor(PrimitiveType.Sphere, "CloudPuff", parent, center + new Vector3(p * size * 0.6f, Range(-2f, 3f), Range(-3f, 3f)),
                        new Vector3(size, size * 0.55f, size * 0.8f), cloud);
                    puff.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                }
            }
        }

        private static void BuildPoolFloats(Transform parent)
        {
            // The studio's donut as pool rings, plus a beach ball, bobbing in the paddling pool.
            Vector3 pool = BackyardCourseBuilder.PoolCenter;
            Float(Food("Donut_Visual", parent, pool + new Vector3(-3f, 0.86f, -4f), 9f, 30f), 0.06f);
            Float(Food("Donut_Visual", parent, pool + new Vector3(3.5f, 0.86f, 3.5f), 7f, 200f), 0.05f);

            GameObject ball = Decor(PrimitiveType.Sphere, "FloatingBeachBall", parent, pool + new Vector3(2f, 1.5f, -5f), Vector3.one * 1.4f,
                PlaceholderAssets.Material("BeachBallStripes", Color.white, 0.4f, GeneratedTextures.Stripes("T_BeachBall", ArtPalette.Hazard, ArtPalette.Picket, 3), Vector2.one));
            Float(ball, 0.1f);
        }

        private static void Float(GameObject floatingObject, float bob)
        {
            if (floatingObject != null)
            {
                floatingObject.AddComponent<IdleMotion>().Configure(bob, Range(0.2f, 0.35f), 4f, Range(-6f, 6f), Range(0f, 6.3f));
            }
        }

        private static void BuildPicnicFood(Transform parent)
        {
            // The studio's food props as giant picnic set pieces on the BBQ table, and ice-cream landmarks by the house.
            float w = BackyardCourseBuilder.HalfWidth;
            float north = BackyardCourseBuilder.NorthEdge;
            Food("IceCream_Visual", parent, new Vector3(-(w - 8f), 0f, north - 3f), 38f, 15f);
            Food("IceCream_Visual", parent, new Vector3(w - 7f, 0f, north - 4f), 32f, -30f);
        }

        /// <summary>Places one of the Drive food models at a scale, sitting on the given height, with the shared colour atlas.</summary>
        private static GameObject Food(string modelName, Transform parent, Vector3 basePosition, float scale, float yaw)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{FoodFolder}/{modelName}.fbx");
            if (model == null)
            {
                Debug.LogWarning($"[Dressing] Missing food model {modelName}.fbx; skipped.");
                return null;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
            instance.name = "Giant" + modelName.Replace("_Visual", string.Empty);
            instance.transform.localScale = Vector3.one * scale;
            instance.transform.SetPositionAndRotation(basePosition, Quaternion.Euler(0f, yaw, 0f));

            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>())
            {
                renderer.sharedMaterial = _atlas;
            }

            // Sit the model's lowest point on the base height (pivots differ per model).
            Bounds bounds = instance.GetComponentInChildren<Renderer>().bounds;
            instance.transform.position += Vector3.up * (basePosition.y - bounds.min.y);
            return instance;
        }

        private static void BuildStartArch(Transform parent)
        {
            // START arch at the front of the stage and a line of candy bunting: the show's opening shot.
            Material striped = PlaceholderAssets.Material("GateStripes", Color.white, 0.4f, GeneratedTextures.Stripes("T_GateStripes", ArtPalette.Accent, ArtPalette.Picket, 3),
                new Vector2(1f, 6f));
            Material gold = PlaceholderAssets.Material("Gate", ArtPalette.Accent, 0.4f);
            Material banner = PlaceholderAssets.Material("StartBanner", Color.white, 0.2f,
                GeneratedTextures.Checker("T_FinishChecker", ArtPalette.Picket, ArtPalette.Charcoal, 8), new Vector2(6f, 1f));

            const float z = -1.4f;
            const float y = 0.4f;
            Decor(PrimitiveType.Cylinder, "StartPostLeft", parent, new Vector3(-5.6f, y + 2.4f, z), new Vector3(0.35f, 2.4f, 0.35f), striped);
            Decor(PrimitiveType.Cylinder, "StartPostRight", parent, new Vector3(5.6f, y + 2.4f, z), new Vector3(0.35f, 2.4f, 0.35f), striped);
            Decor(PrimitiveType.Cube, "StartBanner", parent, new Vector3(0f, y + 5.1f, z), new Vector3(11.6f, 0.9f, 0.25f), banner);
            Decor(PrimitiveType.Cube, "StartBannerTrim", parent, new Vector3(0f, y + 5.65f, z), new Vector3(11.8f, 0.2f, 0.35f), gold);
            PlaceholderShapes.Text("StartSign", parent, new Vector3(0f, y + 6.2f, z), 0.09f, "START", ArtPalette.Accent);

            for (int i = 0; i < 16; i++)
            {
                float x = -5.2f + i * 0.69f;
                float sag = Mathf.Sin(i / 15f * Mathf.PI) * 0.4f;
                Color color = ArtPalette.Candy[i % ArtPalette.Candy.Length];
                Decor(PrimitiveType.Cube, "Bunting", parent, new Vector3(x, y + 4.4f - sag, z - 0.1f), new Vector3(0.36f, 0.36f, 0.03f),
                    PlaceholderAssets.Material($"Bunting_{ColorUtility.ToHtmlStringRGB(color)}", color), Quaternion.Euler(0f, 0f, 45f));
            }
        }

        /// <summary>A purely visual primitive: no collider, so dressing never changes how the yard plays.</summary>
        private static GameObject Decor(PrimitiveType type, string name, Transform parent, Vector3 worldPosition, Vector3 scale, Material material, Quaternion? rotation = null)
        {
            GameObject shape = PlaceholderShapes.Primitive(type, name, parent, Vector3.zero, scale, material, false);
            shape.transform.SetPositionAndRotation(worldPosition, rotation ?? Quaternion.identity);
            shape.layer = GameLayers.IgnoreRaycast;
            return shape;
        }

        private static float Range(float min, float max) => min + (float)_random.NextDouble() * (max - min);

        private static void MarkStatic(GameObject root)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.GetComponent<TextMesh>() != null)
                {
                    continue; // text meshes are generated at runtime; batching would freeze them
                }

                GameObjectUtility.SetStaticEditorFlags(child.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            }
        }
    }
}
