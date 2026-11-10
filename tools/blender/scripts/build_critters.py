"""Builds the backyard critter audience (frog, snail, squirrel, ladybug, songbird, mouse) and exports one Unity FBX each.

    tools/blender/blender.sh tools/blender/scripts/build_critters.py [out_dir] [--only Frog,Snail]

Default out_dir: Assets/GameJam/Art/Critters. Chunky cartoon toys in the yard's candy palette with big googly eyes, built
from the same primitives as the item props (toy_mesh.py), each a few hundred triangles (the yard shows ~40 of them on
Quest). Origin at the middle of the base (they stand on logs and the lawn), facing +Z in Unity, transforms applied.

One shared material for every critter: each face's colour is a cell of a small palette texture (T_CritterPalette.png,
written next to the models, 8 x 8 cells of 4 px), picked by its UVs. The dressing builder gives all critters one URP
material with that texture (point filtered, no mipmaps), so the whole audience batches as one material instead of ~6 each.
"""
import math
import os
import sys

sys.path.append(os.path.dirname(os.path.abspath(__file__)))

import bpy  # noqa: E402
from mathutils import Vector  # noqa: E402

from blender_common import clear_scene, emit, fail, script_args  # noqa: E402
from toy_mesh import Item, U  # noqa: E402

args = script_args()
positional = [a for i, a in enumerate(args) if not a.startswith("--") and (i == 0 or args[i - 1] != "--only")]
OUT_DIR = os.path.abspath(positional[0] if positional else "Assets/GameJam/Art/Critters")
ONLY = set(args[args.index("--only") + 1].split(",")) if "--only" in args else None

# --- Palette (sRGB hex, ArtPalette where it has one) -----------------------------------------------------------------
INK, WHITE, CREAM, PINK = "#15171C", "#F6F1E7", "#F3D9B1", "#FF7EB6"
FROG, FROG_DARK = "#8FD16A", "#5FA83A"
SNAIL_BODY, SHELL, SHELL_DARK = "#B8D8B0", "#E8B66A", "#B9823F"
SQUIRREL, SQUIRREL_TAIL = "#D9644A", "#E8875F"
BUG_RED, BUG_BLACK = "#E8384F", "#343A46"
BIRD, BIRD_DARK, BIRD_BELLY, BEAK = "#5AA9FF", "#3D7FD1", "#FFD23F", "#FF8A3D"
MOUSE, MOUSE_DARK = "#C9C2B8", "#A39C93"

CELLS = 8      # palette cells per row
CELL_PX = 4    # pixels per cell
palette = []   # hex values in cell order, shared by every critter


def cell(hex_value):
    if hex_value not in palette:
        palette.append(hex_value)
        if len(palette) > CELLS * CELLS:
            fail("palette full")
    return palette.index(hex_value)


def eyes(it, centre, radius, spread, up=0.0):
    """A pair of googly eyes looking forward (+Z): white balls with big ink pupils."""
    white, pupil = it.mat("White", WHITE), it.mat("Ink", INK)
    for sx in (-1, 1):
        c = centre + U(sx * spread, up, 0)
        it.ellipsoid(c, (radius, radius, radius), white, segments=8, rings=5)
        it.ellipsoid(c + U(sx * radius * 0.08, radius * 0.1, radius * 0.62), (radius * 0.5, radius * 0.55, radius * 0.45), pupil, segments=6, rings=4)


