"""Adds a "Kick" clip to a character rigged by rig_character.py, in place (no unrigged source needed).

    tools/blender/blender.sh tools/blender/scripts/add_kick_clip.py <rigged.fbx> <out.fbx> [--leg R]

The giants kick players (MischiefMaker: KickWindUp then Kick). The clip starts from the Idle pose, draws the kicking leg
back with the knee bent and the body leaning in (frames 0-17, the wind-up, held a moment), swings it through straight so
the foot is out in front at frame 20 (CharacterDefinition.KickHitTime = 20/30 s), follows through and settles back to
idle by frame 30. Arms swing opposite the leg for balance. Every pose bone is keyed on every frame, like the other clips.
Running it again replaces an existing Kick clip. Re-exports with rig_character.py's exact FBX settings; write back to the
same *_Game.fbx path so Unity keeps the .meta GUID, then rebuild the prototype (it links the clip into the
CharacterDefinition). Check it with render_pose.py <file> Screenshots/<name> Kick:0 Kick:15 Kick:20 --angle 90.
"""
import math
import os
import sys

sys.path.append(os.path.dirname(os.path.abspath(__file__)))

import bpy  # noqa: E402
from mathutils import Matrix, Quaternion, Vector  # noqa: E402

from blender_common import emit, fail, load, script_args  # noqa: E402

FRAMES = 30
# (frame, kicking thigh, kicking knee, standing knee, torso lean back, arm swing) in degrees; + thigh/arm = forward.
KEYS = [
    (0, 0, 0, 0, 0, 0),
    (10, -38, 75, 12, -8, 22),    # leg drawn back, leaning into it
    (17, -44, 85, 14, -10, 26),   # wind-up held (the telegraph)
    (20, 92, 0, 10, 18, -34),     # the foot lands: leg straight out in front, body leans back
    (23, 100, 8, 10, 20, -36),    # follow-through
    (30, 0, 0, 0, 0, 0),
]

args = script_args()
if len(args) < 2:
    fail("usage: add_kick_clip.py <rigged.fbx> <out.fbx> [--leg R]")
source, output = args[0], os.path.abspath(args[1])
kick_side = args[args.index("--leg") + 1] if "--leg" in args else "R"
stand_side = "L" if kick_side == "R" else "R"

load(source)
rig = next((o for o in bpy.data.objects if o.type == "ARMATURE"), None)
mesh = next((o for o in bpy.data.objects if o.type == "MESH" and o.vertex_groups), None)
if rig is None or mesh is None:
    fail("needs a rigged model (an armature and a skinned mesh)")
bones = rig.data.bones
for name in (f"UpperLeg.{kick_side}", f"LowerLeg.{kick_side}", f"Foot.{kick_side}", "Spine", "UpperArm.L", "UpperArm.R"):
    if name not in bones:
        fail(f"no {name} bone: not a rig_character.py rig")

idle = next((a for a in bpy.data.actions if a.name.split("|")[-1] == "Idle"), None)
if idle is None:
    fail("no Idle clip to start from")
for old in [a for a in bpy.data.actions if a.name.split("|")[-1] == "Kick"]:
    bpy.data.actions.remove(old)

# Armature-space axes: up is the rest Hips->Spine direction, forward is where the feet point.
up = (bones["Spine"].tail_local - bones["Spine"].head_local).normalized()
foot = bones[f"Foot.{kick_side}"]
forward = foot.tail_local - foot.head_local
forward = (forward - up * forward.dot(up)).normalized()
swing_axis = (-up).cross(forward).normalized()  # + rotates "down" toward "forward": a leg or arm swings forward


def lerp_keys(f):
    for (f0, *a), (f1, *b) in zip(KEYS, KEYS[1:]):
        if f0 <= f <= f1:
            t = (f - f0) / max(f1 - f0, 1e-6)
            t = t * t * (3 - 2 * t)
            return [x + (y - x) * t for x, y in zip(a, b)]
    return list(KEYS[-1][1:])


def turn(pb, degrees):
    """Rotates a pose bone about its head, around the swing axis, in armature space."""
    if abs(degrees) < 1e-4:
        return
    m = pb.matrix.copy()
    pivot = m.to_translation()
    r = Quaternion(swing_axis, math.radians(degrees)).to_matrix().to_4x4()
    pb.matrix = Matrix.Translation(pivot) @ r @ Matrix.Translation(-pivot) @ m
    bpy.context.view_layer.update()


scene = bpy.context.scene
rig.animation_data_create()
pose = rig.pose.bones
baked = []
for f in range(FRAMES + 1):
    thigh, knee, stand_knee, lean_back, arm_swing = lerp_keys(f)
    rig.animation_data.action = idle
    scene.frame_set(0)
    bpy.context.view_layer.update()
    turn(pose["Spine"], lean_back)  # + leans the torso back (up rotates away from forward)
    turn(pose[f"UpperLeg.{kick_side}"], thigh)
    turn(pose[f"LowerLeg.{kick_side}"], -knee)  # the knee bends the shin back
    turn(pose[f"Foot.{kick_side}"], knee * 0.3)
    turn(pose[f"LowerLeg.{stand_side}"], -stand_knee)
    turn(pose[f"UpperLeg.{stand_side}"], stand_knee * 0.5)
    turn(pose[f"UpperArm.{kick_side}"], -arm_swing)
    turn(pose[f"UpperArm.{stand_side}"], arm_swing)
    baked.append({pb.name: (pb.rotation_mode, pb.rotation_quaternion.copy(), pb.rotation_euler.copy(), pb.location.copy())
                  for pb in pose})

action = bpy.data.actions.new(idle.name[: -len("Idle")] + "Kick")
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
emit({"success": os.path.exists(output), "output": output, "kickLeg": kick_side, "frames": FRAMES + 1,
      "hitFrame": 20, "actions": [a.name for a in bpy.data.actions]})
