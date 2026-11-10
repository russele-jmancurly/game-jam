"""Builds the stylised backyard set pieces (visual replacements for BackyardCourseBuilder's collider boxes) as Unity FBX.

    tools/blender/blender.sh tools/blender/scripts/build_set_pieces.py [--only Tree,Slide] [--out Assets/GameJam/Art/Set]
                                                                       [--check]

Every piece is modelled in Unity axes ("U-space": X right, Y up, Z forward, metres) at the same offsets from its anchor
as the builder's boxes, then mirrored into Blender axes (Unity (x, y, z) -> Blender (-x, -z, y)) just before export, so
the FBX lands in Unity exactly over the colliders. Walkable tops (branches, slide deck/chute, merry-go-round disc, picnic
top, pool rim) are flush with the collider tops; decoration lives elsewhere. Plain coloured materials, named for Unity's
remap-by-name (Bark, LeavesLight, PoolBlue...). --check re-imports every FBX and prints the Unity-space heights of the
walkable surfaces and each file's root rotation.

Pieces and anchors (all at ground unless noted):
    Tree             TreeCenter                       FencePicket   picket base centre (identity rotation = south fence)
    PaddlingPool     PoolCenter                       RotaryPole    WashingLineCenter
    Grill            BbqCenter + (0, 0, -6)           RotaryArms    the spinning arms' centre (y = 2.4 above ground)
    MerryGoRound     the Mover (disc centre, 0.25 up) Slide         PlaygroundCenter + (10, 0, 8)
    SwingSet         PlaygroundCenter + (-9, 0, -4)   Trampoline    PlaygroundCenter + (12, 0, -6)
    PicnicTable      BbqCenter + (0, 0, 5)           Cactus        its pot's base centre (BackyardCourseBuilder.CactusPot)
"""
import math
import os
import struct
import sys
import zlib

sys.path.append(os.path.dirname(os.path.abspath(__file__)))

import bmesh  # noqa: E402
import bpy  # noqa: E402
from mathutils import Matrix, Vector, noise  # noqa: E402

from blender_common import clear_scene, emit, fail, script_args  # noqa: E402

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))

# Unity (x, y, z) -> Blender (-x, -z, y). A mirror (det -1), so normals are recalculated after it.
U_TO_B = Matrix(((-1, 0, 0, 0), (0, 0, -1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))

# Art bible colours (ArtPalette.cs) plus a few darker/derived tones.
COLOURS = {
    "Bark": (0xB9823F, 0.8), "BarkDark": (0x7E5627, 0.85), "Leaves": (0x5FA83A, 0.7), "LeavesLight": (0x7CC84B, 0.7),
    "PoolBlue": (0x5AA9FF, 0.5), "PoolWhite": (0xF6F1E7, 0.5), "PoolSeam": (0xC9D6E3, 0.6), "PoolFloor": (0x8CC4FF, 0.6),
    "Valve": (0xF6F1E7, 0.5),
    "GrillBody": (0x343A46, 0.6), "Grate": (0x1F232B, 0.5), "Coals": (0x2A2A2E, 0.9), "Embers": (0xFF8A3D, 0.6),
    "Steel": (0x8E97A6, 0.5), "Tyre": (0x23262E, 0.8), "Timber": (0xE8B66A, 0.7), "TimberDark": (0xB9823F, 0.75),
    "MerryPink": (0xFF7EB6, 0.6), "MerryWhite": (0xF6F1E7, 0.6),
    "SlideChute": (0xA77BF3, 0.5), "Climb": (0xFF8A3D, 0.6),
    "TrampolineMat": (0x1E2028, 0.8), "PadBlue": (0x5AA9FF, 0.6),
    "Line": (0xF6F1E7, 0.6), "Cap": (0xE8384F, 0.5), "Picket": (0xF6F1E7, 0.7),
    "CactusGreen": (0x4E9A3A, 0.75), "CactusRidge": (0x7CC84B, 0.7), "Spine": (0xFFF4D6, 0.5),
    "Terracotta": (0xC2614F, 0.8), "TerracottaRim": (0xD9644A, 0.75), "PotSoil": (0x5A3B22, 0.95),
    "CactusFlower": (0xFF7EB6, 0.5), "FlowerHeart": (0xFFD23F, 0.5),
}


def srgb_to_linear(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def make_material(name):
    if name in bpy.data.materials:
        return bpy.data.materials[name]
    hex_value, roughness = COLOURS[name]
    rgb = tuple(srgb_to_linear(((hex_value >> s) & 0xFF) / 255.0) for s in (16, 8, 0))
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    for node in mat.node_tree.nodes:
        if node.type == "BSDF_PRINCIPLED":
            node.inputs["Base Color"].default_value = (*rgb, 1.0)
            node.inputs["Roughness"].default_value = roughness
    mat.diffuse_color = (*rgb, 1.0)
    return mat


V = Vector
UP = V((0, 1, 0))


def lerp(a, b, t):
    return a + (b - a) * t


def smooth01(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)


def piecewise(x, points):
    if x <= points[0][0]:
        return points[0][1]
    for (x0, y0), (x1, y1) in zip(points, points[1:]):
        if x <= x1:
            return lerp(y0, y1, (x - x0) / (x1 - x0))
    return points[-1][1]


def superellipse(phi, n):
    c, s = math.cos(phi), math.sin(phi)
    return (math.copysign(abs(c) ** (2.0 / n), c), math.copysign(abs(s) ** (2.0 / n), s))


class Piece:
    """One exported object: a bmesh built in Unity axes, one material slot per named material."""

    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.materials = []
        self.sharp_angle = 50.0  # degrees: 8-sided tubes and bevels stay smooth, box corners stay crisp

    def mat(self, name):
        if name not in self.materials:
            self.materials.append(name)
        return self.materials.index(name)

    # ---- primitives (all coordinates in U-space) ---------------------------------------------------------------------
    def rings(self, rings, mat, closed=True, wrap=False, cap_start=False, cap_end=False, mat_fn=None, cap_mat=None):
        """Skins consecutive rings of points (same count) with quads; optional fan caps and ring wrap-around (torus)."""
        verts = [[self.bm.verts.new(p) for p in ring] for ring in rings]
        n = len(rings[0])
        count = len(rings) if wrap else len(rings) - 1
        for i in range(count):
            a, b = verts[i], verts[(i + 1) % len(rings)]
            for j in range(n if closed else n - 1):
                j2 = (j + 1) % n
                f = self.bm.faces.new((a[j], a[j2], b[j2], b[j]))
                f.material_index = mat_fn(i, j) if mat_fn else mat
        for flag, ring in ((cap_start, verts[0]), (cap_end, verts[-1])):
            if flag:
                centre = self.bm.verts.new(sum((v.co for v in ring), V()) / len(ring))
                for j in range(n):
                    f = self.bm.faces.new((ring[j], ring[(j + 1) % n], centre))
                    f.material_index = cap_mat(j) if callable(cap_mat) else (mat if cap_mat is None else cap_mat)
        return verts

    def lathe(self, profile, segments, mat, centre=V(), cap_start=False, cap_end=False, mat_fn=None, cap_mat=None,
              phase=0.0):
        """Revolves (radius, y) points around the Y axis through `centre`."""
        rings = []
        for r, y in profile:
            rings.append([centre + V((r * math.sin(phase + 2 * math.pi * j / segments), y,
                                      r * math.cos(phase + 2 * math.pi * j / segments))) for j in range(segments)])
        return self.rings(rings, mat, cap_start=cap_start, cap_end=cap_end, mat_fn=mat_fn, cap_mat=cap_mat)

    def tube(self, points, radius, mat, sides=8, caps=True, loop=False):
        """A round tube along a polyline; `radius` may be a list (one per point)."""
        pts = [V(p) for p in points]
        radii = radius if isinstance(radius, (list, tuple)) else [radius] * len(pts)
        tangents = []
        for i in range(len(pts)):
            if loop:
                t = pts[(i + 1) % len(pts)] - pts[i - 1]
            else:
                t = pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]
            tangents.append(t.normalized())
        ref = V((0, 1, 0)) if abs(tangents[0].y) < 0.9 else V((1, 0, 0))
        normal = (ref - tangents[0] * ref.dot(tangents[0])).normalized()
        rings = []
        for i, (p, t) in enumerate(zip(pts, tangents)):
            normal = (normal - t * normal.dot(t)).normalized()  # parallel transport
            binormal = t.cross(normal)
            rings.append([p + (normal * math.cos(2 * math.pi * k / sides) + binormal * math.sin(2 * math.pi * k / sides))
                          * radii[i] for k in range(sides)])
        return self.rings(rings, mat, wrap=loop, cap_start=caps and not loop, cap_end=caps and not loop)

    def box(self, centre, size, mat, bevel=0.0, segments=1, rotation=None):
        m = Matrix.Translation(V(centre)) @ (rotation or Matrix.Identity(4)) @ Matrix.Diagonal((*size, 1.0))
        verts = bmesh.ops.create_cube(self.bm, size=1.0, matrix=m)["verts"]
        for f in {f for v in verts for f in v.link_faces}:
            f.material_index = mat
        if bevel > 0:
            edges = list({e for v in verts for e in v.link_edges})
            bmesh.ops.bevel(self.bm, geom=edges, offset=bevel, offset_type="OFFSET", segments=segments, profile=0.5,
                            affect="EDGES", clamp_overlap=True, material=-1)

    def blob(self, centre, radii, mat_fn, seed=0.0, amp=0.18, subdivisions=2, min_y=None):
        """A lumpy low-poly ellipsoid (leaf clusters, coals); `min_y` flattens its underside."""
        verts = bmesh.ops.create_icosphere(self.bm, subdivisions=subdivisions, radius=1.0, matrix=Matrix.Identity(4))["verts"]
        for v in verts:
            n = v.co.normalized()
            d = 1.0 + amp * noise.noise(n * 1.7 + V((seed, seed * 0.7, -seed)))
            p = V(centre) + V((n.x * radii[0], n.y * radii[1], n.z * radii[2])) * d
            if min_y is not None and p.y < min_y:
                p.y = min_y + (p.y - min_y) * 0.04
            v.co = p
        for f in {f for v in verts for f in v.link_faces}:
            f.material_index = mat_fn(f, V(centre), radii)

    # ---- output ------------------------------------------------------------------------------------------------------
    def build_object(self):
        bm = self.bm
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.0005)
        bmesh.ops.transform(bm, matrix=U_TO_B, verts=bm.verts)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        mesh = bpy.data.meshes.new(self.name)
        bm.to_mesh(mesh)
        bm.free()
        for name in self.materials:
            mesh.materials.append(make_material(name))
        try:
            mesh.shade_smooth()
            mesh.set_sharp_from_angle(angle=math.radians(self.sharp_angle))
        except AttributeError:
            for poly in mesh.polygons:
                poly.use_smooth = True
        obj = bpy.data.objects.new(self.name, mesh)
        bpy.context.scene.collection.objects.link(obj)
        return obj


