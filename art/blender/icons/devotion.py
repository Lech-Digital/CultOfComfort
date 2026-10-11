"""Devotion: a lit candle on a dish."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 300


def build():
    icon = root("icon_devotion")
    part = Part("Prop", parent=icon)
    part.cylinder(0.16, 0.03, (0, 0, 0.015), "gold_trim", segments=12)
    part.cylinder(0.06, 0.32, (0, 0, 0.19), "linen", segments=10, smooth=True)
    part.cylinder(0.006, 0.04, (0, 0, 0.37), "tarred_wood", segments=4)
    part.sphere(0.04, (0, 0, 0.42), "fire", segments=8, rings=6, scale=(0.8, 0.8, 1.6), smooth=True)
    part.build()
    return icon
