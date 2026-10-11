"""HUD icons: a model rendered small, three-quarter view, transparent background (spec: HUD Kit, Icons)."""
import math

import bpy
from mathutils import Vector

from lib.preview import _bounds, _engine, _light, _world


def _part_bounds(root, focus):
    part = next(o for o in [root, *root.children_recursive] if o.name.split(".")[0] == focus)
    points = [part.matrix_world @ Vector(c) for c in part.bound_box]
    lo = Vector((min(p.x for p in points), min(p.y for p in points), min(p.z for p in points)))
    hi = Vector((max(p.x for p in points), max(p.y for p in points), max(p.z for p in points)))
    return lo, hi


def render_icon(root, path, size=128, focus=None):
    """Frames the whole model, or one named part plus its surroundings (portraits: focus="Head")."""
    bpy.context.view_layer.update()
    scene = bpy.context.scene
    scene.render.engine = _engine()
    scene.render.resolution_x = scene.render.resolution_y = size
    scene.render.film_transparent = True
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.view_settings.view_transform = "Standard"

    lo, hi = _bounds(root) if focus is None else _part_bounds(root, focus)
    centre = (lo + hi) / 2
    extent = max(hi.x - lo.x, hi.y - lo.y, hi.z - lo.z)
    if focus is not None:
        centre.z -= extent * 0.12   # face and collar, hair kept in frame
        extent *= 1.3

    camera_data = bpy.data.cameras.new("IconCamera")
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = extent * 1.3
    camera = bpy.data.objects.new("IconCamera", camera_data)
    scene.collection.objects.link(camera)
    yaw, pitch = math.radians(25), math.radians(28)
    direction = Vector((math.sin(yaw) * math.cos(pitch), math.cos(yaw) * math.cos(pitch), math.sin(pitch)))
    camera.location = centre + direction * extent * 6
    camera.rotation_euler = (-direction).to_track_quat("-Z", "Y").to_euler()
    scene.camera = camera

    _world((0.62, 0.6, 0.58), 0.9)
    sun = _light("IconSun", "SUN", 3.0, (1.0, 0.9, 0.75), rotation=(55, 0, -50))
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    for obj in (sun, camera):
        bpy.data.objects.remove(obj, do_unlink=True)
