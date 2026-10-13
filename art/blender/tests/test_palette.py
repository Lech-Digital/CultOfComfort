import os
import struct
import tempfile
import unittest
import zlib

import palette


class PaletteTests(unittest.TestCase):
    def test_swatches_match_spec_and_are_unique(self):
        # Arrange
        names = [name for name, _ in palette.SWATCHES]

        # Act & Assert
        self.assertEqual(len(names), 40)
        self.assertEqual(len(set(names)), len(names))
        self.assertEqual(dict(palette.SWATCHES)["cone_orange"], "#ff5a00")
        self.assertEqual(dict(palette.SWATCHES)["linen"], "#ece6d6")
        self.assertEqual(dict(palette.SWATCHES)["woad"], "#46618c")  # knock-offs (spec: The Boot)

    def test_uv_returns_cell_centre_and_round_trips(self):
        # Arrange
        rows = -(-len(palette.SWATCHES) // palette.COLUMNS)

        # Act
        first = palette.uv(palette.SWATCHES[0][0])

        # Assert
        self.assertAlmostEqual(first[0], 0.5 / palette.COLUMNS)
        self.assertAlmostEqual(first[1], 1 - 0.5 / rows)
        for name, _ in palette.SWATCHES:
            self.assertEqual(palette.swatch_at(*palette.uv(name)), name)

    def test_uv_unknown_name_raises(self):
        with self.assertRaises(KeyError):
            palette.uv("beige_of_regret")

    def test_swatch_at_off_centre_returns_none(self):
        # Arrange
        u, v = palette.uv("grass")

        # Act & Assert
        self.assertIsNone(palette.swatch_at(u + 0.01, v))

    def test_write_png_writes_expected_size_and_first_pixel(self):
        # Arrange
        path = os.path.join(tempfile.mkdtemp(), "Palette.png")

        # Act
        palette.write_png(path)

        # Assert
        data = open(path, "rb").read()
        self.assertEqual(data[:8], b"\x89PNG\r\n\x1a\n")
        width, height = struct.unpack(">II", data[16:24])
        rows = -(-len(palette.SWATCHES) // palette.COLUMNS)
        self.assertEqual((width, height), (palette.COLUMNS * palette.CELL, rows * palette.CELL))
        idat_at = data.index(b"IDAT")
        length = struct.unpack(">I", data[idat_at - 4:idat_at])[0]
        raw = zlib.decompress(data[idat_at + 4:idat_at + 4 + length])
        self.assertEqual(tuple(raw[1:4]), palette.rgb(palette.SWATCHES[0][0]))  # byte 0 is the row filter
