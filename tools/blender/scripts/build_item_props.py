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


def srgb_to_linear(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def hex_color(h):
    h = h.lstrip("#")
    return tuple(srgb_to_linear(int(h[i:i + 2], 16) / 255.0) for i in (0, 2, 4))


def U(x, y, z):
    return Vector((x, y, z))


class Item:
    """One prop: geometry in Unity axes, one material slot per named colour."""

    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.materials = []  # [(material name, hex, roughness)]

    def mat(self, name, hex_value, roughness=0.6):
        for i, (n, _, _) in enumerate(self.materials):
            if n == name:
                return i
        self.materials.append((name, hex_value, roughness))
        return len(self.materials) - 1

    def v(self, co):
        return self.bm.verts.new(co)

    def face(self, verts, slot):
        f = self.bm.faces.new(verts)
        f.material_index = slot
        f.smooth = True
        return f

    # --- Primitives (all closed shells) ---------------------------------------------------------------------------
    def revolve(self, origin, axis, ref, profile, segments, slot, slot_fn=None):
        """Surface of revolution. profile: [(dist along axis, radius[, sx, sy])]; radius 0 at an end = a pole.
        slot_fn(band, seg) overrides the slot per face (band i joins profile rows i and i+1)."""
        axis = axis.normalized()
        side = ref.cross(axis).normalized()
        up = axis.cross(side).normalized()
        rows = []
        for p in profile:
            d, r = p[0], p[1]
            sx, sy = (p[2], p[3]) if len(p) > 2 else (1.0, 1.0)
            c = origin + axis * d
            if r <= 1e-6:
                rows.append(self.v(c))
            else:
                rows.append([self.v(c + side * (math.cos(a) * r * sx) + up * (math.sin(a) * r * sy))
                             for a in (2.0 * math.pi * i / segments for i in range(segments))])
        for b, (r0, r1) in enumerate(zip(rows, rows[1:])):
            for i in range(segments):
                j = (i + 1) % segments
                s = slot_fn(b, i) if slot_fn else slot
                if isinstance(r0, list) and isinstance(r1, list):
                    self.face([r0[i], r0[j], r1[j], r1[i]], s)
                elif isinstance(r1, list):
                    self.face([r0, r1[j], r1[i]], s)
                elif isinstance(r0, list):
                    self.face([r0[i], r0[j], r1], s)
        for end in (rows[0], rows[-1]):  # flat cap on an open end
            if isinstance(end, list):
                self.face(end, slot)

    def ellipsoid(self, center, radii, slot, segments=12, rings=8, axis="y", slot_fn=None):
        """Ellipsoid with its poles on the given Unity axis; radii are (x, y, z)."""
        rx, ry, rz = radii
        if axis == "y":
            ax, ref, ra, rs, ru = U(0, 1, 0), U(0, 0, 1), ry, rx, rz
            # side = ref x axis = (0,0,1)x(0,1,0) = -X ; up = axis x side = +Z
        elif axis == "x":
            ax, ref, ra, rs, ru = U(1, 0, 0), U(0, 1, 0), rx, rz, ry
            # side = Y x X = -Z ; up = X x -Z = +Y
        else:
            ax, ref, ra, rs, ru = U(0, 0, 1), U(0, 1, 0), rz, rx, ry
            # side = Y x Z = +X ; up = Z x X = +Y
        base = max(rs, ru)
        profile = [(-ra, 0.0)]
        for k in range(1, rings):
            t = math.pi * k / rings
            profile.append((-ra * math.cos(t), base * math.sin(t), rs / base, ru / base))
        profile.append((ra, 0.0))
        self.revolve(center, ax, ref, profile, segments, slot, slot_fn)

    def superellipsoid(self, center, half, slot, exp=0.4, segments=16, rings=10):
        """Rounded box (pillow-ish/boxy shape); exp < 1 squares it off, 1 = ellipsoid."""
        def sp(c, e):
            return math.copysign(abs(c) ** e, c)

        top, bottom = self.v(center + U(0, half.y, 0)), self.v(center - U(0, half.y, 0))
        grid = []
        for r in range(1, rings):
            th = math.pi * r / rings
            row = []
            for s in range(segments):
                ph = 2.0 * math.pi * s / segments
                row.append(self.v(center + U(half.x * sp(math.sin(th), exp) * sp(math.cos(ph), exp),
                                             half.y * sp(math.cos(th), exp),
                                             half.z * sp(math.sin(th), exp) * sp(math.sin(ph), exp))))
            grid.append(row)
        for s in range(segments):
            t = (s + 1) % segments
            self.face([top, grid[0][t], grid[0][s]], slot)
            self.face([bottom, grid[-1][s], grid[-1][t]], slot)
        for r0, r1 in zip(grid, grid[1:]):
            for s in range(segments):
                t = (s + 1) % segments
                self.face([r0[s], r0[t], r1[t], r1[s]], slot)

    def tube(self, path, radius, slot, sides=6, closed=False, ref=U(0, 1, 0), squash=(1.0, 1.0)):
        """A round tube along a polyline (closed loop or capped ends)."""
        n = len(path)
        rings = []
        for i, p in enumerate(path):
            if closed:
                t = (path[(i + 1) % n] - path[i - 1]).normalized()
            else:
                t = (path[min(i + 1, n - 1)] - path[max(i - 1, 0)]).normalized()
            side = ref.cross(t)
            if side.length < 1e-6:
                side = U(1, 0, 0).cross(t)
            side.normalize()
            up = t.cross(side).normalized()
            rings.append([self.v(p + side * (math.cos(a) * radius * squash[0]) + up * (math.sin(a) * radius * squash[1]))
                          for a in (2.0 * math.pi * k / sides for k in range(sides))])
        pairs = list(zip(rings, rings[1:])) + ([(rings[-1], rings[0])] if closed else [])
        for r0, r1 in pairs:
            for k in range(sides):
                m = (k + 1) % sides
                self.face([r0[k], r0[m], r1[m], r1[k]], slot)
        if not closed:
            self.face(rings[0], slot)
            self.face(rings[-1], slot)

    def torus(self, center, normal, ref, major, minor, slot, segments=20, sides=6):
        normal = normal.normalized()
        a = ref.cross(normal).normalized()
        b = normal.cross(a).normalized()
        path = [center + (a * math.cos(t) + b * math.sin(t)) * major
                for t in (2.0 * math.pi * i / segments for i in range(segments))]
        self.tube(path, minor, slot, sides=sides, closed=True, ref=normal)

    def slab(self, outline, offset, slot):
        """Flat prism: a convex polygon outline (in order) extruded by `offset` both ways (thin fins, flights)."""
        front = [self.v(p + offset) for p in outline]
        back = [self.v(p - offset) for p in outline]
        self.face(front, slot)
        self.face(list(reversed(back)), slot)
        n = len(outline)
        for i in range(n):
            j = (i + 1) % n
            self.face([front[i], back[i], back[j], front[j]], slot)

    # --- Finish ------------------------------------------------------------------------------------------------------
    def build(self, target):
        """Fits the Unity-axis bounds to `target` (x, y, z; None = keep that axis), centres it, converts to Blender axes
        and returns the mesh object."""
        bm = self.bm
        lo = Vector((min(v.co[i] for v in bm.verts) for i in range(3)))
        hi = Vector((max(v.co[i] for v in bm.verts) for i in range(3)))
        size, mid = hi - lo, (hi + lo) / 2
        scale = Vector((target[i] / size[i] if target[i] else 1.0 for i in range(3)))
        for vert in bm.verts:
            c = vert.co - mid
            c = Vector((c.x * scale.x, c.y * scale.y, c.z * scale.z))
            vert.co = Vector((-c.x, -c.z, c.y))  # Unity -> Blender
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        mesh = bpy.data.meshes.new(self.name)
        bm.to_mesh(mesh)
        bm.free()
        if hasattr(mesh, "set_sharp_from_angle"):
            mesh.set_sharp_from_angle(angle=math.radians(55))
        for name, hex_value, rough in self.materials:
            m = bpy.data.materials.new(name)
            m.use_nodes = True
            col = hex_color(hex_value)
            for node in m.node_tree.nodes:
                if node.type == "BSDF_PRINCIPLED":
                    node.inputs["Base Color"].default_value = (*col, 1.0)
                    node.inputs["Roughness"].default_value = rough
            m.diffuse_color = (*col, 1.0)
            mesh.materials.append(m)
        obj = bpy.data.objects.new(self.name, mesh)
        bpy.context.scene.collection.objects.link(obj)
        return obj


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
