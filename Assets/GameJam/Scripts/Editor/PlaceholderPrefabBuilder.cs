using Fusion;
using GameJam.Core;
using GameJam.Gameplay;
using GameJam.Net;
using Photon.Voice.Fusion;
using Photon.Voice.Unity;
using UnityEditor;
using UnityEngine;

namespace GameJam.Editor
{
    /// <summary>
    /// Builds the networked placeholder prefabs from primitives: avatar, balloon, game state and items.
    /// Prefabs are regenerated on every build; tuning lives in the config assets, which are kept.
    /// </summary>
    public static class PlaceholderPrefabBuilder
    {
        private const NetworkObjectFlags SharedObjectFlags = NetworkObjectFlags.MasterClientObject;
        private const NetworkObjectFlags ItemFlags = NetworkObjectFlags.AllowStateAuthorityOverride;
        private const NetworkObjectFlags NeverDestroyOnLeave = NetworkObjectFlags.DestroyWhenStateAuthorityLeaves;

        private readonly struct ItemSpec
        {
            public readonly string Name;
            public readonly ItemKind Kind;
            public readonly PrimitiveType Shape;
            public readonly Vector3 Scale;
            public readonly float Mass;
            public readonly Color Color;
            public readonly float BalloonPower;
            public readonly bool IsBouncy;

            /// <summary>Optional FBX under Art/Props/Food to use as the visual instead of a primitive.</summary>
            public readonly string ModelName;

            /// <summary>How strongly a hit from this item is steered through the nearest open hoop (seeded into new assets).</summary>
            public readonly float HoopAssist;

            /// <summary>What it sets off where it lands (only for items whose asset is new).</summary>
            public readonly ItemEffect Effect;

            public ItemSpec(string name, ItemKind kind, PrimitiveType shape, Vector3 scale, float mass, Color color, float balloonPower, bool isBouncy = false,
                string modelName = null, float hoopAssist = 0f, ItemEffect effect = ItemEffect.None)
            {
                Name = name;
                Kind = kind;
                Shape = shape;
                Scale = scale;
                Mass = mass;
                Color = color;
                BalloonPower = balloonPower;
                IsBouncy = isBouncy;
                ModelName = modelName;
                HoopAssist = hoopAssist;
                Effect = effect;
            }
        }

        /// <summary>Item models built by tools/blender/scripts/build_item_props.py, named after the item; a primitive stands in while one is missing.</summary>
        private const string ItemModelFolder = "Assets/GameJam/Art/Props/Items";

        // Hoop assist (GDD: most items help with the balloon): soft throws steer it most, heavy knocks a little (they are
        // risky), traps never. The bubble wand and leaf blower help by what they set off where they land.
        private static readonly ItemSpec[] Items =
        {
            new ItemSpec("Pillow", ItemKind.Soft, PrimitiveType.Cube, new Vector3(0.4f, 0.15f, 0.3f), 0.3f, new Color(1f, 0.75f, 0.85f), 0.9f, hoopAssist: 0.6f),
            new ItemSpec("BeachBall", ItemKind.Soft, PrimitiveType.Sphere, Vector3.one * 0.4f, 0.15f, new Color(0.3f, 0.9f, 1f), 1f, true, hoopAssist: 0.8f),
            new ItemSpec("RubberDuck", ItemKind.Soft, PrimitiveType.Cube, new Vector3(0.15f, 0.15f, 0.2f), 0.1f, new Color(1f, 0.9f, 0.1f), 0.7f, hoopAssist: 0.5f),
            new ItemSpec("FryingPan", ItemKind.Heavy, PrimitiveType.Cylinder, new Vector3(0.3f, 0.02f, 0.3f), 1.5f, new Color(0.2f, 0.2f, 0.2f), 1.4f, hoopAssist: 0.3f),
            new ItemSpec("Watermelon", ItemKind.Heavy, PrimitiveType.Sphere, new Vector3(0.35f, 0.28f, 0.28f), 3f, new Color(0.1f, 0.6f, 0.2f), 1.6f, hoopAssist: 0.3f),
            new ItemSpec("Darts", ItemKind.Trap, PrimitiveType.Capsule, new Vector3(0.04f, 0.1f, 0.04f), 0.05f, new Color(0.9f, 0.1f, 0.1f), 0f),
            // The studio's own food props (Drive: Shiesty Assets/Props/Food), scaled up to toy-scale throwables.
            new ItemSpec("Donut", ItemKind.Soft, PrimitiveType.Cube, Vector3.one * 1.6f, 0.2f, Color.white, 0.8f, modelName: "Donut_Visual", hoopAssist: 0.5f),
            new ItemSpec("Burger", ItemKind.Soft, PrimitiveType.Cube, Vector3.one * 1.8f, 0.35f, Color.white, 1f, modelName: "Burger_Visual", hoopAssist: 0.5f),
            new ItemSpec("BubbleWand", ItemKind.Soft, PrimitiveType.Capsule, new Vector3(0.08f, 0.2f, 0.08f), 0.15f, ArtPalette.CandyPink, 0.6f, hoopAssist: 0.4f, effect: ItemEffect.Updraft),
            new ItemSpec("LeafBlower", ItemKind.Soft, PrimitiveType.Cube, new Vector3(0.25f, 0.3f, 0.6f), 1.2f, ArtPalette.Climbable, 0.8f, hoopAssist: 0.3f, effect: ItemEffect.HoopGust),
        };

