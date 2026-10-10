import os
import unittest

import build


class PipelineTests(unittest.TestCase):
    def test_build_asset_pipeline_cube_writes_fbx_and_both_previews(self):
        # Act
        result = build.build_asset("pipeline_cube")

        # Assert
        for key in ("fbx", "day", "night"):
            self.assertTrue(os.path.getsize(result[key]) > 1000, key)
        self.assertTrue(result["fbx"].endswith(os.path.join("Resources", "Art", "Models", "pipeline_cube.fbx")))
        self.assertLessEqual(result["triangles"], 300)

    def test_build_asset_unknown_name_raises(self):
        with self.assertRaises(ModuleNotFoundError):
            build.build_asset("does_not_exist", preview=False)
