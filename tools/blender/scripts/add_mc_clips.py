"""Adds the MC's clips to a character rigged by rig_character.py, in place: "Cheer" and "Shadowbox".

    tools/blender/blender.sh tools/blender/scripts/add_mc_clips.py <rigged.fbx> <out.fbx> [--glove L]

Gonzales, the boxing baby who MCs the show from the headline board:
- Cheer (1.2 s): both fists punch up into the air twice with a little hop, then back to idle. Played when a hoop is
  cleared, on a save and at the finish (McReactions in Unity).
- Shadowbox (1.6 s): up into a boxer's guard, bounce, a jab with the glove hand (--glove, the character's own side), a jab
  with the other, back to idle. One of his idle gestures, next to Wave.
Both start from the Idle pose and key every pose bone on every frame, like the other clips. Running it again replaces
them. Run it after rig_character.py and before fix_shoulders.py (which then gives these clips natural shoulders too).
Re-exports with rig_character.py's exact FBX settings; write back to the same *_Game.fbx path, then rebuild the prototype.
Check: render_pose.py <file> Screenshots/<name> Cheer:8 Shadowbox:14 --angle 20
"""
import math
import os
import sys

sys.path.append(os.path.dirname(os.path.abspath(__file__)))

import bpy  # noqa: E402
from mathutils import Matrix, Quaternion  # noqa: E402

from blender_common import emit, fail, load, script_args  # noqa: E402

args = script_args()
if len(args) < 2:
    fail("usage: add_mc_clips.py <rigged.fbx> <out.fbx> [--glove L]")
source, output = args[0], os.path.abspath(args[1])
glove = args[args.index("--glove") + 1] if "--glove" in args else "L"
bare = "R" if glove == "L" else "L"

load(source)
rig = next((o for o in bpy.data.objects if o.type == "ARMATURE"), None)
mesh = next((o for o in bpy.data.objects if o.type == "MESH" and o.vertex_groups), None)
if rig is None or mesh is None:
    fail("needs a rigged model (an armature and a skinned mesh)")
bones = rig.data.bones
for name in ("Hips", "Spine", "Chest", "Head", "UpperArm.L", "UpperArm.R", "LowerArm.L", "LowerArm.R", "Foot.R"):
    if name not in bones:
        fail(f"no {name} bone: not a rig_character.py rig")
idle = next((a for a in bpy.data.actions if a.name.split("|")[-1] == "Idle"), None)
if idle is None:
    fail("no Idle clip to start from")
for old in [a for a in bpy.data.actions if a.name.split("|")[-1] in ("Cheer", "Shadowbox")]:
    bpy.data.actions.remove(old)

# Armature-space axes.
up = (bones["Spine"].tail_local - bones["Spine"].head_local).normalized()
foot = bones["Foot.R"]
forward = foot.tail_local - foot.head_local
forward = (forward - up * forward.dot(up)).normalized()
height = max((b.tail_local - bones["Hips"].head_local).dot(up) for b in bones) * 1.15  # rough body height
side_dir = {}
for s in ("L", "R"):
    v = bones[f"UpperArm.{s}"].head_local - bones["Spine"].head_local
    side_dir[s] = (v - up * v.dot(up) - forward * v.dot(forward)).normalized()
swing_axis = (-up).cross(forward).normalized()  # + swings a hanging limb forward (and up, past 90)


def out_axis(s):
    return (-up).cross(side_dir[s]).normalized()  # + swings a hanging arm out to that side


def turn(pb, axis, degrees):
    if abs(degrees) < 1e-4:
        return
    m = pb.matrix.copy()
    pivot = m.to_translation()
    r = Quaternion(axis, math.radians(degrees)).to_matrix().to_4x4()
    pb.matrix = Matrix.Translation(pivot) @ r @ Matrix.Translation(-pivot) @ m
    bpy.context.view_layer.update()


def lift(pb, amount):
    m = pb.matrix.copy()
    m.translation = m.translation + up * amount
    pb.matrix = m
    bpy.context.view_layer.update()


def keyed(keys, f):
    for (f0, a), (f1, b) in zip(keys, keys[1:]):
        if f0 <= f <= f1:
            t = (f - f0) / max(f1 - f0, 1e-6)
            t = t * t * (3 - 2 * t)
            return {k: a.get(k, 0.0) + (b.get(k, 0.0) - a.get(k, 0.0)) * t for k in set(a) | set(b)}
    return dict(keys[-1][1])


