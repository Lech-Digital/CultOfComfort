import os
import tempfile
import unittest

import bpy

import build

ICONS = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "icons")
EXPECTED = ("timber", "scrap", "peat", "devotion", "penny", "comfort", "sluice", "voice", "stones", "speed",
            "map", "research", "missionaries", "tithes", "shop", "cult", "settings", "portrait_prophet", "mud", "swords", "b_hearth", "b_woodcutter", "b_peat_cut", "b_storage_hut", "b_cultist_hut", "b_cistern", "b_slurry_chute", "b_sapling_flinger", "b_wattle_fence", "b_stone", "b_tree", "b_wooden_wall", "b_gate", "b_dirt_track", "b_plank_walk", "b_cobbled_road")


class IconTests(unittest.TestCase):
    def setUp(self):
        self._dir = build.ICON_DIR
        build.ICON_DIR = tempfile.mkdtemp()

    def tearDown(self):
        build.ICON_DIR = self._dir

    def test_icon_modules_match_the_hud_list(self):
        names = sorted(f[:-3] for f in os.listdir(ICONS) if f.endswith(".py") and not f.startswith("_"))
        self.assertEqual(names, sorted(EXPECTED))

    def test_build_icon_every_icon_renders_transparent_128px(self):
        for name in EXPECTED:
            with self.subTest(name):
                # Act
                path = build.build_icon(name)

                # Assert
                image = bpy.data.images.load(path)
                self.assertEqual(tuple(image.size), (128, 128))
                pixels = list(image.pixels)
                self.assertEqual(pixels[3], 0.0, "corner must be transparent")
                self.assertGreater(max(pixels[3::4]), 0.99, "something must be drawn")
                bpy.data.images.remove(image)