# ======================================================================================================================
# Pieces
# ======================================================================================================================

def build_tree():
    p = Piece("Tree")
    bark, dark, leaves, light = p.mat("Bark"), p.mat("BarkDark"), p.mat("Leaves"), p.mat("LeavesLight")

    # Trunk: a rounded-square (superellipse n=3) bark column that covers the 2.4 m collider box corners (r >= 1.70 m at
    # 45 degrees up to y = 10), tapering slightly, with vertical furrows and five root lobes flaring at the base.
    seg = 32
    heights = [-0.1, 0.25, 0.6, 1.1, 1.8, 2.8, 4.0, 5.2, 6.4, 7.6, 8.8, 10.0, 11.0, 11.8]
    size = [(-0.1, 2.0), (0.6, 1.8), (1.1, 1.72), (1.8, 1.68), (10.0, 1.58), (11.0, 1.45), (11.8, 1.25)]
    rings = []
    for y in heights:
        a = piecewise(y, size)
        twist = 0.05 * y
        ring = []
        for j in range(seg):
            theta = 2 * math.pi * (j + 0.5) / seg + twist
            c, s = abs(math.cos(theta)), abs(math.sin(theta))
            r = a / (c ** 3 + s ** 3) ** (1.0 / 3.0)
            if j % 4 == 2:
                r += 0.07   # ridge
            elif j % 4 == 0:
                r -= 0.035  # furrow
            if y < 1.4:
                lobe = max(0.0, math.cos(5 * (theta - 0.3))) ** 4
                r *= 1 + 0.5 * lobe * ((1.4 - y) / 1.5) ** 2
            r *= 1 + 0.025 * noise.noise(V((math.cos(theta) * 2, y * 0.6, math.sin(theta) * 2)))
            ring.append(V((r * math.sin(theta), y, r * math.cos(theta))))
        rings.append(ring)
    p.rings(rings, bark, cap_start=True, cap_end=True, mat_fn=lambda i, j: dark if j % 4 in (3, 0) else bark)

    # Branch platforms: flat tops exactly at the collider tops, rounded undersides, covering the collider footprint.
    def branch(start, direction, side, top, t_end, half_width, depth_near, depth_far):
        def profile(hw, depth):
            pts = [(hw, 0.0), (hw + 0.04, -0.06)]
            for k in range(1, 8):
                th = k / 8 * math.pi
                pts.append(((hw + 0.04) * math.cos(th), -0.06 - (depth - 0.06) * math.sin(th)))
            return pts + [(-hw - 0.04, -0.06), (-hw, 0.0)]

        ts = [0.3, 1.2, 1.7, 2.3, 2.9, 3.5, t_end]
        branch_rings = []
        for t in ts:
            k = smooth01((t - 0.3) / (t_end - 0.3))
            depth = lerp(depth_near, depth_far, k)
            hw = half_width + 0.3 * max(0.0, 1 - (t - 0.3) / 1.2)
            base = start + direction * t
            branch_rings.append([base + side * u + UP * (top + v) for u, v in profile(hw, depth)])
        # rounded tip just past the collider end
        tip_base = start + direction * (t_end + 0.3) + UP * (top - depth_far * 0.45)
        branch_rings.append([tip_base + side * u * 0.55 + UP * (v + depth_far * 0.45) * 0.45
                             for u, v in profile(half_width, depth_far)])
        p.rings(branch_rings, bark, cap_start=True, cap_end=True, mat_fn=lambda i, j: dark if j in (3, 6) else bark)

    X, Z = V((1, 0, 0)), V((0, 0, 1))
    branch(V((0, 0, 0)), X, Z, 4.25, 4.35, 0.76, 1.3, 0.6)               # BranchLow:  x 0.85..4.35, z +-0.7, top 4.25
    branch(V((0, 0, 0.6)), -X, Z, 6.75, 4.0, 0.76, 1.2, 0.55)            # BranchMid:  x -4.0..-0.8, z -0.1..1.3, top 6.75
    branch(V((0.4, 0, 0)), -Z, X, 9.45, 4.0, 0.76, 1.1, 0.55)            # BranchHigh: z -4.0..-0.8, x -0.3..1.1, top 9.45

    # Upper limbs disappearing into the canopy (all above y 10.3, clear of the platforms and the hoop at y 5.2..7.2).
    for end in ((2.6, 12.4, 1.3), (-2.6, 12.6, -0.6), (0.4, 12.8, 2.8), (-0.6, 13.2, -2.4)):
        e = V(end)
        mid = V((e.x * 0.45, 11.2, e.z * 0.45))
        p.tube([V((0, 10.4, 0)), mid, e], [0.6, 0.42, 0.25], bark, sides=8)

    # Canopy: overlapping lumpy leaf clusters, flat-bottomed at y 10.6; sun-facing faces lighter.
    def leaf_mat(f, centre, radii):
        rel = f.calc_center_median() - centre
        return light if rel.y / radii[1] > 0.3 or (rel.x + rel.z) / radii[0] > 0.75 else leaves

    clusters = [((0, 13, 0), (3, 2.25, 3)), ((3.5, 11.5, 1.5), (3, 2.25, 3)), ((-3.5, 12, -1), (3, 2.25, 3)),
                ((1, 12, 3.5), (3, 2.25, 3)), ((-1, 12.5, -3.5), (3, 2.25, 3)), ((2, 14.3, -1.2), (2.3, 1.8, 2.3)),
                ((-2, 14.1, 1.8), (2.4, 1.8, 2.4)), ((0.4, 15.2, 0.4), (1.9, 1.4, 1.9)), ((4.0, 12.8, -2.2), (2.0, 1.6, 2.0))]
    for k, (centre, radii) in enumerate(clusters):
        p.blob(centre, radii, leaf_mat, seed=k * 3.1, amp=0.2, subdivisions=3, min_y=10.6)
    return p


