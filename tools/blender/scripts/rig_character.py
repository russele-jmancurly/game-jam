"""Rigs and animates a prepared show character (static T-pose meshes from prepare_character.py) for Unity.

    tools/blender/blender.sh tools/blender/scripts/rig_character.py <in.fbx> <out.fbx>

- Measures the meshes for joint positions (any realistic T-posed human: height, arm line, leg columns, spine centre)
  and builds a 16-bone humanoid: Hips > Spine > Chest > Neck > Head, Shoulder/UpperArm/LowerArm/Hand, UpperLeg/
  LowerLeg/Foot (.L/.R = the character's own left/right).
- Skins the body with Blender's automatic (heat) weights; clothes copy the weights of the body surface under them
  and body faces hidden under clothes are deleted (so skin never pokes through); eyes, hair and glasses go fully to
  Head; any vertex the solver left
  unweighted takes its nearest bone. Joins every part into one skinned mesh (one SkinnedMeshRenderer, materials kept by
  name, so Unity remaps still apply).
- Keys five clips, exported as FBX takes: Idle (3 s loop), Walk (1 s loop), WindUp (0.8 s), Slap (0.73 s: swing,
  hit at 0.23 s, follow-through, back to idle), Wave (2 s). `walkStrideHeights` in the result = ground speed the Walk
  clip implies (measured from the planted foot), in body heights per second; put it in the CharacterDefinition so Unity
  scales walk playback by speed / (that * height). `<rigged.fbx> --measure` re-measures an already rigged model.
- Rest pose is the T-pose; every clip brings the arms down itself.

Run it on the *_Game.fbx and write back to the same path (the textures stay in <name>.fbm and the .meta GUID is kept).
Run it once per prepared model: it expects an unrigged input.
"""
import math
import os
import sys

sys.path.append(os.path.dirname(os.path.abspath(__file__)))

import bpy  # noqa: E402
from mathutils import Quaternion, Vector  # noqa: E402

from blender_common import emit, fail, load, script_args  # noqa: E402

FPS = 30
HEAD_PARTS = ("eye", "hair", "glass", "lens")
WALK_THIGH = 24.0