# --- Critters (Unity axes: X right, Y up, Z forward) ------------------------------------------------------------------
def frog():
    it = Item("Frog")
    body, dark, belly, cheek = it.mat("Frog", FROG), it.mat("FrogDark", FROG_DARK), it.mat("Cream", CREAM), it.mat("Pink", PINK)
    it.ellipsoid(U(0, 0.22, 0), (0.3, 0.2, 0.32), body, segments=12, rings=6)
    it.ellipsoid(U(0, 0.17, 0.13), (0.22, 0.13, 0.2), belly, segments=10, rings=5)
    for sx in (-1, 1):
        it.ellipsoid(U(sx * 0.13, 0.4, 0.1), (0.1, 0.09, 0.09), body, segments=8, rings=5)  # eye bumps
        it.ellipsoid(U(sx * 0.27, 0.1, -0.1), (0.1, 0.09, 0.2), dark, segments=8, rings=5)   # haunches
        it.ellipsoid(U(sx * 0.3, 0.03, 0.12), (0.08, 0.03, 0.1), dark, segments=6, rings=4)  # back feet
        it.ellipsoid(U(sx * 0.15, 0.03, 0.27), (0.07, 0.03, 0.07), dark, segments=6, rings=4)  # front feet
        it.ellipsoid(U(sx * 0.2, 0.25, 0.24), (0.05, 0.03, 0.02), cheek, segments=6, rings=3)
    eyes(it, U(0, 0.44, 0.15), 0.065, 0.13)
    return it, (0.7, 0.55, 0.7)


def snail():
    it = Item("Snail")
    body, shell, dark = it.mat("SnailBody", SNAIL_BODY), it.mat("Shell", SHELL), it.mat("ShellDark", SHELL_DARK)
    it.ellipsoid(U(0, 0.09, 0.05), (0.15, 0.09, 0.42), body, segments=10, rings=6, axis="z")  # foot
    it.ellipsoid(U(0, 0.27, 0.33), (0.11, 0.17, 0.11), body, segments=10, rings=6)            # neck and head
    for sx in (-1, 1):
        it.tube([U(sx * 0.04, 0.4, 0.34), U(sx * 0.08, 0.52, 0.36), U(sx * 0.1, 0.6, 0.37)], 0.018, body, sides=5)
    eyes(it, U(0, 0.64, 0.37), 0.05, 0.1)
    it.ellipsoid(U(0, 0.4, -0.1), (0.17, 0.3, 0.3), shell, segments=12, rings=7)
    for sx in (-1, 1):  # the spiral, two rings on each side
        it.torus(U(sx * 0.15, 0.41, -0.1), U(sx, 0, 0), U(0, 1, 0), 0.2, 0.03, dark, segments=14, sides=4)
        it.torus(U(sx * 0.17, 0.42, -0.08), U(sx, 0, 0), U(0, 1, 0), 0.09, 0.03, dark, segments=10, sides=4)
    return it, (0.5, 0.75, 0.9)


def squirrel():
    it = Item("Squirrel")
    fur, tail, belly, ink = it.mat("Squirrel", SQUIRREL), it.mat("SquirrelTail", SQUIRREL_TAIL), it.mat("Cream", CREAM), it.mat("Ink", INK)
    it.ellipsoid(U(0, 0.35, 0), (0.22, 0.32, 0.2), fur, segments=12, rings=6)            # sitting up
    it.ellipsoid(U(0, 0.33, 0.11), (0.15, 0.24, 0.11), belly, segments=10, rings=5)
    it.ellipsoid(U(0, 0.78, 0.05), (0.17, 0.16, 0.16), fur, segments=12, rings=6)
    it.ellipsoid(U(0, 0.73, 0.19), (0.08, 0.065, 0.07), belly, segments=8, rings=5)       # snout
    it.ellipsoid(U(0, 0.76, 0.26), (0.025, 0.02, 0.015), ink, segments=6, rings=3)        # nose
    for sx in (-1, 1):
        it.ellipsoid(U(sx * 0.1, 0.96, 0.02), (0.045, 0.08, 0.03), fur, segments=6, rings=4)   # ears
        it.ellipsoid(U(sx * 0.1, 0.5, 0.2), (0.05, 0.08, 0.05), fur, segments=6, rings=4)      # paws up, cheering
        it.ellipsoid(U(sx * 0.11, 0.04, 0.12), (0.07, 0.04, 0.1), fur, segments=6, rings=4)    # feet
    eyes(it, U(0, 0.83, 0.17), 0.05, 0.075)
    it.tube([U(0, 0.12, -0.18), U(0, 0.3, -0.4), U(0, 0.62, -0.46), U(0, 0.9, -0.36), U(0, 1.04, -0.16)], 0.13, tail, sides=8,
            ref=U(1, 0, 0))
    return it, (0.6, 1.15, 0.8)