def build_pool():
    p = Piece("PaddlingPool")
    blue, white, seam = p.mat("PoolBlue"), p.mat("PoolWhite"), p.mat("PoolSeam")
    panels, R, hw, hh, cy, sides = 20, 9.0, 0.45, 0.6, 0.6, 16
    # Ring angles: per panel (centred on a collider rim box) a narrow seam, then two puffy interior stations.
    stations = []
    for k in range(panels):
        base = math.radians(k * 360.0 / panels - 9.0)
        for offset, puff in ((-0.6, 0.97), (0.6, 0.97), (6.0, 1.02), (12.0, 1.02)):
            stations.append((base + math.radians(offset), puff, k))
    rings = []
    for theta, puff, _ in stations:
        out = V((math.sin(theta), 0, math.cos(theta)))
        ring = []
        for s in range(sides):
            u, v = superellipse(2 * math.pi * s / sides, 2.6)
            ring.append(out * (R + hw * u * puff) + UP * (cy + hh * v * (puff if v < 0 else min(puff, 1.0))))
        rings.append(ring)

    def panel_mat(i, j):
        if i % 4 == 0:
            return seam
        return blue if stations[i][2] % 2 == 0 else white

    p.rings(rings, blue, wrap=True, mat_fn=panel_mat)
    # Valves on the outside of three blue panels; a pool floor flush with the PoolFloor collider top (y 0.16).
    for k in (2, 8, 14):
        theta = math.radians(k * 18.0)
        out = V((math.sin(theta), 0, math.cos(theta)))
        y = 0.75
        p.tube([out * 9.38 + UP * y, out * 9.62 + UP * y], 0.13, p.mat("Valve"), sides=8)
        p.tube([out * 9.6 + UP * y, out * 9.7 + UP * y], 0.07, p.mat("Valve"), sides=8)
    p.lathe([(8.75, 0.16), (8.75, 0.0)], 32, p.mat("PoolFloor"), cap_start=True, cap_end=True)
    return p


