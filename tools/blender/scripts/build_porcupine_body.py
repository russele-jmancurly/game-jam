"""Builds the player body, a porcupine in natural colours, for gorilla locomotion, and exports it as a Unity FBX.

    tools/blender/blender.sh tools/blender/scripts/build_porcupine_body.py Assets/GameJam/Art/Characters/PorcupineBody.fbx [--blend out.blend]

Every player is this one porcupine (no per-player colours; name tags tell the crew apart). The mesh is our own: a
round, bottom-heavy body in dark brown fur with a lighter belly, a blunt dark snout, small round ears, and quills
(dark shafts, cream tips) swept back over the back, the head and a short tail. The padded paw mitts are the gloves,
the only part safe to touch the balloon with, so they are a light tan that reads clearly against the dark fur.
Only the skeleton layout and proportions follow the reference player rig the team supplied (UGPlayerModel): T-pose,
narrow shoulders just under the head, upper arm ~0.9x the forearm, a hand bone pointing forward with index, middle,
ring and thumb finger chains. Scale: shoulder to glove centre ~0.74 m. The paws are a padded palm with three stubby
fingers and a thumb skinned to those chains, so GorillaBodyRig can curl them into a fist with the grip button.

Rig (names are what GorillaBodyRig looks up in Unity), origin = the player's eyes:
    Body                                    torso, belly, back quills, tail (root)
      Head                                  pivots at the eyes, follows where the player looks
      UpperArm.L/R -> Forearm.L/R -> Hand.L/R
        Hand.L/R -> Index1-3, Middle1-3, Ring1-3, Thumb1-2 (.L/.R)   finger chains, curled by the grip
        Hand.L/R -> GloveCenter.L/R          non-deforming marker where the tracked hand (controller) sits

Two meshes share the armature: "Body" (torso + head: hidden in your own view) and "Arms" (arms + paws: shown to
everyone, you included). Material names (Unity maps them to its own URP materials): Fur, Belly, Face, Eye,
QuillBase, QuillTip, Paw.
"""
import math
import random
import os
import sys

sys.path.append(os.path.dirname(os.path.abspath(__file__)))

import bmesh  # noqa: E402
import bpy  # noqa: E402
from mathutils import Vector  # noqa: E402

from blender_common import clear_scene, emit, fail, script_args  # noqa: E402

args = script_args()
if not args:
    fail("usage: build_gorilla_body.py <out.fbx> [--blend out.blend]")
output = os.path.abspath(args[0])
blend_out = os.path.abspath(args[args.index("--blend") + 1]) if "--blend" in args else None

# --- Proportions (metres, Blender Z-up, faces -Y, origin = eyes) ------------------------------------------------------
HEAD_CENTER = Vector((0.0, 0.07, -0.02))  # the head sits behind and around the eyes
HEAD_RADIUS = 0.165
SHOULDER = Vector((0.12, 0.03, -0.22))    # x is mirrored per side
UPPER_ARM = 0.315
FOREARM = 0.35
HAND = 0.035                              # wrist -> knuckles
GLOVE_FORWARD = 0.075                     # wrist -> glove centre, along the hand
TORSO_TOP = -0.14
TORSO_BOTTOM = -0.57
ELBOW_BLEND = 0.07                        # half-width of the elbow skin blend
SHOULDER_BLEND = 0.06                     # where the upper arm starts to follow the body near the shoulder
# Paw: a padded palm, three stubby fingers stacked top to bottom (index next to the thumb) and a thumb on top, palm
# facing in. Each finger is a tube skinned across its three bones, so GorillaBodyRig can curl it into a fist.
KNUCKLE = 0.05                            # wrist -> finger roots, along the hand
FINGER_SEGMENT = 0.03                     # each of the three finger bones
FINGER_RADIUS = 0.021
FINGER_ROWS = (("Index", 0.034), ("Middle", -0.002), ("Ring", -0.038))  # height of each finger's root
THUMB_SEGMENT = 0.03
THUMB_RADIUS = 0.018
JOINT_BLEND = 0.012                       # half-width of the skin blend at each finger joint