        public static NetworkObject BuildAvatar(PrototypeConfigSet configs)
        {
            var root = new GameObject("PlayerAvatar");
            root.AddComponent<NetworkObject>();
            var avatar = root.AddComponent<NetworkAvatar>();

            Material skin = PlaceholderAssets.Material("AvatarSkin", new Color(0.7f, 0.7f, 0.7f));
            Material glove = PlaceholderAssets.Material("Glove", Color.white);

            // Spiky head and body hitboxes (gameplay: the balloon leaks or pops on the porcupine's quills).
            Transform head = Hitbox("Head", root.transform, new Vector3(0f, 1.6f, 0f), avatar, configs.Slap, out _);
            var headCollider = head.gameObject.AddComponent<SphereCollider>();
            headCollider.radius = 0.14f;
            headCollider.isTrigger = true;

            // Matches the porcupine torso (eyes - 0.14 m down to eyes - 0.6 m).
            Transform body = Hitbox("Body", root.transform, new Vector3(0f, 1.15f, 0f), avatar, configs.Slap, out _);
            var bodyCollider = body.gameObject.AddComponent<CapsuleCollider>();
            bodyCollider.radius = 0.21f;
            bodyCollider.height = 0.52f;
            bodyCollider.center = new Vector3(0f, 0.08f, 0f);
            bodyCollider.isTrigger = true;

            // Every player is the same porcupine: a skinned gorilla-locomotion body on the body transform (player's yaw),
            // origin at the eyes, whose arms reach the gloves. Its paw mitts draw the gloves, so they get no sphere.
            GorillaBodyRig bodyRig = GorillaBodyBuilder.Build(body, head.localPosition.y - body.localPosition.y);
            TextMesh nameTag = PlaceholderShapes.Text("NameTag", head, new Vector3(0f, 0.45f, 0f), 0.012f, string.Empty, Color.white);

            // Gloves: the only safe part.
            Transform leftHand = Glove("LeftHand", root.transform, bodyRig == null ? glove : null);
            Transform rightHand = Glove("RightHand", root.transform, bodyRig == null ? glove : null);
            if (bodyRig != null)
            {
                bodyRig.SetTargets(head, leftHand, rightHand);
            }

            avatar.Configure(head, body, leftHand, rightHand, System.Array.Empty<Renderer>(), skin);
            avatar.ConfigureBody(bodyRig, nameTag);
            AddVoice(root, head);

            PlaceholderShapes.SetLayerRecursively(root, GameLayers.IgnoreRaycast);
            return PlaceholderAssets.SavePrefab(root, "PlayerAvatar");
        }

        /// <summary>
        /// The giant roaming mischief maker: a random roster character at RoamerScale, solid (you can climb it, the balloon
        /// bounces off it), owned by the master client like the balloon.
        /// </summary>
        public static NetworkObject BuildRoamer(PrototypeConfigSet configs, Vector3[] wanderPoints, Vector3[] avoidCircles, Vector4 bounds)
        {
            float height = 1.6f * configs.Roster.RoamerScale;
            var root = new GameObject("MischiefMaker");
            var networkObject = root.AddComponent<NetworkObject>();
            root.AddComponent<NetworkTransform>();
            var roamer = root.AddComponent<MischiefMaker>();

            var body = root.AddComponent<CapsuleCollider>();
            body.height = height;
            body.radius = 0.45f * configs.Roster.RoamerScale;
            body.center = new Vector3(0f, height / 2f, 0f);

            var visualRoot = new GameObject("VisualRoot").transform;
            visualRoot.SetParent(root.transform, false);
            var model = new GameObject("CharacterModel");
            model.transform.SetParent(visualRoot, false);
            var visual = model.AddComponent<CharacterVisual>();
            visual.Configure(PlaceholderAssets.Material("CharacterPlaceholder", Color.white, 0.3f));

            TextMesh alert = PlaceholderShapes.Text("Alert", root.transform, new Vector3(0f, height + 0.9f, 0f), 0.12f, "!", ArtPalette.Hazard);
            alert.fontStyle = FontStyle.Bold;

            // The slap arm: a mitt and a forearm the roamer animates from its shoulder (visual only, no colliders).
            Material mitt = PlaceholderAssets.Material("RoamerMitt", ArtPalette.Timber, 0.25f);
            Transform slapArm = PlaceholderShapes.Primitive(PrimitiveType.Cylinder, "SlapArm", visualRoot, new Vector3(0.3f * height, 0.55f * height, 0f),
                new Vector3(0.07f * height, 0.15f * height, 0.07f * height), mitt, false).transform;
            Transform slapHand = PlaceholderShapes.Primitive(PrimitiveType.Sphere, "SlapHand", visualRoot, new Vector3(0.3f * height, 0.42f * height, 0f),
                Vector3.one * (0.17f * height), mitt, false).transform;

            roamer.Configure(configs.Roster, visual, visualRoot, alert, slapHand, slapArm, wanderPoints, avoidCircles, bounds);
            PlaceholderAssets.SetNetworkFlags(networkObject, SharedObjectFlags, NeverDestroyOnLeave | NetworkObjectFlags.AllowStateAuthorityOverride);
            return PlaceholderAssets.SavePrefab(root, "MischiefMaker");
        }