def build_grill():
    p = Piece("Grill")
    body, grate, steel, coals, embers = p.mat("GrillBody"), p.mat("Grate"), p.mat("Steel"), p.mat("Coals"), p.mat("Embers")
    tyre, wood = p.mat("Tyre"), p.mat("Timber")
    # Firebox: a rounded tub (y 0.75..1.45) and a rim (to y 1.65) around the open top; collider 3.4 x 0.9 x 2.2.
    p.box((0, 1.1, 0), (3.4, 0.7, 2.2), body, bevel=0.12, segments=2)
    for z in (-1.03, 1.03):
        p.box((0, 1.55, z), (3.4, 0.2, 0.14), body, bevel=0.04)
    for x in (-1.63, 1.63):
        p.box((x, 1.55, 0), (0.14, 0.2, 2.2), body, bevel=0.04)
    # Coal bed under the grate.
    for k, (x, z, ember) in enumerate(((-1.1, -0.4, False), (-0.5, 0.35, True), (0.1, -0.3, False), (0.7, 0.4, True),
                                        (1.2, -0.35, False), (-1.15, 0.5, True), (0.35, 0.05, False))):
        p.blob((x, 1.47, z), (0.42, 0.13, 0.36), lambda f, c, r, e=ember: embers if e else coals, seed=k * 1.7,
               amp=0.25, subdivisions=1)
    # Grate: bars across the firebox, top flush at y 1.65.
    for k in range(16):
        x = -1.5 + k * 0.2
        p.box((x, 1.625, 0), (0.05, 0.05, 1.95), grate)
    for z in (-0.95, 0.95):
        p.box((0, 1.62, z), (3.2, 0.06, 0.06), grate)

    # Lid: a half-round hood hinged at the back edge (y 1.65, z 1.1), propped open 100 degrees, entirely behind z 1.1.
    hinge = Matrix.Translation(V((0, 1.65, 1.1))) @ Matrix.Rotation(math.radians(100), 4, "X") @ Matrix.Translation(V((0, -1.65, -1.1)))
    arc = 12
    outer = [(1.1 * math.cos(math.pi * k / arc), 0.55 * math.sin(math.pi * k / arc)) for k in range(arc + 1)]
    inner = [(1.04 * math.cos(math.pi * k / arc), 0.49 * math.sin(math.pi * k / arc)) for k in range(arc + 1)]

    def lid_point(x, zy):
        return hinge @ V((x, 1.65 + zy[1], zy[0]))

    ends = []
    for x in (-1.7, 1.7):
        ends.append(([p.bm.verts.new(lid_point(x, q)) for q in outer], [p.bm.verts.new(lid_point(x, q)) for q in inner]))
    (o0, i0), (o1, i1) = ends
    for k in range(arc):
        for quad in ((o0[k], o0[k + 1], o1[k + 1], o1[k]), (i0[k], i1[k], i1[k + 1], i0[k + 1]),
                     (o0[k], i0[k], i0[k + 1], o0[k + 1]), (o1[k], o1[k + 1], i1[k + 1], i1[k])):
            p.bm.faces.new(quad).material_index = body
    for k in (0, arc):  # the lid's two long edges
        p.bm.faces.new((o0[k], o1[k], i1[k], i0[k])).material_index = body
    # Lid handle (wooden bar on stand-offs at the lid's front edge).
    handle_z = -1.1 - 0.18
    for x in (-0.6, 0.6):
        p.tube([lid_point(x, (-1.08, 0.2)), lid_point(x, (handle_z + 0.02, 0.2))], 0.035, steel, sides=6)
    p.tube([lid_point(-0.7, (handle_z, 0.2)), lid_point(0.7, (handle_z, 0.2))], 0.07, wood, sides=8)

    # Side shelves (slatted, below the rim) on brackets.
    for sx in (-1, 1):
        for z in (-0.55, 0.0, 0.55):
            p.box((sx * 2.18, 1.47, z), (0.85, 0.05, 0.46), wood, bevel=0.015)
        for z in (-0.75, 0.75):
            p.tube([V((sx * 1.72, 1.12, z)), V((sx * 2.5, 1.43, z))], 0.035, steel, sides=6)
            p.tube([V((sx * 1.72, 1.43, z)), V((sx * 2.55, 1.43, z))], 0.035, steel, sides=6)

    # Legs (collider legs at x +-1.4, z +-0.9) with wheels on the west pair and feet on the east pair, plus a low rack.
    for x in (-1.4, 1.4):
        for z in (-0.9, 0.9):
            bottom = 0.2 if x < 0 else 0.04
            p.tube([V((x, bottom, z)), V((x, 0.85, z))], 0.1, steel, sides=8)
            if x < 0:
                zz = z + math.copysign(0.13, z)
                p.tube([V((x, 0.2, zz - 0.07)), V((x, 0.2, zz + 0.07))], 0.2, tyre, sides=12)
                p.tube([V((x, 0.2, zz - 0.09)), V((x, 0.2, zz + 0.09))], 0.08, steel, sides=8)
            else:
                p.lathe([(0.15, 0.06), (0.15, 0.0)], 10, steel, centre=V((x, 0, z)), cap_start=True, cap_end=True)
    for z in (-0.9, 0.9):
        p.tube([V((-1.4, 0.35, z)), V((1.4, 0.35, z))], 0.04, steel, sides=6)
    return p


