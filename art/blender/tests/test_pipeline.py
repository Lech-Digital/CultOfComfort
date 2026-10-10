import os
import tempfile
import unittest

import build


class PipelineTests(unittest.TestCase):
    def setUp(self):
        # Write into a scratch folder so running the tests never touches the committed models and previews.
        self._dirs = build.MODEL_DIR, build.PREVIEW_DIR
        scratch = tempfile.mkdtemp()
        build.MODEL_DIR = os.path.join(scratch, "Models")
        build.PREVIEW_DIR = os.path.join(scratch, "previews")

    def tearDown(self):
        build.MODEL_DIR, build.PREVIEW_DIR = self._dirs

    def test_build_asset_pipeline_cube_writes_fbx_and_both_previews(self):
        # Act
        result = build.build_asset("pipeline_cube")

        # Assert
        for key in ("fbx", "day", "night"):
            self.assertTrue(os.path.getsize(result[key]) > 1000, key)
        self.assertEqual(result["fbx"], os.path.join(build.MODEL_DIR, "pipeline_cube.fbx"))
        self.assertLessEqual(result["triangles"], 300)

    def test_build_asset_unknown_name_raises(self):
        with self.assertRaises(ModuleNotFoundError):
            build.build_asset("does_not_exist", preview=False)

    def test_default_output_is_the_unity_models_folder(self):
        self.assertTrue(self._dirs[0].endswith(os.path.join("Resources", "Art", "Models")))
