"""A wrecked Sapling Flinger: the frame knocked flat, the sapling snapped in two, the sling cup thrown aside, the
ammunition scattered."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 1500
REQUIRED_PARTS = ("Prop",)


def build():
    tower = root("sapling_flinger_broken")
    p = Part("Prop", parent=tower)
    p.box((0.8, 0.5, 0.14), (0, 0, 0.07), "old_wood", bevel=0.02, rotation=(0, 6, 8))                       # sledge, askew
    p.box((0.08, 0.08, 0.7), (0.25, -0.3, 0.06), "old_wood", bevel=0.012, rotation=(90, 0, 25))             # frame, fallen
    p.box((0.08, 0.08, 0.4), (0.3, 0.18, 0.3), "old_wood", bevel=0.012, rotation=(-15, -20, 0))            # one post left
    p.cylinder(0.11, 0.18, (-0.3, 0, 0.23), "fresh_log", segments=8)                                         # root ball
    p.cylinder(0.05, 0.35, (-0.22, 0, 0.45), "fresh_log", segments=7, radius_top=0.04, rotation=(0, 25, 0))  # snapped stump
    p.cylinder(0.035, 0.55, (0.15, 0.32, 0.05), "fresh_log", segments=6, radius_top=0.02, rotation=(0, 90, 30))  # the top, in the mud
    p.sphere(0.1, (0.4, 0.42, 0.08), "foliage", segments=6, rings=4, smooth=True)
    p.cylinder(0.09, 0.06, (-0.42, -0.35, 0.05), "levy_wool", segments=8, radius_top=0.11, rotation=(70, 0, 0))  # sling cup
    for x, y in ((0.4, -0.2), (-0.1, -0.42), (0.1, 0.45), (-0.45, 0.3)):
        p.sphere(0.06, (x, y, 0.05), "fieldstone", segments=6, rings=4)
    p.build()
    return tower
