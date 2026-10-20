"""The Comfy Altar: a flat stone slab on two standing stones, and on it the holiest things the cult owns: a plump red
cushion, three candles, a folded towel and a rubber duck. Set in the middle of the stone circle across the river."""
from lib.buildings import rubber_duck
from lib.mesh import Part, root

TRIANGLE_BUDGET = 1600
REQUIRED_PARTS = ("Building", "Flames")


def build():
    altar = root("comfy_altar")
    b = Part("Building", parent=altar)
    b.box((1.5, 1.5, 0.06), (0, 0, 0.03), "worn_stone", bevel=0.02)                                          # the paved floor
    for x in (-0.38, 0.38):
        b.box((0.22, 0.5, 0.42), (x, 0, 0.27), "fieldstone", bevel=0.03)                                      # legs
    b.box((1.1, 0.66, 0.12), (0, 0, 0.54), "worn_stone", bevel=0.03, bevel_segments=2)                         # slab
    b.box((0.5, 0.42, 0.12), (-0.12, 0.02, 0.66), "wulfric_red", bevel=0.05, bevel_segments=2, smooth=True)   # the cushion
    for x, y in ((-0.06, -0.06), (-0.22, 0.1), (0.04, 0.1)):
        b.sphere(0.03, (x, y, 0.735), "gold_trim", segments=5, rings=3)                                       # tassels
    b.box((0.2, 0.16, 0.05), (0.33, 0.12, 0.625), "linen", bevel=0.01)                                         # folded towel
    for x, y in ((0.42, -0.2), (0.3, -0.22), (-0.44, -0.22)):
        b.cylinder(0.025, 0.14, (x, y, 0.67), "linen", segments=6)                                            # candles
    rubber_duck(b, (0.33, 0.12, 0.65), size=0.6, yaw=200)
    for x, y in ((-0.6, -0.6), (0.6, -0.6), (-0.6, 0.6), (0.6, 0.6)):
        b.cylinder(0.05, 0.08, (x, y, 0.1), "ochre_clay", segments=6, radius_top=0.035)                         # lamps at the corners
    building = b.build()
    flames = Part("Flames", pivot=(0, 0, 0.75), parent=building)
    for x, y in ((0.42, -0.2), (0.3, -0.22), (-0.44, -0.22)):
        flames.sphere(0.022, (x, y, 0.755), "fire", segments=5, rings=3, scale=(1, 1, 1.6))
    flames.build()
    return altar