def build_merry_go_round():
    p = Piece("MerryGoRound")
    pink, white, steel = p.mat("MerryPink"), p.mat("MerryWhite"), p.mat("Steel")
    seg = 32
    # Disc: 6 m across, 0.2 thick centred on the anchor (top +0.1), eight pink/white wedges, steel rim band.
    wedge = lambda j: pink if (j // 4) % 2 == 0 else white  # noqa: E731
    p.lathe([(2.92, 0.1), (2.985, 0.085), (3.0, 0.04), (3.0, -0.04), (2.985, -0.085), (2.92, -0.1)], seg, steel,
            cap_start=True, cap_end=True, cap_mat=wedge)  # top cap (y 0.1) = the painted wedges; bottom is hidden
    # Raised hub and centre pillar with a knob.
    p.lathe([(0.62, 0.1), (0.62, 0.16), (0.54, 0.26), (0.32, 0.33), (0.12, 0.35)], 24, pink, cap_end=True)
    p.tube([V((0, 0.3, 0)), V((0, 1.6, 0))], 0.12, steel, sides=10)
    p.lathe([(0.12, 1.56), (0.18, 1.62), (0.16, 1.7), (0.08, 1.74)], 12, pink, cap_end=True)
    # Four grab bars over the collider bars (radius 2.3, y 0.1..1.5) curving in to the pillar, joined by a ring rail.
    for i in range(4):
        a = math.radians(i * 90)
        d = V((math.sin(a), 0, math.cos(a)))
        path = [(2.3, 0.08), (2.3, 1.36), (2.27, 1.46), (2.18, 1.52), (2.05, 1.54), (0.1, 1.54)]
        p.tube([d * r + UP * y for r, y in path], 0.065, steel, sides=8)
    p.tube([V((1.65 * math.sin(2 * math.pi * k / 24), 1.54, 1.65 * math.cos(2 * math.pi * k / 24))) for k in range(24)],
           0.05, steel, sides=6, loop=True)
    # Spindle and base plate down to the ground (0.25 m below the anchor).
    p.lathe([(0.32, -0.1), (0.28, -0.2), (0.62, -0.22), (0.62, -0.25)], 16, steel, cap_end=True)
    return p


def build_slide():
    p = Piece("Slide")
    steel, timber, purple, climb = p.mat("Steel"), p.mat("Timber"), p.mat("SlideChute"), p.mat("Climb")
    # Tower deck (collider 2.6 x 0.3 x 2.6 at y 3.2: top 3.35): steel frame plus four timber planks flush with the top.
    p.box((0, 3.15, 0), (2.6, 0.2, 2.6), steel, bevel=0.04)
    for k in range(4):
        z = -0.975 + k * 0.65
        p.box((0, 3.3, z), (2.6, 0.1, 0.62), timber, bevel=0.02)
    # Corner posts = the four collider legs (0.25 square at +-1.2), continuing up as rail posts.
    for x in (-1.2, 1.2):
        for z in (-1.2, 1.2):
            p.tube([V((x, 0, z)), V((x, 4.45, z))], 0.13, steel, sides=10)
            p.lathe([(0.15, 4.42), (0.17, 4.5), (0.1, 4.56)], 10, p.mat("Cap"), centre=V((x, 0, z)), cap_end=True)
    # Guard rails above the deck: both sides and the chute side either side of the opening; the ladder side stays open.
    for x in (-1.2, 1.2):
        for y in (3.9, 4.35):
            p.tube([V((x, y, -1.2)), V((x, y, 1.2))], 0.06, steel, sides=8)
        for x2, y in ((x * 0.62, 3.9), (x * 0.62, 4.35)):
            p.tube([V((x, y, -1.2)), V((x2, y, -1.2))], 0.06, steel, sides=8)
        p.tube([V((x * 0.62, 3.35, -1.2)), V((x * 0.62, 4.4, -1.2))], 0.07, steel, sides=8)

    # Ladder over the collider box (x +-0.7, y 0..3.2, z 1.325..1.575): rails that loop over into grab handles.
    for x in (-0.7, 0.7):
        p.tube([V((x, 0, 1.45)), V((x, 3.9, 1.45)), V((x, 4.15, 1.38)), V((x, 4.25, 1.2)), V((x, 4.1, 1.02)),
                V((x, 3.35, 0.95))], 0.08, climb, sides=8)
    for k in range(7):
        y = 0.4 + 0.45 * k
        p.tube([V((-0.7, y, 1.45)), V((0.7, y, 1.45))], 0.07, climb, sides=8)

    # Chute: the collider is 1.4 x 0.2 x 5.6 centred (0, 1.65, -3.6), pitched Euler(-34, 0, 0). The trough floor lies on
    # the collider's top face; side walls rise from it; a short lip bends flat onto the deck at the top.
    c = V((0, 1.65, -3.6))
    pitch = math.radians(34)
    along = V((0, math.sin(pitch), math.cos(pitch)))   # local +Z (up the slide)
    normal = V((0, math.cos(pitch), -math.sin(pitch)))  # local +Y
    side = V((1, 0, 0))
    section = [(0.0, 0.1), (0.5, 0.1), (0.6, 0.12), (0.66, 0.18), (0.69, 0.3), (0.69, 0.48), (0.73, 0.53), (0.78, 0.48),
               (0.78, 0.0), (0.7, -0.1)]
    section = section + [(-u, v) for u, v in reversed(section[1:])]
    section = section[:-1]  # the closed loop: centre-top, right side, bottom, left side

    def ring_at(origin, nrm, scale=1.0):
        return [origin + side * u * scale + nrm * v for u, v in section]

    chute_rings = [ring_at(c + along * t, normal) for t in (-2.8, -1.4, 0.0, 1.4, 2.8)]
    # top lip: two steps of 0.15 m bending the floor from 34 degrees to horizontal at the deck
    floor_point = c + along * 2.8 + normal * 0.1
    lip_rings = []
    for angle in (17, 0):
        a = math.radians(angle)
        tangent = V((0, math.sin(a), math.cos(a)))
        nrm = V((0, math.cos(a), -math.sin(a)))
        floor_point = floor_point + tangent * 0.15
        lip_rings.append(ring_at(floor_point - nrm * 0.1, nrm))
    # bottom: a rounded end cap just past the collider end
    end = c + along * -2.9
    p.rings([ring_at(end, normal, 0.92)] + chute_rings + lip_rings, purple, cap_start=True, cap_end=True)
    # Chute support: two short legs and a crossbar under the lower half.
    z_leg = -4.6
    y_under = 1.65 + (z_leg + 3.6) * math.tan(pitch) - 0.1 / math.cos(pitch)
    for x in (-0.55, 0.55):
        p.tube([V((x, 0, z_leg)), V((x, y_under + 0.02, z_leg))], 0.07, steel, sides=8)
    p.tube([V((-0.55, y_under - 0.08, z_leg)), V((0.55, y_under - 0.08, z_leg))], 0.06, steel, sides=8)
    return p


def build_swing_set():
    p = Piece("SwingSet")
    steel, cap = p.mat("Steel"), p.mat("Cap")
    # Top bar (collider 7.4 x 0.3 x 0.3 at y 5) with end brackets; splayed A-frame legs replace the vertical posts.
    p.tube([V((-3.85, 5.0, 0)), V((3.85, 5.0, 0))], 0.15, steel, sides=12)
    for sx in (-1, 1):
        apex = V((sx * 3.55, 4.98, 0))
        p.lathe([(0.24, -0.25), (0.26, 0.0), (0.24, 0.25)], 12, cap, centre=V((sx * 3.55, 5.0, 0)), cap_start=True,
                cap_end=True)
        for sz in (-1, 1):
            foot = V((sx * 3.95, 0.0, sz * 1.75))
            p.tube([apex, foot], 0.12, steel, sides=10)
            p.lathe([(0.22, 0.05), (0.22, 0.0)], 10, steel, centre=foot, cap_start=True, cap_end=True)
        # brace across the A
        t = 0.62
        a, b = apex.lerp(V((sx * 3.95, 0, -1.75)), t), apex.lerp(V((sx * 3.95, 0, 1.75)), t)
        p.tube([a, b], 0.07, steel, sides=8)
    # Seat hangers on the bar at the swing pivots (x +-1.6).
    for x in (-1.6, 1.6):
        for dx in (-0.12, 0.12):
            p.tube([V((x + dx + 0.06 * math.cos(2 * math.pi * k / 8), 5.0 + 0.2 * math.sin(2 * math.pi * k / 8), 0))
                    for k in range(8)], 0.035, steel, sides=6, loop=True)
    return p


def build_trampoline():
    p = Piece("Trampoline")
    mat, pad, steel = p.mat("TrampolineMat"), p.mat("PadBlue"), p.mat("Steel")
    # Mat: 3 m trampoline, black jumping mat at y 0.5.
    p.lathe([(1.3, 0.5), (1.3, 0.47)], 32, mat, cap_start=True, cap_end=True)
    # Padded rim cover: a soft flattened ring over the springs.
    sides, seg = 8, 40
    rings = []
    for j in range(seg):
        theta = 2 * math.pi * j / seg
        out = V((math.sin(theta), 0, math.cos(theta)))
        ring = []
        for s in range(sides):
            u, v = superellipse(2 * math.pi * s / sides, 2.4)
            ring.append(out * (1.45 + 0.2 * u) + UP * (0.52 + 0.06 * v))
        rings.append(ring)
    p.rings(rings, pad, wrap=True)
    # Frame ring, six legs in W pairs.
    p.tube([V((1.5 * math.sin(2 * math.pi * k / 24), 0.44, 1.5 * math.cos(2 * math.pi * k / 24))) for k in range(24)],
           0.05, steel, sides=6, loop=True)
    for k in range(3):
        a0, a1 = math.radians(k * 120 - 18), math.radians(k * 120 + 18)
        top0 = V((1.5 * math.sin(a0), 0.44, 1.5 * math.cos(a0)))
        top1 = V((1.5 * math.sin(a1), 0.44, 1.5 * math.cos(a1)))
        foot0 = V((1.6 * math.sin(a0), 0.04, 1.6 * math.cos(a0)))
        foot1 = V((1.6 * math.sin(a1), 0.04, 1.6 * math.cos(a1)))
        p.tube([top0, foot0, foot1, top1], 0.045, steel, sides=6)
    return p


def build_picnic_table():
    p = Piece("PicnicTable")
    timber, dark = p.mat("Timber"), p.mat("TimberDark")
    top = 3.25
    # Table top (collider 10 x 0.5 x 5, top 3.25): five planks with gaps over three cleats.
    for k in range(5):
        p.box((0, top - 0.1, -2.0 + k * 1.0), (10.0, 0.2, 0.95), timber, bevel=0.04)
    for x in (-4.4, 0.0, 4.4):
        p.box((x, top - 0.35, 0), (0.45, 0.3, 4.8), dark, bevel=0.03)
    # Legs over the collider legs (0.5 square at (+-4.4, +-2), up to 2.75).
    for x in (-4.4, 4.4):
        for z in (-2.0, 2.0):
            p.box((x, (top - 0.5) / 2, z), (0.5, top - 0.5, 0.5), dark, bevel=0.04)
        # bench support beam from the table legs out under both benches
        p.box((x, 1.2, 0), (0.42, 0.2, 9.8), dark, bevel=0.03)
    # Benches (collider 10 x 0.4 x 1.8 at y 1.5, z +-4: top 1.7): two planks each, on a cleat and two legs.
    for sz in (-1, 1):
        for k in (-1, 1):
            p.box((0, 1.6, sz * 4.0 + k * 0.45), (10.0, 0.2, 0.86), timber, bevel=0.04)
        p.box((0, 1.4, sz * 4.0), (0.4, 0.2, 1.7), dark, bevel=0.03)
        for x in (-4.4, 4.4):
            p.box((x, 0.55, sz * 4.3), (0.42, 1.1, 0.45), dark, bevel=0.03)
    return p


def build_rotary_pole():
    p = Piece("RotaryPole")
    steel, cap = p.mat("Steel"), p.mat("Cap")
    p.tube([V((0, -0.05, 0)), V((0, 2.6, 0))], 0.12, steel, sides=10)
    p.lathe([(0.28, 0.0), (0.26, 0.08), (0.16, 0.18), (0.13, 0.22)], 12, steel, cap_start=True)  # ground socket
    p.lathe([(0.13, 2.58), (0.14, 2.64), (0.07, 2.7)], 10, cap, cap_end=True)
    # Winder box and crank handle.
    p.box((0, 1.1, 0.16), (0.18, 0.26, 0.14), cap, bevel=0.03)
    p.tube([V((0, 1.1, 0.23)), V((0, 1.1, 0.32)), V((0.18, 1.1, 0.32)), V((0.18, 1.1, 0.42))], 0.025, steel, sides=6)
    return p


def build_rotary_arms():
    p = Piece("RotaryArms")
    steel, line, cap = p.mat("Steel"), p.mat("Line"), p.mat("Cap")
    # Hub at the arms' centre (origin), four 3.5 m arms flat at y 0 (the LineA/LineB colliders), struts from a collar
    # lower on the mast, and five square rings of washing line strung between the arms.
    p.lathe([(0.22, -0.12), (0.22, 0.12), (0.12, 0.2)], 12, cap, cap_start=True, cap_end=True)
    p.lathe([(0.2, -0.75), (0.2, -0.6)], 10, steel, cap_start=True, cap_end=True)
    dirs = [V((1, 0, 0)), V((0, 0, 1)), V((-1, 0, 0)), V((0, 0, -1))]
    for d in dirs:
        p.tube([d * 0.15, d * 3.5], 0.05, steel, sides=6)
        p.tube([d * 0.18 + UP * -0.68, d * 1.5 + UP * -0.03], 0.035, steel, sides=6)
        p.lathe([(0.07, -0.06), (0.07, 0.06)], 6, cap, centre=d * 3.52, cap_start=True, cap_end=True)
    for r in (1.0, 1.6, 2.2, 2.8, 3.4):
        p.tube([d * r + UP * 0.05 for d in dirs], 0.02, line, sides=3, loop=True)
    return p


def build_fence_picket():
    p = Piece("FencePicket")
    picket = p.mat("Picket")
    p.sharp_angle = 30.0  # keep the chamfers crisp
    # One board 3.6 wide, 0.4 thick, up to 7.0 at the shoulders and a point at 7.5; chamfered faces (36 triangles).
    w, h, peak, t, inset = 1.8, 7.0, 7.5, 0.2, 0.06
    outline = [(-w, 0.0), (w, 0.0), (w, h), (0.0, peak), (-w, h)]
    inner = [(-w + inset, 0.0), (w - inset, 0.0), (w - inset, h - inset * 0.3), (0.0, peak - inset * 1.1),
             (-w + inset, h - inset * 0.3)]
    bm = p.bm
    front = [bm.verts.new((x, y, t)) for x, y in inner]
    front_edge = [bm.verts.new((x, y, t - inset)) for x, y in outline]
    back_edge = [bm.verts.new((x, y, -t + inset)) for x, y in outline]
    back = [bm.verts.new((x, y, -t)) for x, y in inner]
    n = len(outline)
    bm.faces.new(front).material_index = picket
    bm.faces.new(list(reversed(back))).material_index = picket
    for a, b in ((front, front_edge), (front_edge, back_edge), (back_edge, back)):
        for j in range(n):
            j2 = (j + 1) % n
            bm.faces.new((a[j], a[j2], b[j2], b[j])).material_index = picket
    return p


def build_cactus():
    """A potted saguaro, 1.7 m tall: the "spikes" that pop the balloon. Ribbed body with two arms, rows of pale spines
    big enough to read from across a room, a flower on top, in a terracotta pot. Origin at the pot's base centre.
    BackyardCourseBuilder.CactusPot wraps it in colliders: pot (0..0.5), body (r 0.4 to y 1.7), the two arms."""
    p = Piece("Cactus")
    green, ridge, spine = p.mat("CactusGreen"), p.mat("CactusRidge"), p.mat("Spine")
    pot, rim, soil = p.mat("Terracotta"), p.mat("TerracottaRim"), p.mat("PotSoil")
    flower, heart = p.mat("CactusFlower"), p.mat("FlowerHeart")
    bm = p.bm

    # Pot: tapered, with a thick rolled rim and soil just below it.
    p.lathe([(0.0, 0.0), (0.25, 0.0), (0.33, 0.4)], 16, pot)
    p.lathe([(0.33, 0.4), (0.385, 0.4), (0.395, 0.45), (0.385, 0.5), (0.33, 0.5), (0.31, 0.46)], 16, rim)
    p.lathe([(0.31, 0.46), (0.0, 0.47)], 16, soil)

    def spike(base, direction, length=0.15, width=0.03):
        """A three-sided spine: a small cone pointing along `direction`."""
        d = direction.normalized()
        ref = UP if abs(d.y) < 0.9 else V((1, 0, 0))
        a = d.cross(ref).normalized()
        b = d.cross(a)
        ring = [bm.verts.new(base + (a * math.cos(k * 2.094) + b * math.sin(k * 2.094)) * width) for k in range(3)]
        tip = bm.verts.new(base + d * length)
        for k in range(3):
            bm.faces.new((ring[k], ring[(k + 1) % 3], tip)).material_index = spine

    def ribbed(points, radii, ribs, spines_at, squash=1.0):
        """A ribbed column along a polyline (round in cross-section, ridges lighter), with spines on the ridges."""
        pts = [V(q) for q in points]
        segments = ribs * 2
        rings = []
        frames = []
        normal = None
        for i, q in enumerate(pts):
            t = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized()
            if normal is None:
                ref = V((0, 0, 1)) if abs(t.z) < 0.9 else V((1, 0, 0))
                normal = (ref - t * ref.dot(t)).normalized()
            normal = (normal - t * normal.dot(t)).normalized()
            binormal = t.cross(normal)
            frames.append((q, normal, binormal))
            ring = []
            for j in range(segments):
                phi = 2 * math.pi * j / segments
                r = radii[i] * (1.0 if j % 2 == 0 else 0.8)  # even = ridge, odd = groove
                ring.append(q + (normal * math.cos(phi) + binormal * math.sin(phi) * squash) * r)
            rings.append(ring)
        p.rings(rings, green, cap_start=True, cap_end=True)
        for i in spines_at:
            q, n, b = frames[i]
            for j in range(0, segments, 2):
                phi = 2 * math.pi * j / segments
                out = (n * math.cos(phi) + b * math.sin(phi) * squash)
                spike(q + out * radii[i] * 0.97, out + UP * 0.25)

    # Body: swells out of the pot, straight, domed on top.
    body_pts = [(0, 0.44, 0), (0, 0.6, 0), (0, 0.8, 0), (0, 1.0, 0), (0, 1.2, 0), (0, 1.38, 0), (0, 1.52, 0), (0, 1.61, 0), (0, 1.655, 0)]
    body_r = [0.17, 0.25, 0.285, 0.29, 0.285, 0.265, 0.21, 0.13, 0.05]
    ribbed(body_pts, body_r, 9, spines_at=(1, 2, 3, 4, 5, 6))

    # Arms: out of the body's side, then up, each ending in a rounded tip.
    left = [(-0.2, 0.92, 0), (-0.36, 0.93, 0), (-0.47, 0.99, 0), (-0.5, 1.1, 0), (-0.5, 1.24, 0), (-0.5, 1.36, 0), (-0.5, 1.42, 0)]
    right = [(0.2, 0.74, 0.02), (0.34, 0.75, 0.03), (0.44, 0.8, 0.04), (0.47, 0.9, 0.04), (0.47, 1.02, 0.04), (0.47, 1.11, 0.04), (0.47, 1.16, 0.04)]
    arm_r = [0.105, 0.115, 0.12, 0.125, 0.125, 0.1, 0.045]
    ribbed(left, arm_r, 6, spines_at=(2, 3, 4, 5))
    ribbed(right, [r * 0.92 for r in arm_r], 6, spines_at=(2, 3, 4, 5))

    # Flower on top: five petals round a yellow heart.
    top = V((0, 1.66, 0))
    for k in range(5):
        a = 2 * math.pi * k / 5
        out = V((math.cos(a), 0, math.sin(a)))
        p.blob(top + out * 0.075 + UP * 0.02, (0.07, 0.035, 0.07), lambda f, c, r: flower, seed=k, amp=0.05, subdivisions=1)
    p.blob(top + UP * 0.045, (0.045, 0.04, 0.045), lambda f, c, r: heart, seed=9.0, amp=0.03, subdivisions=1)

    # The domed tops (faces turned up toward the light) take the lighter green.
    for f in bm.faces:
        if f.material_index != green:
            continue
        f.normal_update()
        if f.normal.y > 0.35:
            f.material_index = ridge
    return p


PIECES = {
    "Tree": build_tree, "PaddlingPool": build_pool, "Grill": build_grill, "MerryGoRound": build_merry_go_round,
    "Slide": build_slide, "SwingSet": build_swing_set, "Trampoline": build_trampoline,
    "PicnicTable": build_picnic_table, "RotaryPole": build_rotary_pole, "RotaryArms": build_rotary_arms,
    "FencePicket": build_fence_picket, "Cactus": build_cactus,
}
BUDGETS = {"Tree": 6000, "PaddlingPool": 3000, "Grill": 2500, "MerryGoRound": 2000, "Slide": 2500, "SwingSet": 1200,
           "Trampoline": 1500, "PicnicTable": 1500, "RotaryPole": 800, "RotaryArms": 800, "FencePicket": 60, "Cactus": 1800}


def export(obj, path):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={"MESH"}, global_scale=1.0, apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y", bake_space_transform=BAKE_SPACE,
        add_leaf_bones=False, bake_anim=False, mesh_smooth_type="FACE", use_mesh_modifiers=True)


