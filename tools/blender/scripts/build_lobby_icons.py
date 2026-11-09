"""Renders the lobby sign icons (how to play, how to use items) as transparent PNGs with a cartoon outline.

    tools/blender/blender.sh tools/blender/scripts/build_lobby_icons.py [--size 512] [--out Assets/GameJam/Art/UI]

Writes Icon_Item.png (a frying pan), Icon_Throw.png (the pan flying along a dotted arc) and Icon_Knockdown.png
(Jmancurly flat on his back with stars circling his head and the pan beside him). The lobby sign
(BackyardCourseBuilder.BuildItemHowTo) shows them left to right with arrows between.

How to play (BackyardCourseBuilder.BuildHowToPlay): Icon_Slap.png (a paw mitt slapping the balloon), Icon_Hoop.png (the
balloon flying through a hoop of gold beads, like the game's), Icon_AllHoops.png (six hoops, each with a green tick) and
Icon_Win.png (a gold medal with a ribbon and confetti). Balloon pink, beads and medal from ArtPalette.
"""
import math
import os
import sys

sys.path.append(os.path.dirname(os.path.abspath(__file__)))

import bmesh  # noqa: E402
import bpy  # noqa: E402
from mathutils import Euler, Vector  # noqa: E402

from blender_common import clear_scene, emit, fail, load, script_args, set_engine, world_bounds  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
PAN = os.path.join(ROOT, "Assets", "GameJam", "Art", "Props", "Items", "FryingPan.fbx")
JMAN = os.path.join(ROOT, "Assets", "GameJam", "Characters", "Jmancurly", "Jmancurly_Game.fbx")

ACCENT = (1.0, 0.824, 0.247, 1.0)    # ArtPalette.Accent  #FFD23F
PICKET = (0.965, 0.945, 0.906, 1.0)  # ArtPalette.Picket  #F6F1E7
OUTLINE = (0.05, 0.06, 0.09, 1.0)    # dark ink
BALLOON = (1.0, 0.494, 0.714, 1.0)   # ArtPalette.CandyPink #FF7EB6
MITT = (0.851, 0.659, 0.424, 1.0)    # tan paw mitt
TICK = (0.486, 0.784, 0.294, 1.0)    # ArtPalette.Lawn #7CC84B
GOLD_DEEP = (0.949, 0.659, 0.114, 1.0)  # ArtPalette.AccentDeep #F2A81D
RIBBON_A = (0.353, 0.663, 1.0, 1.0)  # ArtPalette.CandyBlue
RIBBON_B = (0.910, 0.220, 0.310, 1.0)  # ArtPalette.Hazard
KNOCKDOWN_VIEW = (0.1, -0.7, 1.0)    # from above and a little in front

args = script_args()
size = int(args[args.index("--size") + 1]) if "--size" in args else 512
out_dir = os.path.abspath(args[args.index("--out") + 1]) if "--out" in args else os.path.join(ROOT, "Assets", "GameJam", "Art", "UI")
for path in (PAN, JMAN):
    if not os.path.exists(path):
        fail(f"missing {path}")


def srgb_to_linear(c):
    return tuple(((v + 0.055) / 1.055) ** 2.4 if v > 0.04045 else v / 12.92 for v in c[:3]) + (c[3],)


def flat_material(name, color):
    """Unlit colour (emission), so icons keep the palette exactly."""
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    nodes.clear()
    out = nodes.new("ShaderNodeOutputMaterial")
    emission = nodes.new("ShaderNodeEmission")
    emission.inputs["Color"].default_value = srgb_to_linear(color)
    emission.inputs["Strength"].default_value = 1.0
    mat.node_tree.links.new(emission.outputs["Emission"], out.inputs["Surface"])
    return mat


def outline_material():
    mat = flat_material("IconOutline", OUTLINE)
    mat.use_backface_culling = True
    return mat


def add_outline(obj, thickness):
    """Inverted hull: a solidified shell, normals flipped, drawn only from behind = an ink line round the shape."""
    if obj.type != "MESH":
        return
    obj.data.materials.append(outline_material())
    mod = obj.modifiers.new("Outline", "SOLIDIFY")
    mod.thickness = thickness
    mod.offset = 1.0
    mod.use_flip_normals = True
    mod.use_rim = False
    mod.material_offset = len(obj.data.materials) - 1


