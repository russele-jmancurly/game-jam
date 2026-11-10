"""Shared helpers for the procedural toy-style meshes (build_item_props.py, build_critters.py).

Geometry is written in UNITY axes (X right, Y up, Z forward) into a bmesh; Item.build converts to Blender axes
(Unity (x, y, z) -> Blender (-x, -z, y)), which the FBX export (axis_forward=-Z, axis_up=Y) maps back to Unity (x, y, z).
Materials are plain Principled BSDFs, one per named colour (sRGB hex).
"""
import math

import bmesh  # noqa: E402
import bpy  # noqa: E402
from mathutils import Vector  # noqa: E402


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
    def build(self, target, anchor="center"):
        """Fits the Unity-axis bounds to `target` (x, y, z; None = keep that axis), centres it (anchor "bottom": the
        origin at the middle of the base instead, for things that stand on the ground), converts to Blender axes and
        returns the mesh object."""
        bm = self.bm
        lo = Vector((min(v.co[i] for v in bm.verts) for i in range(3)))
        hi = Vector((max(v.co[i] for v in bm.verts) for i in range(3)))
        size, mid = hi - lo, (hi + lo) / 2
        if anchor == "bottom":
            mid.y = lo.y
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
