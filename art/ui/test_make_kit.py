import os
import tempfile
import unittest

from PIL import Image

import make_kit


class MakeKitTests(unittest.TestCase):
    def test_write_kit_writes_every_texture(self):
        out = tempfile.mkdtemp()
        paths = make_kit.write_kit(out)
        self.assertEqual(sorted(os.path.basename(p)[:-4] for p in paths), sorted(make_kit.NAMES))

    def test_write_kit_twice_is_byte_identical(self):
        a, b = tempfile.mkdtemp(), tempfile.mkdtemp()
        make_kit.write_kit(a)
        make_kit.write_kit(b)
        for name in make_kit.NAMES:
            with open(os.path.join(a, name + ".png"), "rb") as x, open(os.path.join(b, name + ".png"), "rb") as y:
                self.assertEqual(x.read(), y.read(), name)

    def test_parchment_has_transparent_corner_and_opaque_centre(self):
        out = tempfile.mkdtemp()
        make_kit.write_kit(out)
        im = Image.open(os.path.join(out, "parchment.png")).convert("RGBA")
        self.assertEqual(im.getpixel((0, 0))[3], 0)
        self.assertEqual(im.getpixel((im.width // 2, im.height // 2))[3], 255)

    def test_hazard_tiles_seamlessly(self):
        out = tempfile.mkdtemp()
        make_kit.write_kit(out)
        im = Image.open(os.path.join(out, "hazard.png")).convert("RGB")
        for y in range(im.height):
            self.assertEqual(im.getpixel((0, y)), im.getpixel((im.width - 1, (y + 1) % im.height)))


if __name__ == "__main__":
    unittest.main()
