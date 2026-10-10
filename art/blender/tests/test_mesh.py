import unittest

import bpy

import palette
from lib import scene
from lib.mesh import MATERIAL_NAME, Part, marker, root


def uvs(obj):
    layer = obj.data.uv_layers.active.data
    return {(round(d.uv[0], 5), round(d.uv[1], 5)) for d in layer}


class MeshTests(unittest.TestCase):
    def setUp(self):
        scene.reset()

    def test_box_part_is_one_mesh_at_pivot_painted_with_one_swatch(self):
        # Arrange
        parent = root("thing")

        # Act
        obj = Part("Body", pivot=(0, 0, 0.5), parent=parent).box((1, 1, 1), (0, 0, 0.5), "linen").build()

        # Assert
        self.assertEqual(obj.type, "MESH")
        self.assertEqual(obj.parent, parent)
        self.assertEqual(tuple(round(c, 5) for c in obj.location), (0, 0, 0.5))
        self.assertEqual(uvs(obj), {tuple(round(c, 5) for c in palette.uv("linen"))})
        self.assertEqual(obj.data.materials[0].name, MATERIAL_NAME)
        zs = [v.co.z for v in obj.data.vertices]
        self.assertAlmostEqual(min(zs), -0.5, places=4)  # geometry is relative to the pivot

    def test_part_with_several_pieces_keeps_each_swatch(self):
        # Act
        obj = (Part("Head")
               .sphere(0.2, (0, 0, 0), "prophet_skin")
               .box((0.1, 0.02, 0.02), (0, -0.2, 0), "umbrella_black", bevel=0)
               .cylinder(0.05, 0.1, (0, 0, 0.3), "cone_orange")
               .build())

        # Assert
        expected = {tuple(round(c, 5) for c in palette.uv(n)) for n in ("prophet_skin", "umbrella_black", "cone_orange")}
        self.assertEqual(uvs(obj), expected)

    def test_child_part_location_is_relative_to_parent_part(self):
        # Arrange
        body = Part("Body", pivot=(0, 0, 0)).box((0.2, 0.2, 0.2), (0, 0, 0.1), "linen").build()
        arm = Part("Arm", pivot=(0.2, 0, 0.5), parent=body).box((0.1, 0.1, 0.2), (0.2, 0, 0.4), "linen").build()

        # Act
        hand = Part("Hand", pivot=(0.2, 0, 0.35), parent=arm).sphere(0.05, (0.2, 0, 0.35), "prophet_skin").build()
        bpy.context.view_layer.update()

        # Assert
        self.assertEqual(tuple(round(c, 5) for c in hand.location), (0, 0, -0.15))
        self.assertAlmostEqual(hand.matrix_world.translation.z, 0.35, places=5)

    def test_faces_are_flat_shaded(self):
        # Act
        obj = Part("Ball").sphere(0.3, (0, 0, 0), "grass").build()

        # Assert
        self.assertFalse(any(p.use_smooth for p in obj.data.polygons))

    def test_marker_is_parented_empty(self):
        # Arrange
        parent = root("thing")

        # Act
        front = marker("Front", (0, -0.3, 0.5), parent)

        # Assert
        self.assertEqual(front.type, "EMPTY")
        self.assertEqual(front.parent, parent)

    def test_unknown_swatch_raises(self):
        with self.assertRaises(KeyError):
            Part("Bad").box((1, 1, 1), (0, 0, 0), "beige_of_regret")
