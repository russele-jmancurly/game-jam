"""Makes a Quest-friendly character model for the game from a .blend/.fbx: static mesh, triangle budget, Unity FBX.

    tools/blender/blender.sh tools/blender/scripts/prepare_character.py <in> <out.fbx> [--max-tris 6000] [--exclude GloveL,GloveR] [--budget Hair=3000] [--max-texture 1024] [--rest-pose]

- Drops the objects named in --exclude (e.g. the model's own gloves: the avatar has networked gloves).
- Bakes armatures into the rest pose and removes them, so Unity renders a plain mesh (no skinning cost on Quest).
- Decimates every mesh by one shared ratio so the total stays under --max-tris (default 6000). --budget Name=N gives a
  mesh its own triangle budget (out of the total), for one dense part (e.g. 700k-triangle hair) that would starve the rest.
- Writes packed base colour / normal maps into <out>.fbm (downscaled to --max-texture), where Unity finds them.
- Exports with the same Unity settings as export_unity_fbx.py. Prints JSON with before/after triangle counts.
"""
import os
import sys

sys.path.append(os.path.dirname(os.path.abspath(__file__)))

import bpy  # noqa: E402

from blender_common import emit, fail, load, script_args, world_bounds  # noqa: E402

args = script_args()
if len(args) < 2:
    fail("usage: prepare_character.py <in> <out.fbx> [--max-tris N] [--exclude A,B]")

source, output = args[0], os.path.abspath(args[1])
max_tris = int(args[args.index("--max-tris") + 1]) if "--max-tris" in args else 6000
exclude = set(args[args.index("--exclude") + 1].split(",")) if "--exclude" in args else set()
budgets = {k: int(v) for k, v in (p.split("=") for p in args[args.index("--budget") + 1].split(","))} if "--budget" in args else {}

load(source)
scene = bpy.context.scene


def triangle_count():
    depsgraph = bpy.context.evaluated_depsgraph_get()
    total = 0
    for obj in scene.objects:
        if obj.type == "MESH" and obj.visible_get():
            mesh = obj.evaluated_get(depsgraph).to_mesh()
            mesh.calc_loop_triangles()
            total += len(mesh.loop_triangles)
            obj.evaluated_get(depsgraph).to_mesh_clear()
    return total


# 1. Remove excluded objects and anything that isn't a mesh or armature (cameras, lights, empties used as helpers).
removed = []
for obj in list(scene.objects):
    if obj.name in exclude or obj.type not in {"MESH", "ARMATURE"}:
        removed.append(obj.name)
        bpy.data.objects.remove(obj, do_unlink=True)

before = triangle_count()

# Shape keys (facial expressions) block applying modifiers and aren't used by the game: keep only the basis.
for obj in [o for o in scene.objects if o.type == "MESH"]:
    if obj.data.shape_keys:
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.shape_key_remove(all=True, apply_mix=False)

# 2. Bake armatures: apply each mesh's Armature modifier at the model's current pose (frame 1 of its idle, usually arms
#    down) or, with --rest-pose, the bind pose (often a T-pose), then delete the armatures.
use_rest_pose = "--rest-pose" in args
scene.frame_set(scene.frame_start)
for armature in [o for o in scene.objects if o.type == "ARMATURE"]:
    armature.data.pose_position = "REST" if use_rest_pose else "POSE"
bpy.context.view_layer.update()

for obj in [o for o in scene.objects if o.type == "MESH"]:
    bpy.context.view_layer.objects.active = obj
    for modifier in list(obj.modifiers):
        if modifier.type == "ARMATURE" and modifier.object is None:
            obj.modifiers.remove(modifier)  # points at no armature: a no-op Blender refuses to apply
            continue
        if modifier.type == "ARMATURE":
            matrix = obj.matrix_world.copy()
            obj.parent = None
            obj.matrix_world = matrix
            bpy.ops.object.modifier_apply(modifier=modifier.name)

for armature in [o for o in scene.objects if o.type == "ARMATURE"]:
    bpy.data.objects.remove(armature, do_unlink=True)

# Bake rotation and scale into the mesh data. Source FBX files are often Y-up, which leaves a 90 degree object
# rotation; exported as-is, Unity would show the character lying down.
bpy.ops.object.select_all(action="DESELECT")
for obj in [o for o in scene.objects if o.type == "MESH"]:
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)

