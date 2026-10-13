"""Midden Sorter: the village rubbish heap, systematically sifted. A steaming mound, a sorting bench with a sieve,
and three wicker baskets dyed woad, weld and madder: medieval recycling, enforced by the Prophet. Smells."""
from lib.buildings import clay_cone, sack
from lib.mesh import Part, root

TRIANGLE_BUDGET = 3500
REQUIRED_PARTS = ("Building",)


def build():
    midden = root("midden_sorter")
    b = Part("Building", parent=midden)
    b.box((1.04, 1.0, 0.05), (0, 0, 0.025), "road_mud", bevel=0.02)
    # The heap.
    for x, y, r, h, sw in ((-0.18, 0.18, 0.32, 0.55, "wet_mud"), (0.12, 0.25, 0.24, 0.42, "old_wood"), (-0.3, -0.05, 0.2, 0.35, "wet_mud")):
        b.sphere(r, (x, y, 0.05), sw, segments=9, rings=6, scale=(1.0, 0.9, h / r))
    for k, (x, y, z, sw) in enumerate(((-0.2, 0.1, 0.42, "mail"), (0.05, 0.3, 0.36, "fieldstone"), (-0.35, 0.2, 0.3, "mail"), (0.15, 0.12, 0.28, "tarred_wood"))):
        b.box((0.1, 0.04, 0.03), (x, y, z), sw, bevel=0.005, rotation=(20 * k, 35 * k, 10 * k))          # bent nails, a cauldron lid
    b.cylinder(0.06, 0.03, (-0.05, 0.05, 0.47), "mail", segments=8, rotation=(30, 0, 0))                     # a cracked pot
    for k in range(3):                                                                                        # flies' steam
        b.sphere(0.035, (-0.1 + k * 0.08, 0.15, 0.62 + k * 0.06), "linen", segments=5, rings=3)
    # The sorting bench with a sieve.
    b.box((0.5, 0.2, 0.04), (0.2, -0.25, 0.3), "fresh_log", bevel=0.01)
    for x in (0.0, 0.4):
        b.box((0.04, 0.16, 0.28), (x, -0.25, 0.14), "old_wood", bevel=0.005)
    b.cylinder(0.12, 0.04, (0.2, -0.25, 0.34), "old_wood", segments=10)
    b.cylinder(0.105, 0.01, (0.2, -0.25, 0.36), "thatch", segments=10)
    # Three baskets: woad, weld, madder.
    for k, dye in enumerate(("woad", "weld", "madder")):
        x = -0.35 + k * 0.17
        b.cylinder(0.075, 0.16, (x, -0.38, 0.13), dye, segments=8, radius_top=0.085)
        b.cylinder(0.088, 0.02, (x, -0.38, 0.21), "thatch", segments=8)
    sack(b, (0.42, 0.32, 0.05), swatch="levy_wool")
    clay_cone(b, (0.42, -0.05, 0.05), size=0.8)
    b.build()
    return midden
