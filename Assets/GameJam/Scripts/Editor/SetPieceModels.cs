using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GameJam.Editor
{
    /// <summary>
    /// Dresses the yard's gameplay primitives with real-looking set-piece models (Art/Set, built by
    /// tools/blender/scripts/build_set_pieces.py). Each model is placed at its piece's anchor; the primitives it stands for
    /// keep their colliders (so walking, climbing and balloon physics don't change) and only stop rendering. Moving pieces
    /// (merry-go-round, rotary clothesline arms) get the model parented to the mover so it moves with it. A piece whose FBX
    /// is missing keeps its primitives, so the yard always builds.
    /// </summary>
    public static class SetPieceModels
    {
        private const string Folder = "Assets/GameJam/Art/Set";

        private readonly struct Piece
        {
            public readonly string Model;
            public readonly Vector3 Anchor;
            public readonly float Radius;
            public readonly string[] Hides;

            public Piece(string model, Vector3 anchor, float radius, params string[] hides)
            {
                Model = model;
                Anchor = anchor;
                Radius = radius;
                Hides = hides;
            }
        }

        /// <summary>Places every available model; returns how many pieces were dressed.</summary>
        public static int Apply(Transform course)
        {
            Vector3 playground = BackyardCourseBuilder.PlaygroundCenter;
            Vector3 bbq = BackyardCourseBuilder.BbqCenter;
            Piece[] pieces =
            {
                // Anchors match BackyardCourseBuilder (BuildTree, BuildPool, BuildBbq, BuildPlayground, BuildWashingLine).
                new Piece("Tree", BackyardCourseBuilder.TreeCenter, 9f, "TreeTrunk", "BranchLow", "BranchMid", "BranchHigh", "Canopy"),
                new Piece("PaddlingPool", BackyardCourseBuilder.PoolCenter, 12f, "PoolRim"),
                new Piece("Grill", bbq + new Vector3(0f, 0f, -6f), 3f, "Grill", "GrillLeg"),
                new Piece("Trampoline", playground + new Vector3(0f, 0f, -6f), 2.5f, "Trampoline"),
                new Piece("RotaryPole", BackyardCourseBuilder.WashingLineCenter, 1f, "ClotheslinePole"),
            };

            int dressed = 0;
            foreach (Piece piece in pieces)
            {
                GameObject model = Load(piece.Model);
                if (model == null)
                {
                    continue;
                }

                // Hide first: some models share their primitive's name (Grill, Trampoline) and must not hide themselves.
                HideNear(course, piece.Anchor, piece.Radius, piece.Hides);
                Place(model, course, piece.Anchor, Quaternion.identity, true);
                dressed++;
            }

            dressed += DressMover(course, "MerryGoRound", "MerryGoRound", "MerryDisc", "MerryBar");
            dressed += DressMover(course, "SpinningClothesline", "RotaryArms", "LineA", "LineB");
            dressed += DressPickets(course);
            return dressed;
        }

        private static GameObject Load(string name) => AssetDatabase.LoadAssetAtPath<GameObject>($"{Folder}/{name}.fbx");

        private static GameObject Place(GameObject model, Transform parent, Vector3 position, Quaternion rotation, bool isStatic)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
            instance.transform.SetPositionAndRotation(position, rotation);
            foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
            {
                Object.DestroyImmediate(collider); // visual only: the primitives keep the gameplay colliders
            }

            if (isStatic)
            {
                foreach (Transform child in instance.GetComponentsInChildren<Transform>(true))
                {
                    GameObjectUtility.SetStaticEditorFlags(child.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
                }
            }

            return instance;
        }

        /// <summary>Stops rendering (keeps colliders of) the named primitives within range of a point, horizontally.</summary>
        private static void HideNear(Transform course, Vector3 anchor, float radius, string[] names)
        {
            var wanted = new HashSet<string>(names);
            foreach (Transform child in course)
            {
                Vector3 offset = child.position - anchor;
                offset.y = 0f;
                if (wanted.Contains(child.name) && offset.magnitude <= radius && child.TryGetComponent(out MeshRenderer renderer))
                {
                    renderer.enabled = false;
                }
            }
        }

        /// <summary>A moving piece: the model rides the mover, and the mover's own primitive parts stop rendering.</summary>
        private static int DressMover(Transform course, string moverName, string modelName, params string[] hides)
        {
            Transform mover = course.Find(moverName);
            GameObject model = Load(modelName);
            if (mover == null || model == null)
            {
                return 0;
            }

            GameObject instance = Place(model, mover, mover.position, mover.rotation, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            var wanted = new HashSet<string>(hides);
            foreach (MeshRenderer renderer in mover.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (wanted.Contains(renderer.name))
                {
                    renderer.enabled = false;
                }
            }

            return 1;
        }

        /// <summary>Every fence picket board (and its cap) becomes the pointed picket model, standing where the board stood.</summary>
        private static int DressPickets(Transform course)
        {
            GameObject model = Load("FencePicket");
            if (model == null)
            {
                return 0;
            }

            var pickets = new List<Transform>();
            foreach (Transform child in course)
            {
                if (child.name == "Picket" || child.name == "PicketCap")
                {
                    pickets.Add(child);
                }
            }

            foreach (Transform picket in pickets)
            {
                if (!picket.TryGetComponent(out MeshRenderer renderer))
                {
                    continue;
                }

                renderer.enabled = false;
                if (picket.name == "Picket")
                {
                    // The board box is centred half its height up; the model's origin is its base centre.
                    Vector3 basePosition = picket.position - Vector3.up * (picket.lossyScale.y * 0.5f);
                    Place(model, course, basePosition, picket.rotation, true);
                }
            }

            return 1;
        }
    }
}
