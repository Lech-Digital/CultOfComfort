"""Midden Sorter, level 2: the Scrap Works. A treadle drum-sieve the size of a cart, a chute off the heap, a woad
awning, a heap of shiny Scrap (one heroically salvaged zipper), and a sorter in a weld-dyed "safety" vest."""
from lib.buildings import clay_cone, weld_vest, wheel
from lib.mesh import Part, root

TRIANGLE_BUDGET = 4500
REQUIRED_PARTS = ("Building",)


def build():
    works = root("midden_sorter_l2")
    b = Part("Building", parent=works)
    b.box((1.06, 1.04, 0.05), (0, 0, 0.025), "road_mud", bevel=0.02)
    for x, y, r, h in ((-0.25, 0.25, 0.32, 0.7), (0.0, 0.3, 0.26, 0.5)):
        b.sphere(r, (x, y, 0.05), "wet_mud", segments=9, rings=6, scale=(1.0, 0.85, h / r))
    b.box((0.16, 0.5, 0.03), (-0.05, -0.02, 0.45), "old_wood", bevel=0.006, rotation=(-35, 0, 0))            # chute off the heap
    # The drum-sieve on its trestles, turned by a treadwheel.
    b.cylinder(0.17, 0.5, (0.18, -0.2, 0.42), "thatch", segments=10, rotation=(0, 90, 0))
    for k in range(4):
        b.cylinder(0.175, 0.02, (-0.05 + k * 0.15, -0.2, 0.42), "old_wood", segments=10, rotation=(0, 90, 0))
    for x in (-0.08, 0.44):
        for side in (-1, 1):
            b.box((0.04, 0.04, 0.45), (x, -0.2 + side * 0.1, 0.22), "old_wood", bevel=0.005, rotation=(side * 14, 0, 0))
    wheel(b, (0.47, -0.2, 0.42), 0.2, spokes=6, rim=10, width=0.06)
    # The good stuff: a heap of Scrap, glinting.
    for k in range(7):
        b.box((0.07, 0.03, 0.05), (0.32 + (k % 3) * 0.05, 0.25 + (k // 3) * 0.05, 0.07 + (k % 2) * 0.04), "mail", bevel=0.005,
              rotation=(k * 30, k * 47, k * 13))
    b.box((0.04, 0.12, 0.012), (0.36, 0.33, 0.16), "gold_trim", bevel=0.002, rotation=(0, 20, 30))         # the zipper, heroically salvaged
    # Woad awning over the sorting.
    for x in (-0.45, 0.45):
        b.cylinder(0.02, 0.75, (x, -0.42, 0.37), "old_wood", segments=5)
    b.box((0.98, 0.35, 0.02), (0, -0.3, 0.74), "woad", bevel=0, rotation=(-12, 0, 0))
    weld_vest(b, (-0.42, -0.42, 0.45))
    clay_cone(b, (-0.38, 0.0, 0.05), size=0.9)
    clay_cone(b, (0.45, 0.05, 0.05), size=0.7, lean=-6)
    b.build()
    return works
