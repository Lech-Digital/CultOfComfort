import unittest

import bpy
from mathutils import Vector

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

    def test_model_authored_facing_minus_y_is_turned_to_face_plus_y(self):
        # Arrange — authors place the face at −Y (Blender front view) and the left hand at +X.
        body = Part("Body").box((0.1, 0.1, 0.1), (0, -0.2, 0.5), "linen", bevel=0).build()
        hand = Part("HandL", pivot=(0.2, 0, 0.4), parent=body).sphere(0.05, (0.2, 0, 0.4), "prophet_skin").build()
        front = marker("Front", (0, -0.3, 0.5), body)
        bpy.context.view_layer.update()

        # Act
        face_y = sum(v.co.y for v in body.data.vertices) / len(body.data.vertices)

        # Assert — exported models face +Y (→ Unity +Z) and keep the left hand on the model's left (−X when facing +Y).
        self.assertAlmostEqual(face_y, 0.2, places=4)
        self.assertAlmostEqual(front.matrix_world.translation.y, 0.3, places=4)
        self.assertAlmostEqual(hand.matrix_world.translation.x, -0.2, places=4)
        self.assertAlmostEqual(hand.matrix_world.translation.z, 0.4, places=4)

    def test_build_rotation_is_given_in_authoring_space(self):
        # Arrange — chin up by tilting the face (−Y) upwards: −12° about X while authoring.
        head = Part("Head", pivot=(0, 0, 0.6)).sphere(0.2, (0, 0, 0.8), "prophet_skin").build(rotation=(-12, 0, 0))
        bpy.context.view_layer.update()

        # Act — the point straight in front of the face, transformed by the object's rotation.
        nose = head.matrix_world.to_3x3() @ Vector((0, 1, 0))

        # Assert — after the turn the face is +Y, and it still points up.
        self.assertGreater(nose.z, 0.15)

    def test_prism_is_a_gable_roof_shape(self):
        # Act — 1 m wide (X), 2 m long (Y), 0.5 m high, ridge along Y.
        roof = Part("Roof").prism((1.0, 2.0, 0.5), (0, 0, 1.0), "thatch").build()

        # Assert — 6 corners, 5 faces (2 triangles + 3 quads), ridge at the top centre.
        self.assertEqual(len(roof.data.vertices), 6)
        self.assertEqual(len(roof.data.polygons), 5)
        top = max(v.co.z for v in roof.data.vertices)
        self.assertAlmostEqual(top, 1.25, places=4)
        self.assertTrue(all(abs(v.co.x) < 1e-4 for v in roof.data.vertices if abs(v.co.z - top) < 1e-4))

    def test_faces_are_flat_shaded(self):
        # Act
        obj = Part("Ball").sphere(0.3, (0, 0, 0), "grass").build()

        # Assert
        self.assertFalse(any(p.use_smooth for p in obj.data.polygons))

    def test_smooth_pieces_are_smooth_and_others_stay_flat(self):
        # Act — skin reads as soft and human when smooth; clothes and props keep the chunky facets.
        obj = (Part("Head")
               .sphere(0.2, (0, 0, 0), "prophet_skin", smooth=True)
               .box((0.1, 0.1, 0.1), (0, 0, 0.3), "linen", bevel=0)
               .build())

        # Assert
        smooth = [p.use_smooth for p in obj.data.polygons]
        self.assertEqual(smooth.count(False), 6)  # the unbevelled box
        self.assertTrue(smooth.count(True) > 6)

    def test_rounded_box_has_more_faces_than_sharp_bevel(self):
        # Act
        sharp = Part("A").box((0.3, 0.3, 0.3), (0, 0, 0), "linen", bevel=0.03).build()
        rounded = Part("B").box((0.3, 0.3, 0.3), (0, 0, 0), "linen", bevel=0.03, bevel_segments=2).build()

        # Assert
        self.assertGreater(len(rounded.data.polygons), len(sharp.data.polygons))

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
