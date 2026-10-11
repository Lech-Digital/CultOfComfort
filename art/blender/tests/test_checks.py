import unittest

from lib import checks, scene
from lib.mesh import Part, root


class ChecksTests(unittest.TestCase):
    def setUp(self):
        scene.reset()
        self.root = root("thing")
        Part("Body", parent=self.root).box((1, 1, 1), (0, 0, 0.5), "linen", bevel=0).build()

    def test_triangles_counts_all_meshes_under_root(self):
        # A cube without bevel is 6 quads = 12 triangles.
        self.assertEqual(checks.triangles(self.root), 12)

    def test_verify_passes_within_budget_with_parts_present(self):
        checks.verify(self.root, budget=12, required=("Body",))

    def test_verify_over_budget_raises_with_numbers(self):
        with self.assertRaises(checks.AssetCheckError) as error:
            checks.verify(self.root, budget=11, required=())
        self.assertIn("12 triangles > budget 11", str(error.exception))

    def test_verify_missing_part_raises_with_name(self):
        with self.assertRaises(checks.AssetCheckError) as error:
            checks.verify(self.root, budget=100, required=("Body", "Umbrella"))
        self.assertIn("Umbrella", str(error.exception))

    def test_off_palette_detects_moved_uvs(self):
        # Arrange
        body = self.root.children[0]
        body.data.uv_layers.active.data[0].uv = (0.001, 0.001)

        # Act & Assert
        self.assertEqual(checks.off_palette(self.root), ["Body"])
