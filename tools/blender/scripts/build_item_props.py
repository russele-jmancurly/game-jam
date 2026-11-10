"""Builds the throwable item props as chunky, rounded toy-style meshes and exports one Unity FBX per item.

    tools/blender/blender.sh tools/blender/scripts/build_item_props.py [out_dir] [--only RubberDuck,Pillow]

Default out_dir: Assets/GameJam/Art/Props/Items. Each item is one mesh object, origin at the centre of its bounding
box, no textures (plain Principled materials named after the part, e.g. "DuckYellow"), transforms applied.

Everything is modelled in UNITY axes (X right, Y up, Z forward) and converted to Blender at the end:
Unity (x, y, z) -> Blender (-x, -z, y), which the FBX export (axis_forward=-Z, axis_up=Y) maps back to Unity (x, y, z).
Each item is then scaled to its target bounding-box size, which is also the collider the game builds for it.
"""
import math
import os
import sys

sys.path.append(os.path.dirname(os.path.abspath(__file__)))

import bmesh  # noqa: E402
import bpy  # noqa: E402
from mathutils import Vector  # noqa: E402

from blender_common import clear_scene, emit, fail, script_args  # noqa: E402
from toy_mesh import Item, U  # noqa: E402

args = script_args()
positional = [a for i, a in enumerate(args) if not a.startswith("--") and (i == 0 or args[i - 1] != "--only")]
OUT_DIR = os.path.abspath(positional[0] if positional else "Assets/GameJam/Art/Props/Items")
ONLY = set(args[args.index("--only") + 1].split(",")) if "--only" in args else None

# --- Palette (sRGB hex) ----------------------------------------------------------------------------------------------
YELLOW, ORANGE, RED, PINK = "#FFD23F", "#FF8A3D", "#E8384F", "#FF7EB6"
PURPLE, BLUE, MINT, WHITE = "#A77BF3", "#5AA9FF", "#6FE3C1", "#F6F1E7"
STEEL, CHARCOAL, WOOD, GREEN = "#8E97A6", "#343A46", "#B9823F", "#5FA83A"
INK = "#15171C"          # eyes
MELON_DARK = "#2F6B25"   # watermelon dark stripes
STEM = "#7A5A2E"


# --- Items ---------------------------------------------------------------------------------------------------------
def rubber_duck():
    it = Item("RubberDuck")
    body, beak, eye = it.mat("DuckYellow", YELLOW, 0.45), it.mat("DuckBeak", ORANGE, 0.5), it.mat("DuckEye", INK, 0.3)
    it.ellipsoid(U(0, -0.03, -0.01), (0.072, 0.045, 0.088), body, segments=14, rings=9, axis="z")  # chubby body
    it.revolve(U(0, -0.02, -0.07), U(0, 0.55, -0.6), U(1, 0, 0),                                       # perky tail
               [(0.0, 0.035), (0.03, 0.025), (0.055, 0.009), (0.065, 0.0)], 10, body)
    it.ellipsoid(U(0, 0.035, 0.035), (0.046, 0.044, 0.044), body, segments=14, rings=8)               # head
    it.ellipsoid(U(0, 0.025, 0.084), (0.028, 0.010, 0.026), beak, segments=10, rings=5)                # flat beak
    for sx in (-1, 1):
        it.ellipsoid(U(sx * 0.026, 0.048, 0.070), (0.007, 0.009, 0.006), eye, segments=8, rings=5)
        it.ellipsoid(U(sx * 0.058, -0.02, -0.01), (0.018, 0.026, 0.05), body, segments=8, rings=5)    # wing bumps
    return it, (0.15, 0.15, 0.2)


