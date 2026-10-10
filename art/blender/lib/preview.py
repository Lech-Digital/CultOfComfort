"""Golden Mud preview renders (spec: Lighting) at the game camera's elevation, three-quarter view."""
import math

import bpy
from mathutils import Vector

from lib.mesh import Part

GAME_ELEVATION = math.degrees(math.atan2(16.5, 10.4))  # matches CameraRig's offset (0, 16.5, -10.4)


def _engine():
    items = bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items.keys()
    return "BLENDER_EEVEE_NEXT" if "BLENDER_EEVEE_NEXT" in items else "BLENDER_EEVEE"


def _bounds(root):
    points = [o.matrix_world @ Vector(c) for o in [root, *root.children_recursive] if o.type == "MESH" for c in o.bound_box]
    lo = Vector((min(p.x for p in points), min(p.y for p in points), min(p.z for p in points)))
    hi = Vector((max(p.x for p in points), max(p.y for p in points), max(p.z for p in points)))
    return lo, hi


def _light(name, kind, energy, color, rotation=(0, 0, 0), location=(0, 0, 0)):
    data = bpy.data.lights.new(name, kind)
    data.energy = energy
    data.color = color
    obj = bpy.data.objects.new(name, data)
    obj.rotation_euler = tuple(math.radians(a) for a in rotation)
    obj.location = location
    bpy.context.scene.collection.objects.link(obj)
    return obj


def _world(color, strength):
    world = bpy.data.worlds.new("Preview") if bpy.context.scene.world is None else bpy.context.scene.world
    world.use_nodes = True
    background = world.node_tree.nodes["Background"]
    background.inputs["Color"].default_value = (*color, 1)
    background.inputs["Strength"].default_value = strength
    bpy.context.scene.world = world


def render(root, day_path, night_path, size=512):
    bpy.context.view_layer.update()  # matrix_world is stale until the depsgraph runs
    scene = bpy.context.scene
    scene.render.engine = _engine()
    scene.render.resolution_x = scene.render.resolution_y = size
    scene.render.image_settings.file_format = "PNG"
    scene.view_settings.view_transform = "Standard"  # keep palette colours true

    lo, hi = _bounds(root)
    centre = (lo + hi) / 2
    extent = max(hi.x - lo.x, hi.y - lo.y, hi.z - lo.z)
    ground = Part("PreviewGround").box((extent * 4, extent * 4, 0.02), (centre.x, centre.y, lo.z - 0.01), "grass", bevel=0).build()

    camera_data = bpy.data.cameras.new("PreviewCamera")
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = extent * 1.6
    camera = bpy.data.objects.new("PreviewCamera", camera_data)
    scene.collection.objects.link(camera)
    yaw, pitch = math.radians(30), math.radians(GAME_ELEVATION)
    # Models face +Y once built (lib.mesh.TURN), so the camera stands on the +Y side to see the front.
    direction = Vector((math.sin(yaw) * math.cos(pitch), math.cos(yaw) * math.cos(pitch), math.sin(pitch)))
    camera.location = centre + direction * extent * 6
    camera.rotation_euler = (-direction).to_track_quat("-Z", "Y").to_euler()
    scene.camera = camera

    # Day: low warm sun from the left, cool sky fill.
    _world((0.55, 0.62, 0.72), 0.6)
    sun = _light("Sun", "SUN", 3.2, (1.0, 0.86, 0.66), rotation=(62, 0, -60))
    sun.data.angle = math.radians(3)
    scene.render.filepath = day_path
    bpy.ops.render.render(write_still=True)

    # Night: teal moon, orange fire beside the model.
    bpy.data.objects.remove(sun, do_unlink=True)
    _world((0.02, 0.04, 0.07), 1.0)
    _light("Moon", "SUN", 0.7, (0.55, 0.85, 0.9), rotation=(50, 0, 140))
    _light("Fire", "POINT", 60.0 * extent, (1.0, 0.55, 0.22), location=(centre.x - extent, centre.y - extent * 0.6, lo.z + extent * 0.5))
    scene.render.filepath = night_path
    bpy.ops.render.render(write_still=True)

    bpy.data.objects.remove(ground, do_unlink=True)
