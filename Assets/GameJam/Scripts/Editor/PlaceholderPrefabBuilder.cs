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

            // Seed the hoop-helping fields added later without overwriting tuning already done on the asset.
            bool changed = false;
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
            main.startSize = isBubbles ? new ParticleSystem.MinMaxCurve(0.12f, 0.32f) : new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
            Color a = isBubbles ? Color.white : ArtPalette.LawnDark;
            Color b = isBubbles ? ArtPalette.CandyPink : ArtPalette.Climbable;
            a.a = isBubbles ? 0.55f : 0.9f;
            b.a = a.a;
            main.startColor = new ParticleSystem.MinMaxGradient(a, b);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = isBubbles ? 28f : 40f;

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
            effectZone.Configure(trigger, wind, particles);
            PlaceholderShapes.SetLayerRecursively(zone, GameLayers.IgnoreRaycast);
            return effectZone;
        }

        /// <summary>Soft round transparent particle (URP Particles/Unlit with a generated radial blob).</summary>
        private static Material EffectParticleMaterial()
        {
            string path = $"{PlaceholderAssets.MaterialFolder}/EffectParticle.mat";
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
            material.SetTexture("_BaseMap", GeneratedTextures.RadialBlob("T_ParticleDot"));
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