# ======================================================================================================================
# Verification: re-import, convert back to Unity axes, measure walkable tops; read the FBX root transform.
# ======================================================================================================================

def fbx_model_transforms(path):
    """Minimal binary-FBX reader: returns {model name: {"Lcl Rotation": [...], ...}} for the Model nodes."""
    data = open(path, "rb").read()
    version = struct.unpack_from("<I", data, 23)[0]
    wide = version >= 7500

    def read_props(pos, count):
        props = []
        for _ in range(count):
            kind = chr(data[pos]); pos += 1
            if kind in "YCIFDL":
                fmt = {"Y": "<h", "C": "<?", "I": "<i", "F": "<f", "D": "<d", "L": "<q"}[kind]
                props.append(struct.unpack_from(fmt, data, pos)[0]); pos += struct.calcsize(fmt)
            elif kind in "SR":
                ln = struct.unpack_from("<I", data, pos)[0]; pos += 4
                raw = data[pos:pos + ln]; pos += ln
                props.append(raw.decode("utf-8", "replace") if kind == "S" else raw)
            else:
                ln, enc, clen = struct.unpack_from("<III", data, pos); pos += 12
                pos += clen
                props.append(None)
        return props, pos

    def read_node(pos):
        if wide:
            end, count, _ = struct.unpack_from("<QQQ", data, pos); pos += 24
        else:
            end, count, _ = struct.unpack_from("<III", data, pos); pos += 12
        name_len = data[pos]; pos += 1
        name = data[pos:pos + name_len].decode(); pos += name_len
        if end == 0:
            return None, pos
        props, pos = read_props(pos, count)
        children = []
        while pos < end:
            child, pos = read_node(pos)
            if child is None:
                break
            children.append(child)
        return (name, props, children), end

    pos, nodes = 27, []
    while pos < len(data) - 160:
        node, pos = read_node(pos)
        if node is None:
            break
        nodes.append(node)
    result = {}
    for name, _, children in nodes:
        if name != "Objects":
            continue
        for cname, cprops, cchildren in children:
            if cname != "Model":
                continue
            entry = {}
            for pname, _, pchildren in cchildren:
                if pname == "Properties70":
                    for _, pp, _ in pchildren:
                        if pp and pp[0] in ("Lcl Translation", "Lcl Rotation", "Lcl Scaling", "PreRotation"):
                            entry[pp[0]] = [round(v, 4) for v in pp[4:7]]
            result[str(cprops[1]).split("\x00")[0]] = entry
    return result