def thumb_frame(wrist, sign):
    """Thumb root (top of the palm, toward the palm side) and direction (forward, up a little, toward the palm)."""
    palm = Vector((-sign, 0, 0))
    root = wrist + Vector((0, -0.02, 0.045)) + palm * 0.02
    return root, (Vector((0, -1, 0)) + Vector((0, 0, 0.45)) + palm * 0.35).normalized()

clear_scene()
scene = bpy.context.scene


def material(name, color):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    for node in mat.node_tree.nodes:
        if node.type == "BSDF_PRINCIPLED":
            node.inputs["Base Color"].default_value = (*color, 1.0)
            node.inputs["Roughness"].default_value = 0.8
    mat.diffuse_color = (*color, 1.0)
    return mat


# Natural North American porcupine colours: near-black brown fur, warmer belly, dark face, cream-tipped quills.
FUR = material("Fur", (0.20, 0.14, 0.09))
BELLY = material("Belly", (0.36, 0.26, 0.17))
FACE = material("Face", (0.13, 0.09, 0.07))
EYE = material("Eye", (0.02, 0.02, 0.02))
QUILL_BASE = material("QuillBase", (0.10, 0.07, 0.05))
QUILL_TIP = material("QuillTip", (0.93, 0.89, 0.80))
PAW = material("Paw", (0.80, 0.70, 0.56))
MATERIALS = [FUR, BELLY, FACE, EYE, QUILL_BASE, QUILL_TIP, PAW]
SLOT_FUR, SLOT_BELLY, SLOT_FACE, SLOT_EYE, SLOT_QUILL, SLOT_TIP, SLOT_PAW = range(len(MATERIALS))
rng = random.Random(7)  # fixed seed: the same quills every build


def smoothstep(e0, e1, x):
    t = max(0.0, min(1.0, (x - e0) / (e1 - e0)))
    return t * t * (3.0 - 2.0 * t)


