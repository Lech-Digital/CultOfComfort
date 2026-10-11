"""A campfire ring of stones with a log tripod and a pot. `Flame` flickers."""
import math

from mathutils import Vector

from lib.mesh import Part, root

TRIANGLE_BUDGET = 600
REQUIRED_PARTS = ("Prop", "Flame")


def build():
    fire = root("campfire")
    ring = Part("Prop", parent=fire)
    for k in range(7):
        a = k / 7 * math.tau
        ring.sphere(0.07, (math.cos(a) * 0.25, math.sin(a) * 0.25, 0.04), "fieldstone", segments=6, rings=4, scale=(1.2, 1, 0.7))
    for k in range(3):
        a = k / 3 * math.tau
        ring.cylinder(0.025, 0.4, (math.cos(a) * 0.08, math.sin(a) * 0.08, 0.06), "tarred_wood", segments=5, rotation=(70, 0, math.degrees(a) + 90))
        # Tripod leg: from the ground at radius 0.3 up to a point above the pot; rotation from that direction.
        foot = Vector((math.cos(a) * 0.3, math.sin(a) * 0.3, 0.0))
        top = Vector((math.cos(a) * 0.03, math.sin(a) * 0.03, 0.7))
        euler = Vector((0, 0, 1)).rotation_difference((top - foot).normalized()).to_euler("XYZ")
        ring.cylinder(0.015, (top - foot).length, tuple((foot + top) / 2), "old_wood", segments=5,
                      rotation=tuple(math.degrees(v) for v in euler))
    ring.cylinder(0.09, 0.1, (0, 0, 0.42), "tarred_wood", segments=8, radius_top=0.1)                           # pot
    ring_obj = ring.build()
    (Part("Flame", pivot=(0, 0, 0.08), parent=ring_obj)
     .cylinder(0.1, 0.28, (0, 0, 0.2), "fire", segments=7, radius_top=0.0, smooth=True)
     .cylinder(0.06, 0.2, (0.04, 0.02, 0.16), "ember", segments=6, radius_top=0.0, smooth=True)
     .build())
    return fire
