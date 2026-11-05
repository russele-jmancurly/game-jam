"""Exports a .blend (or any importable model) to an FBX that drops into Unity with the right scale and axes.

    tools/blender/blender.sh tools/blender/scripts/export_unity_fbx.py <in.blend> <out.fbx> [--objects A,B] [--no-anim]

Unity settings: metres, Y-up / Z-forward via axis conversion, "FBX All" scale so Unity imports at scale 1, transforms
baked, no leaf bones. Write the FBX under Assets/GameJam/Art/ so Unity imports it.
"""
import os
import sys

sys.path.append(os.path.dirname(os.path.abspath(__file__)))

import bpy  # noqa: E402

from blender_common import emit, fail, load, script_args  # noqa: E402

args = script_args()
if len(args) < 2:
    fail("usage: export_unity_fbx.py <in> <out.fbx> [--objects A,B] [--no-anim]")

source, output = args[0], os.path.abspath(args[1])
names = args[args.index("--objects") + 1].split(",") if "--objects" in args else None
bake_anim = "--no-anim" not in args

load(source)

bpy.ops.object.select_all(action="DESELECT")
selected = []
for obj in bpy.context.scene.objects:
    wanted = obj.name in names if names else obj.type in {"MESH", "ARMATURE", "EMPTY"}
    if wanted:
        obj.select_set(True)
        selected.append(obj.name)

if not selected:
    fail(f"Nothing to export (objects filter: {names}).")

os.makedirs(os.path.dirname(output), exist_ok=True)
bpy.ops.export_scene.fbx(
    filepath=output,
    use_selection=True,
    apply_unit_scale=True,
    apply_scale_options="FBX_SCALE_ALL",
    axis_forward="-Z",
    axis_up="Y",
    bake_space_transform=True,
    object_types={"MESH", "ARMATURE", "EMPTY"},
    use_mesh_modifiers=True,
    mesh_smooth_type="FACE",
    add_leaf_bones=False,
    bake_anim=bake_anim,
    path_mode="COPY",
    embed_textures=False,
)

emit({"success": os.path.exists(output), "output": output, "objects": selected,
      "bytes": os.path.getsize(output) if os.path.exists(output) else 0})