class Part:
    """Geometry collected into one mesh object, with per-vertex bone weights and per-face material slots."""

    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.weights = []  # index-aligned with bm.verts: {bone: weight}

    def vert(self, co, weights):
        self.weights.append(dict(weights))
        return self.bm.verts.new(co)

    def face(self, verts, slot):
        f = self.bm.faces.new(verts)
        f.material_index = slot
        f.smooth = True

    def lathe(self, start, axis, ref, profile, segments, slot, weight_fn, squash=(1.0, 1.0)):
        """Closed surface of revolution along `axis` from `start`; profile is [(distance, radius)]."""
        axis = axis.normalized()
        side = ref.cross(axis).normalized()
        up = axis.cross(side).normalized()
        rings = []
        for dist, radius in profile:
            center = start + axis * dist
            rings.append([self.vert(center + side * (math.cos(a) * radius * squash[0]) + up * (math.sin(a) * radius * squash[1]),
                                    weight_fn(dist))
                          for a in (2.0 * math.pi * i / segments for i in range(segments))])
        for r0, r1 in zip(rings, rings[1:]):
            for i in range(segments):
                j = (i + 1) % segments
                self.face([r0[i], r0[j], r1[j], r1[i]], slot)
        first = self.vert(start + axis * (profile[0][0] - 0.01), weight_fn(profile[0][0]))
        last = self.vert(start + axis * (profile[-1][0] + 0.01), weight_fn(profile[-1][0]))
        for i in range(segments):
            j = (i + 1) % segments
            self.face([first, rings[0][j], rings[0][i]], slot)
            self.face([last, rings[-1][i], rings[-1][j]], slot)

    def ellipsoid(self, center, radii, slot, weights, segments=14, rings=9):
        def co(theta, phi):
            return center + Vector((radii[0] * math.sin(theta) * math.cos(phi),
                                    radii[1] * math.sin(theta) * math.sin(phi),
                                    radii[2] * math.cos(theta)))

        weight_fn = weights if callable(weights) else (lambda p: weights)
        top_co, bottom_co = co(0.0, 0.0), co(math.pi, 0.0)
        top, bottom = self.vert(top_co, weight_fn(top_co)), self.vert(bottom_co, weight_fn(bottom_co))
        grid = []
        for r in range(1, rings):
            theta = math.pi * r / rings
            row = []
            for s in range(segments):
                p = co(theta, 2.0 * math.pi * s / segments)
                row.append(self.vert(p, weight_fn(p)))
            grid.append(row)
        for s in range(segments):
            t = (s + 1) % segments
            self.face([top, grid[0][s], grid[0][t]], slot)
            self.face([bottom, grid[-1][t], grid[-1][s]], slot)
        for r0, r1 in zip(grid, grid[1:]):
            for s in range(segments):
                t = (s + 1) % segments
                self.face([r0[s], r1[s], r1[t], r0[t]], slot)

    def cone(self, base, tip, radius, slot, weights, segments=6):
        axis = (tip - base).normalized()
        ref = Vector((0, 0, 1)) if abs(axis.z) < 0.9 else Vector((1, 0, 0))
        side = ref.cross(axis).normalized()
        up = axis.cross(side)
        ring = [self.vert(base + (side * math.cos(a) + up * math.sin(a)) * radius, weights)
                for a in (2.0 * math.pi * i / segments for i in range(segments))]
        apex, cap = self.vert(tip, weights), self.vert(base - axis * 0.005, weights)
        for i in range(segments):
            j = (i + 1) % segments
            self.face([ring[i], ring[j], apex], slot)
            self.face([cap, ring[j], ring[i]], slot)

    def quill(self, base, direction, length, radius, shaft_slot, tip_slot, weights, segments=4):
        """A porcupine quill: a dark tapering shaft (70%) ending in a pale pointed tip (30%), capped at the base."""
        axis = direction.normalized()
        ref = Vector((0, 0, 1)) if abs(axis.z) < 0.9 else Vector((1, 0, 0))
        side = ref.cross(axis).normalized()
        up = axis.cross(side)

        def ring(center, r):
            return [self.vert(center + (side * math.cos(a) + up * math.sin(a)) * r, weights)
                    for a in (2.0 * math.pi * i / segments for i in range(segments))]

        root = ring(base, radius)
        band = ring(base + axis * (length * 0.7), radius * 0.6)
        tip, cap = self.vert(base + axis * length, weights), self.vert(base - axis * 0.004, weights)
        for i in range(segments):
            j = (i + 1) % segments
            self.face([root[i], root[j], band[j], band[i]], shaft_slot)
            self.face([band[i], band[j], tip], tip_slot)
            self.face([cap, root[j], root[i]], shaft_slot)

    def build(self, armature, materials):
        mesh = bpy.data.meshes.new(self.name)
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)  # every shell is closed: normals point outward
        self.bm.to_mesh(mesh)
        self.bm.free()
        for mat in materials:
            mesh.materials.append(mat)
        obj = bpy.data.objects.new(self.name, mesh)
        scene.collection.objects.link(obj)
        groups = {}
        for index, weights in enumerate(self.weights):
            total = sum(weights.values()) or 1.0
            for bone_name, w in weights.items():
                if w > 0.0:
                    if bone_name not in groups:
                        groups[bone_name] = obj.vertex_groups.new(name=bone_name)
                    groups[bone_name].add([index], w / total, "REPLACE")
        obj.parent = armature
        obj.modifiers.new("Armature", "ARMATURE").object = armature
        return obj


# --- Armature (T-pose, like the reference) ---------------------------------------------------------------------------
arm_data = bpy.data.armatures.new("GorillaRig")
armature = bpy.data.objects.new("GorillaRig", arm_data)
scene.collection.objects.link(armature)
bpy.context.view_layer.objects.active = armature
bpy.ops.object.mode_set(mode="EDIT")


def bone(name, head, tail, parent=None, deform=True):
    b = arm_data.edit_bones.new(name)
    b.head, b.tail, b.roll = head, tail, 0.0
    b.use_deform = deform
    if parent:
        b.parent = arm_data.edit_bones[parent]
        b.use_connect = False


