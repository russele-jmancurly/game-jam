"""Builds the original player body for gorilla locomotion and exports it as a Unity FBX.

    tools/blender/blender.sh tools/blender/scripts/build_gorilla_body.py Assets/GameJam/Art/Characters/GorillaBody.fbx [--blend out.blend]

The mesh is our own (a porcupine-suit gorilla: big round head with a muzzle, small legless torso with back spikes,
thick long arms, padded mitt gloves). Only the skeleton layout and proportions follow the reference player rig the
team supplied (UGPlayerModel): T-pose, narrow shoulders just under the head, upper arm ~0.9x the forearm, a hand bone
pointing forward with index, middle and thumb finger chains. Scale: shoulder to glove centre ~0.74 m.

Rig (names are what GorillaBodyRig looks up in Unity), origin = the player's eyes:
    Body                                    torso, back spikes (root)
      Head                                  pivots at the eyes, follows where the player looks
      UpperArm.L/R -> Forearm.L/R -> Hand.L/R
        Hand.L/R -> Index1-3, Middle1-3, Thumb1-2 (.L/.R)   finger chains, ready for a grip curl
        Hand.L/R -> GloveCenter.L/R          non-deforming marker where the tracked hand (controller) sits

Two meshes share the armature: "Body" (torso + head: hidden in your own view) and "Arms" (arms + gloves: shown to
everyone, you included). Material slots: Fur (tinted per character in Unity), Skin (muzzle, belly), Eye, Glove.
"""
import math
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


FUR = material("Fur", (0.55, 0.45, 0.38))
SKIN = material("Skin", (0.93, 0.80, 0.66))
EYE = material("Eye", (0.05, 0.05, 0.07))
GLOVE = material("Glove", (0.97, 0.97, 0.95))
SLOT_FUR, SLOT_SKIN, SLOT_EYE, SLOT_GLOVE = range(4)


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
    knuckle = wrist + FWD * (HAND + 0.015)
    for finger, height, count in (("Index", -0.03, 3), ("Middle", 0.015, 3)):
        start = knuckle + Vector((0, 0, height))
        parent = f"Hand.{s}"
        for i in range(count):
            name = f"{finger}{i + 1}.{s}"
            bone(name, start + FWD * (0.03 * i), start + FWD * (0.03 * (i + 1)), parent)
            parent = name
    thumb = wrist + FWD * 0.01 + Vector((0, 0, 0.045))
    thumb_dir = (FWD + Vector((0, 0, 0.9))).normalized()
    bone(f"Thumb1.{s}", thumb, thumb + thumb_dir * 0.035, f"Hand.{s}")
    bone(f"Thumb2.{s}", thumb + thumb_dir * 0.035, thumb + thumb_dir * 0.07, f"Thumb1.{s}")
bpy.ops.object.mode_set(mode="OBJECT")

# --- Body mesh: torso with back spikes, head --------------------------------------------------------------------------
body = Part("Body")
torso_len = TORSO_TOP - TORSO_BOTTOM
torso_profile = [(0.00, 0.05), (0.03, 0.12), (0.08, 0.155), (0.16, 0.165), (0.26, 0.16), (0.35, 0.145), (0.40, 0.11),
                 (0.425, 0.06)]
body.lathe(Vector((0, 0.03, TORSO_TOP)), Vector((0, 0, -1)), Vector((0, 1, 0)), torso_profile, 20, SLOT_FUR,
           lambda d: {"Body": 1.0}, squash=(1.0, 0.85))
body.ellipsoid(Vector((0, -0.085, -0.38)), (0.11, 0.05, 0.13), SLOT_SKIN, {"Body": 1.0})  # belly
for i, (z, x) in enumerate(((-0.20, 0.0), (-0.27, 0.07), (-0.27, -0.07), (-0.35, 0.0), (-0.42, 0.06), (-0.42, -0.06),
                             (-0.49, 0.0))):  # porcupine-suit spikes down the back
    base = Vector((x, 0.13, z))
    body.cone(base, base + Vector((x * 0.6, 0.09, 0.035)), 0.028, SLOT_FUR, {"Body": 1.0})

head = {"Head": 1.0}
h = HEAD_CENTER
body.ellipsoid(h, (HEAD_RADIUS, HEAD_RADIUS * 0.95, HEAD_RADIUS * 0.9), SLOT_FUR, head, segments=18, rings=12)
body.ellipsoid(h + Vector((0, -0.135, -0.055)), (0.1, 0.07, 0.065), SLOT_SKIN, head)        # muzzle
body.ellipsoid(h + Vector((0, -0.125, 0.04)), (0.12, 0.055, 0.035), SLOT_FUR, head, rings=7)  # brow
for sign in (1.0, -1.0):
    body.ellipsoid(h + Vector((sign * 0.16, 0.01, 0.0)), (0.022, 0.045, 0.05), SLOT_SKIN, head, segments=10, rings=6)  # ear
    body.ellipsoid(h + Vector((sign * 0.055, -0.15, 0.005)), (0.025, 0.02, 0.025), SLOT_EYE, head, segments=10, rings=6)
    body.ellipsoid(h + Vector((sign * 0.03, -0.2, -0.04)), (0.013, 0.01, 0.01), SLOT_EYE, head, segments=8, rings=5)  # nostril
    for k, (dz, dy) in enumerate(((0.12, 0.06), (0.08, 0.13))):  # spiky hood
        base = h + Vector((sign * 0.06, dy, dz))
        body.cone(base, base + Vector((sign * 0.04, 0.05, 0.07)), 0.025, SLOT_FUR, head)

# --- Arms mesh: arms and padded mitt gloves ----------------------------------------------------------------------------
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
    arms.ellipsoid(shoulder, (0.08, 0.08, 0.08), SLOT_FUR, {"Body": 0.5, upper: 0.5}, segments=12, rings=8)

    # Mitt: a padded paddle pointing forward from the wrist, palm facing in (like the hand holding the controller).
    glove_center = wrist + FWD * GLOVE_FORWARD

    def mitt_weights(p, hand=hand, s=s, wrist=wrist):
        along = (wrist - p).dot(Vector((0, 1, 0)))  # metres forward of the wrist
        f = smoothstep(0.06, 0.13, along)
        return {hand: 1.0 - f, f"Middle1.{s}": f * 0.5, f"Index1.{s}": f * 0.5}

    arms.ellipsoid(glove_center + Vector((sign * 0.0, 0, 0)), (0.05, 0.085, 0.075), SLOT_GLOVE, mitt_weights)
    arms.ellipsoid(wrist + Vector((sign * 0.0, -0.015, 0)), (0.07, 0.04, 0.07), SLOT_GLOVE, {hand: 1.0},
                   segments=12, rings=6)  # cuff
    thumb_base = wrist + FWD * 0.03 + Vector((-sign * 0.03, 0, 0.055))
    arms.ellipsoid(thumb_base + Vector((0, -0.02, 0.015)), (0.025, 0.035, 0.03), SLOT_GLOVE, {f"Thumb1.{s}": 1.0},
                   segments=10, rings=6)

body_obj = body.build(armature, [FUR, SKIN, EYE, GLOVE])
arms_obj = arms.build(armature, [FUR, SKIN, EYE, GLOVE])
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