def star_mesh(name, radius, depth):
    mesh = bpy.data.meshes.new(name)
    bm = bmesh.new()
    top, bottom = [], []
    for i in range(10):
        a = math.pi / 2 + i * math.pi / 5
        r = radius if i % 2 == 0 else radius * 0.45
        top.append(bm.verts.new((math.cos(a) * r, depth / 2, math.sin(a) * r)))
        bottom.append(bm.verts.new((math.cos(a) * r, -depth / 2, math.sin(a) * r)))
    bm.faces.new(top)
    bm.faces.new(list(reversed(bottom)))
    for i in range(10):
        j = (i + 1) % 10
        bm.faces.new((top[i], top[j], bottom[j], bottom[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    obj.data.materials.append(flat_material("IconStar", ACCENT))
    return obj


def sphere(name, location, radius, color):
    bpy.ops.mesh.primitive_uv_sphere_add(radius=radius, location=location, segments=24, ring_count=12)
    obj = bpy.context.active_object
    obj.name = name
    obj.data.materials.append(flat_material(f"Icon_{name}", color))
    bpy.ops.object.shade_smooth()
    return obj


def import_model(path, location=(0, 0, 0), rotation=(0, 0, 0), scale=1.0):
    """Imports under a pivot empty and moves the pivot: the FBX roots keep the importer's own upright correction
    (setting their rotation directly threw it away and stood Jman up instead of laying him down)."""
    before = set(bpy.data.objects)
    load_into_scene(path)
    new = [o for o in bpy.data.objects if o not in before]
    pivot = bpy.data.objects.new(f"Pivot_{os.path.basename(path)}", None)
    bpy.context.scene.collection.objects.link(pivot)
    for root in [o for o in new if o.parent is None]:
        root.parent = pivot
    pivot.location = Vector(location)
    pivot.rotation_euler = Euler(rotation)
    pivot.scale = (scale, scale, scale)
    bpy.context.view_layer.update()
    return [o for o in new if o.type == "MESH"], new + [pivot]


def load_into_scene(path):
    ext = os.path.splitext(path)[1].lower()
    if ext == ".fbx":
        bpy.ops.import_scene.fbx(filepath=path)
    else:
        fail(f"unsupported {path}")


def setup_scene():
    clear_scene()
    scene = bpy.context.scene
    set_engine(scene)
    scene.render.resolution_x = size
    scene.render.resolution_y = size
    scene.render.film_transparent = True
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    try:
        scene.view_settings.view_transform = "Standard"  # keep the palette vivid (AgX/Filmic wash it out)
    except TypeError:
        pass
    if scene.world is None:
        scene.world = bpy.data.worlds.new("IconWorld")
    scene.world.use_nodes = True
    bg = next(n for n in scene.world.node_tree.nodes if n.type == "BACKGROUND")
    bg.inputs[0].default_value = (1, 1, 1, 1)
    bg.inputs[1].default_value = 0.9
    sun_data = bpy.data.lights.new("IconSun", type="SUN")
    sun_data.energy = 2.5
    sun = bpy.data.objects.new("IconSun", sun_data)
    sun.rotation_euler = (math.radians(40), math.radians(10), math.radians(25))
    scene.collection.objects.link(sun)
    return scene


def frame_and_render(scene, objects, direction, output, margin=1.12):
    """Orthographic camera looking along -direction, framing every object."""
    bpy.context.view_layer.update()
    lo, hi = world_bounds(objects)
    center = (lo + hi) / 2
    radius = max((hi - lo).length / 2, 0.001)
    cam_data = bpy.data.cameras.new("IconCamera")
    cam_data.type = "ORTHO"
    cam = bpy.data.objects.new("IconCamera", cam_data)
    scene.collection.objects.link(cam)
    d = Vector(direction).normalized()
    cam.location = center + d * radius * 4
    cam.rotation_euler = (center - cam.location).to_track_quat("-Z", "Y").to_euler()
    cam_data.clip_end = radius * 10
    scene.camera = cam
    bpy.context.view_layer.update()
    # Fit the projected bounds (8 corners) into the square frame.
    inv = cam.matrix_world.inverted()
    corners = [Vector((x, y, z)) for x in (lo.x, hi.x) for y in (lo.y, hi.y) for z in (lo.z, hi.z)]
    local = [inv @ c for c in corners]
    w = max(c.x for c in local) - min(c.x for c in local)
    h = max(c.y for c in local) - min(c.y for c in local)
    cx = (max(c.x for c in local) + min(c.x for c in local)) / 2
    cy = (max(c.y for c in local) + min(c.y for c in local)) / 2
    cam.location = cam.matrix_world @ Vector((cx, cy, 0))
    cam_data.ortho_scale = max(w, h) * margin
    scene.render.filepath = output
    bpy.ops.render.render(write_still=True)
    return os.path.exists(output)


os.makedirs(out_dir, exist_ok=True)
results = {}

# 1. The item: a frying pan, tilted toward the viewer.
scene = setup_scene()
pan_meshes, _ = import_model(PAN, rotation=(math.radians(60), 0, math.radians(-30)))
if not pan_meshes:
    fail("frying pan has no meshes")
lo, hi = world_bounds(pan_meshes)
pan_size = (hi - lo).length
for obj in pan_meshes:
    add_outline(obj, pan_size * 0.025)
results["item"] = frame_and_render(scene, pan_meshes, (0, -1, 0.25), os.path.join(out_dir, "Icon_Item.png"))

# 2. The throw: the pan at the end of a dotted arc rising from the lower left.
scene = setup_scene()
pan_meshes, pan_all = import_model(PAN, rotation=(math.radians(70), math.radians(-35), math.radians(-20)), scale=1.4)
lo, hi = world_bounds(pan_meshes)
pan_size = (hi - lo).length
span = pan_size * 2.4


def arc(t):
    """Rising, then starting to drop: a throw seen from the side."""
    return Vector((-span * 0.5 + t * span, 0, -span * 0.3 + math.sin(t * math.pi * 0.8) * span * 0.55))


pivot = next(o for o in pan_all if o.name.startswith("Pivot_"))
pivot.location += arc(1.0) - (lo + hi) / 2
bpy.context.view_layer.update()
dots = []
count = 8
for i in range(count):
    t = 0.05 + i / count * 0.8  # the trail stops just short of the pan
    dots.append(sphere(f"Dot{i}", arc(t), pan_size * (0.03 + 0.035 * t), PICKET))
for obj in pan_meshes + dots:
    add_outline(obj, pan_size * 0.022)
results["throw"] = frame_and_render(scene, pan_meshes + dots, (0, -1, 0.1), os.path.join(out_dir, "Icon_Throw.png"))

# 3. The knockdown: Jmancurly flat on his back, stars over his head, the pan beside him.
scene = setup_scene()
jman_meshes, jman_all = import_model(JMAN, rotation=(math.radians(-90), 0, math.radians(-90)))
if not jman_meshes:
    fail("Jmancurly has no meshes")
bpy.context.view_layer.update()
armature = next((o for o in jman_all if o.type == "ARMATURE"), None)
lo, hi = world_bounds(jman_meshes)
body = (hi - lo).length
head = None
if armature is not None and "Head" in armature.pose.bones:
    bone = armature.pose.bones["Head"]
    head = armature.matrix_world @ ((bone.head + bone.tail) / 2)
if head is None:
    head = Vector((hi.x, (lo.y + hi.y) / 2, hi.z))
stars = []
for i in range(5):
    a = i * math.tau / 5
    star = star_mesh(f"Star{i}", body * 0.045, body * 0.012)
    # A ring just past the crown (he lies with his head toward +X), each star turned to face the camera.
    star.location = head + Vector((body * 0.11 + math.cos(a) * body * 0.07, math.sin(a) * body * 0.12, body * 0.06 + math.sin(a * 2) * body * 0.02))
    star.rotation_euler = (Vector(KNOCKDOWN_VIEW).to_track_quat("-Y", "Z") @ Euler((0, a * 0.6, 0)).to_quaternion()).to_euler()
    stars.append(star)
pan_meshes, _ = import_model(PAN, location=((lo.x + hi.x) / 2, lo.y - body * 0.18, lo.z + body * 0.01),
                             rotation=(math.radians(-5), 0, math.radians(30)), scale=1.8)
for obj in jman_meshes:
    add_outline(obj, body * 0.006)
for obj in stars + pan_meshes:
    add_outline(obj, body * 0.008)
results["knockdown"] = frame_and_render(scene, jman_meshes + stars + pan_meshes, KNOCKDOWN_VIEW,
                                        os.path.join(out_dir, "Icon_Knockdown.png"), margin=1.12)

# --- How to play --------------------------------------------------------------------------------------------------


def balloon(center, r):
    """The game's balloon: a slightly tall sphere, a knot, a short string and a shine."""
    body = sphere("Balloon", center, r, BALLOON)
    body.scale = (1.0, 1.0, 1.12)
    knot = sphere("Knot", (center[0], center[1], center[2] - r * 1.12), r * 0.14, BALLOON)
    bpy.ops.mesh.primitive_cylinder_add(radius=r * 0.025, depth=r * 0.9, location=(center[0], center[1], center[2] - r * 1.6))
    string = bpy.context.active_object
    string.data.materials.append(flat_material("IconString", PICKET))
    shine = sphere("Shine", (center[0] - r * 0.38, center[1] - r * 0.75, center[2] + r * 0.45), r * 0.16, PICKET)
    shine.scale = (1.0, 0.5, 1.5)
    return [body, knot, string], [shine]


def box(name, location, size, color, rotation=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(size=1, location=location, rotation=rotation)
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = size
    obj.data.materials.append(flat_material(f"Icon_{name}", color))
    return obj


def bead_hoop(center, radius, beads=16, bead=0.09):
    """A hoop like the game's: a ring of gold beads, facing the camera (in the XZ plane)."""
    parts = []
    for i in range(beads):
        a = i * math.tau / beads
        parts.append(sphere(f"Bead{i}", (center[0] + math.cos(a) * radius, center[1], center[2] + math.sin(a) * radius),
                            bead * radius, ACCENT))
    return parts


def bar(name, p, q, thickness, color, y=0.0):
    """A flat bar from point p to point q (x, z) in the camera-facing plane."""
    dx, dz = q[0] - p[0], q[1] - p[1]
    length = math.hypot(dx, dz)
    return box(name, ((p[0] + q[0]) / 2, y, (p[1] + q[1]) / 2), (thickness, thickness * 0.5, length + thickness * 0.6),
               color, (0, math.atan2(dx, dz), 0))


def tick(center, size):
    """A green check mark: a short stroke down to the bottom point, a long one up to the right."""
    cx, cy, cz = center
    bottom = (cx - size * 0.12, cz - size * 0.32)
    a = bar("TickShort", (cx - size * 0.45, cz - size * 0.02), bottom, size * 0.16, TICK, cy)
    b = bar("TickLong", bottom, (cx + size * 0.45, cz + size * 0.42), size * 0.16, TICK, cy)
    return [a, b]


def outline_all(objects, thickness):
    for obj in objects:
        add_outline(obj, thickness)


# 4. Slap: a paw mitt swinging into the balloon, with motion lines and an impact burst.
scene = setup_scene()
outlined, plain = balloon((0.35, 0, 0.1), 0.42)
palm = sphere("Palm", (-0.45, 0, 0.0), 0.24, MITT)
palm.scale = (0.75, 0.45, 1.0)
palm.rotation_euler = (0, math.radians(-25), 0)
thumb = sphere("Thumb", (-0.3, -0.04, -0.17), 0.09, MITT)
thumb.scale = (1.0, 0.8, 1.6)
thumb.rotation_euler = (0, math.radians(40), 0)
cuff = sphere("Cuff", (-0.62, 0, -0.18), 0.15, PICKET)
cuff.scale = (0.8, 0.7, 0.8)
lines = [box(f"Motion{i}", (-0.95, 0, 0.18 - i * 0.18), (0.28 - i * 0.04, 0.03, 0.035), PICKET) for i in range(3)]
burst = []
hit = (-0.1, 0.12)  # where the paw meets the balloon
for i in range(5):
    a = math.radians(55 + i * 32)  # fanning up and over, away from the balloon
    burst.append(bar(f"Burst{i}", (hit[0] + math.cos(a) * 0.2, hit[1] + math.sin(a) * 0.2),
                     (hit[0] + math.cos(a) * 0.36, hit[1] + math.sin(a) * 0.36), 0.035, ACCENT, -0.1))
outline_all(outlined + [palm, thumb, cuff] + lines + burst, 0.018)
results["slap"] = frame_and_render(scene, outlined + plain + [palm, thumb, cuff] + lines + burst, (0, -1, 0.05),
                                   os.path.join(out_dir, "Icon_Slap.png"), margin=1.15)

# 5. Through a hoop: the balloon coming through the ring, a dotted trail behind it.
scene = setup_scene()
beads = bead_hoop((0, 0.05, 0), 0.62)
outlined, plain = balloon((0.12, -0.15, 0.02), 0.3)
trail = [sphere(f"Trail{i}", (-0.95 + i * 0.2, -0.15, -0.38 + math.sin(i / 4 * math.pi * 0.5) * 0.36), 0.03 + 0.008 * i, PICKET) for i in range(4)]
outline_all(beads + outlined + trail, 0.016)
results["hoop"] = frame_and_render(scene, beads + outlined + plain + trail, (0.2, -1, 0.05),
                                   os.path.join(out_dir, "Icon_Hoop.png"), margin=1.12)

# 6. All six hoops: a 3 x 2 grid of small hoops, each ticked.
scene = setup_scene()
everything = []
for row in range(2):
    for col in range(3):
        c = ((col - 1) * 0.75, 0.0, (0.5 - row) * 0.75)
        ring = bead_hoop(c, 0.27, beads=12, bead=0.13)
        marks = tick((c[0], c[1] - 0.06, c[2]), 0.32)
        everything += ring + marks
outline_all(everything, 0.012)
results["allHoops"] = frame_and_render(scene, everything, (0, -1, 0.0), os.path.join(out_dir, "Icon_AllHoops.png"), margin=1.1)

# 7. Win: a gold medal on a V of ribbon, with confetti.
scene = setup_scene()
bpy.ops.mesh.primitive_cylinder_add(radius=0.36, depth=0.08, location=(0, 0, -0.15), rotation=(math.radians(90), 0, 0))
disc = bpy.context.active_object
disc.data.materials.append(flat_material("IconGold", ACCENT))
bpy.ops.mesh.primitive_cylinder_add(radius=0.27, depth=0.1, location=(0, -0.01, -0.15), rotation=(math.radians(90), 0, 0))
inner = bpy.context.active_object
inner.data.materials.append(flat_material("IconGoldDeep", GOLD_DEEP))
star = star_mesh("MedalStar", 0.19, 0.06)
star.location = (0, -0.07, -0.15)
star.data.materials.clear()
star.data.materials.append(flat_material("IconGoldStar", ACCENT))
left = box("RibbonL", (-0.17, 0.06, 0.38), (0.2, 0.03, 0.75), RIBBON_A, (0, math.radians(-22), 0))
right = box("RibbonR", (0.17, 0.06, 0.38), (0.2, 0.03, 0.75), RIBBON_B, (0, math.radians(22), 0))
confetti = []
colors = [BALLOON, RIBBON_A, TICK, ACCENT, RIBBON_B]
for i in range(10):
    a = i * 2.39996
    r = 0.62 + (i % 3) * 0.1
    confetti.append(box(f"Confetti{i}", (math.cos(a) * r, -0.1, math.sin(a) * r * 0.85 + 0.05), (0.06, 0.02, 0.11), colors[i % 5],
                        (0, a * 1.7, 0)))
medal = [disc, inner, star, left, right]
outline_all(medal + confetti, 0.014)
results["win"] = frame_and_render(scene, medal + confetti, (0, -1, 0.05), os.path.join(out_dir, "Icon_Win.png"), margin=1.08)

emit({"success": all(results.values()), "rendered": results, "out": out_dir, "size": size})