        public static NetworkObject BuildBalloon(PrototypeConfigSet configs)
        {
            var root = new GameObject("Balloon");
            // A real Rigidbody synced with Fusion 2.1 Forecast Physics: extrapolated into local time on every client.
            var collider = root.AddComponent<SphereCollider>();
            collider.radius = configs.Balloon.Radius;
            collider.sharedMaterial = BalloonPhysicsMaterial(configs.Balloon.Bounciness);

            var body = root.AddComponent<Rigidbody>();
            body.mass = 0.2f;
            body.useGravity = false;          // BalloonPhysics applies its own floaty gravity and drag
            body.linearDamping = 0f;
            body.angularDamping = 1f;
            body.interpolation = RigidbodyInterpolation.Interpolate; // required by Forecast Physics
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            var networkObject = root.AddComponent<NetworkObject>();
            var networkTransform = root.AddComponent<NetworkTransform>();
            PhysicsSettings forecast = networkTransform.PhysicsSettings;
            forecast.ForecastEnabled = true;
            forecast.GravityForecast = GravityForecast.None; // custom gravity: extrapolate velocity only
            networkTransform.PhysicsSettings = forecast;
            var balloon = root.AddComponent<NetworkBalloon>();

            Material rubber = PlaceholderAssets.Material("Balloon", Color.white);
            float diameter = configs.Balloon.Radius * 2f;
            // Everything you see sits under "View", so the slapper's client can fly it ahead of the network (a predicted
            // hit, NetworkBalloon.PredictHit) without touching the networked body or its collider.
            Transform view = new GameObject("View").transform;
            view.SetParent(root.transform, false);
            GameObject visual = PlaceholderShapes.Primitive(PrimitiveType.Sphere, "Visual", view, Vector3.zero, Vector3.one * diameter, rubber, false);
            PlaceholderShapes.Primitive(PrimitiveType.Cylinder, "String", view, new Vector3(0f, -0.45f, 0f), new Vector3(0.01f, 0.2f, 0.01f), PlaceholderAssets.Material("String", Color.white), false);
            TextMesh rallyText = PlaceholderShapes.Text("RallyText", view, new Vector3(0f, 0.5f, 0f), 0.04f, string.Empty, Color.white);

            var hitAudio = root.AddComponent<AudioSource>();
            hitAudio.playOnAwake = false;
            AudioClip[] slaps =
            {
                GeneratedAudio.BalloonSlap("Balloon_Slap_1", 0),
                GeneratedAudio.BalloonSlap("Balloon_Slap_2", 1),
                GeneratedAudio.BalloonSlap("Balloon_Slap_3", 2),
            };
            hitAudio.clip = slaps[0];
            hitAudio.spatialBlend = 1f;
            hitAudio.rolloffMode = AudioRolloffMode.Linear;
            hitAudio.minDistance = 1.5f;
            hitAudio.maxDistance = 30f;
            hitAudio.dopplerLevel = 0f;
            AddSoundLevel(hitAudio, SoundCategory.Effects, 1f); // slaps and pop play through it (PlayOneShot)

            balloon.Configure(configs.Balloon, configs.Slap, visual.GetComponent<Renderer>(), rallyText, rubber, hitAudio, slaps);
            balloon.ConfigurePopSound(GeneratedAudio.BalloonPop("Balloon_Pop"));
            AddGroundShadow(root);
            PlaceholderAssets.SetNetworkFlags(networkObject, SharedObjectFlags, NeverDestroyOnLeave | NetworkObjectFlags.AllowStateAuthorityOverride);
            PlaceholderShapes.SetLayerRecursively(root, GameLayers.Balloon);
            return PlaceholderAssets.SavePrefab(root, "Balloon");
        }

        private static PhysicsMaterial BalloonPhysicsMaterial(float bounciness)
        {
            string path = $"{PlaceholderAssets.MaterialFolder}/BalloonBounce.physicMaterial";
            var material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (material == null)
            {
                material = new PhysicsMaterial("BalloonBounce");
                AssetDatabase.CreateAsset(material, path);
            }

            material.bounciness = bounciness;
            material.bounceCombine = PhysicsMaterialCombine.Maximum;
            material.dynamicFriction = 0.1f;
            material.staticFriction = 0.1f;
            material.frictionCombine = PhysicsMaterialCombine.Minimum;
            EditorUtility.SetDirty(material);
            return material;
        }

        public static NetworkObject BuildGameState(PrototypeConfigSet configs)
        {
            var root = new GameObject("GameState");
            var networkObject = root.AddComponent<NetworkObject>();
            var gameState = root.AddComponent<GameState>();
            gameState.Configure(configs.Run);
            AudioSource winAudio = Sound(root, GeneratedAudio.WinFanfare("Win_Fanfare"), false, 0f, 0.8f); // 2D: everyone hears it
            // Its own child: each SoundLevel drives the one AudioSource on its GameObject.
            var whistle = new GameObject("TimeUpWhistle");
            whistle.transform.SetParent(root.transform, false);
            AudioSource timeUpAudio = Sound(whistle, GeneratedAudio.RefereeWhistle("Whistle_TimeUp"), false, 0f, 0.85f); // 2D
            gameState.ConfigureSounds(winAudio, timeUpAudio);
            PlaceholderAssets.SetNetworkFlags(networkObject, SharedObjectFlags, NeverDestroyOnLeave | NetworkObjectFlags.AllowStateAuthorityOverride);
            return PlaceholderAssets.SavePrefab(root, "GameState");
        }