def ladybug():
    it = Item("Ladybug")
    red, black = it.mat("BugRed", BUG_RED), it.mat("BugBlack", BUG_BLACK)
    it.ellipsoid(U(0, 0.09, 0.02), (0.25, 0.08, 0.3), black, segments=10, rings=5, axis="z")
    it.ellipsoid(U(0, 0.17, -0.03), (0.28, 0.16, 0.31), red, segments=12, rings=7)
    it.ellipsoid(U(0, 0.31, -0.03), (0.012, 0.03, 0.27), black, segments=4, rings=4, axis="z")  # wing seam
    centre, radii = U(0, 0.17, -0.03), Vector((0.28, 0.16, 0.31))
    for sx in (-1, 1):
        for lat, lon in ((0.55, 0.45), (0.95, -0.15), (0.6, -0.6)):  # three spots a side, on the shell
            d = Vector((sx * math.cos(lat) * math.cos(lon), math.sin(lat), math.cos(lat) * math.sin(lon)))
            p = centre + Vector((d.x * radii.x, d.y * radii.y, d.z * radii.z)) * 0.97
            it.ellipsoid(p, (0.055, 0.04, 0.055), black, segments=6, rings=3)
        it.tube([U(sx * 0.05, 0.22, 0.36), U(sx * 0.1, 0.33, 0.42), U(sx * 0.13, 0.38, 0.4)], 0.012, black, sides=4)
        for z in (-0.12, 0.05, 0.2):
            it.tube([U(sx * 0.2, 0.08, z), U(sx * 0.3, 0.02, z + 0.02)], 0.018, black, sides=4)  # legs
    it.ellipsoid(U(0, 0.15, 0.3), (0.15, 0.1, 0.1), black, segments=10, rings=5)  # head
    eyes(it, U(0, 0.2, 0.37), 0.045, 0.07)
    return it, (0.62, 0.38, 0.78)


def songbird():
    it = Item("Songbird")
    blue, dark, belly, beak = it.mat("Bird", BIRD), it.mat("BirdDark", BIRD_DARK), it.mat("BirdBelly", BIRD_BELLY), it.mat("Beak", BEAK)
    it.ellipsoid(U(0, 0.26, -0.01), (0.16, 0.19, 0.2), blue, segments=12, rings=6)
    it.ellipsoid(U(0, 0.22, 0.08), (0.12, 0.14, 0.13), belly, segments=10, rings=5)
    it.ellipsoid(U(0, 0.46, 0.04), (0.13, 0.12, 0.12), blue, segments=10, rings=6)
    it.revolve(U(0, 0.45, 0.14), U(0, -0.1, 1), U(1, 0, 0), [(0.0, 0.045), (0.06, 0.02), (0.09, 0.0)], 8, beak)
    for sx in (-1, 1):
        it.ellipsoid(U(sx * 0.15, 0.27, -0.03), (0.04, 0.12, 0.15), dark, segments=8, rings=4)  # wings
        it.tube([U(sx * 0.05, 0.1, 0.0), U(sx * 0.05, 0.0, 0.02)], 0.012, beak, sides=4)        # legs
        it.ellipsoid(U(sx * 0.05, 0.01, 0.05), (0.03, 0.01, 0.05), beak, segments=6, rings=3)   # toes
    it.slab([U(-0.07, 0.24, -0.18), U(0.07, 0.24, -0.18), U(0.1, 0.36, -0.36), U(-0.1, 0.36, -0.36)], U(0, 0.015, 0), dark)  # tail
    eyes(it, U(0, 0.5, 0.12), 0.04, 0.065)
    return it, (0.45, 0.62, 0.58)


