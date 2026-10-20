"""The Guardhouse: a stone-footed timber hall with a lookout on the corner, a bell to ring when the lord's men come,
and a rack of the cult's weapons (a pitchfork, two brooms and a rolling pin). The wardens take it very seriously."""
from lib.buildings import barrel, log_wall, plank_roof, thatch_roof
from lib.mesh import Part, root

TRIANGLE_BUDGET = 3000
REQUIRED_PARTS = ("Building", "Banner")


def build():
    house = root("guardhouse")
    b = Part("Building", parent=house)
    b.box((1.3, 1.1, 0.08), (0, 0, 0.04), "worn_stone", bevel=0.02)                                    # yard
    b.box((0.95, 0.7, 0.22), (-0.1, 0.12, 0.19), "fieldstone", bevel=0.02)                              # stone footing
    corners = ((-0.55, -0.2), (0.35, -0.2), (0.35, 0.44), (-0.55, 0.44))
    for k in range(4):
        log_wall(b, corners[k], corners[(k + 1) % 4], height=0.4, z0=0.3, radius=0.05)
    thatch_roof(b, 0.9, 0.68, (-0.1, 0.12, 0.7), 0.24, overhang=0.06, lean=1.2)
    b.box((0.22, 0.04, 0.34), (-0.2, -0.22, 0.47), "tarred_wood", bevel=0.01)                          # door
    b.box((0.14, 0.03, 0.12), (0.12, -0.22, 0.55), "window", bevel=0.005)                              # window
    # The lookout: four posts, a platform, a little roof, and the bell under it.
    for x, y in ((0.4, -0.45), (0.62, -0.45), (0.4, -0.23), (0.62, -0.23)):
        b.cylinder(0.025, 1.15, (x, y, 0.6), "old_wood", segments=5)
    b.box((0.3, 0.3, 0.04), (0.51, -0.34, 0.92), "fresh_log", bevel=0.005)                              # platform
    for z in (0.98, 1.06):
        b.box((0.3, 0.02, 0.02), (0.51, -0.49, z), "old_wood", bevel=0.0)                                # rail
    plank_roof(b, 0.36, 0.36, (0.51, -0.34, 1.28), 28)
    b.cylinder(0.05, 0.08, (0.51, -0.34, 1.12), "gold_trim", segments=8, radius_top=0.03)              # bell
    # The weapons rack: a pitchfork, two brooms and a rolling pin.
    b.box((0.36, 0.04, 0.03), (-0.3, -0.42, 0.42), "old_wood", bevel=0.0)
    for k, x in enumerate((-0.44, -0.34, -0.24, -0.14)):
        b.cylinder(0.012, 0.42, (x, -0.44, 0.27), "fresh_log", segments=4, rotation=(8, 0, 0))
        if k in (1, 2):
            b.box((0.06, 0.03, 0.09), (x, -0.45, 0.07), "thatch", bevel=0.005)                          # broom heads
    b.box((0.08, 0.02, 0.02), (-0.44, -0.45, 0.49), "mail", bevel=0.0)                                  # fork tines
    barrel(b, (0.15, 0.5, 0.06), radius=0.09, height=0.22)
    building = b.build()
    (Part("Banner", pivot=(0.62, -0.45, 1.3), parent=building)
     .cylinder(0.015, 0.4, (0.64, -0.47, 1.5), "old_wood", segments=4)
     .box((0.2, 0.02, 0.14), (0.75, -0.47, 1.62), "zealot_red", bevel=0.0)
     .build())
    return house
