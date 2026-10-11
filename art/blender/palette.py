"""Muddy Satire palette (spec: Style guide). Every model samples these swatch centres from one texture.

Pure Python on purpose: no bpy, so it is trivially testable and the PNG is byte-for-byte reproducible.
"""
import os
import struct
import zlib

COLUMNS = 8
CELL = 16  # pixels per swatch cell

SWATCHES = [
    # Ground & nature
    ("grass", "#6f7a49"), ("grass_alt", "#687244"), ("road_mud", "#8a6a47"), ("wet_mud", "#4e3520"),
    ("water", "#5b7f86"), ("foliage", "#6f8a3a"), ("pine", "#4c5b33"),
    # Building materials
    ("roman_marble", "#d7cfbf"), ("worn_stone", "#b8b0a2"), ("fieldstone", "#7f7b73"), ("fresh_log", "#9b7a52"),
    ("old_wood", "#6d4c2f"), ("tarred_wood", "#4a321e"), ("thatch", "#a08450"), ("daub", "#bfa98a"),
    # People
    ("prophet_skin", "#e7b590"), ("local_skin", "#d9a37f"), ("linen", "#ece6d6"), ("levy_wool", "#9c8460"),
    ("mail", "#8e9399"), ("zealot_red", "#a8432c"), ("wulfric_red", "#8a2f24"), ("gold_trim", "#d9b45a"),
    # 2024 junk
    ("cone_orange", "#ff5a00"), ("tarp_blue", "#1f6dff"), ("hivis_yellow", "#e8ff3a"), ("flipflop_pink", "#ff4fa3"),
    ("umbrella_black", "#2b2b2e"),
    # Emissive
    ("fire", "#ffb04d"), ("ember", "#ff7a1f"), ("window", "#ffcf80"), ("moon_rim", "#9fe3ea"),
]

_INDEX = {name: i for i, (name, _) in enumerate(SWATCHES)}
ROWS = -(-len(SWATCHES) // COLUMNS)

_REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
UNITY_ART_DIR = os.path.join(_REPO, "client", "CultOfComfort.Unity", "Assets", "Resources", "Art")
UNITY_PALETTE_PATH = os.path.join(UNITY_ART_DIR, "Palette.png")


def rgb(name):
    h = dict(SWATCHES)[name].lstrip("#")
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


def uv(name):
    """UV of the swatch's cell centre. Row 0 is the top of the image (v = 1 is the top in Blender and Unity)."""
    i = _INDEX[name]
    col, row = i % COLUMNS, i // COLUMNS
    return (col + 0.5) / COLUMNS, 1 - (row + 0.5) / ROWS


def swatch_at(u, v, tolerance=1e-4):
    for name in _INDEX:
        su, sv = uv(name)
        if abs(su - u) <= tolerance and abs(sv - v) <= tolerance:
            return name
    return None


def write_png(path):
    """8-bit RGB PNG, one CELL×CELL block per swatch, unused cells black."""
    width, height = COLUMNS * CELL, ROWS * CELL
    rows = []
    for y in range(height):
        row = bytearray([0])  # filter type: none
        for x in range(width):
            i = (y // CELL) * COLUMNS + x // CELL
            row += bytes(rgb(SWATCHES[i][0])) if i < len(SWATCHES) else b"\x00\x00\x00"
        rows.append(bytes(row))

    def chunk(kind, data):
        return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data) & 0xFFFFFFFF)

    png = b"\x89PNG\r\n\x1a\n"
    png += chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 2, 0, 0, 0))
    png += chunk(b"IDAT", zlib.compress(b"".join(rows), 9))
    png += chunk(b"IEND", b"")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "wb") as f:
        f.write(png)