def mouse():
    it = Item("Mouse")
    fur, dark, pink = it.mat("Mouse", MOUSE), it.mat("MouseDark", MOUSE_DARK), it.mat("Pink", PINK)
    it.ellipsoid(U(0, 0.2, -0.02), (0.17, 0.19, 0.24), fur, segments=12, rings=6)
    it.ellipsoid(U(0, 0.36, 0.16), (0.13, 0.12, 0.14), fur, segments=10, rings=6)
    it.ellipsoid(U(0, 0.35, 0.3), (0.035, 0.03, 0.03), pink, segments=6, rings=3)  # nose
    for sx in (-1, 1):
        it.ellipsoid(U(sx * 0.12, 0.52, 0.1), (0.09, 0.09, 0.02), dark, segments=8, rings=4)   # big ears
        it.ellipsoid(U(sx * 0.12, 0.52, 0.115), (0.06, 0.06, 0.012), pink, segments=8, rings=4)
        it.ellipsoid(U(sx * 0.09, 0.04, 0.12), (0.05, 0.03, 0.07), pink, segments=6, rings=3)  # feet
    it.tube([U(0, 0.08, -0.24), U(0, 0.05, -0.38), U(0.08, 0.08, -0.5), U(0.15, 0.2, -0.5), U(0.14, 0.3, -0.42)], 0.02, pink, sides=4)
    eyes(it, U(0, 0.42, 0.25), 0.04, 0.06)
    return it, (0.45, 0.58, 0.68)


CRITTERS = [frog, snail, squirrel, ladybug, songbird, mouse]


def palette_uvs(obj, item):
    """Points every face's UVs at its colour's palette cell and leaves one material on the mesh."""
    mesh = obj.data
    slot_cell = [cell(hex_value) for _, hex_value, _ in item.materials]
    uv = mesh.uv_layers.new(name="UVMap")
    size = CELLS
    for poly in mesh.polygons:
        index = slot_cell[poly.material_index]
        u, v = (index % size + 0.5) / size, 1.0 - (index // size + 0.5) / size
        for loop in poly.loop_indices:
            uv.data[loop].uv = (u, v)
        poly.material_index = 0
    mesh.materials.clear()
    material = bpy.data.materials.get("CritterPalette") or bpy.data.materials.new("CritterPalette")
    mesh.materials.append(material)


def write_palette(path):
    side = CELLS * CELL_PX
    image = bpy.data.images.new("T_CritterPalette", side, side, alpha=False)
    pixels = [1.0] * (side * side * 4)
    for index, hex_value in enumerate(palette):
        h = hex_value.lstrip("#")
        rgb = [int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)]
        cx, cy = index % CELLS, CELLS - 1 - index // CELLS  # Blender rows start at the bottom
        for y in range(cy * CELL_PX, (cy + 1) * CELL_PX):
            for x in range(cx * CELL_PX, (cx + 1) * CELL_PX):
                o = (y * side + x) * 4
                pixels[o:o + 4] = [*rgb, 1.0]
    image.pixels = pixels
    image.filepath_raw = path
    image.file_format = "PNG"
    image.save()


# --- Build and export ----------------------------------------------------------------------------------------------
os.makedirs(OUT_DIR, exist_ok=True)
results = {}
for build in CRITTERS:
    clear_scene()
    item, target = build()
    if ONLY and item.name not in ONLY:
        for _, hex_value, _ in item.materials:
            cell(hex_value)  # keep the palette order the same with or without --only
        continue
    obj = item.build(target, anchor="bottom")
    palette_uvs(obj, item)
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
    results[item.name] = {"fbx": path, "triangles": len(mesh.loop_triangles),
                          "unitySize": [round(size.x, 3), round(size.z, 3), round(size.y, 3)]}

if not results:
    fail("No critters built (check --only).")
palette_path = os.path.join(OUT_DIR, "T_CritterPalette.png")
write_palette(palette_path)
emit({"success": True, "critters": results, "palette": palette_path, "colours": len(palette)})
