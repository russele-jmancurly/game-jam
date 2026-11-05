"""Reports what a model or .blend contains: objects, size, triangle counts, materials, textures, armatures.

    tools/blender/blender.sh tools/blender/scripts/inspect.py <file.blend|.fbx|.obj|.glb>

Prints JSON between @@RESULT_BEGIN@@ / @@RESULT_END@@. Sizes are in Blender metres (Z-up).
"""
import os
import sys

sys.path.append(os.path.dirname(os.path.abspath(__file__)))

import bpy  # noqa: E402

from blender_common import emit, fail, load, script_args, world_bounds  # noqa: E402

args = script_args()
if not args:
    fail("usage: inspect.py <file>")

meshes = load(args[0])
depsgraph = bpy.context.evaluated_depsgraph_get()

objects = []
total_tris = 0
for obj in bpy.context.scene.objects:
    entry = {"name": obj.name, "type": obj.type, "parent": obj.parent.name if obj.parent else None}
    if obj.type == "MESH":
        mesh = obj.evaluated_get(depsgraph).to_mesh()
        mesh.calc_loop_triangles()
        tris = len(mesh.loop_triangles)
        total_tris += tris
        entry.update({
            "triangles": tris,
            "vertices": len(mesh.vertices),
            "materials": [slot.material.name for slot in obj.material_slots if slot.material],
            "uv_layers": [uv.name for uv in obj.data.uv_layers],
            "dimensions": [round(d, 4) for d in obj.dimensions],
            "modifiers": [m.type for m in obj.modifiers],
        })
        obj.evaluated_get(depsgraph).to_mesh_clear()
    elif obj.type == "ARMATURE":
        entry["bones"] = len(obj.data.bones)
    objects.append(entry)

bounds = world_bounds(meshes)
textures = sorted({
    os.path.basename(node.image.filepath or node.image.name)
    for mat in bpy.data.materials if mat.use_nodes
    for node in mat.node_tree.nodes if node.type == "TEX_IMAGE" and node.image
})

emit({
    "success": True,
    "file": os.path.abspath(args[0]),
    "blender": bpy.app.version_string,
    "objectCount": len(objects),
    "meshCount": len(meshes),
    "totalTriangles": total_tris,
    "boundsSize": [round(v, 4) for v in (bounds[1] - bounds[0])] if bounds else None,
    "boundsMin": [round(v, 4) for v in bounds[0]] if bounds else None,
    "materials": sorted(m.name for m in bpy.data.materials),
    "textures": textures,
    "animations": sorted(a.name for a in bpy.data.actions),
    "objects": objects,
})