FWD = Vector((0, -1, 0))
bone("Body", Vector((0, 0.03, TORSO_BOTTOM + 0.05)), Vector((0, 0.03, TORSO_TOP)))
bone("Head", Vector((0, 0, 0)), Vector((0, 0, HEAD_RADIUS)), "Body")
sides = (("L", 1.0), ("R", -1.0))
joints = {}
for s, sign in sides:
    out = Vector((sign, 0, 0))
    shoulder = Vector((sign * SHOULDER.x, SHOULDER.y, SHOULDER.z))
    elbow = shoulder + out * UPPER_ARM
    wrist = elbow + out * FOREARM
    joints[s] = (shoulder, elbow, wrist)
    bone(f"UpperArm.{s}", shoulder, elbow, "Body")
    bone(f"Forearm.{s}", elbow, wrist, f"UpperArm.{s}")
    bone(f"Hand.{s}", wrist, wrist + FWD * HAND, f"Forearm.{s}")
    bone(f"GloveCenter.{s}", wrist + FWD * GLOVE_FORWARD, wrist + FWD * (GLOVE_FORWARD + 0.03), f"Hand.{s}", deform=False)
    knuckle = wrist + FWD * KNUCKLE
    for finger, height in FINGER_ROWS:
        start = knuckle + Vector((0, 0, height))
        parent = f"Hand.{s}"
        for i in range(3):
            name = f"{finger}{i + 1}.{s}"
            bone(name, start + FWD * (FINGER_SEGMENT * i), start + FWD * (FINGER_SEGMENT * (i + 1)), parent)
            parent = name
    thumb, thumb_dir = thumb_frame(wrist, sign)
    bone(f"Thumb1.{s}", thumb, thumb + thumb_dir * THUMB_SEGMENT, f"Hand.{s}")
    bone(f"Thumb2.{s}", thumb + thumb_dir * THUMB_SEGMENT, thumb + thumb_dir * THUMB_SEGMENT * 2, f"Thumb1.{s}")
bpy.ops.object.mode_set(mode="OBJECT")

# --- Body mesh: round torso, belly, quills, tail, head -----------------------------------------------------------------
body = Part("Body")
torso_center = Vector((0, 0.03, TORSO_TOP))
torso_squash = 0.9
# Bottom-heavy like a real porcupine: narrow shoulders, wide rump.
torso_profile = [(0.00, 0.05), (0.03, 0.12), (0.08, 0.16), (0.16, 0.18), (0.26, 0.2), (0.35, 0.195), (0.41, 0.16),
                 (0.45, 0.1), (0.465, 0.04)]
body.lathe(torso_center, Vector((0, 0, -1)), Vector((0, 1, 0)), torso_profile, 22, SLOT_FUR,
           lambda d: {"Body": 1.0}, squash=(1.0, torso_squash))
body.ellipsoid(Vector((0, -0.12, -0.39)), (0.13, 0.06, 0.15), SLOT_BELLY, {"Body": 1.0})


def torso_radius(d):
    for (d0, r0), (d1, r1) in zip(torso_profile, torso_profile[1:]):
        if d0 <= d <= d1:
            return r0 + (r1 - r0) * (d - d0) / (d1 - d0)
    return torso_profile[-1][1]


# Quills: dense over the back and sides, swept back and a little down, longest on the rump.
SWEEP = Vector((0, 1.0, -0.35))
for _ in range(95):
    d = rng.uniform(0.04, 0.43)
    a = rng.uniform(0.0, 1.0) * math.pi  # 0..pi = the back half (+Y), side to side, so they show from the front too
    r = torso_radius(d)
    normal = Vector((math.cos(a), math.sin(a) / torso_squash, 0.0)).normalized()
    base = torso_center + Vector((0, 0, -d)) + Vector((math.cos(a) * r, math.sin(a) * r * torso_squash, 0.0)) * 0.93
    direction = (normal * 0.75 + SWEEP.normalized() * 0.6 + Vector((0, 0, rng.uniform(0.0, 0.35)))).normalized()
    length = rng.uniform(0.14, 0.22) * (0.8 + 0.5 * d / 0.43)
    body.quill(base, direction, length, rng.uniform(0.013, 0.018), SLOT_QUILL, SLOT_TIP, {"Body": 1.0})

# Short, thick tail at the bottom of the back, bristling with quills.
tail_base = Vector((0, 0.15, -0.5))
tail_tip = Vector((0, 0.27, -0.58))
body.lathe(tail_base, tail_tip - tail_base, Vector((0, 0, 1)), [(0.0, 0.06), (0.06, 0.065), (0.12, 0.045), (0.15, 0.02)],
           10, SLOT_FUR, lambda d: {"Body": 1.0})