def pillow():
    it = Item("Pillow")
    cloth, seam = it.mat("PillowPink", PINK, 0.75), it.mat("PillowSeam", WHITE, 0.7)
    W, D, H = 0.2, 0.15, 0.072  # half sizes (x, z), half thickness
    nx, nz = 10, 8

    def puff(t):
        return math.cos(t * math.pi / 2) ** 0.35

    def plan(u, w):
        # slight pinch along the edges so the corners poke out like real pillow "ears"
        return U(u * W * (1.0 - 0.06 * (1 - abs(w) ** 2)), 0, w * D * (1.0 - 0.07 * (1 - abs(u) ** 2)))

    us = [-1 + 2 * i / nx for i in range(nx + 1)]
    ws = [-1 + 2 * k / nz for k in range(nz + 1)]
    edge = {}
    grids = []
    for sign in (1, -1):
        grid = []
        for i, u in enumerate(us):
            row = []
            for k, w in enumerate(ws):
                on_edge = i in (0, nx) or k in (0, nz)
                if on_edge and (i, k) in edge:
                    row.append(edge[(i, k)])
                    continue
                p = plan(u, w) + U(0, sign * H * puff(u) * puff(w), 0)
                vert = it.v(p)
                if on_edge:
                    edge[(i, k)] = vert
                row.append(vert)
            grid.append(row)
        grids.append(grid)
    for grid in grids:
        for i in range(nx):
            for k in range(nz):
                it.face([grid[i][k], grid[i + 1][k], grid[i + 1][k + 1], grid[i][k + 1]], cloth)
    # Piping round the seam, which also hides the knife edge where the two halves meet.
    loop = ([plan(u, -1) for u in us[:-1]] + [plan(1, w) for w in ws[:-1]] +
            [plan(u, 1) for u in reversed(us[1:])] + [plan(-1, w) for w in reversed(ws[1:])])
    it.tube(loop, 0.008, seam, sides=5, closed=True, ref=U(0, 1, 0))
    return it, (0.4, 0.15, 0.3)