def measure_stride(rig, walk_action, height):
    """
    Ground speed the Walk clip implies at normal playback, in body heights per second: how fast the planted foot (the
    lower ankle, within 2 % of the height of its lowest point) moves backward. Unity divides speed by this so feet
    don't skate.
    """
    scene = bpy.context.scene
    rig.animation_data.action = walk_action
    if hasattr(rig.animation_data, "action_suitable_slots") and rig.animation_data.action_slot is None:
        slots = list(rig.animation_data.action_suitable_slots)
        if slots:
            rig.animation_data.action_slot = slots[0]
    start, end = (int(v) for v in walk_action.frame_range)
    track = {"Foot.L": [], "Foot.R": []}
    for f in range(start, end + 1):
        scene.frame_set(f)
        for name in track:
            track[name].append(rig.matrix_world @ rig.pose.bones[name].head)
    speeds = []
    for positions in track.values():
        low = min(p.z for p in positions)
        for a, b in zip(positions, positions[1:]):
            if a.z < low + 0.02 * height and b.z < low + 0.02 * height:
                speeds.append(Vector((b.x - a.x, b.y - a.y)).length * scene.render.fps)
    rig.animation_data.action = None
    speeds.sort()
    return speeds[len(speeds) // 2] / height if speeds else 0.0


args = script_args()
if len(args) == 2 and args[1] == "--measure":
    # Measure an already rigged FBX: tools/blender/blender.sh .../rig_character.py <rigged.fbx> --measure
    load(args[0])
    rig = next((o for o in bpy.context.scene.objects if o.type == "ARMATURE"), None)
    walk = next((a for a in bpy.data.actions if a.name.split("|")[-1] == "Walk"), None)
    if rig is None or walk is None:
        fail("No armature with a Walk clip.")
    bpy.context.scene.render.fps = FPS
    if rig.animation_data is None:
        rig.animation_data_create()
    mesh_objects = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    height = max((o.matrix_world @ Vector(c)).z for o in mesh_objects for c in o.bound_box) -         min((o.matrix_world @ Vector(c)).z for o in mesh_objects for c in o.bound_box)
    emit({"success": True, "height": round(height, 3), "walkStrideHeights": round(measure_stride(rig, walk, height), 3)})
    sys.exit(0)

if len(args) < 2:
    fail("usage: rig_character.py <in.fbx> <out.fbx>   |   rig_character.py <rigged.fbx> --measure")
source, output = args[0], os.path.abspath(args[1])

meshes = load(source)
if not meshes:
    fail("No meshes in the input.")
if any(o.type == "ARMATURE" for o in bpy.context.scene.objects):
    fail("The input already has an armature; rig the unrigged *_Game.fbx from prepare_character.py.")

scene = bpy.context.scene
scene.render.fps = FPS

# --- Bake transforms so mesh data is in world space ---------------------------------------------------------------
for o in bpy.context.scene.objects:
    o.select_set(o.type == "MESH")
bpy.context.view_layer.objects.active = meshes[0]
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

points = [v.co.copy() for o in meshes for v in o.data.vertices]
H = max(p.z for p in points)
U = Vector((0, 0, 1))


def median(values, fallback=0.0):
    values = sorted(values)
    return values[len(values) // 2] if values else fallback


def is_head_part(obj):
    return any(key in obj.name.lower() for key in HEAD_PARTS)


# Facing: the eyes sit in front of the head's centre.
eyes = [o for o in meshes if "eye" in o.name.lower()]
head_pts = [p for p in points if p.z > 0.88 * H]
head_y = median([p.y for p in head_pts])
eye_y = median([v.co.y for o in eyes for v in o.data.vertices], head_y - 0.01) if eyes else head_y - 0.01
fy = 1.0 if eye_y > head_y else -1.0
F = Vector((0, fy, 0))
RIGHT = F.cross(U)  # the character's own right
LEFT = -RIGHT
SIDES = {"L": LEFT, "R": RIGHT}

body = [o for o in meshes if not is_head_part(o)]
body_pts = [v.co.copy() for o in body for v in o.data.vertices]


def mid(values, fallback):
    return (min(values) + max(values)) / 2 if values else fallback


def centre_y(z, half_width=0.1, band=0.012):
    """Middle of the body's depth at a height (midpoint, so a nose or collar doesn't drag it)."""
    return mid([p.y for p in body_pts if abs(p.z - z) < band * H and abs(p.x) < half_width * H], head_y)


joints = {}
for key, frac in (("hips", 0.53), ("spine", 0.62), ("chest", 0.72), ("neck", 0.835), ("head", 0.87)):
    z = frac * H
    joints[key] = Vector((0.0, centre_y(z), z))
joints["top"] = Vector((0.0, head_y, H))

for side, vec in SIDES.items():
    s = 1.0 if vec.x > 0 else -1.0
    arm_pts = [p for p in body_pts if p.x * s > 0.14 * H and p.z > 0.6 * H]
    if not arm_pts:
        fail(f"No arm found on side {side}; is the model in a T-pose?")
    tip = max(p.x * s for p in arm_pts)
    sx = 0.1 * H
    length = tip - sx

    def arm_at(fraction, s=s, arm_pts=arm_pts, sx=sx, length=length):
        x = sx + fraction * length
        band = [p for p in arm_pts if abs(p.x * s - x) < 0.015]
        return Vector((x * s, median([p.y for p in band]), median([p.z for p in band], 0.8 * H)))

    elbow, wrist, hand_end = arm_at(0.42), arm_at(0.78), arm_at(0.95)
    slope = (elbow.z - wrist.z) / max(abs(wrist.x - elbow.x), 1e-4)
    shoulder_z = min(elbow.z + slope * abs(elbow.x - sx * s), 0.84 * H)
    joints[f"clavicle.{side}"] = Vector((0.03 * H * s, joints["chest"].y, 0.8 * H))
    joints[f"shoulder.{side}"] = Vector((sx * s, elbow.y, shoulder_z))
    joints[f"elbow.{side}"] = elbow
    joints[f"wrist.{side}"] = wrist
    joints[f"handend.{side}"] = hand_end

    leg_pts = [p for p in body_pts if p.x * s > 0.008 * H and 0.1 * H < p.z < 0.42 * H]
    leg_x = median([p.x for p in leg_pts], 0.08 * H * s)

    def leg_y(z, s=s):
        return mid([p.y for p in body_pts if p.x * s > 0.008 * H and abs(p.z - z) < 0.012 * H], head_y)

    ankle = Vector((leg_x, leg_y(0.07 * H), 0.055 * H))
    foot_pts = [p for p in body_pts if p.x * s > 0.008 * H and p.z < 0.045 * H]
    front = max((p.y * fy for p in foot_pts), default=ankle.y * fy + 0.12 * H) * fy
    joints[f"hip.{side}"] = Vector((leg_x * 0.95, leg_y(0.47 * H), 0.5 * H))
    joints[f"knee.{side}"] = Vector((leg_x, leg_y(0.28 * H), 0.28 * H))
    joints[f"ankle.{side}"] = ankle
    joints[f"toe.{side}"] = Vector((leg_x, ankle.y + (front - ankle.y) * 0.8, 0.02 * H))

# --- Armature ------------------------------------------------------------------------------------------------------
arm_data = bpy.data.armatures.new("Armature")
rig = bpy.data.objects.new("Armature", arm_data)
scene.collection.objects.link(rig)
for o in scene.objects:
    o.select_set(False)
rig.select_set(True)
bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode="EDIT")

BONES = [
    ("Hips", None, "hips", "spine"),
    ("Spine", "Hips", "spine", "chest"),
    ("Chest", "Spine", "chest", "neck"),
    ("Neck", "Chest", "neck", "head"),
    ("Head", "Neck", "head", "top"),
]
for side in ("L", "R"):
    BONES += [
        (f"Shoulder.{side}", "Chest", f"clavicle.{side}", f"shoulder.{side}"),
        (f"UpperArm.{side}", f"Shoulder.{side}", f"shoulder.{side}", f"elbow.{side}"),
        (f"LowerArm.{side}", f"UpperArm.{side}", f"elbow.{side}", f"wrist.{side}"),
        (f"Hand.{side}", f"LowerArm.{side}", f"wrist.{side}", f"handend.{side}"),
        (f"UpperLeg.{side}", "Hips", f"hip.{side}", f"knee.{side}"),
        (f"LowerLeg.{side}", f"UpperLeg.{side}", f"knee.{side}", f"ankle.{side}"),
        (f"Foot.{side}", f"LowerLeg.{side}", f"ankle.{side}", f"toe.{side}"),
    ]

for name, parent, head, tail in BONES:
    bone = arm_data.edit_bones.new(name)
    bone.head = joints[head]
    bone.tail = joints[tail]
    bone.align_roll(F)  # consistent local axes: Z toward the character's front
    if parent:
        bone.parent = arm_data.edit_bones[parent]
        bone.use_connect = False
bpy.ops.object.mode_set(mode="OBJECT")

# --- Skinning ------------------------------------------------------------------------------------------------------
for o in scene.objects:
    o.select_set(o.type == "MESH")
rig.select_set(True)
bpy.context.view_layer.objects.active = rig
bpy.ops.object.parent_set(type="ARMATURE_AUTO")

segments = {b.name: (b.head_local.copy(), b.tail_local.copy()) for b in arm_data.bones}


def nearest_bone(p):
    best, best_d = "Hips", 1e9
    for name, (a, b) in segments.items():
        ab = b - a
        t = max(0.0, min(1.0, (p - a).dot(ab) / max(ab.length_squared, 1e-9)))
        d = (a + ab * t - p).length
        if d < best_d:
            best, best_d = name, d
    return best


fallback_vertices = 0
for o in meshes:
    groups = {g.name: g for g in o.vertex_groups}
    for name in segments:
        if name not in groups:
            groups[name] = o.vertex_groups.new(name=name)
    if is_head_part(o):
        for g in list(o.vertex_groups):
            o.vertex_groups.remove(g)
        head = o.vertex_groups.new(name="Head")
        head.add([v.index for v in o.data.vertices], 1.0, "REPLACE")
        continue
    for v in o.data.vertices:
        if sum(g.weight for g in v.groups) < 1e-4:
            groups[nearest_bone(v.co)].add([v.index], 1.0, "REPLACE")
            fallback_vertices += 1

# Clothes (shirt, shorts, socks, shoes...) copy the skin weights of the body surface under them, so cloth and skin bend
# together and the body doesn't poke through at the shoulders and hips. Weights are blended from the nearest body face's
# corners by inverse distance.
main = max(meshes, key=lambda o: len(o.data.vertices))
body_names = {g.index: g.name for g in main.vertex_groups}
body_weights = [{body_names[g.group]: g.weight for g in v.groups if g.weight > 1e-4} for v in main.data.vertices]
cloth_vertices = 0
for o in meshes:
    if o is main or is_head_part(o):
        continue
    groups = {g.name: g for g in o.vertex_groups}
    for v in o.data.vertices:
        found, location, _normal, face = main.closest_point_on_mesh(v.co)
        if not found or face < 0:
            continue
        mixed = {}
        corners = main.data.polygons[face].vertices
        for index in corners:
            share = 1.0 / max((main.data.vertices[index].co - location).length, 1e-5)
            for name, weight in body_weights[index].items():
                mixed[name] = mixed.get(name, 0.0) + weight * share
        total = sum(mixed.values())
        if total <= 0:
            continue
        for name, group in groups.items():
            group.remove([v.index])
        for name, weight in mixed.items():
            if weight / total > 0.01:
                groups[name].add([v.index], weight / total, "REPLACE")
        cloth_vertices += 1

# Skin hidden under clothes is never seen, but pokes through low-poly clothes as they bend: delete body faces whose
# centre and corners all have cloth within COVER_DISTANCE straight out along the face normal.
import bmesh  # noqa: E402
from mathutils.bvhtree import BVHTree  # noqa: E402

COVER_DISTANCE = 0.035 * H
cloth_trees = []
for o in meshes:
    if o is not main and not is_head_part(o):
        cloth_bm = bmesh.new()
        cloth_bm.from_mesh(o.data)
        cloth_trees.append(BVHTree.FromBMesh(cloth_bm))
        cloth_bm.free()


def covered(point, normal):
    for tree in cloth_trees:
        hit = tree.ray_cast(point + normal * 0.0005, normal, COVER_DISTANCE)
        if hit[0] is not None:
            return True
    return False


hidden_faces = 0
if cloth_trees:
    body_bm = bmesh.new()
    body_bm.from_mesh(main.data)
    doomed = [f for f in body_bm.faces
              if covered(f.calc_center_median(), f.normal) and all(covered(v.co, f.normal) for v in f.verts)]
    hidden_faces = len(doomed)
    bmesh.ops.delete(body_bm, geom=doomed, context="FACES")
    body_bm.to_mesh(main.data)
    body_bm.free()
    main.data.update()

# One skinned mesh: the body keeps its name, the other parts merge into it.
for o in scene.objects:
    o.select_set(o.type == "MESH")
bpy.context.view_layer.objects.active = main
bpy.ops.object.join()
main.parent = rig
if not any(m.type == "ARMATURE" for m in main.modifiers):
    mod = main.modifiers.new("Armature", "ARMATURE")
    mod.object = rig

# --- Posing helpers ------------------------------------------------------------------------------------------------
A_FWD = (-U).cross(F).normalized()  # rotating about this swings a downward limb toward the front
B_LEAN = U.cross(F).normalized()    # rotating about this leans the torso forward
pose_bones = rig.pose.bones
rest_q = {b.name: b.matrix_local.to_quaternion() for b in arm_data.bones}
rest_dir = {b.name: (b.tail_local - b.head_local).normalized() for b in arm_data.bones}
order = [name for name, *_ in BONES]
parents = {name: parent for name, parent, *_ in BONES}


def q(axis, degrees):
    return Quaternion(axis, math.radians(degrees))


def n(v):
    return v.normalized()


def swing(degrees, v):
    return q(A_FWD, degrees) @ v


def lean(degrees):
    return q(B_LEAN, degrees)


def twist(degrees):
    """Positive turns the right shoulder toward the front."""
    return q(U, degrees)


def arm_down(side):
    s = SIDES[side]
    return n(-U + 0.2 * s + 0.04 * F), n(-U + 0.14 * s + 0.24 * F)


FOOT_REST = {side: rest_dir[f"Foot.{side}"] for side in SIDES}


def base_pose():
    pose = {"_loc": Vector((0, 0, 0))}
    for side in SIDES:
        upper, fore = arm_down(side)
        pose[f"UpperArm.{side}"] = ("aim", upper)
        pose[f"LowerArm.{side}"] = ("aim", fore)
        pose[f"Hand.{side}"] = ("aim", fore)
        pose[f"UpperLeg.{side}"] = ("aim", swing(2, -U))
        pose[f"LowerLeg.{side}"] = ("aim", swing(-2, -U))
        pose[f"Foot.{side}"] = ("aim", FOOT_REST[side])
    for name in ("Hips", "Spine", "Chest", "Neck", "Head", "Shoulder.L", "Shoulder.R"):
        pose[name] = ("rot", Quaternion())
    return pose


def legs(pose, side, thigh, knee, ankle=0.0):
    pose[f"UpperLeg.{side}"] = ("aim", swing(thigh, -U))
    pose[f"LowerLeg.{side}"] = ("aim", swing(thigh - knee, -U))
    pose[f"Foot.{side}"] = ("aim", swing(ankle, FOOT_REST[side]))


def arm(pose, side, upper, fore):
    pose[f"UpperArm.{side}"] = ("aim", n(upper))
    pose[f"LowerArm.{side}"] = ("aim", n(fore))
    pose[f"Hand.{side}"] = ("aim", n(fore))


def idle_pose(p):
    pose = base_pose()
    b = math.sin(2 * math.pi * p)
    pose["Chest"] = ("rot", lean(-1.2 * b))
    pose["Head"] = ("rot", twist(5 * math.sin(2 * math.pi * p + 1.0)) @ lean(1.0 * b))
    pose["_loc"] = LEFT * (0.005 * H * math.sin(2 * math.pi * p)) + U * (0.002 * H * b)
    for side in SIDES:
        upper, fore = arm_down(side)
        arm(pose, side, swing(1.5 * b, upper), swing(2.5 * b, fore))
    return pose


def walk_pose(p):
    pose = base_pose()
    w = 2 * math.pi * p
    for side, offset in (("L", 0.0), ("R", 0.5)):
        phase = w + offset * 2 * math.pi
        thigh = WALK_THIGH * math.sin(phase)
        knee = 6 + 40 * max(0.0, math.cos(phase + 0.5)) ** 1.3
        legs(pose, side, thigh, knee, (thigh - knee) * 0.35)
        upper, fore = arm_down(side)
        arm_swing = -18 * math.sin(phase)
        arm(pose, side, swing(arm_swing, upper), swing(arm_swing * 1.25 + 10, fore))
    s = math.sin(w)
    pose["Hips"] = ("rot", twist(-5 * s))
    pose["Spine"] = ("rot", twist(3 * s) @ lean(4))
    pose["Chest"] = ("rot", twist(5 * s))
    pose["Head"] = ("rot", twist(-3 * s) @ lean(-4))
    pose["_loc"] = U * (0.012 * H * math.cos(2 * w) - 0.012 * H)
    return pose


def raised_pose(extra=0.0):
    pose = base_pose()
    pose["Hips"] = ("rot", twist(-10))
    pose["Spine"] = ("rot", twist(-10))
    pose["Chest"] = ("rot", twist(-12 - extra) @ lean(-6))
    pose["Head"] = ("rot", twist(28 + extra))
    pose["Shoulder.R"] = ("rot", q(F, 8 if RIGHT.x < 0 else -8))
    arm(pose, "R", U * 0.55 + RIGHT * 0.6 - F * (0.45 + extra * 0.01), U * 0.85 - F * 0.4 + RIGHT * 0.15)
    arm(pose, "L", -U * 0.55 + F * 0.7 + LEFT * 0.35, F + LEFT * 0.1 - U * 0.15)
    legs(pose, "L", 22, 30, 6)
    legs(pose, "R", -10, 26, 8)
    pose["_loc"] = -U * (0.035 * H)
    return pose


def strike_pose():
    pose = base_pose()
    pose["Hips"] = ("rot", twist(8))
    pose["Spine"] = ("rot", twist(10))
    pose["Chest"] = ("rot", twist(14) @ lean(8))
    pose["Head"] = ("rot", twist(-14))
    arm(pose, "R", F * 0.95 + LEFT * 0.25 + U * 0.15, F * 0.8 + LEFT * 0.5 + U * 0.05)
    arm(pose, "L", -U * 0.7 - F * 0.4 + LEFT * 0.4, -U * 0.6 - F * 0.2 + LEFT * 0.3)
    legs(pose, "L", 28, 22, 4)
    legs(pose, "R", -14, 26, 10)
    pose["_loc"] = -U * (0.04 * H) + F * (0.03 * H)
    return pose


def follow_pose():
    pose = strike_pose()
    pose["Hips"] = ("rot", twist(12))
    pose["Spine"] = ("rot", twist(14))
    pose["Chest"] = ("rot", twist(16) @ lean(10))
    arm(pose, "R", LEFT * 0.7 + F * 0.3 - U * 0.5, LEFT * 0.8 - U * 0.5 + F * 0.2)
    return pose


def wave_pose(amount, wave):
    rest = idle_pose(0.0)
    raised = idle_pose(0.0)
    arm(raised, "R", RIGHT * 0.9 + U * 0.45 + F * 0.15, U + RIGHT * (0.15 + 0.45 * wave) + F * 0.2)
    raised["Head"] = ("rot", twist(-6))
    return blend(rest, raised, amount)


def blend(a, b, t):
    out = {"_loc": a["_loc"].lerp(b["_loc"], t)}
    for name in order:
        kind, va = a[name]
        _, vb = b[name]
        if kind == "rot":
            out[name] = (kind, va.slerp(vb, t))
        else:
            mixed = va.lerp(vb, t)
            out[name] = (kind, n(mixed) if mixed.length > 1e-4 else vb)
    return out


def smooth(x):
    x = max(0.0, min(1.0, x))
    return x * x * (3 - 2 * x)


def keyed(keys, f):
    """keys: [(frame, pose)] sorted; smoothstep between neighbours."""
    for (f0, p0), (f1, p1) in zip(keys, keys[1:]):
        if f0 <= f <= f1:
            return blend(p0, p1, smooth((f - f0) / max(f1 - f0, 1e-6)))
    return keys[-1][1]


# --- Keying --------------------------------------------------------------------------------------------------------
for pb in pose_bones:
    pb.rotation_mode = "QUATERNION"
rig.animation_data_create()
previous = {}


def apply(pose):
    acc = {}
    for name in order:
        parent = parents[name]
        P = acc[parent] if parent else Quaternion()
        kind, value = pose[name]
        if kind == "rot":
            R = value
        else:
            R = rest_dir[name].rotation_difference(P.inverted() @ value)
        acc[name] = P @ R
        M = rest_q[name]
        local = M.inverted() @ R @ M
        if name in previous and previous[name].dot(local) < 0:
            local.negate()  # keep quaternion keys on one hemisphere so interpolation never flips
        previous[name] = local
        pose_bones[name].rotation_quaternion = local
    pose_bones["Hips"].location = rest_q["Hips"].inverted() @ pose["_loc"]


def make_clip(name, frames, pose_at):
    action = bpy.data.actions.new(name)
    action.use_fake_user = True
    rig.animation_data.action = action
    previous.clear()
    for f in range(frames + 1):
        apply(pose_at(f))
        for pb in pose_bones:
            pb.keyframe_insert("rotation_quaternion", frame=f, group=pb.name)
        pose_bones["Hips"].keyframe_insert("location", frame=f, group="Hips")
    return action


clips = {
    "Idle": make_clip("Idle", 90, lambda f: idle_pose(f / 90)),
    "Walk": make_clip("Walk", 30, lambda f: walk_pose(f / 30)),
    "WindUp": make_clip("WindUp", 24, lambda f: keyed([(0, idle_pose(0)), (10, raised_pose()), (24, raised_pose(8))], f)),
    "Slap": make_clip("Slap", 22, lambda f: keyed(
        [(0, raised_pose(8)), (7, strike_pose()), (12, follow_pose()), (22, idle_pose(0))], f)),
    "Wave": make_clip("Wave", 60, lambda f: wave_pose(
        smooth(f / 8) * smooth((60 - f) / 8), math.sin(2 * math.pi * 2.5 * f / 60))),
}
rig.animation_data.action = None

stride = measure_stride(rig, clips["Walk"], H)
for pb in pose_bones:
    pb.rotation_quaternion = Quaternion()
    pb.location = Vector()

# --- Export --------------------------------------------------------------------------------------------------------
for o in scene.objects:
    o.select_set(o in (rig, main))
bpy.context.view_layer.objects.active = rig
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

emit({
    "success": os.path.exists(output),
    "output": output,
    "height": round(H, 3),
    "facing": "+Y" if fy > 0 else "-Y",
    "bones": len(arm_data.bones),
    "triangles": sum(len(p.vertices) - 2 for p in main.data.polygons),
    "materials": [m.name for m in main.data.materials if m],
    "fallbackWeightedVertices": fallback_vertices,
    "clothVerticesFromBody": cloth_vertices,
    "hiddenBodyFacesRemoved": hidden_faces,
    "clips": {k: [int(a.frame_range[0]), int(a.frame_range[1])] for k, a in clips.items()},
    "walkStrideHeights": round(stride, 3),
    "joints": {k: [round(c, 3) for c in v] for k, v in joints.items()},
})