# Pose parameters (degrees; hop in body heights). g = glove arm, b = bare arm: fwd swing, out swing, elbow bend.
UP_FISTS = dict(gF=148, gO=22, gE=40, bF=145, bO=25, bE=35, lean=-6, hop=0.035, head=-10)
DOWN_FISTS = dict(gF=115, gO=15, gE=95, bF=110, bO=20, bE=90, lean=4, hop=0.0, head=5)
CHEER = [(0, {}), (7, UP_FISTS), (12, DOWN_FISTS), (17, UP_FISTS), (22, DOWN_FISTS), (27, dict(UP_FISTS, hop=0.02)), (36, {})]

GUARD = dict(gF=60, gO=8, gE=115, bF=60, bO=8, bE=115, lean=8, hop=0.0, head=4, knees=10)
GUARD_UP = dict(GUARD, hop=0.012)
JAB_G = dict(GUARD, gF=88, gO=2, gE=8, lean=12, twist=-18)
JAB_B = dict(GUARD, bF=88, bO=2, bE=8, lean=12, twist=18)
SHADOWBOX = [(0, {}), (6, GUARD), (9, GUARD_UP), (12, GUARD), (15, JAB_G), (19, GUARD), (22, GUARD_UP), (25, GUARD),
             (28, JAB_B), (32, GUARD), (36, GUARD_UP), (40, GUARD), (48, {})]

CLIPS = {"Cheer": (36, CHEER), "Shadowbox": (48, SHADOWBOX)}

scene = bpy.context.scene
rig.animation_data_create()
pose = rig.pose.bones
made = {}
for clip, (frames, keys) in CLIPS.items():
    baked = []
    for f in range(frames + 1):
        k = keyed(keys, f)
        rig.animation_data.action = idle
        scene.frame_set(0)
        bpy.context.view_layer.update()
        lift(pose["Hips"], k.get("hop", 0.0) * height)
        turn(pose["Spine"], up, k.get("twist", 0.0))
        turn(pose["Spine"], swing_axis, -k.get("lean", 0.0) * 0.5)  # + lean forward
        turn(pose["Chest"], swing_axis, -k.get("lean", 0.0) * 0.5)
        turn(pose["Head"], swing_axis, -k.get("head", 0.0))
        for s, prefix in ((glove, "g"), (bare, "b")):
            turn(pose[f"UpperArm.{s}"], out_axis(s), k.get(prefix + "O", 0.0))
            turn(pose[f"UpperArm.{s}"], swing_axis, k.get(prefix + "F", 0.0))
            turn(pose[f"LowerArm.{s}"], swing_axis, k.get(prefix + "E", 0.0))  # the forearm folds up toward the face
            knee = k.get("knees", 0.0)
            turn(pose[f"UpperLeg.{s}"], swing_axis, knee * 0.6)
            turn(pose[f"LowerLeg.{s}"], swing_axis, -knee * 1.2)
        baked.append({pb.name: (pb.rotation_mode, pb.rotation_quaternion.copy(), pb.rotation_euler.copy(), pb.location.copy())
                      for pb in pose})

    action = bpy.data.actions.new(idle.name[: -len("Idle")] + clip)
    action.use_fake_user = True
    rig.animation_data.action = action
    for f, values in enumerate(baked):
        for name, (mode, quat, euler, loc) in values.items():
            pb = pose[name]
            pb.location = loc
            pb.keyframe_insert("location", frame=f, group=name)
            if mode == "QUATERNION":
                pb.rotation_quaternion = quat
                pb.keyframe_insert("rotation_quaternion", frame=f, group=name)
            else:
                pb.rotation_euler = euler
                pb.keyframe_insert("rotation_euler", frame=f, group=name)
    made[clip] = frames + 1

# Export exactly as rig_character.py does (clips are the imported actions, all exported as takes).
for o in scene.objects:
    o.select_set(o in (rig, mesh))
bpy.context.view_layer.objects.active = rig
rig.animation_data.action = None
for pb in pose:
    pb.matrix_basis.identity()
os.makedirs(os.path.dirname(output), exist_ok=True)
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
    mesh_smooth_type="FACE",
    bake_anim=True,
    bake_anim_use_all_bones=True,
    bake_anim_use_nla_strips=False,
    bake_anim_use_all_actions=True,
    bake_anim_force_startend_keying=True,
    bake_anim_simplify_factor=0.0,
    path_mode="STRIP",
    embed_textures=False,
)
emit({"success": os.path.exists(output), "output": output, "glove": glove, "clips": made,
      "actions": [a.name for a in bpy.data.actions]})