        public static NetworkObject[] BuildItems(PrototypeConfigSet configs)
        {
            var bouncy = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>($"{PlaceholderAssets.MaterialFolder}/Bouncy.physicMaterial");
            if (bouncy == null)
            {
                bouncy = new PhysicsMaterial("Bouncy") { bounciness = 0.8f, bounceCombine = PhysicsMaterialCombine.Maximum };
                AssetDatabase.CreateAsset(bouncy, $"{PlaceholderAssets.MaterialFolder}/Bouncy.physicMaterial");
            }

            var prefabs = new NetworkObject[Items.Length];
            for (int i = 0; i < Items.Length; i++)
            {
                ItemSpec spec = Items[i];
                ItemDefinition definition = LoadOrCreateDefinition(spec);

                GameObject item = AssetDatabase.LoadAssetAtPath<GameObject>($"{ItemModelFolder}/{spec.Name}.fbx") != null
                    ? OwnModelItem(spec)
                    : spec.ModelName != null
                    ? ModelItem(spec)
                    : PlaceholderShapes.Primitive(spec.Shape, spec.Name, null, Vector3.zero, spec.Scale, PlaceholderAssets.Material("Item" + spec.Name, spec.Color), true);
                if (spec.IsBouncy)
                {
                    item.GetComponent<Collider>().sharedMaterial = bouncy;
                }

                var networkObject = item.AddComponent<NetworkObject>();
                var itemTransform = item.AddComponent<NetworkTransform>();
                PhysicsSettings forecast = itemTransform.PhysicsSettings;
                forecast.ForecastEnabled = true; // thrown items extrapolate into local time on every client
                itemTransform.PhysicsSettings = forecast;
                var body = item.AddComponent<Rigidbody>();
                body.mass = definition.Mass;
                body.interpolation = RigidbodyInterpolation.Interpolate; // required by Forecast Physics
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                PlaceholderShapes.SetLayerRecursively(item, GameLayers.Item); // before the effect zone, which has its own layer
                var networkItem = item.AddComponent<NetworkItem>();
                networkItem.Configure(definition, configs.Knockback);
                if (definition.Effect != ItemEffect.None)
                {
                    networkItem.ConfigureEffect(EffectZone(item.transform, definition.Effect));
                }

                PlaceholderAssets.SetNetworkFlags(networkObject, ItemFlags, NeverDestroyOnLeave);
                prefabs[i] = PlaceholderAssets.SavePrefab(item, "Item_" + spec.Name);
            }

            return prefabs;
        }

        /// <summary>An item whose visual is an imported model: box collider fitted to the scaled model, colour atlas material.</summary>
        private static GameObject ModelItem(in ItemSpec spec)
        {
            var root = new GameObject(spec.Name);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/GameJam/Art/Props/Food/{spec.ModelName}.fbx");
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            visual.name = "Visual";
            visual.transform.localScale = spec.Scale;

            Material atlas = PlaceholderAssets.Material("FoodAtlas", Color.white, 0.25f,
                AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/GameJam/Art/Textures/T_ColorAtlas_v1.png"));
            foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>())
            {
                renderer.sharedMaterial = atlas;
            }

            Bounds bounds = visual.GetComponentInChildren<Renderer>().bounds;
            visual.transform.localPosition = -bounds.center; // centre the model on the item's pivot
            var box = root.AddComponent<BoxCollider>();
            box.size = bounds.size;
            return root;
        }

        /// <summary>
        /// How each item sits in the hand, measured from Art/Props/Items (built in Unity axes, origin at the bounds centre).
        /// The controller grip's +Z points out of the fist: business ends go there. Round items sit in front of the palm.
        /// </summary>
        private static void ApplyHoldPose(ItemDefinition definition, string name)
        {
            (Vector3 offset, Vector3 euler, Vector3 emitter) = name switch
            {
                // Top handle in the fist (model 0, 0.12, -0.15), nozzle (tube end z 0.30) pointing forward.
                "LeafBlower" => (new Vector3(0f, -0.12f, 0.15f), Vector3.zero, new Vector3(0f, -0.064f, 0.32f)),
                // Stick along +Y turned to point out of the fist; the ring (y 0.161) leads.
                "BubbleWand" => (new Vector3(0f, 0f, 0.08f), new Vector3(90f, 0f, 0f), new Vector3(0f, 0.161f, 0f)),
                // Handle (z +0.164) turned into the fist, pan out in front.
                "FryingPan" => (new Vector3(0f, 0f, 0.16f), new Vector3(0f, 180f, 0f), Vector3.zero),
                // Tip (+Y) pointing out of the fist, ready to throw.
                "Darts" => (new Vector3(0f, 0f, 0.05f), new Vector3(90f, 0f, 0f), Vector3.zero),
                "RubberDuck" => (new Vector3(0f, 0f, 0.1f), Vector3.zero, Vector3.zero),
                "BeachBall" => (new Vector3(0f, 0f, 0.22f), Vector3.zero, Vector3.zero),
                "Watermelon" => (new Vector3(0f, 0f, 0.2f), Vector3.zero, Vector3.zero),
                "Pillow" => (new Vector3(0f, 0f, 0.16f), Vector3.zero, Vector3.zero),
                _ => (new Vector3(0f, 0f, 0.12f), Vector3.zero, Vector3.zero),
            };
            definition.HoldOffset = offset;
            definition.HoldEuler = euler;
            definition.EffectEmitter = emitter;
        }

        private static ItemDefinition LoadOrCreateDefinition(in ItemSpec spec)
        {
            string path = $"{PlaceholderAssets.ItemConfigFolder}/Item_{spec.Name}.asset";
            var definition = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<ItemDefinition>();
                definition.DisplayName = ObjectNames.NicifyVariableName(spec.Name);
                definition.Kind = spec.Kind;
                definition.BalloonPower = spec.BalloonPower;
                definition.Mass = spec.Mass;
                AssetDatabase.CreateAsset(definition, path);
            }

            // Hold pose and effect emitter: fitted to the model, so always rewritten (not tuning).
            ApplyHoldPose(definition, spec.Name);

            // Seed the hoop-helping fields added later without overwriting tuning already done on the asset.
            bool changed = true;
            if (definition.HoopAssist <= 0f && spec.HoopAssist > 0f)
            {
                definition.HoopAssist = spec.HoopAssist;
                changed = true;
            }