# 3. Decimate to the triangle budget: meshes named in --budget get their own share, the rest one shared ratio.
def mesh_triangles(obj):
    depsgraph = bpy.context.evaluated_depsgraph_get()
    mesh = obj.evaluated_get(depsgraph).to_mesh()
    mesh.calc_loop_triangles()
    count = len(mesh.loop_triangles)
    obj.evaluated_get(depsgraph).to_mesh_clear()
    return count


def decimate(obj, obj_ratio):
    if obj_ratio < 1.0:
        modifier = obj.modifiers.new("GameDecimate", "DECIMATE")
        modifier.decimate_type = "COLLAPSE"
        modifier.ratio = obj_ratio
        modifier.use_collapse_triangulate = True


ratio = 1.0
if before > max_tris:
    meshes = [o for o in scene.objects if o.type == "MESH"]
    budgeted = [o for o in meshes if o.name in budgets]
    for obj in budgeted:
        decimate(obj, min(1.0, budgets[obj.name] / max(1, mesh_triangles(obj))))
    rest = [o for o in meshes if o.name not in budgets]
    rest_tris = sum(mesh_triangles(o) for o in rest)
    remaining = max(0, max_tris - sum(min(budgets[o.name], mesh_triangles(o)) for o in budgeted))
    ratio = min(1.0, remaining / rest_tris) if rest_tris else 1.0
    for obj in rest:
        decimate(obj, ratio)

after = triangle_count()

# 4a. Packed textures: the FBX exporter only copies images that exist as files, so write the base colour and normal maps
#     the exported materials use into <name>.fbm next to the FBX (where Unity looks), downscaled to --max-texture.
max_texture = int(args[args.index("--max-texture") + 1]) if "--max-texture" in args else 1024
written = []


def linked_image(socket):
    """The image feeding a shader socket, directly or through a Normal Map node."""
    if not socket.is_linked:
        return None
    node = socket.links[0].from_node
    if node.type == "NORMAL_MAP":
        return linked_image(node.inputs["Color"])
    return node.image if node.type == "TEX_IMAGE" else None


fbm = os.path.splitext(output)[0] + ".fbm"
for material in {slot.material for obj in scene.objects if obj.type == "MESH" for slot in obj.material_slots if slot.material}:
    if not material.use_nodes:
        continue
    for node in material.node_tree.nodes:
        if node.type != "BSDF_PRINCIPLED":
            continue
        for socket_name in ("Base Color", "Normal"):
            image = linked_image(node.inputs[socket_name])
            if image is None or image.packed_file is None:
                continue
            os.makedirs(fbm, exist_ok=True)
            name = os.path.splitext(bpy.path.basename(image.filepath) or image.name)[0] + ".png"
            path = os.path.join(fbm, name)
            copy = image.copy()
            if max(copy.size) > max_texture:
                copy.scale(min(copy.size[0], max_texture), min(copy.size[1], max_texture))
            copy.filepath_raw = path
            copy.file_format = "PNG"
            copy.save()
            image.filepath = path
            image.source = "FILE"
            written.append(name)

# 4. Export for Unity (modifiers applied on export).
meshes = [o for o in scene.objects if o.type == "MESH"]
if not meshes:
    fail("No meshes left to export.")

bpy.ops.object.select_all(action="DESELECT")
for obj in meshes:
    obj.select_set(True)

os.makedirs(os.path.dirname(output), exist_ok=True)
bpy.ops.export_scene.fbx(
    filepath=output,
    use_selection=True,
    apply_unit_scale=True,
    apply_scale_options="FBX_SCALE_ALL",
    axis_forward="-Z",
    axis_up="Y",
    bake_space_transform=True,
    object_types={"MESH"},
    use_mesh_modifiers=True,
    mesh_smooth_type="FACE",
    bake_anim=False,
    path_mode="COPY",
    embed_textures=False,
)

lo, hi = world_bounds(meshes)
emit({
    "success": os.path.exists(output),
    "output": output,
    "removed": removed,
    "trianglesBefore": before,
    "trianglesAfter": after,
    "decimateRatio": round(ratio, 4),
    "meshes": [o.name for o in meshes],
    "boundsSize": [round(v, 4) for v in (hi - lo)],
    "bytes": os.path.getsize(output) if os.path.exists(output) else 0,
    "texturesWritten": written,
})