def reimport_unity_verts(path):
    """Re-imports an FBX; returns its vertices in Unity axes, a BVH of its faces in Unity axes, and object rotations."""
    from mathutils.bvhtree import BVHTree

    clear_scene()
    bpy.ops.import_scene.fbx(filepath=path)
    pts, polys, rotations = [], [], {}
    for obj in bpy.context.scene.objects:
        if obj.type != "MESH":
            continue
        rotations[obj.name] = [round(math.degrees(a), 2) for a in obj.matrix_world.to_euler()]
        base = len(pts)
        for v in obj.data.vertices:
            b = obj.matrix_world @ v.co
            pts.append(V((-b.x, b.z, -b.y)))  # Blender -> Unity
        polys += [[base + i for i in poly.vertices] for poly in obj.data.polygons]
    return pts, BVHTree.FromPolygons(pts, polys), rotations


def top_in(bvh, xr, zr, yr, steps=9):
    """Casts rays down over a grid in the x/z rectangle from yr[1]; returns [min, max] hit height within yr."""
    hits = []
    for a in range(steps):
        for b in range(steps):
            x = lerp(xr[0], xr[1], a / max(steps - 1, 1))
            z = lerp(zr[0], zr[1], b / max(steps - 1, 1))
            loc, _, _, _ = bvh.ray_cast(V((x, yr[1], z)), V((0, -1, 0)), yr[1] - yr[0])
            if loc is not None:
                hits.append(loc.y)
    return [round(min(hits), 4), round(max(hits), 4), f"{len(hits)}/{steps * steps} rays hit"] if hits else None