            if (definition.Effect == ItemEffect.None && spec.Effect != ItemEffect.None)
            {
                definition.Effect = spec.Effect;
                bool isGust = spec.Effect == ItemEffect.HoopGust;
                definition.EffectSeconds = 6f;
                definition.EffectStrength = isGust ? 3.5f : 3f; // balloon gravity is 1: an updraft of 3 lifts it at ~1.7 m/s
                definition.EffectWidth = isGust ? 2.6f : 1.8f;
                definition.EffectLength = isGust ? 9f : 7f;      // 7 m of bubbles reach the tree and fence hoops from below
                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(definition);
            }

            return definition;
        }

        /// <summary>An item modelled for this game (Art/Props/Items): keeps its own coloured materials, collider fitted to it.</summary>
        private static GameObject OwnModelItem(in ItemSpec spec)
        {
            var root = new GameObject(spec.Name);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{ItemModelFolder}/{spec.Name}.fbx");
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            visual.name = "Visual";

            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
                GlossyToy(renderer, spec.Name, spec.Name == "Pillow" ? 0.22f : 0.58f); // fabric stays soft
            }

            visual.transform.localPosition = -bounds.center; // centre the model on the item pivot
            if (spec.Shape == PrimitiveType.Sphere)
            {
                root.AddComponent<SphereCollider>().radius = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
            }
            else
            {
                root.AddComponent<BoxCollider>().size = bounds.size;
            }

