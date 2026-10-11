import unittest

import bpy

from lib import checks, scene
from lib.people import REQUIRED_PARTS, Look, build_person, prop


def height(root):
    bpy.context.view_layer.update()
    zs = [(o.matrix_world @ v.co).z for o in root.children_recursive if o.type == "MESH" for v in o.data.vertices]
    return min(zs), max(zs)


class PeopleTests(unittest.TestCase):
    def setUp(self):
        scene.reset()

    def test_build_person_default_local_has_the_shared_rig_within_budget(self):
        # Act
        person = build_person("villager", Look())

        # Assert
        checks.verify(person, 2200, REQUIRED_PARTS)
        bottom, top = height(person)
        self.assertAlmostEqual(bottom, 0.0, delta=0.01)
        self.assertAlmostEqual(top, 0.8, delta=0.06)

    def test_build_person_robe_and_tonsure_variant_builds(self):
        # Act
        monk = build_person("monk", Look(robe=True, hair_style="tonsure", brows=-10, mouth=0.01, eye_size=1.2))

        # Assert
        checks.verify(monk, 2200, REQUIRED_PARTS)

    def test_build_person_long_hair_bun_and_beard_variants_build(self):
        for look in (Look(hair_style="long", hair="thatch"), Look(hair_style="bun", hair="worn_stone"), Look(beard=True)):
            with self.subTest(look.hair_style):
                scene.reset()
                checks.verify(build_person("variant", look), 2200, REQUIRED_PARTS)

    def test_prop_attaches_to_the_named_part(self):
        # Arrange
        def stick(parts, d):
            prop(parts, "Stick", attach_to="ArmR", pivot=(-d.arm_x, 0, d.hand)).box((0.03, 0.03, 0.4), (-d.arm_x, 0, 0.2), "fresh_log")

        # Act
        person = build_person("walker", Look(extras=[stick]))
        objects = {o.name: o for o in person.children_recursive}

        # Assert
        self.assertEqual(objects["Stick"].parent.name, "ArmR")
