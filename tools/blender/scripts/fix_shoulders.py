"""Fixes squished shoulders on a character rigged by rig_character.py, in place (no unrigged source needed).

    tools/blender/blender.sh tools/blender/scripts/fix_shoulders.py <rigged.fbx> <out.fbx> [--share 1.0] [--clavicle 0.3]
        [--hide-under <skinMaterial>:<clothMaterial>[,<clothMaterial>...]] [--cover 0.03]

Why: Blender's heat weights gave the top of the shoulder (deltoid cap and the top of the sleeve) 70-100 % to the
Shoulder (collarbone) bone, which barely moves. Every clip swings the UpperArm down from the T-pose, so the cap stayed up
while the arm went down under it: thin sleeves collapsed into flaps, narrow pinched shoulders. This moves that weight from
Shoulder.<side> to UpperArm.<side>, fading in from halfway along the collarbone to just past the shoulder joint (so the
neck and trapezius stay on the collarbone), and leaves the side of the chest under the armpit alone. --share scales how
much moves (1 = all of it at the joint).

Then the clips: every one swung the whole arm ~80 degrees down at the shoulder joint alone, which squeezed the upper arm thin
and left sharp, pad-like shoulder corners (linear blend skinning loses volume at big single-joint bends). Real shoulders
share it: the collarbone dips as the arm comes down and lifts as it goes up. On every frame of every clip the Shoulder bone
takes --clavicle (default 0.3) of the upper arm's swing away from the T-pose, and the UpperArm is counter-rotated so it
points exactly where it did; the hands end up where they were, a few centimetres lower with the dropped shoulder.
Re-exports with rig_character.py's exact FBX settings.
Write back to the same *_Game.fbx path so Unity keeps the .meta GUID (and every CharacterDefinition link).

--hide-under deletes skin faces with cloth straight over them (a ray out along the face normal, from its centre and all but
one corner, hits a cloth face within --cover x height) or just under them (poking through: an inward ray hits cloth within
1.8 % of the height), for skin that pokes through a low-poly shirt as the shoulders bend
(Squoof: --hide-under Squoof:outfit_top). The open collar and arms below the sleeves stay: their rays escape.
"""
import os
import sys

sys.path.append(os.path.dirname(os.path.abspath(__file__)))

import bpy  # noqa: E402

from blender_common import emit, fail, load, script_args, world_bounds  # noqa: E402

args = script_args()
if len(args) < 2:
    fail("usage: fix_shoulders.py <rigged.fbx> <out.fbx> [--share 1.0]")
source, output = args[0], os.path.abspath(args[1])
share = float(args[args.index("--share") + 1]) if "--share" in args else 1.0
clavicle = float(args[args.index("--clavicle") + 1]) if "--clavicle" in args else 0.3
hide_under = args[args.index("--hide-under") + 1] if "--hide-under" in args else None
cover = float(args[args.index("--cover") + 1]) if "--cover" in args else 0.03

load(source)
rig = next((o for o in bpy.data.objects if o.type == "ARMATURE"), None)
mesh = next((o for o in bpy.data.objects if o.type == "MESH" and o.vertex_groups), None)
if rig is None or mesh is None:
    fail("needs a rigged model (an armature and a skinned mesh)")

lo, hi = world_bounds([mesh])
H = hi.z - lo.z
head = {b.name: rig.matrix_world @ b.head_local for b in rig.data.bones}
for name in ("Shoulder.L", "Shoulder.R", "UpperArm.L", "UpperArm.R"):
    if name not in head:
        fail(f"no {name} bone: not a rig_character.py rig")


def smoothstep(e0, e1, x):
    t = max(0.0, min(1.0, (x - e0) / max(e1 - e0, 1e-6)))
    return t * t * (3 - 2 * t)


groups = {g.name: g for g in mesh.vertex_groups}
index_of = {g.index: g.name for g in mesh.vertex_groups}
moved = {}
for side in ("L", "R"):
    shoulder, upper = groups[f"Shoulder.{side}"], groups[f"UpperArm.{side}"]
    s = 1.0 if head[f"UpperArm.{side}"].x > 0 else -1.0
    inner = abs(head[f"Shoulder.{side}"].x)
    joint = head[f"UpperArm.{side}"]
    jx = abs(joint.x)
    start, end = inner + 0.5 * (jx - inner), jx + 0.01 * H
    count, total = 0, 0.0
    for v in mesh.data.vertices:
        co = mesh.matrix_world @ v.co
        if co.x * s <= 0:
            continue
        weights = {index_of[g.group]: g.weight for g in v.groups}
        w_sh = weights.get(shoulder.name, 0.0)
        if w_sh < 1e-4:
            continue
        ax = abs(co.x)
        t = smoothstep(start, end, ax)
        if ax < jx + 0.03 * H:
            t *= smoothstep(joint.z - 0.10 * H, joint.z - 0.03 * H, co.z)  # the chest's side under the armpit stays put
        move = w_sh * t * share
        if move < 1e-4:
            continue
        shoulder.add([v.index], w_sh - move, "REPLACE")
        upper.add([v.index], weights.get(upper.name, 0.0) + move, "REPLACE")
        count += 1
        total += move
    moved[side] = {"vertices": count, "weightMoved": round(total, 2)}

