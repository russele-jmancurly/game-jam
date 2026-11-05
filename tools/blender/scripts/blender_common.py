"""Shared helpers for the headless Blender scripts (run through tools/blender/blender.sh)."""
import json
import os
import sys

import bpy

RESULT_BEGIN = "@@RESULT_BEGIN@@"
RESULT_END = "@@RESULT_END@@"


def script_args():
    """Arguments after Blender's `--` separator."""
    return sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []


def emit(result):
    """Prints a JSON result between markers so callers can parse it out of Blender's log noise."""
    print(RESULT_BEGIN)
    print(json.dumps(result, indent=1, default=str))
    print(RESULT_END)


def fail(message):
    emit({"success": False, "error": message})
    sys.exit(1)


def clear_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def load(path):
    """Opens a .blend, or imports a model file into an empty scene. Returns the imported/loaded mesh objects."""
    path = os.path.abspath(path)
    if not os.path.exists(path):
        fail(f"File not found: {path}")

    ext = os.path.splitext(path)[1].lower()
    if ext == ".blend":
        bpy.ops.wm.open_mainfile(filepath=path)
    else:
        clear_scene()
        importers = {
            ".fbx": lambda: bpy.ops.import_scene.fbx(filepath=path),
            ".obj": lambda: bpy.ops.wm.obj_import(filepath=path),
            ".glb": lambda: bpy.ops.import_scene.gltf(filepath=path),
            ".gltf": lambda: bpy.ops.import_scene.gltf(filepath=path),
            ".stl": lambda: bpy.ops.wm.stl_import(filepath=path),
        }
        if ext not in importers:
            fail(f"Unsupported file type: {ext}")
        importers[ext]()

    return [o for o in bpy.context.scene.objects if o.type == "MESH"]


def world_bounds(objects):
    """Axis-aligned world bounds (Blender Z-up) of the given objects."""
    from mathutils import Vector

    corners = [o.matrix_world @ Vector(c) for o in objects for c in o.bound_box]
    if not corners:
        return None
    lo = Vector((min(c.x for c in corners), min(c.y for c in corners), min(c.z for c in corners)))
    hi = Vector((max(c.x for c in corners), max(c.y for c in corners), max(c.z for c in corners)))
    return lo, hi


def set_engine(scene, preferred=("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE", "BLENDER_WORKBENCH", "CYCLES")):
    """Engine identifiers differ between Blender versions; take the first one this build accepts."""
    for engine in preferred:
        try:
            scene.render.engine = engine
            return engine
        except TypeError:
            continue
    return scene.render.engine
