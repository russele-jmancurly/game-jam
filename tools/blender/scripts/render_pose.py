"""Renders frames of a rigged model's animations, to check a rig and its clips.

    tools/blender/blender.sh tools/blender/scripts/render_pose.py <file.fbx> <out_prefix> <Clip:frame> [...] [--size 512] [--angle 35]

Writes <out_prefix>_<Clip>_<frame>.png for each request (clip name matched by suffix, e.g. "Walk" finds
"Armature|Walk"). "Rest:0" renders the bind pose. Prints JSON with the outputs.
"""
import math
import os
import sys

sys.path.append(os.path.dirname(os.path.abspath(__file__)))

import bpy  # noqa: E402
from mathutils import Vector  # noqa: E402

from blender_common import emit, fail, load, script_args, set_engine, world_bounds  # noqa: E402

args = script_args()
size = int(args[args.index("--size") + 1]) if "--size" in args else 512
yaw = math.radians(float(args[args.index("--angle") + 1])) if "--angle" in args else math.radians(35)
positional = [a for i, a in enumerate(args) if not a.startswith("--") and (i == 0 or not args[i - 1].startswith("--"))]
if len(positional) < 3:
    fail("usage: render_pose.py <file> <out_prefix> <Clip:frame> [...]")
source, prefix, requests = positional[0], os.path.abspath(positional[1]), positional[2:]

meshes = load(source)
rig = next((o for o in bpy.context.scene.objects if o.type == "ARMATURE"), None)
if rig is None:
    fail("No armature in the file.")
if rig.animation_data is None:
    rig.animation_data_create()

scene = bpy.context.scene
lo, hi = world_bounds(meshes)
center = (lo + hi) / 2
radius = max((hi - lo).length / 2, 0.001)

camera_data = bpy.data.cameras.new("PoseCamera")
camera = bpy.data.objects.new("PoseCamera", camera_data)
scene.collection.objects.link(camera)
distance = radius / math.tan(camera_data.angle / 2) * 1.05
sun_data = bpy.data.lights.new("PoseSun", type="SUN")
sun_data.energy = 3.5
sun = bpy.data.objects.new("PoseSun", sun_data)
sun.rotation_euler = (math.radians(50), 0, math.radians(30))
scene.collection.objects.link(sun)
if scene.world is None:
    scene.world = bpy.data.worlds.new("PoseWorld")
scene.world.use_nodes = True
background = next(nd for nd in scene.world.node_tree.nodes if nd.type == "BACKGROUND")
background.inputs[0].default_value = (0.78, 0.86, 0.94, 1.0)
engine = set_engine(scene)
scene.render.resolution_x = size
scene.render.resolution_y = size
scene.render.image_settings.file_format = "PNG"
scene.camera = camera

outputs = []
for request in requests:
    clip, _, frame = request.partition(":")
    frame = int(frame or 0)
    if clip.lower() == "rest":
        rig.animation_data.action = None
        for pb in rig.pose.bones:
            pb.matrix_basis.identity()
    else:
        action = next((a for a in bpy.data.actions if a.name.split("|")[-1] == clip), None)
        if action is None:
            fail(f"No clip '{clip}'. Clips: {[a.name for a in bpy.data.actions]}")
        rig.animation_data.action = action
        if hasattr(rig.animation_data, "action_suitable_slots") and rig.animation_data.action_slot is None:
            slots = list(rig.animation_data.action_suitable_slots)
            if slots:
                rig.animation_data.action_slot = slots[0]
    scene.frame_set(frame)
    direction = Vector((math.sin(yaw), -math.cos(yaw), 0.35)).normalized()
    camera.location = center + direction * distance
    camera.rotation_euler = (center - camera.location).to_track_quat("-Z", "Y").to_euler()
    path = f"{prefix}_{clip}_{frame}.png"
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    outputs.append(path)

emit({"success": True, "engine": engine, "outputs": outputs, "clips": [a.name for a in bpy.data.actions]})
