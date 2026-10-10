import unittest

import bpy
from mathutils import Vector

from lib import preview, scene
from lib.mesh import Part, root


class PreviewTests(unittest.TestCase):
    def setUp(self):
        scene.reset()

    def test_ground_sits_under_an_off_centre_model(self):
        # Arrange — a building whose footprint is far from the origin.
        model = root("shed")
        Part("Walls", parent=model).box((1, 1, 1), (2.0, -3.0, 0.5), "daub", bevel=0).build()
        bpy.context.view_layer.update()
        lo, hi = preview._bounds(model)

        # Act
        ground = preview._ground(lo, hi)
        bpy.context.view_layer.update()
        corners = [ground.matrix_world @ Vector(c) for c in ground.bound_box]
        centre = sum(corners, Vector()) / 8

        # Assert
        self.assertAlmostEqual(centre.x, (lo.x + hi.x) / 2, places=4)
        self.assertAlmostEqual(centre.y, (lo.y + hi.y) / 2, places=4)
        self.assertAlmostEqual(max(c.z for c in corners), lo.z, places=4)