hidden = 0
if hide_under:
    import bmesh  # noqa: E402
    from mathutils.bvhtree import BVHTree  # noqa: E402

    skin_name, cloth_names = hide_under.split(":", 1)
    cloth_names = [c for c in cloth_names.split(",") if c]
    slots = [m.name if m else "" for m in mesh.data.materials]
    skin_slots = {i for i, n in enumerate(slots) if n == skin_name}
    cloth_slots = {i for i, n in enumerate(slots) if any(n.startswith(c) for c in cloth_names)}
    if not skin_slots or not cloth_slots:
        fail(f"--hide-under: materials not found (have {slots})")
    bm = bmesh.new()
    bm.from_mesh(mesh.data)
    bm.faces.ensure_lookup_table()
    cloth_bm = bm.copy()
    bmesh.ops.delete(cloth_bm, geom=[f for f in cloth_bm.faces if f.material_index not in cloth_slots], context="FACES")
    tree = BVHTree.FromBMesh(cloth_bm)
    scale = max(mesh.matrix_world.to_scale().x, 1e-6)
    reach = cover * H / scale  # mesh space
    poke = 0.018 * H / scale
    doomed = []
    for f in bm.faces:
        if f.material_index not in skin_slots:
            continue
        n = f.normal

        def covered(p, n=n):
            # Cloth straight over the skin, or skin poking just through it (cloth right under it: Squoof's shoulder in
            # the T-pose). An arm below the sleeve or the neck never has cloth just inside it, so they stay.
            return (tree.ray_cast(p + n * 1e-4, n, reach)[0] is not None
                    or tree.ray_cast(p - n * 1e-4, -n, poke)[0] is not None)

        corners = [v.co for v in f.verts]
        if covered(f.calc_center_median()) and sum(1 for c in corners if covered(c)) >= len(corners) - 1:
            doomed.append(f)
    bmesh.ops.delete(bm, geom=doomed, context="FACES")
    bm.to_mesh(mesh.data)
    bm.free()
    cloth_bm.free()
    hidden = len(doomed)

# --- Clips: the collarbone takes a share of the arm's swing ----------------------------------------------------------
from mathutils import Matrix, Quaternion, Vector  # noqa: E402

scene = bpy.context.scene
rig.animation_data_create()
keyed_frames = 0
if clavicle > 0:
    for action in list(bpy.data.actions):
        rig.animation_data.action = action
        start, end = (int(round(v)) for v in action.frame_range)
        frames = range(start, end + 1)
        baked = {}  # frame -> {bone: (rotation_quaternion, location)}
        for f in frames:
            scene.frame_set(f)
            bpy.context.view_layer.update()
            result = {}
            for side in ("L", "R"):
                sh = rig.pose.bones[f"Shoulder.{side}"]
                ua = rig.pose.bones[f"UpperArm.{side}"]
                m_sh, m_ua = sh.matrix.copy(), ua.matrix.copy()  # armature space, as animated
                rest_dir = ua.bone.matrix_local.to_3x3() @ Vector((0, 1, 0))
                now_dir = m_ua.to_3x3() @ Vector((0, 1, 0))
                swing = rest_dir.rotation_difference(now_dir)  # the arm's swing away from the T-pose
                q = Quaternion().slerp(swing, clavicle)
                pivot = m_sh.to_translation()
                turn = Matrix.Translation(pivot) @ q.to_matrix().to_4x4() @ Matrix.Translation(-pivot)
                sh.matrix = turn @ m_sh
                bpy.context.view_layer.update()
                # The upper arm keeps its animated direction, riding on the dropped/raised shoulder.
                new_head = turn @ m_ua.to_translation()
                ua.matrix = Matrix.LocRotScale(new_head, m_ua.to_quaternion(), m_ua.to_scale())
                bpy.context.view_layer.update()
                result[sh.name] = (sh.rotation_quaternion.copy(), sh.location.copy())
                result[ua.name] = (ua.rotation_quaternion.copy(), ua.location.copy())
            baked[f] = result
        # Overwrite those bones' keys on every frame (the imported clips are keyed on every frame, so nothing old remains;
        # Blender 5 has no Action.fcurves to clear them with).
        for f, bones in baked.items():
            for name, (rot, loc) in bones.items():
                pb = rig.pose.bones[name]
                pb.location = loc
                pb.keyframe_insert("location", frame=f, group=name)
                if pb.rotation_mode == "QUATERNION":
                    pb.rotation_quaternion = rot
                    pb.keyframe_insert("rotation_quaternion", frame=f, group=name)
                else:
                    pb.rotation_euler = rot.to_euler(pb.rotation_mode)
                    pb.keyframe_insert("rotation_euler", frame=f, group=name)
                keyed_frames += 1

# Export exactly as rig_character.py does (clips are the imported actions, all exported as takes).
for o in bpy.context.scene.objects:
    o.select_set(o in (rig, mesh))
bpy.context.view_layer.objects.active = rig
if rig.animation_data:
    rig.animation_data.action = None
for pb in rig.pose.bones:
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
emit({"success": os.path.exists(output), "output": output, "height": round(H, 3), "moved": moved, "hiddenSkinFaces": hidden, "clavicleShare": clavicle, "keyedBoneFrames": keyed_frames,
      "actions": [a.name for a in bpy.data.actions]})
