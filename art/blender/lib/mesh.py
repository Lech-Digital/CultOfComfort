"""Low-poly building blocks. A Part is one mesh object (one animatable piece) made of several coloured pieces."""
import math

import bmesh
import bpy
from mathutils import Euler, Matrix, Vector

import palette

MATERIAL_NAME = "Palette"


def palette_material():
    material = bpy.data.materials.get(MATERIAL_NAME)
    if material is not None:
        return material
    material = bpy.data.materials.new(MATERIAL_NAME)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    bsdf = nodes["Principled BSDF"]
    texture = nodes.new("ShaderNodeTexImage")
    texture.interpolation = "Closest"  # swatches must not blend into each other
    texture.image = bpy.data.images.load(palette.UNITY_PALETTE_PATH, check_existing=True)
    material.node_tree.links.new(texture.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.85
    return material


def world_position(obj):
    """Sum of locations up the parent chain. Parts are never rotated at build time, so this is exact,
    and unlike matrix_world it does not depend on a depsgraph update."""
    total = Vector((0, 0, 0))
    while obj is not None:
        total += obj.location
        obj = obj.parent
    return total


def _link(obj, parent):
    bpy.context.scene.collection.objects.link(obj)
    if parent is not None:
        obj.parent = parent
    return obj


def root(name):
    obj = bpy.data.objects.new(name, None)
    obj.empty_display_type = "PLAIN_AXES"
    return _link(obj, None)


def marker(name, location, parent):
    """A named empty that is exported, e.g. `Front` so Unity tests can check which way the model faces."""
    obj = bpy.data.objects.new(name, None)
    obj.empty_display_type = "SPHERE"
    obj.empty_display_size = 0.03
    _link(obj, parent)
    obj.location = Vector(location) - world_position(parent)
    return obj


class Part:
    def __init__(self, name, pivot=(0, 0, 0), parent=None):
        self.name = name
        self.pivot = Vector(pivot)
        self.parent = parent
        self.bm = bmesh.new()
        self.uv_layer = self.bm.loops.layers.uv.verify()

    # ---- pieces -------------------------------------------------------------------------------------------

    def box(self, size, center, swatch, bevel=0.015, rotation=(0, 0, 0), bevel_segments=1, smooth=False):
        """`bevel_segments` > 1 rounds the edges (softer, friendlier); `smooth` shades it like skin, not wood."""
        piece = bmesh.new()
        bmesh.ops.create_cube(piece, size=1.0)
        bmesh.ops.scale(piece, vec=Vector(size), verts=piece.verts)
        if bevel > 0:
            bmesh.ops.bevel(piece, geom=list(piece.verts) + list(piece.edges), offset=bevel, offset_type="OFFSET",
                            segments=bevel_segments, profile=0.5, affect="EDGES", clamp_overlap=True)
        return self._add(piece, center, swatch, rotation, smooth)

    def cylinder(self, radius, depth, center, swatch, segments=8, radius_top=None, rotation=(0, 0, 0), smooth=False):
        piece = bmesh.new()
        bmesh.ops.create_cone(piece, cap_ends=True, cap_tris=False, segments=segments, radius1=radius,
                              radius2=radius if radius_top is None else radius_top, depth=depth)
        return self._add(piece, center, swatch, rotation, smooth)

    def sphere(self, radius, center, swatch, segments=10, rings=7, scale=(1, 1, 1), rotation=(0, 0, 0), smooth=False):
        piece = bmesh.new()
        bmesh.ops.create_uvsphere(piece, u_segments=segments, v_segments=rings, radius=radius)
        bmesh.ops.scale(piece, vec=Vector(scale), verts=piece.verts)
        return self._add(piece, center, swatch, rotation, smooth)

    # ---- assembly -----------------------------------------------------------------------------------------

    def _add(self, piece, center, swatch, rotation, smooth=False):
        u, v = palette.uv(swatch)  # KeyError for unknown swatches, before any geometry is merged
        rot = Euler(tuple(math.radians(a) for a in rotation), "XYZ").to_matrix().to_4x4()
        bmesh.ops.transform(piece, matrix=Matrix.Translation(Vector(center) - self.pivot) @ rot, verts=piece.verts)
        layer = piece.loops.layers.uv.verify()
        for face in piece.faces:
            face.smooth = smooth
            for loop in face.loops:
                loop[layer].uv = (u, v)
        mesh = bpy.data.meshes.new("_piece")
        piece.to_mesh(mesh)
        piece.free()
        self.bm.from_mesh(mesh)  # appends to the part's geometry
        bpy.data.meshes.remove(mesh)
        return self

    def build(self):
        mesh = bpy.data.meshes.new(self.name)
        self.bm.to_mesh(mesh)
        self.bm.free()
        mesh.materials.append(palette_material())
        obj = _link(bpy.data.objects.new(self.name, mesh), self.parent)
        # Pivots are given in model space; a child stores its location relative to its parent.
        obj.location = self.pivot - world_position(self.parent) if self.parent is not None else self.pivot
        return obj
