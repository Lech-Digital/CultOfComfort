import unittest

import bpy
from mathutils import Vector

from lib import checks, scene
from models import prophet


class ProphetTests(unittest.TestCase):
    def setUp(self):
        scene.reset()
        self.root = prophet.build()
        bpy.context.view_layer.update()  # so matrix_world reflects the built hierarchy

    def test_build_passes_budget_palette_and_parts(self):
        checks.verify(self.root, prophet.TRIANGLE_BUDGET, prophet.REQUIRED_PARTS)

    def test_build_is_about_one_metre_tall_standing_on_the_ground(self):
        # Arrange — real vertices, not bound_box corners: a rotated part's local box overestimates its height.
        points = [o.matrix_world @ v.co for o in self.root.children_recursive if o.type == "MESH" for v in o.data.vertices]

        # Act
        bottom, top = min(p.z for p in points), max(p.z for p in points)

        # Assert
        self.assertAlmostEqual(bottom, 0.0, delta=0.01)
        self.assertAlmostEqual(top, 1.0, delta=0.06)

    def test_build_pivots_sit_at_feet_shoulders_and_hand(self):
        # Arrange
        objects = {o.name: o for o in self.root.children_recursive}

        # Act & Assert
        self.assertAlmostEqual(objects["Body"].matrix_world.translation.z, 0.0, places=4)
        self.assertAlmostEqual(objects["ArmR"].matrix_world.translation.z, 0.56, places=3)
        self.assertAlmostEqual(objects["Umbrella"].matrix_world.translation.z, 0.35, places=3)
        self.assertEqual(objects["Umbrella"].parent.name, "ArmR")

    def test_front_marker_is_on_the_face_side(self):
        # Exported models face +Y (Unity +Z); see lib.mesh.TURN.
        objects = {o.name: o for o in self.root.children_recursive}
        self.assertGreater(objects["Front"].matrix_world.translation.y, 0.2)