def check(name, path):
    pts, bvh, rotations = reimport_unity_verts(path)
    lo = V((min(q.x for q in pts), min(q.y for q in pts), min(q.z for q in pts)))
    hi = V((max(q.x for q in pts), max(q.y for q in pts), max(q.z for q in pts)))
    out = {"unityBoundsMin": [round(v, 3) for v in lo], "unityBoundsMax": [round(v, 3) for v in hi],
           "blenderReimportRotation": rotations, "fbxModelNodes": fbx_model_transforms(path)}
    surfaces = {}
    if name == "Tree":
        surfaces["BranchLow (collider top 4.25)"] = top_in(bvh, (1.9, 4.3), (-0.65, 0.65), (3.0, 5.5))
        surfaces["BranchMid (collider top 6.75)"] = top_in(bvh, (-3.95, -1.9), (-0.05, 1.25), (5.5, 8.0))
        surfaces["BranchHigh (collider top 9.45)"] = top_in(bvh, (-0.25, 1.05), (-3.95, -1.9), (8.5, 10.4))
        surfaces["lowest canopy vertex outside trunk (>= 10.5)"] = round(min(
            (q.y for q in pts if q.y > 10.0 and math.hypot(q.x, q.z) > 2.2), default=99), 3)
    elif name == "MerryGoRound":
        surfaces["disc top (collider top +0.10 => world 0.35)"] = top_in(bvh, (-2.8, 2.8), (0.7, 2.8), (-0.2, 0.3))
    elif name == "Slide":
        surfaces["deck top (collider top 3.35)"] = top_in(bvh, (-1.0, 1.0), (-1.0, 1.0), (3.0, 3.6))
        c = V((0, 1.65, -3.6))
        pitch = math.radians(34)
        along = V((0, math.sin(pitch), math.cos(pitch)))
        normal = V((0, math.cos(pitch), -math.sin(pitch)))
        floor = []
        for k in range(15):
            for x in (-0.5, -0.25, 0.0, 0.25, 0.5):
                start = c + along * lerp(-2.75, 2.75, k / 14) + normal * 0.4 + V((x, 0, 0))
                loc, _, _, _ = bvh.ray_cast(start, -normal, 0.6)
                if loc is not None:
                    floor.append((loc - c).dot(normal))
        surfaces["chute floor offset along the chute normal (collider top = 0.10)"] = (
            [round(min(floor), 4), round(max(floor), 4), f"{len(floor)}/75 rays hit"] if floor else None)
    elif name == "PicnicTable":
        surfaces["table top (collider 3.25)"] = top_in(bvh, (-4.9, 4.9), (-2.4, 2.4), (2.5, 3.6))
        surfaces["bench top (collider 1.7)"] = top_in(bvh, (-4.9, 4.9), (3.2, 4.8), (1.0, 2.0))
    elif name == "PaddlingPool":
        surfaces["rim top (collider 1.2)"] = top_in(bvh, (-10, 10), (8.4, 9.6), (0.5, 1.6))
    elif name == "Grill":
        surfaces["grate/rim top (collider 1.65)"] = top_in(bvh, (-1.7, 1.7), (-1.1, 1.1), (1.0, 1.66 + 0.2))
        surfaces["lid min z above y 1.66 (must be >= 1.1)"] = round(min(
            (q.z for q in pts if q.y > 1.7), default=99), 3)
    elif name == "FencePicket":
        surfaces["shoulder (x 1.75) / peak"] = [top_in(bvh, (1.75, 1.75), (0, 0), (0, 8), steps=1), round(hi.y, 3)]
    out["surfaces"] = surfaces
    return out


# ======================================================================================================================

args = script_args()
only = args[args.index("--only") + 1].split(",") if "--only" in args else list(PIECES)
out_dir = os.path.abspath(os.path.join(REPO, args[args.index("--out") + 1])) if "--out" in args else \
    os.path.join(REPO, "Assets", "GameJam", "Art", "Set")
BAKE_SPACE = "--no-bake" not in args
do_check = "--check" in args
os.makedirs(out_dir, exist_ok=True)

results = {}
for name in only:
    if name not in PIECES:
        fail(f"Unknown piece {name}; choose from {', '.join(PIECES)}")
    clear_scene()
    piece = PIECES[name]()
    obj = piece.build_object()
    tris = sum(len(poly.vertices) - 2 for poly in obj.data.polygons)
    path = os.path.join(out_dir, f"{name}.fbx")
    export(obj, path)
    results[name] = {"fbx": path, "triangles": tris, "budget": BUDGETS[name], "withinBudget": tris <= BUDGETS[name],
                     "materials": {m: "#%06X" % COLOURS[m][0] for m in piece.materials}}

if do_check:
    for name in only:
        results[name]["check"] = check(name, results[name]["fbx"])

emit({"success": all(r["withinBudget"] for r in results.values()), "bakeSpaceTransform": BAKE_SPACE,
      "pieces": results})
