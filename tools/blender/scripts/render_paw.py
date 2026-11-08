"""Close-up of the player body's right paw, open and/or curled into a fist, from the exported FBX (what Unity gets).

    tools/blender/blender.sh tools/blender/scripts/render_paw.py Assets/GameJam/Art/Characters/PorcupineBody.fbx Screenshots/paw [--curl 0,0.5,1] [--size 360]

For each curl amount it writes <out>_curl<amount>.png: two views side by side (palm side, from above: shows the curl). The curl uses
the same rule as GorillaBodyRig in Unity: every finger bone turns about the axis that swings its tip toward the palm
(cross(bone direction, palm normal)) by CURL_DEGREES x amount; the thumb folds toward the palm and down.
"""
import math
import os
import sys

sys.path.append(os.path.dirname(os.path.abspath(__file__)))

import bpy  # noqa: E402
import numpy as np  # noqa: E402
from mathutils import Quaternion, Vector  # noqa: E402

from blender_common import emit, fail, load, script_args, set_engine  # noqa: E402

# Keep in step with GorillaBodyRig (Unity) so the preview shows the in-game fist.
CURL_DEGREES = {"1": 60.0, "2": 70.0, "3": 50.0}
THUMB_DEGREES = {"1": 30.0, "2": 40.0}
FINGERS = ("Index", "Middle", "Ring")

args = script_args()
if len(args) < 2:
    fail("usage: render_paw.py <body.fbx> <out_prefix> [--curl 0,1] [--size N]")
source, prefix = args[0], os.path.abspath(args[1])
curls = [float(c) for c in args[args.index("--curl") + 1].split(",")] if "--curl" in args else [0.0, 1.0]
size = int(args[args.index("--size") + 1]) if "--size" in args else 360

load(source)
armature = next((o for o in bpy.data.objects if o.type == "ARMATURE"), None)
if armature is None:
    fail("No armature in " + source)
scene = bpy.context.scene
bones = armature.data.bones


def world_matrix(name):
    return armature.matrix_world @ bones[name].matrix_local


def head_world(name):
    return world_matrix(name).to_translation()


def direction_world(name):
    return (world_matrix(name).to_3x3() @ Vector((0, 1, 0))).normalized()


missing = [n for n in ("Hand.R", "GloveCenter.R") if n not in bones]
if missing:
    fail("Missing bones: " + ", ".join(missing))

hand = head_world("Hand.R")
palm_normal = Vector((-1.0 if hand.x > 0 else 1.0, 0.0, 0.0))  # toward the body's midline
down = Vector((0, 0, -1))


def set_curl(amount):
    for pb in armature.pose.bones:
        pb.rotation_mode = "QUATERNION"
        pb.rotation_quaternion = Quaternion()
    for s in ("L", "R"):
        palm = Vector((-1.0 if head_world(f"Hand.{s}").x > 0 else 1.0, 0.0, 0.0))
        chains = [(f, CURL_DEGREES, None) for f in FINGERS] + [("Thumb", THUMB_DEGREES, palm * 0.6 + down)]
        for finger, degrees, target in chains:
            for index, angle in degrees.items():
                name = f"{finger}{index}.{s}"
                if name not in bones:
                    continue
                d = direction_world(name)
                axis_world = d.cross(target.normalized() if target is not None else palm)
                if axis_world.length < 1e-6:
                    continue
                to_local = world_matrix(name).to_3x3().normalized().inverted()
                axis_local = (to_local @ axis_world).normalized()
                armature.pose.bones[name].rotation_quaternion = Quaternion(axis_local, math.radians(angle * amount))
    bpy.context.view_layer.update()


camera_data = bpy.data.cameras.new("PawCamera")
camera_data.lens = 50
camera_data.clip_start = 0.01
camera = bpy.data.objects.new("PawCamera", camera_data)
scene.collection.objects.link(camera)
scene.camera = camera
sun_data = bpy.data.lights.new("PawSun", type="SUN")
sun_data.energy = 3.5
sun = bpy.data.objects.new("PawSun", sun_data)
sun.rotation_euler = (math.radians(40), 0, math.radians(-30))
scene.collection.objects.link(sun)
if scene.world is None:
    scene.world = bpy.data.worlds.new("PawWorld")
scene.world.use_nodes = True
background = next(n for n in scene.world.node_tree.nodes if n.type == "BACKGROUND")
background.inputs[0].default_value = (0.78, 0.86, 0.94, 1.0)
background.inputs[1].default_value = 0.9
engine = set_engine(scene)
scene.render.resolution_x = size
scene.render.resolution_y = size
scene.render.image_settings.file_format = "PNG"

forward = Vector((0, -1, 0))
target = head_world("GloveCenter.R")
views = {
    "palm": (palm_normal * 1.0 + forward * 0.7 + Vector((0, 0, 0.3))).normalized(),
    "top": (Vector((0, 0, 1)) + forward * 0.35 + palm_normal * 0.15).normalized(),
}
tmp_dir = os.path.join(os.path.dirname(prefix), "_paw_tmp")
os.makedirs(tmp_dir, exist_ok=True)
written = []
for amount in curls:
    set_curl(amount)
    tiles = []
    for view, direction in views.items():
        camera.location = target + direction * 0.42
        camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Z").to_euler()
        path = os.path.join(tmp_dir, f"{view}.png")
        scene.render.filepath = path
        bpy.ops.render.render(write_still=True)
        image = bpy.data.images.load(path)
        tiles.append(np.array(image.pixels[:], dtype=np.float32).reshape(size, size, 4))
        bpy.data.images.remove(image)
    sheet = np.concatenate(tiles, axis=1)
    out = bpy.data.images.new("PawSheet", width=size * len(tiles), height=size, alpha=True)
    out.pixels = sheet.ravel()
    output = f"{prefix}_curl{amount:g}.png"
    os.makedirs(os.path.dirname(output), exist_ok=True)
    out.filepath_raw = output
    out.file_format = "PNG"
    out.save()
    bpy.data.images.remove(out)
    written.append(output)

emit({"success": True, "outputs": written, "engine": engine,
      "fingerBones": sorted(b.name for b in bones if any(b.name.startswith(f) for f in FINGERS + ("Thumb",)))})
