"""Renders a close-up of a character's face (to check eyes, glasses, hair), front and three-quarter.

    tools/blender/blender.sh tools/blender/scripts/render_face.py <file.fbx> <out_prefix> [--size 512] [--pose Clip:frame]

Frames the top 14 % of the model's height from the side the eyes face. Writes <out_prefix>_front.png and _side.png.
"""
import math
import os
import sys

sys.path.append(os.path.dirname(os.path.abspath(__file__)))

import bpy  # noqa: E402
from mathutils import Vector  # noqa: E402

from blender_common import emit, fail, load, script_args, set_engine, world_bounds  # noqa: E402

args = script_args()
if len(args) < 2:
    fail("usage: render_face.py <file> <out_prefix> [--size N] [--pose Clip:frame]")
source, prefix = args[0], os.path.abspath(args[1])
size = int(args[args.index("--size") + 1]) if "--size" in args else 512

meshes = load(source)
scene = bpy.context.scene
rig = next((o for o in scene.objects if o.type == "ARMATURE"), None)
if "--pose" in args and rig is not None:
    clip, _, frame = args[args.index("--pose") + 1].partition(":")
    action = next((a for a in bpy.data.actions if a.name.split("|")[-1] == clip), None)
    if action is not None:
        rig.animation_data_create()
        rig.animation_data.action = action
        if hasattr(rig.animation_data, "action_suitable_slots") and rig.animation_data.action_slot is None:
            slots = list(rig.animation_data.action_suitable_slots)
            if slots:
                rig.animation_data.action_slot = slots[0]
        scene.frame_set(int(frame or 0))

depsgraph = bpy.context.evaluated_depsgraph_get()
points = []
for o in meshes:
    evaluated = o.evaluated_get(depsgraph)
    mesh = evaluated.to_mesh()
    points += [o.matrix_world @ v.co for v in mesh.vertices]
    evaluated.to_mesh_clear()
top = max(p.z for p in points)
bottom = min(p.z for p in points)
height = top - bottom
head = [p for p in points if p.z > top - 0.14 * height]
center = sum(head, Vector()) / len(head)
eyes = [o for o in meshes if "eye" in o.name.lower()]
facing = -1.0
if eyes:
    eye_y = sum((o.matrix_world @ Vector(c)).y for o in eyes for c in o.bound_box) / (8 * len(eyes))
    facing = 1.0 if eye_y > center.y else -1.0

camera_data = bpy.data.cameras.new("FaceCamera")
camera_data.lens = 85
camera = bpy.data.objects.new("FaceCamera", camera_data)
scene.collection.objects.link(camera)
scene.camera = camera
sun_data = bpy.data.lights.new("FaceSun", type="SUN")
sun_data.energy = 3.0
sun = bpy.data.objects.new("FaceSun", sun_data)
sun.rotation_euler = (math.radians(60), 0, math.radians(20 if facing < 0 else 200))
scene.collection.objects.link(sun)
if scene.world is None:
    scene.world = bpy.data.worlds.new("FaceWorld")
scene.world.use_nodes = True
background = next(n for n in scene.world.node_tree.nodes if n.type == "BACKGROUND")
background.inputs[0].default_value = (0.78, 0.86, 0.94, 1.0)
background.inputs[1].default_value = 0.9
engine = set_engine(scene)
scene.render.resolution_x = size
scene.render.resolution_y = size
scene.render.image_settings.file_format = "PNG"

distance = 0.14 * height / math.tan(camera_data.angle / 2) * 1.1
outputs = []
for label, yaw in (("front", 0.0), ("side", 35.0)):
    direction = Vector((math.sin(math.radians(yaw)), facing * math.cos(math.radians(yaw)), 0.05)).normalized()
    camera.location = center + direction * distance
    camera.rotation_euler = (center - camera.location).to_track_quat("-Z", "Y").to_euler()
    path = f"{prefix}_{label}.png"
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    outputs.append(path)

emit({"success": True, "engine": engine, "outputs": outputs, "eyeObjects": [o.name for o in eyes],
      "meshes": [o.name for o in meshes]})
