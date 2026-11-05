using Fusion;
using GameJam.Core;
using GameJam.Net;
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

            public ItemSpec(string name, ItemKind kind, PrimitiveType shape, Vector3 scale, float mass, Color color, float balloonPower, bool isBouncy = false)
            {
                Name = name;
                Kind = kind;
                Shape = shape;
                Scale = scale;
                Mass = mass;
                Color = color;
                BalloonPower = balloonPower;
                IsBouncy = isBouncy;
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
        };

        public static NetworkObject BuildAvatar(PrototypeConfigSet configs)
        {
            var root = new GameObject("PlayerAvatar");
            root.AddComponent<NetworkObject>();
            var avatar = root.AddComponent<NetworkAvatar>();

            Material skin = PlaceholderAssets.Material("AvatarSkin", new Color(0.7f, 0.7f, 0.7f));
            Material spikes = PlaceholderAssets.Material("Spike", new Color(0.9f, 0.1f, 0.1f));
            Material glove = PlaceholderAssets.Material("Glove", Color.white);
            Material eye = PlaceholderAssets.Material("Eye", Color.black);

            // Head with a spiky hood.
            Transform head = Hitbox("Head", root.transform, new Vector3(0f, 1.6f, 0f), avatar, configs.Slap, out _);
            head.gameObject.AddComponent<SphereCollider>().radius = 0.14f;
            head.GetComponent<SphereCollider>().isTrigger = true;
            GameObject headVisual = PlaceholderShapes.Primitive(PrimitiveType.Sphere, "HeadVisual", head, Vector3.zero, Vector3.one * 0.28f, skin, false);
            PlaceholderShapes.Primitive(PrimitiveType.Sphere, "EyeLeft", head, new Vector3(-0.05f, 0.03f, 0.12f), Vector3.one * 0.05f, eye, false);
            PlaceholderShapes.Primitive(PrimitiveType.Sphere, "EyeRight", head, new Vector3(0.05f, 0.03f, 0.12f), Vector3.one * 0.05f, eye, false);
            for (int i = 0; i < 5; i++)
            {
                Vector3 direction = Quaternion.Euler(-60f + i * 30f, 0f, 0f) * Vector3.back;
                PlaceholderShapes.Primitive(PrimitiveType.Cube, "HoodSpike", head, direction * 0.15f, new Vector3(0.04f, 0.04f, 0.12f), spikes, false, Quaternion.LookRotation(direction));
            }

            // Body with a spiky vest.
            Transform body = Hitbox("Body", root.transform, new Vector3(0f, 1.15f, 0f), avatar, configs.Slap, out _);
            var bodyCollider = body.gameObject.AddComponent<CapsuleCollider>();
            bodyCollider.radius = 0.2f;
            bodyCollider.height = 0.6f;
            bodyCollider.isTrigger = true;
            GameObject bodyVisual = PlaceholderShapes.Primitive(PrimitiveType.Capsule, "BodyVisual", body, Vector3.zero, new Vector3(0.4f, 0.3f, 0.35f), skin, false);
            for (int row = 0; row < 3; row++)
            {
                for (int column = -1; column <= 1; column++)
                {
                    PlaceholderShapes.Primitive(PrimitiveType.Cube, "BackSpike", body, new Vector3(column * 0.12f, 0.15f - row * 0.15f, -0.2f),
                        new Vector3(0.05f, 0.05f, 0.16f), spikes, false, Quaternion.Euler(-15f, 0f, 0f));
                }
            }

            // Gloves: the only safe part.
            Transform leftHand = Glove("LeftHand", root.transform, glove);
            Transform rightHand = Glove("RightHand", root.transform, glove);

            var tintRenderers = new[] { headVisual.GetComponent<Renderer>(), bodyVisual.GetComponent<Renderer>() };
            avatar.Configure(head, body, leftHand, rightHand, tintRenderers, skin);

            PlaceholderShapes.SetLayerRecursively(root, GameLayers.IgnoreRaycast);
            return PlaceholderAssets.SavePrefab(root, "PlayerAvatar");
        }

        public static NetworkObject BuildBalloon(PrototypeConfigSet configs)
        {
            var root = new GameObject("Balloon");
            var trigger = root.AddComponent<SphereCollider>();
            trigger.radius = configs.Balloon.Radius;
            trigger.isTrigger = true;
            var networkObject = root.AddComponent<NetworkObject>();
            root.AddComponent<NetworkTransform>();
            var balloon = root.AddComponent<NetworkBalloon>();

            Material rubber = PlaceholderAssets.Material("Balloon", Color.white);
            float diameter = configs.Balloon.Radius * 2f;
            GameObject visual = PlaceholderShapes.Primitive(PrimitiveType.Sphere, "Visual", root.transform, Vector3.zero, Vector3.one * diameter, rubber, false);
            PlaceholderShapes.Primitive(PrimitiveType.Cylinder, "String", root.transform, new Vector3(0f, -0.45f, 0f), new Vector3(0.01f, 0.2f, 0.01f), PlaceholderAssets.Material("String", Color.white), false);
            TextMesh rallyText = PlaceholderShapes.Text("RallyText", root.transform, new Vector3(0f, 0.5f, 0f), 0.04f, string.Empty, Color.white);

            balloon.Configure(configs.Balloon, configs.Slap, visual.GetComponent<Renderer>(), rallyText, rubber);
            PlaceholderAssets.SetNetworkFlags(networkObject, SharedObjectFlags, NeverDestroyOnLeave | NetworkObjectFlags.AllowStateAuthorityOverride);
            PlaceholderShapes.SetLayerRecursively(root, GameLayers.IgnoreRaycast);
            return PlaceholderAssets.SavePrefab(root, "Balloon");
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

                GameObject item = PlaceholderShapes.Primitive(spec.Shape, spec.Name, null, Vector3.zero, spec.Scale,
                    PlaceholderAssets.Material("Item" + spec.Name, spec.Color), true);
                if (spec.IsBouncy)
                {
                    item.GetComponent<Collider>().sharedMaterial = bouncy;
                }

                var networkObject = item.AddComponent<NetworkObject>();
                item.AddComponent<NetworkTransform>();
                var body = item.AddComponent<Rigidbody>();
                body.mass = definition.Mass;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                item.AddComponent<NetworkItem>().Configure(definition, configs.Knockback);

                PlaceholderAssets.SetNetworkFlags(networkObject, ItemFlags, NeverDestroyOnLeave);
                prefabs[i] = PlaceholderAssets.SavePrefab(item, "Item_" + spec.Name);
            }

            return prefabs;
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
