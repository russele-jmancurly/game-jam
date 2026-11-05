"""Renders a quick PNG preview of a model or .blend from a 3/4 angle (replaces the MCP "look").

    tools/blender/blender.sh tools/blender/scripts/render_preview.py <file> <out.png> [--size 768] [--angle 35]

The camera frames all mesh objects; a sun and a soft world colour light the scene. Prints JSON with the output path.
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
    fail("usage: render_preview.py <file> <out.png> [--size N] [--angle DEG]")

source, output = args[0], os.path.abspath(args[1])
size = int(args[args.index("--size") + 1]) if "--size" in args else 768
yaw = math.radians(float(args[args.index("--angle") + 1])) if "--angle" in args else math.radians(35)

meshes = load(source)
if not meshes:
    fail("No mesh objects to render.")

scene = bpy.context.scene
lo, hi = world_bounds(meshes)
center = (lo + hi) / 2
radius = max((hi - lo).length / 2, 0.001)

camera_data = bpy.data.cameras.new("PreviewCamera")
camera_data.lens = 50
camera = bpy.data.objects.new("PreviewCamera", camera_data)
scene.collection.objects.link(camera)
distance = radius / math.tan(camera_data.angle / 2) * 0.95
direction = Vector((math.sin(yaw), -math.cos(yaw), 0.55)).normalized()
camera.location = center + direction * distance
camera.rotation_euler = (center - camera.location).to_track_quat("-Z", "Y").to_euler()
camera_data.clip_end = distance * 10
scene.camera = camera

sun_data = bpy.data.lights.new("PreviewSun", type="SUN")
sun_data.energy = 3.5
sun = bpy.data.objects.new("PreviewSun", sun_data)
sun.rotation_euler = (math.radians(50), 0, math.radians(30))
scene.collection.objects.link(sun)

if scene.world is None:
    scene.world = bpy.data.worlds.new("PreviewWorld")
scene.world.use_nodes = True
background = next(n for n in scene.world.node_tree.nodes if n.type == "BACKGROUND")
background.inputs[0].default_value = (0.78, 0.86, 0.94, 1.0)
background.inputs[1].default_value = 0.8

engine = set_engine(scene)
scene.render.resolution_x = size
scene.render.resolution_y = size
scene.render.film_transparent = False
scene.render.image_settings.file_format = "PNG"
scene.render.filepath = output
os.makedirs(os.path.dirname(output), exist_ok=True)
bpy.ops.render.render(write_still=True)

emit({"success": os.path.exists(output), "output": output, "engine": engine, "size": size,
      "framedSize": [round(v, 4) for v in (hi - lo)]})