            return root;
        }

        /// <summary>
        /// Items are toys: each of the model's materials becomes a project material of the same colour (and texture) with a
        /// glossy plastic finish, so a sky highlight runs over them. Costs nothing extra to render.
        /// </summary>
        private static void GlossyToy(Renderer renderer, string itemName, float smoothness)
        {
            Material[] slots = renderer.sharedMaterials;
            for (int i = 0; i < slots.Length; i++)
            {
                Material source = slots[i];
                if (source == null)
                {
                    continue;
                }

                slots[i] = PlaceholderAssets.Material($"Item_{itemName}_{source.name}", source.color, smoothness, source.mainTexture as Texture2D,
                    source.mainTextureScale);
            }

            renderer.sharedMaterials = slots;
        }

        /// <summary>
        /// The wind an effect item sets off where it lands: a trigger + WindVolume (read by the balloon contact check) and
        /// particles along its forward axis: bubbles for an updraft, leaves and air streaks for a gust. Starts off;
        /// NetworkItem detaches it on spawn and switches it on from networked state.
        /// </summary>
        private static ItemEffectZone EffectZone(Transform item, ItemEffect effect)
        {
            var zone = new GameObject("EffectZone");
            zone.transform.SetParent(item, false);
            var trigger = zone.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.enabled = false;
            var wind = zone.AddComponent<WindVolume>();

            var emitter = new GameObject("Particles");
            emitter.transform.SetParent(zone.transform, false);
            var particles = emitter.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            bool isBubbles = effect == ItemEffect.Updraft;

            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 160;
            main.startLifetime = 2.5f;
            main.startSpeed = 2.5f;
            // Bubbles: soft pink-white blobs. Gust: big pale air streaks (small orange specks were hard to see).
            main.startSize = isBubbles ? new ParticleSystem.MinMaxCurve(0.12f, 0.32f) : new ParticleSystem.MinMaxCurve(0.18f, 0.34f);
            Color a = isBubbles ? Color.white : Color.white;
            Color b = isBubbles ? ArtPalette.CandyPink : ArtPalette.Window;
            a.a = isBubbles ? 0.55f : 0.75f;
            b.a = a.a;
            main.startColor = new ParticleSystem.MinMaxGradient(a, b);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = isBubbles ? 28f : 70f;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box; // emits along the zone forward axis
            shape.scale = new Vector3(1.5f, 1.5f, 0.1f);

            ParticleSystem.NoiseModule noise = particles.noise;
            noise.enabled = true;
            noise.strength = isBubbles ? 0.35f : 0.6f;
            noise.frequency = 0.6f;

            ParticleSystem.ColorOverLifetimeModule fade = particles.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;

            var particleRenderer = emitter.GetComponent<ParticleSystemRenderer>();
            particleRenderer.sharedMaterial = EffectParticleMaterial();
            particleRenderer.renderMode = isBubbles ? ParticleSystemRenderMode.Billboard : ParticleSystemRenderMode.Stretch;
            particleRenderer.lengthScale = 2.5f;
            particleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            particleRenderer.receiveShadows = false;

            var effectZone = zone.AddComponent<ItemEffectZone>();
            if (isBubbles)
            {
                StyleAsBubbles(particles);
            }
            else
            {
                // Same reach as before (~6 m), but as real-looking wind: long thin wisps and a few tumbling leaves.
                main.startSpeed = 4f;
                main.startLifetime = 1.6f;
                StyleAsWind(particles, 22f, 1.2f);
                NozzleBlast(particles.transform);
            }

            AudioClip loop = isBubbles ? GeneratedAudio.BubbleLoop("Item_BubbleLoop") : GeneratedAudio.WindLoop("Wind_Loop");
            effectZone.Configure(trigger, wind, particles, Sound(zone, loop, true, 18f, isBubbles ? 0.7f : 0.6f));
            PlaceholderShapes.SetLayerRecursively(zone, GameLayers.IgnoreRaycast);
            return effectZone;
        }

        /// <summary>
        /// An always-on stream of pale air streaks for set-piece wind (a box fan): emitted from the back face of a wind zone
        /// (local -Z) and flowing along its +Z for its whole length, so players can see where the wind blows.
        /// </summary>
        public static ParticleSystem AirStream(Transform windZone, Vector3 zoneSize, float speed)
        {
            // A sibling, not a child: trigger boxes are scaled to their size, which would scale the emitter's offset too.
            var emitter = new GameObject("AirStream");
            emitter.transform.SetParent(windZone.parent, false);
            emitter.transform.SetPositionAndRotation(windZone.position + windZone.rotation * new Vector3(0f, 0f, -zoneSize.z * 0.5f), windZone.rotation);
            var particles = emitter.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.playOnAwake = true;
            main.prewarm = true; // already flowing when the scene starts
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 220;
            main.startSpeed = speed;
            main.startLifetime = zoneSize.z / speed;
            main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.34f);
            Color a = Color.white;
            Color b = ArtPalette.Window;
            a.a = 0.7f;
            b.a = 0.7f;
            main.startColor = new ParticleSystem.MinMaxGradient(a, b);

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 55f;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(zoneSize.x * 0.85f, zoneSize.y * 0.85f, 0.1f);

            ParticleSystem.NoiseModule noise = particles.noise;
            noise.enabled = true;
            noise.strength = 0.4f;
            noise.frequency = 0.6f;

            ParticleSystem.ColorOverLifetimeModule fade = particles.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;

            var particleRenderer = emitter.GetComponent<ParticleSystemRenderer>();
            particleRenderer.sharedMaterial = EffectParticleMaterial();
            particleRenderer.renderMode = ParticleSystemRenderMode.Stretch;
            particleRenderer.lengthScale = 3f;
            particleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            particleRenderer.receiveShadows = false;
            StyleAsWind(particles, 18f, zoneSize.x * 0.85f);
            particles.Play();
            Sound(emitter, GeneratedAudio.WindLoop("Wind_Loop"), true, 16f, 0.5f, playOnAwake: true);
            return particles;
        }

        /// <summary>
        /// Soap bubbles: fewer and bigger, each a clear sphere with a shimmering rim and a highlight (T_Bubble, never rotated
        /// so the highlight stays top left), swelling as it leaves the ring, wobbling as it drifts, then popping in a few
        /// sparkles (a death sub-emitter) instead of fading out.
        /// </summary>
        private static void StyleAsBubbles(ParticleSystem particles)
        {
            ParticleSystem.MainModule main = particles.main;
            main.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.36f);
            main.startColor = new ParticleSystem.MinMaxGradient(Color.white, new Color(1f, 0.94f, 1f, 1f));
            main.startRotation = 0f;
            main.maxParticles = 80;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 18f;

            ParticleSystem.NoiseModule noise = particles.noise;
            noise.strength = 0.45f;
            noise.frequency = 0.8f;
            noise.quality = ParticleSystemNoiseQuality.Low;

            ParticleSystem.SizeOverLifetimeModule grow = particles.sizeOverLifetime;
            grow.enabled = true;
            grow.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.45f), new Keyframe(0.18f, 1f), new Keyframe(1f, 1.08f)));

            ParticleSystem.ColorOverLifetimeModule fade = particles.colorOverLifetime;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.08f), new GradientAlphaKey(1f, 0.97f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;

            var particleRenderer = particles.GetComponent<ParticleSystemRenderer>();
            particleRenderer.sharedMaterial = BubbleParticleMaterial();
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.sortMode = ParticleSystemSortMode.Distance;

            // The pop: a few sparkles flying off where each bubble ends.
            var popObject = new GameObject("BubblePops");
            popObject.transform.SetParent(particles.transform, false);
            var pops = popObject.AddComponent<ParticleSystem>();
            pops.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule popMain = pops.main;
            popMain.loop = false;
            popMain.playOnAwake = false;
            popMain.simulationSpace = ParticleSystemSimulationSpace.World;
            popMain.maxParticles = 300;
            popMain.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
            popMain.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
            popMain.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.05f);
            popMain.startColor = new ParticleSystem.MinMaxGradient(Color.white, Color.Lerp(Color.white, ArtPalette.CandyBlue, 0.4f));
            ParticleSystem.EmissionModule popEmission = pops.emission;
            popEmission.rateOverTime = 0f;
            popEmission.SetBursts(new[] { new ParticleSystem.Burst(0f, 5) });
            ParticleSystem.ShapeModule popShape = pops.shape;
            popShape.shapeType = ParticleSystemShapeType.Sphere;
            popShape.radius = 0.1f;
            ParticleSystem.SizeOverLifetimeModule popShrink = pops.sizeOverLifetime;
            popShrink.enabled = true;
            popShrink.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            var popRenderer = popObject.GetComponent<ParticleSystemRenderer>();
            popRenderer.sharedMaterial = EffectParticleMaterial();
            popRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            popRenderer.receiveShadows = false;

            ParticleSystem.SubEmittersModule subEmitters = particles.subEmitters;
            subEmitters.enabled = true;
            subEmitters.AddSubEmitter(pops, ParticleSystemSubEmitterType.Death, ParticleSystemSubEmitterProperties.InheritNothing);
        }

        /// <summary>
        /// The leaf blower's nozzle: soft puffs of air bursting out and spreading right at the tip, so the blast reads from
        /// the holder's view before the wisps get going. Plays with its parent emitter.
        /// </summary>
        private static void NozzleBlast(Transform emitter)
        {
            var blastObject = new GameObject("NozzleBlast");
            blastObject.transform.SetParent(emitter, false);
            var blast = blastObject.AddComponent<ParticleSystem>();
            blast.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = blast.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 40;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.55f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 4.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.2f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            Color white = Color.white;
            white.a = 0.4f;
            Color pale = ArtPalette.Window;
            pale.a = 0.3f;
            main.startColor = new ParticleSystem.MinMaxGradient(white, pale);
            ParticleSystem.EmissionModule emission = blast.emission;
            emission.rateOverTime = 30f;
            ParticleSystem.ShapeModule shape = blast.shape;
            shape.shapeType = ParticleSystemShapeType.Cone; // along the emitter's +Z
            shape.angle = 14f;
            shape.radius = 0.05f;
            ParticleSystem.LimitVelocityOverLifetimeModule slow = blast.limitVelocityOverLifetime;
            slow.enabled = true;
            slow.limit = 0.6f;
            slow.dampen = 0.12f; // the puff spreads and stalls, as air does
            ParticleSystem.SizeOverLifetimeModule grow = blast.sizeOverLifetime;
            grow.enabled = true;
            grow.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 4f));
            ParticleSystem.ColorOverLifetimeModule fade = blast.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            var blastRenderer = blastObject.GetComponent<ParticleSystemRenderer>();
            blastRenderer.sharedMaterial = EffectParticleMaterial();
            blastRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            blastRenderer.receiveShadows = false;
        }

        /// <summary>
        /// Turns an emitter flowing along its +Z into wind that reads like the real thing: invisible heads trailing long,
        /// thin, tapered wisps that wave on a scrolling noise field (gusty curls rather than straight lines), plus a child
        /// emitter of a few leaves tumbling through the stream. Keeps the emitter's speed, lifetime and shape.
        /// </summary>
        private static void StyleAsWind(ParticleSystem particles, float wispsPerSecond, float width)
        {
            ParticleSystem.MainModule main = particles.main;
            main.startSize = 0.02f;
            main.startColor = new Color(1f, 1f, 1f, 0f); // only the trails show
            main.startRotation = 0f;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = wispsPerSecond;

            ParticleSystem.NoiseModule noise = particles.noise;
            noise.enabled = true;
            noise.strength = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            noise.frequency = 0.35f;
            noise.scrollSpeed = 0.6f; // the field drifts, so wisps curl differently over time
            noise.damping = true;
            noise.octaveCount = 2;
            noise.quality = ParticleSystemNoiseQuality.Medium;

            ParticleSystem.TrailModule trails = particles.trails;
            trails.enabled = true;
            trails.mode = ParticleSystemTrailMode.PerParticle;
            trails.ratio = 1f;
            trails.lifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.4f); // fraction of the particle's life: 1-3 m wisps
            trails.minVertexDistance = 0.15f;
            trails.dieWithParticles = true;
            trails.sizeAffectsWidth = false;
            trails.inheritParticleColor = false;
            trails.textureMode = ParticleSystemTrailTextureMode.Stretch;
            trails.widthOverTrail = new ParticleSystem.MinMaxCurve(0.07f, new AnimationCurve(
                new Keyframe(0f, 0f), new Keyframe(0.3f, 1f), new Keyframe(0.7f, 0.8f), new Keyframe(1f, 0f)));
            var wisp = new Gradient();
            wisp.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(ArtPalette.Window, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.55f, 0.35f), new GradientAlphaKey(0.35f, 0.75f), new GradientAlphaKey(0f, 1f) });
            trails.colorOverTrail = wisp;
            var life = new Gradient();
            life.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            trails.colorOverLifetime = life;

            var particleRenderer = particles.GetComponent<ParticleSystemRenderer>();
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.trailMaterial = EffectParticleMaterial();

            // A few leaves caught in the stream.
            var leavesObject = new GameObject("WindLeaves");
            leavesObject.transform.SetParent(particles.transform, false);
            var leaves = leavesObject.AddComponent<ParticleSystem>();
            leaves.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule leafMain = leaves.main;
            leafMain.loop = true;
            leafMain.playOnAwake = main.playOnAwake;
            leafMain.prewarm = main.prewarm;
            leafMain.simulationSpace = ParticleSystemSimulationSpace.World;
            leafMain.maxParticles = 30;
            leafMain.startSpeed = main.startSpeed.constant * 0.85f;
            leafMain.startLifetime = main.startLifetime.constant;
            leafMain.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.17f);
            leafMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            leafMain.startColor = LeafColors();
            ParticleSystem.EmissionModule leafEmission = leaves.emission;
            leafEmission.rateOverTime = 3f;
            ParticleSystem.ShapeModule leafShape = leaves.shape;
            leafShape.shapeType = ParticleSystemShapeType.Box;
            leafShape.scale = new Vector3(width, width * 0.6f, 0.1f);
            ParticleSystem.RotationOverLifetimeModule spin = leaves.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-6f, 6f);
            ParticleSystem.NoiseModule leafNoise = leaves.noise;
            leafNoise.enabled = true;
            leafNoise.strength = 1f;
            leafNoise.frequency = 0.5f;
            ParticleSystem.ColorOverLifetimeModule leafFade = leaves.colorOverLifetime;
            leafFade.enabled = true;
            leafFade.color = life;
            var leafRenderer = leavesObject.GetComponent<ParticleSystemRenderer>();
            leafRenderer.sharedMaterial = LeafParticleMaterial();
            leafRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            leafRenderer.receiveShadows = false;
        }

        /// <summary>
        /// An AudioSource for a generated sound: 3D with linear rolloff out to <paramref name="maxDistance"/>, or 2D when it
        /// is 0. Never plays on awake unless it loops (a looping one is started by its owner, or plays at once if
        /// <paramref name="loop"/> is set and the owner never stops it).
        /// </summary>
        /// <summary>
        /// An AudioSource whose volume follows the game's mix (SoundLevel + Config/AudioConfig.asset): loops are Ambience,
        /// one-shots Effects (or pass a category). maxDistance 0 = 2D (heard the same everywhere). volume = this sound's
        /// own level within its category.
        /// </summary>
        public static AudioSource Sound(GameObject host, AudioClip clip, bool loop, float maxDistance, float volume, bool playOnAwake = false,
            SoundCategory? category = null)
        {
            var source = host.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = loop;
            source.playOnAwake = playOnAwake;
            source.dopplerLevel = 0f;
            source.spatialBlend = maxDistance > 0f ? 1f : 0f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = Mathf.Min(2f, maxDistance);
            source.maxDistance = Mathf.Max(maxDistance, 1f);
            AddSoundLevel(source, category ?? (loop ? SoundCategory.Ambience : SoundCategory.Effects), volume);
            return source;
        }

        public const string AudioConfigPath = "Assets/GameJam/Config/AudioConfig.asset";

        /// <summary>Puts a SoundLevel on a source so its volume follows Config/AudioConfig.asset.</summary>
        public static SoundLevel AddSoundLevel(AudioSource source, SoundCategory category, float volume)
        {
            AudioConfig config = PlaceholderAssets.LoadOrCreateConfig<AudioConfig>(AudioConfigPath);
            var level = source.gameObject.AddComponent<SoundLevel>();
            level.Configure(config, category, volume);
            return level;
        }

        /// <summary>Soft round transparent particle (URP Particles/Unlit with a generated radial blob).</summary>
        public static Material EffectParticleMaterial() => ParticleMaterial("EffectParticle", GeneratedTextures.RadialBlob("T_ParticleDot"));

        /// <summary>Leaf colours for wind and falling leaves: greens with the odd sun-dried yellow one (random per leaf).</summary>
        public static ParticleSystem.MinMaxGradient LeafColors()
        {
            var colors = new Gradient();
            Color light = Color.Lerp(ArtPalette.LawnDark, ArtPalette.Accent, 0.3f);
            Color dry = Color.Lerp(ArtPalette.Accent, ArtPalette.LawnDark, 0.3f);
            colors.SetKeys(new[] { new GradientColorKey(ArtPalette.LawnDark, 0.45f), new GradientColorKey(light, 0.85f), new GradientColorKey(dry, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            colors.mode = GradientMode.Fixed;
            return new ParticleSystem.MinMaxGradient(colors) { mode = ParticleSystemGradientMode.RandomColor };
        }

        /// <summary>Soap bubble particle: clear middle, shimmering rim, highlight.</summary>
        public static Material BubbleParticleMaterial() => ParticleMaterial("EffectBubble", GeneratedTextures.Bubble("T_Bubble"));

        /// <summary>Leaf-shaped particle, tinted by the particle colour.</summary>
        public static Material LeafParticleMaterial() => ParticleMaterial("EffectLeaf", GeneratedTextures.Leaf("T_Leaf"));

        /// <summary>A transparent URP Particles/Unlit material at Materials/Placeholder/<paramref name="name"/>.mat.</summary>
        public static Material ParticleMaterial(string name, Texture2D texture)
        {
            string path = $"{PlaceholderAssets.MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.SetTexture("_BaseMap", texture);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Transform Hitbox(string name, Transform parent, Vector3 localPosition, NetworkAvatar avatar, SlapConfig slapConfig, out AvatarHitbox hitbox)
        {
            var part = new GameObject(name);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.AddComponent<NetworkTransform>();
            hitbox = part.AddComponent<AvatarHitbox>();
            hitbox.Configure(avatar, slapConfig);
            return part.transform;
        }

        /// <summary>
        /// Proximity voice (GDD: Multiplayer and voice): VoiceNetworkObject + Recorder on the root, a 3D Speaker on
        /// the head so each voice comes from that player's mouth. AvatarVoice starts the mic after permission.
        /// </summary>
        private static void AddVoice(GameObject root, Transform head)
        {
            root.AddComponent<VoiceNetworkObject>();

            var recorder = root.AddComponent<Recorder>();
            recorder.RecordWhenJoined = false; // AvatarVoice starts it once the microphone permission is granted
            recorder.TransmitEnabled = true;
            recorder.RecordingEnabled = false;
            recorder.SamplingRate = POpusCodec.Enums.SamplingRate.Sampling48000; // Quest and PC mics run at 48 kHz: no resampling cost

            var mouth = new GameObject("VoiceSpeaker");
            mouth.transform.SetParent(head, false);
            var source = mouth.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 1.5f;
            source.maxDistance = 25f;
            source.dopplerLevel = 0f;
            var speaker = mouth.AddComponent<Speaker>();

            root.AddComponent<AvatarVoice>().Configure(recorder, speaker);
        }

        /// <summary>Blob shadow under the balloon (GDD Comfort). A quad that detaches to world space at runtime.</summary>
        private static void AddGroundShadow(GameObject balloon)
        {
            Material blob = PlaceholderAssets.TransparentUnlit("BalloonShadow", GeneratedTextures.RadialBlob("T_ShadowBlob"));
            GameObject quad = PlaceholderShapes.Primitive(PrimitiveType.Quad, "GroundShadow", balloon.transform, Vector3.down, Vector3.one * 0.6f, blob, false);
            var renderer = quad.GetComponent<Renderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            balloon.AddComponent<GameJam.Gameplay.BalloonGroundShadow>().Configure(quad.transform, renderer);
        }

        private static Transform Glove(string name, Transform parent, Material material)
        {
            var hand = new GameObject(name);
            hand.transform.SetParent(parent, false);
            hand.AddComponent<NetworkTransform>();
            if (material != null)
            {
                PlaceholderShapes.Primitive(PrimitiveType.Sphere, "Glove", hand.transform, Vector3.zero, Vector3.one * 0.12f, material, false);
            }

            return hand.transform;
        }
    }
}