for _ in range(12):
    t = rng.uniform(0.1, 0.9)
    a = rng.uniform(0.0, 2.0 * math.pi)
    p = tail_base.lerp(tail_tip, t) + Vector((math.cos(a) * 0.05, 0.0, math.sin(a) * 0.05))
    out = (p - tail_base.lerp(tail_tip, t)).normalized()
    body.quill(p, (out * 0.6 + (tail_tip - tail_base).normalized()).normalized(), rng.uniform(0.09, 0.14), 0.012,
               SLOT_QUILL, SLOT_TIP, {"Body": 1.0})

head = {"Head": 1.0}
h = HEAD_CENTER
body.ellipsoid(h, (HEAD_RADIUS * 0.95, HEAD_RADIUS, HEAD_RADIUS * 0.88), SLOT_FUR, head, segments=18, rings=12)
body.ellipsoid(h + Vector((0, -0.13, -0.045)), (0.085, 0.09, 0.075), SLOT_FACE, head)          # blunt snout
body.ellipsoid(h + Vector((0, -0.215, -0.03)), (0.035, 0.022, 0.026), SLOT_EYE, head, segments=10, rings=6)  # nose
for sign in (1.0, -1.0):
    body.ellipsoid(h + Vector((sign * 0.125, -0.03, 0.035)), (0.022, 0.03, 0.03), SLOT_FACE, head, segments=10, rings=6)  # ear
    body.ellipsoid(h + Vector((sign * 0.07, -0.135, 0.03)), (0.019, 0.016, 0.019), SLOT_EYE, head, segments=10, rings=6)
# Crest of quills over the crown and the back of the head, swept back.
for _ in range(26):
    theta = rng.uniform(0.15, 1.1)           # from the crown down the back
    phi = rng.uniform(0.15, 0.85) * math.pi  # across the back half (+Y)
    normal = Vector((math.sin(theta) * math.cos(phi), math.sin(theta) * math.sin(phi) + 0.35, math.cos(theta))).normalized()
    base = h + Vector((normal.x * HEAD_RADIUS * 0.85, normal.y * HEAD_RADIUS * 0.85, normal.z * HEAD_RADIUS * 0.8))
    direction = (normal * 0.6 + Vector((0, 0.8, 0.15))).normalized()
    body.quill(base, direction, rng.uniform(0.11, 0.17), 0.012, SLOT_QUILL, SLOT_TIP, head)

# --- Arms mesh: furry arms and padded paw mitts (the gloves) ----------------------------------------------------------------------------
arms = Part("Arms")
arm_profile = [(-0.03, 0.04), (0.0, 0.075), (0.06, 0.08), (0.16, 0.074), (0.26, 0.062), (UPPER_ARM, 0.06),
               (UPPER_ARM + 0.07, 0.085), (UPPER_ARM + 0.17, 0.095), (UPPER_ARM + 0.27, 0.08), (UPPER_ARM + FOREARM, 0.06),
               (UPPER_ARM + FOREARM + 0.02, 0.045)]
