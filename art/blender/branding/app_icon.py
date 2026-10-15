"""The app icon's star: the sacred rubber duck, hooded in woad like a proper cultist, wearing the sun-in-a-bath
pendant, afloat in a steaming wooden tub. Rendered big on a transparent ground; art/make_app_icon.py composes it."""
import math

from lib.mesh import Part, root

TRIANGLE_BUDGET = 6000
SIZE = 1024


def build():
    icon = root("app_icon")
    tub = Part("Tub", parent=icon)
    staves = 18
    for k in range(staves):  # a cooper's tub: staves, two iron hoops
        a = 2 * math.pi * k / staves
        tub.box((0.19, 0.06, 0.5), (0.52 * math.cos(a), 0.52 * math.sin(a), 0.25), "fresh_log" if k % 2 else "old_wood",
                bevel=0.012, rotation=(0, 0, math.degrees(a) + 90))
    for z in (0.1, 0.4):
        tub.cylinder(0.56, 0.05, (0, 0, z), "tarred_wood", segments=24)
    tub.cylinder(0.5, 0.04, (0, 0, 0.44), "water", segments=24)
    tub.build()

    duck = Part("Duck", parent=icon)
    duck.sphere(0.3, (0, 0.05, 0.6), "hivis_yellow", segments=16, rings=10, scale=(1.0, 1.25, 0.75), smooth=True)
    duck.sphere(0.075, (0.0, 0.42, 0.66), "hivis_yellow", segments=10, rings=6, smooth=True)          # tail
    duck.sphere(0.2, (0, -0.2, 0.92), "hivis_yellow", segments=16, rings=10, smooth=True)              # head
    duck.cylinder(0.075, 0.16, (0, -0.42, 0.88), "cone_orange", segments=12, radius_top=0.05, rotation=(90, 0, 0), smooth=True)
    for x in (-0.085, 0.085):
        duck.sphere(0.035, (x, -0.37, 0.98), "linen", segments=8, rings=6, smooth=True)
        duck.sphere(0.02, (x, -0.395, 0.985), "umbrella_black", segments=8, rings=6, smooth=True)
    # The woad hood: a draped cowl round the head with a point, open at the face.
    duck.sphere(0.235, (0, -0.15, 0.97), "woad", segments=16, rings=10, scale=(1.05, 0.85, 1.0), smooth=True)
    duck.cylinder(0.13, 0.26, (0, -0.06, 1.18), "woad", segments=12, radius_top=0.0, rotation=(-28, 0, 0), smooth=True)
    duck.box((0.5, 0.36, 0.05), (0, -0.02, 0.72), "woad", bevel=0.05, bevel_segments=3, smooth=True)    # the cape on its back
    # The pendant: a gold sun.
    duck.cylinder(0.055, 0.02, (0, -0.4, 0.73), "gold_trim", segments=12, rotation=(80, 0, 0))
    for k in range(8):
        a = 2 * math.pi * k / 8
        duck.box((0.03, 0.012, 0.012), (0.08 * math.cos(a), -0.405, 0.73 + 0.08 * math.sin(a)), "gold_trim", bevel=0.002,
                 rotation=(0, -math.degrees(a), 0))
    duck.build()

    steam = Part("Steam", parent=icon)
    for x, y, z, r in ((-0.45, 0.1, 0.62, 0.11), (-0.55, 0.0, 0.85, 0.09), (-0.48, 0.05, 1.05, 0.07),
                       (0.45, 0.15, 0.6, 0.1), (0.55, 0.1, 0.82, 0.08)):
        steam.sphere(r, (x, y, z), "linen", segments=10, rings=7, smooth=True)
    steam.build()
    return icon
