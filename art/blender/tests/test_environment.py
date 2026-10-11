import unittest

import bpy


class EnvironmentTests(unittest.TestCase):
    def test_blender_version_is_45_lts(self):
        # Arrange & Act
        major, minor, _ = bpy.app.version

        # Assert
        self.assertEqual((major, minor), (4, 5))