def beach_ball():
    it = Item("BeachBall")
    gores = [it.mat("BallRed", RED, 0.45), it.mat("BallWhite", WHITE, 0.45), it.mat("BallBlue", BLUE, 0.45),
             it.mat("BallYellow", YELLOW, 0.45), it.mat("BallWhite", WHITE, 0.45), it.mat("BallMint", MINT, 0.45)]
    cap = it.mat("BallWhite", WHITE)
    rings, segs = 14, 24

    def slot(band, s):
        if band < 2 or band >= rings - 2:
            return cap
        return gores[s // (segs // 6)]

    it.ellipsoid(U(0, 0, 0), (0.2, 0.2, 0.2), cap, segments=segs, rings=rings, slot_fn=slot)
    it.ellipsoid(U(0, 0.2, 0), (0.022, 0.006, 0.022), cap, segments=10, rings=4)  # valve button on the top cap
    return it, (0.4, 0.4, 0.4)


def frying_pan():
    it = Item("FryingPan")
    pan, handle, rivet = it.mat("PanCharcoal", CHARCOAL, 0.45), it.mat("PanHandleWood", WOOD, 0.65), it.mat("PanSteel", STEEL, 0.4)
    # Profile (dist along +Y, radius): outside up the flared wall, rolled rim, down the inside to the cooking surface.
    profile = [(-0.010, 0.0), (-0.010, 0.118), (-0.008, 0.128), (-0.002, 0.136), (0.006, 0.143), (0.0095, 0.150),
               (0.010, 0.146), (0.007, 0.138), (-0.001, 0.130), (-0.004, 0.120), (-0.004, 0.0)]
    it.revolve(U(0, 0, 0), U(0, 1, 0), U(0, 0, 1), profile, 28, pan)
    # Steel collar where the handle meets the pan, then a long wood grip with a rounded end.
    it.revolve(U(0, 0.003, 0.140), U(0, 0.05, 1), U(0, 1, 0),
               [(0.0, 0.0), (0.0, 0.010, 1.0, 0.65), (0.04, 0.009, 1.0, 0.7), (0.045, 0.0)], 8, rivet)
    it.revolve(U(0, 0.003, 0.178), U(0, 0.05, 1), U(0, 1, 0),
               [(0.0, 0.0), (0.0, 0.0105, 1.15, 0.68), (0.06, 0.012, 1.2, 0.65), (0.15, 0.014, 1.2, 0.6),
                (0.175, 0.012, 1.2, 0.55), (0.185, 0.0)], 8, handle)
    return it, (0.3, 0.02, None)


def watermelon():
    it = Item("Watermelon")
    light, dark, stem = it.mat("MelonGreen", GREEN, 0.55), it.mat("MelonStripe", MELON_DARK, 0.55), it.mat("MelonStem", STEM, 0.8)
    segs, rings = 32, 16

    def slot(band, s):
        return dark if s % 4 < 2 else light  # straight stripes pole to pole, two faces dark, two light

    it.ellipsoid(U(0, 0, 0), (0.175, 0.14, 0.14), light, segments=segs, rings=rings, axis="x", slot_fn=slot)
    it.revolve(U(0.17, 0, 0), U(1, 0, 0), U(0, 1, 0), [(0.0, 0.009), (0.015, 0.006), (0.018, 0.0)], 6, stem)
    return it, (0.35, 0.28, 0.28)


def darts():
    it = Item("Darts")
    tip, barrel, shaft = it.mat("DartTip", STEEL, 0.35), it.mat("DartBarrel", BLUE, 0.45), it.mat("DartShaft", CHARCOAL, 0.5)
    flights = [it.mat("DartFlightRed", RED, 0.6), it.mat("DartFlightYellow", YELLOW, 0.6)]
    up = U(0, 1, 0)
    # Steel point (top), knurled barrel, thin shaft, flights at the bottom.
    it.revolve(U(0, 0.012, 0), up, U(0, 0, 1),
               [(0.0, 0.0), (0.0, 0.0028), (0.034, 0.0012), (0.038, 0.0)], 8, tip)
    it.revolve(U(0, -0.022, 0), up, U(0, 0, 1),
               [(0.0, 0.0), (0.0, 0.0035), (0.004, 0.0062), (0.010, 0.0066), (0.012, 0.0060), (0.016, 0.0066),
                (0.022, 0.0066), (0.024, 0.0060), (0.028, 0.0066), (0.031, 0.0060), (0.034, 0.0035), (0.034, 0.0)],
               10, barrel)
    it.revolve(U(0, -0.046, 0), up, U(0, 0, 1), [(0.0, 0.0), (0.0, 0.0025), (0.025, 0.0025), (0.025, 0.0)], 6, shaft)
    for k in range(3):
        a = math.radians(90 + 120 * k)
        out = U(math.cos(a), 0, math.sin(a))
        normal = up.cross(out).normalized() * 0.0009
        y0 = -0.05
        outline = [out * 0.002 + U(0, y0, 0), out * 0.020 + U(0, y0 + 0.003, 0),
                   out * 0.020 + U(0, y0 + 0.020, 0), out * 0.002 + U(0, y0 + 0.036, 0)]
        it.slab(outline, normal, flights[k % 2])
    return it, (0.04, 0.1, 0.04)


def bubble_wand():
    it = Item("BubbleWand")
    pink, cap, ring = it.mat("WandPink", PINK, 0.5), it.mat("WandCapPurple", PURPLE, 0.5), it.mat("WandRing", PINK, 0.5)
    up = U(0, 1, 0)
    # Chunky handle (bottom half) with a rounded butt and grip ridges.
    it.revolve(U(0, -0.2, 0), up, U(0, 0, 1),
               [(0.0, 0.0), (0.0, 0.009), (0.006, 0.0145), (0.05, 0.0155), (0.055, 0.0135), (0.06, 0.0155),
                (0.10, 0.0155), (0.105, 0.0135), (0.11, 0.0155), (0.16, 0.0150), (0.172, 0.0)], 10, pink)
    # Bottle cap: a wide purple disc with a ribbed edge sitting on top of the handle.
    it.revolve(U(0, -0.03, 0), up, U(0, 0, 1),
               [(0.0, 0.0), (0.0, 0.034), (0.003, 0.0395), (0.020, 0.0395), (0.024, 0.036), (0.026, 0.026),
                (0.026, 0.0)], 20, cap)
    # Thin stem up to the bubble loop.
    it.revolve(U(0, -0.004, 0), up, U(0, 0, 1), [(0.0, 0.0), (0.0, 0.006), (0.13, 0.006), (0.13, 0.0)], 6, pink)
    it.torus(U(0, 0.161, 0), U(0, 0, 1), U(0, 1, 0), 0.034, 0.0055, ring, segments=22, sides=6)
    return it, (0.08, 0.4, 0.08)


def leaf_blower():
    it = Item("LeafBlower")
    body, grip, tube, dark = (it.mat("BlowerOrange", ORANGE, 0.5), it.mat("BlowerGrip", CHARCOAL, 0.7),
                              it.mat("BlowerTube", WHITE, 0.5), it.mat("BlowerFan", CHARCOAL, 0.6))
    # Engine housing at the back, a fan cover on each side, top handle with a grip, tube forward to a flat nozzle.
    it.superellipsoid(U(0, -0.05, -0.17), U(0.105, 0.1, 0.13), body, exp=0.45, segments=18, rings=10)
    for sx in (-1, 1):
        it.revolve(U(sx * 0.095, -0.06, -0.17), U(sx, 0, 0), U(0, 1, 0),
                   [(0.0, 0.0), (0.0, 0.075), (0.02, 0.07), (0.028, 0.045), (0.03, 0.0)], 16, dark)
    handle_path = [U(0, 0.03, -0.27), U(0, 0.10, -0.25), U(0, 0.135, -0.19), U(0, 0.135, -0.11), U(0, 0.10, -0.05),
                   U(0, 0.03, -0.035)]
    it.tube(handle_path, 0.016, body, sides=6, ref=U(1, 0, 0))
    it.tube([U(0, 0.137, -0.205), U(0, 0.137, -0.095)], 0.02, grip, sides=8, ref=U(1, 0, 0))
    it.revolve(U(0, -0.07, -0.06), U(0, 0, 1), U(0, 1, 0),
               [(0.0, 0.0), (0.0, 0.05), (0.03, 0.047), (0.36, 0.04), (0.40, 0.042, 1.25, 0.8),
                (0.44, 0.046, 1.55, 0.5), (0.44, 0.0, 1.55, 0.5)], 12, tube)
    it.revolve(U(0, -0.07, -0.05), U(0, 0, 1), U(0, 1, 0),
               [(0.0, 0.0), (0.0, 0.054), (0.04, 0.054), (0.04, 0.0)], 12, grip)  # charcoal collar at the tube root
    it.tube([U(0, -0.07, 0.10), U(0, -0.14, 0.12), U(0, -0.15, 0.15)], 0.012, grip, sides=6, ref=U(1, 0, 0))  # front grip
    return it, (0.25, 0.3, 0.6)


ITEMS = [rubber_duck, pillow, beach_ball, frying_pan, watermelon, darts, bubble_wand, leaf_blower]

# --- Build and export ----------------------------------------------------------------------------------------------
os.makedirs(OUT_DIR, exist_ok=True)
results = {}
for build in ITEMS:
    clear_scene()
    item, target = build()
    if ONLY and item.name not in ONLY:
        continue
    obj = item.build(target)
    mesh = obj.data
    mesh.calc_loop_triangles()
    lo = Vector((min(v.co[i] for v in mesh.vertices) for i in range(3)))
    hi = Vector((max(v.co[i] for v in mesh.vertices) for i in range(3)))
    size = hi - lo
    path = os.path.join(OUT_DIR, f"{item.name}.fbx")
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={"MESH"}, global_scale=1.0, apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y", bake_space_transform=False,
        add_leaf_bones=False, bake_anim=False, mesh_smooth_type="FACE")
    results[item.name] = {
        "fbx": path,
        "triangles": len(mesh.loop_triangles),
        "unitySize": [round(size.x, 4), round(size.z, 4), round(size.y, 4)],
        "materials": {n: h for n, h, _ in item.materials},
    }

if not results:
    fail("No items built (check --only).")
emit({"success": True, "items": results})
