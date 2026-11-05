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

            public ItemSpec(string name, ItemKind kind, PrimitiveType shape, Vector3 scale, float mass, Color color, float balloonPower, bool isBouncy = false,
                string modelName = null)
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
            }
        }

        private static readonly ItemSpec[] Items =
        {
            new ItemSpec("Pillow", ItemKind.Soft, PrimitiveType.Cube, new Vector3(0.4f, 0.15f, 0.3f), 0.3f, new Color(1f, 0.75f, 0.85f), 0.9f),
            new ItemSpec("BeachBall", ItemKind.Soft, PrimitiveType.Sphere, Vector3.one * 0.4f, 0.15f, new Color(0.3f, 0.9f, 1f), 1f, true),
            new ItemSpec("RubberDuck", ItemKind.Soft, PrimitiveType.Cube, new Vector3(0.15f, 0.15f, 0.2f), 0.1f, new Color(1f, 0.9f, 0.1f), 0.7f),
            new ItemSpec("FryingPan", ItemKind.Heavy, PrimitiveType.Cylinder, new Vector3(0.3f, 0.02f, 0.3f), 1.5f, new Color(0.2f, 0.2f, 0.2f), 1.4f),
            new ItemSpec("Watermelon", ItemKind.Heavy, PrimitiveType.Sphere, new Vector3(0.35f, 0.28f, 0.28f), 3f, new Color(0.1f, 0.6f, 0.2f), 1.6f),
            new ItemSpec("Darts", ItemKind.Trap, PrimitiveType.Capsule, new Vector3(0.04f, 0.1f, 0.04f), 0.05f, new Color(0.9f, 0.1f, 0.1f), 0f),
            // The studio's own food props (Drive: Shiesty Assets/Props/Food), scaled up to toy-scale throwables.
            new ItemSpec("Donut", ItemKind.Soft, PrimitiveType.Cube, Vector3.one * 1.6f, 0.2f, Color.white, 0.8f, modelName: "Donut_Visual"),
            new ItemSpec("Burger", ItemKind.Soft, PrimitiveType.Cube, Vector3.one * 1.8f, 0.35f, Color.white, 1f, modelName: "Burger_Visual"),
        };

        public static NetworkObject BuildAvatar(PrototypeConfigSet configs)
        {
            var root = new GameObject("PlayerAvatar");
            root.AddComponent<NetworkObject>();
            var avatar = root.AddComponent<NetworkAvatar>();

            Material skin = PlaceholderAssets.Material("AvatarSkin", new Color(0.7f, 0.7f, 0.7f));
            Material glove = PlaceholderAssets.Material("Glove", Color.white);

            // Spiky head and body hitboxes (gameplay: the balloon leaks or pops on them). Their look comes from the character.
            Transform head = Hitbox("Head", root.transform, new Vector3(0f, 1.6f, 0f), avatar, configs.Slap, out _);
            var headCollider = head.gameObject.AddComponent<SphereCollider>();
            headCollider.radius = 0.14f;
            headCollider.isTrigger = true;

            Transform body = Hitbox("Body", root.transform, new Vector3(0f, 1.15f, 0f), avatar, configs.Slap, out _);
            var bodyCollider = body.gameObject.AddComponent<CapsuleCollider>();
            bodyCollider.radius = 0.2f;
            bodyCollider.height = 0.6f;
            bodyCollider.isTrigger = true;

            // The character model rides on the body (follows the player's yaw); NetworkAvatar picks which one.
            var model = new GameObject("CharacterModel");
            model.transform.SetParent(body, false);
            var visual = model.AddComponent<CharacterVisual>();
            visual.Configure(PlaceholderAssets.Material("CharacterPlaceholder", Color.white, 0.3f));
            TextMesh nameTag = PlaceholderShapes.Text("NameTag", head, new Vector3(0f, 0.45f, 0f), 0.012f, string.Empty, Color.white);

            // Gloves: the only safe part.
            Transform leftHand = Glove("LeftHand", root.transform, glove);
            Transform rightHand = Glove("RightHand", root.transform, glove);

            avatar.Configure(head, body, leftHand, rightHand, System.Array.Empty<Renderer>(), skin);
            avatar.ConfigureCharacter(configs.Roster, visual, nameTag);
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

            roamer.Configure(configs.Roster, visual, visualRoot, alert, wanderPoints, avoidCircles, bounds);
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
            GameObject visual = PlaceholderShapes.Primitive(PrimitiveType.Sphere, "Visual", root.transform, Vector3.zero, Vector3.one * diameter, rubber, false);
            PlaceholderShapes.Primitive(PrimitiveType.Cylinder, "String", root.transform, new Vector3(0f, -0.45f, 0f), new Vector3(0.01f, 0.2f, 0.01f), PlaceholderAssets.Material("String", Color.white), false);
            TextMesh rallyText = PlaceholderShapes.Text("RallyText", root.transform, new Vector3(0f, 0.5f, 0f), 0.04f, string.Empty, Color.white);

            balloon.Configure(configs.Balloon, configs.Slap, visual.GetComponent<Renderer>(), rallyText, rubber);
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
            root.AddComponent<GameState>().Configure(configs.Run);
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

                GameObject item = spec.ModelName != null
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
                item.AddComponent<NetworkItem>().Configure(definition, configs.Knockback);

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

        private static ItemDefinition LoadOrCreateDefinition(in ItemSpec spec)
        {
            string path = $"{PlaceholderAssets.ItemConfigFolder}/Item_{spec.Name}.asset";
            var definition = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            if (definition != null)
            {
                return definition;
            }

            definition = ScriptableObject.CreateInstance<ItemDefinition>();
            definition.DisplayName = ObjectNames.NicifyVariableName(spec.Name);
            definition.Kind = spec.Kind;
            definition.BalloonPower = spec.BalloonPower;
            definition.Mass = spec.Mass;
            AssetDatabase.CreateAsset(definition, path);
            return definition;
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
            PlaceholderShapes.Primitive(PrimitiveType.Sphere, "Glove", hand.transform, Vector3.zero, Vector3.one * 0.12f, material, false);
            return hand.transform;
        }
    }
}
