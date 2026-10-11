"""Cottage: a snug daub-and-timber home under fat thatch, smoke from the chimney, a bench by the door.
Four beds, eight opinions about the Prophet."""
from lib.buildings import thatch_roof
from lib.mesh import Part, root

TRIANGLE_BUDGET = 2500
REQUIRED_PARTS = ("Building",)


def build():
    cottage = root("cottage")
    b = Part("Building", parent=cottage)
    b.box((1.1, 1.0, 0.05), (0, 0, 0.025), "grass", bevel=0.02)
    b.box((0.82, 0.62, 0.46), (0, 0.05, 0.28), "daub", bevel=0.03)                                     # walls
    for x in (-0.41, 0.41):
        b.box((0.06, 0.64, 0.48), (x, 0.05, 0.28), "old_wood", bevel=0.01)                             # corner beams
    b.box((0.84, 0.06, 0.05), (0, -0.27, 0.42), "old_wood", bevel=0.01)                                # lintel beam
    thatch_roof(b, 0.82, 0.62, (0, 0.05, 0.51), 0.48)
    b.box((0.2, 0.04, 0.32), (-0.12, -0.27, 0.21), "tarred_wood", bevel=0.01)                          # door
    b.box((0.14, 0.03, 0.12), (0.22, -0.27, 0.32), "window", bevel=0.005)
    b.box((0.14, 0.14, 0.32), (0.25, 0.22, 0.86), "fieldstone", bevel=0.02)                            # chimney
    b.sphere(0.07, (0.27, 0.2, 1.08), "linen", segments=6, rings=4, scale=(1.0, 1.0, 0.7))             # smoke puff
    b.box((0.3, 0.1, 0.05), (0.2, -0.38, 0.12), "fresh_log", bevel=0.01)                               # bench
    for x in (0.08, 0.32):
        b.box((0.04, 0.08, 0.1), (x, -0.38, 0.05), "old_wood", bevel=0.005)
    b.build()
    return cottage