for s, sign in sides:
    shoulder, elbow, wrist = joints[s]
    upper, fore, hand = f"UpperArm.{s}", f"Forearm.{s}", f"Hand.{s}"

    def arm_weights(d, upper=upper, fore=fore):
        f = smoothstep(UPPER_ARM - ELBOW_BLEND, UPPER_ARM + ELBOW_BLEND, d)
        b = 1.0 - smoothstep(-0.03, SHOULDER_BLEND, d)
        return {"Body": b * 0.5, upper: (1.0 - f) * (1.0 - b * 0.5), fore: f}

    arms.lathe(shoulder, Vector((sign, 0, 0)), Vector((0, 0, 1)), arm_profile, 14, SLOT_FUR, arm_weights,
               squash=(1.0, 0.92))
    arms.ellipsoid(shoulder, (0.08, 0.08, 0.08), SLOT_FUR, {"Body": 0.5, upper: 0.5}, segments=10, rings=7)

    # Paw: padded palm, three stubby fingers and a thumb, palm facing in (like the hand holding the controller). The
    # fingers' first segments start inside the palm so the knuckles stay covered when they curl into a fist.
    arms.ellipsoid(wrist + FWD * 0.028, (0.042, 0.04, 0.062), SLOT_PAW, {hand: 1.0}, segments=10, rings=7)  # palm
    arms.ellipsoid(wrist + Vector((sign * 0.0, -0.015, 0)), (0.07, 0.04, 0.07), SLOT_PAW, {hand: 1.0},
                   segments=10, rings=5)  # cuff

    def chain_weights(chain, length, hand=hand):
        """Skin along a finger: the hand up to its root, then each bone, blended over JOINT_BLEND at every joint."""
        def weights(d):
            on_finger = smoothstep(-JOINT_BLEND, JOINT_BLEND, d)
            result = {hand: 1.0 - on_finger}
            previous = 1.0
            for i, name in enumerate(chain):
                end = length * (i + 1)
                rest = 1.0 - smoothstep(end - JOINT_BLEND, end + JOINT_BLEND, d) if i < len(chain) - 1 else 0.0
                result[name] = on_finger * (previous - rest)
                previous = rest
            return result
        return weights

    # Rings either side of each joint (the skin blend) so the tube bends smoothly instead of kinking; six sides keep
    # the player inside the Quest triangle budget (smooth shading rounds them off at this size).
    r = FINGER_RADIUS
    finger_profile = [(-0.012, r * 0.9), (0.0, r)]
    for joint in (FINGER_SEGMENT, FINGER_SEGMENT * 2):
        finger_profile += [(joint - JOINT_BLEND, r), (joint + JOINT_BLEND, r * 0.98)]
    finger_profile += [(0.08, r * 0.85), (0.088, r * 0.5)]
    for finger, height in FINGER_ROWS:
        chain = [f"{finger}{i}.{s}" for i in (1, 2, 3)]
        root = wrist + FWD * KNUCKLE + Vector((0, 0, height))
        arms.lathe(root, FWD, Vector((0, 0, 1)), finger_profile, 6, SLOT_PAW,
                   chain_weights(chain, FINGER_SEGMENT), squash=(1.0, 0.95))

    thumb_root, thumb_dir = thumb_frame(wrist, sign)
    t = THUMB_RADIUS
    thumb_profile = [(-0.012, t * 0.9), (0.0, t), (THUMB_SEGMENT - JOINT_BLEND, t), (THUMB_SEGMENT + JOINT_BLEND, t * 0.97),
                     (0.052, t * 0.85), (0.06, t * 0.5)]
    arms.lathe(thumb_root, thumb_dir, Vector((0, 0, 1)), thumb_profile, 6, SLOT_PAW,
               chain_weights([f"Thumb1.{s}", f"Thumb2.{s}"], THUMB_SEGMENT))

body_obj = body.build(armature, MATERIALS)
arms_obj = arms.build(armature, MATERIALS)
objects = [body_obj, arms_obj]

# --- Report and export ----------------------------------------------------------------------------------------------
depsgraph = bpy.context.evaluated_depsgraph_get()
triangles = {}
for obj in objects:
    mesh = obj.evaluated_get(depsgraph).to_mesh()
    mesh.calc_loop_triangles()
    triangles[obj.name] = len(mesh.loop_triangles)
    obj.evaluated_get(depsgraph).to_mesh_clear()

os.makedirs(os.path.dirname(output), exist_ok=True)
if blend_out:
    os.makedirs(os.path.dirname(blend_out), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=blend_out)

bpy.ops.object.select_all(action="DESELECT")
for obj in [armature] + objects:
    obj.select_set(True)
bpy.ops.export_scene.fbx(
    filepath=output,
    use_selection=True,
    object_types={"ARMATURE", "MESH"},
    global_scale=1.0,
    apply_unit_scale=True,
    apply_scale_options="FBX_SCALE_ALL",
    axis_forward="-Z",
    axis_up="Y",
    bake_space_transform=False,
    add_leaf_bones=False,
    primary_bone_axis="Y",
    secondary_bone_axis="X",
    use_armature_deform_only=False,
    bake_anim=False,
    mesh_smooth_type="FACE",
)

emit({
    "success": True,
    "output": output,
    "blend": blend_out,
    "triangles": triangles,
    "totalTriangles": sum(triangles.values()),
    "bones": len(arm_data.bones),
    "reachShoulderToGlove": round(UPPER_ARM + FOREARM + GLOVE_FORWARD, 3),
})
