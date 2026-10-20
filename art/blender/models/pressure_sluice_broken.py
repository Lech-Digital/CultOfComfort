"""A wrecked Pressure Sluice: the barrel tipped off its stand and stove in, the hose lying in a puddle, the brass
nozzle bent."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 1200
REQUIRED_PARTS = ("Prop",)


def build():
    tower = root("pressure_sluice_broken")
    p = Part("Prop", parent=tower)
    for x, y, h, tilt in ((-0.22, -0.22, 0.55, 3), (0.22, -0.22, 0.2, 25), (-0.22, 0.22, 0.55, -4), (0.22, 0.22, 0.25, -20)):
        p.box((0.07, 0.07, h), (x, y, h / 2), "old_wood", bevel=0.01, rotation=(tilt, 0, 0))
    p.cylinder(0.26, 0.5, (0.15, 0.05, 0.26), "old_wood", segments=10, rotation=(90, 0, 35))                 # barrel, on its side
    for z in (-0.15, 0.15):
        p.cylinder(0.27, 0.04, (0.15 + z * 0.57, 0.05 + z * 0.82, 0.26), "mail", segments=10, rotation=(90, 0, 35))  # hoops
    p.sphere(0.45, (0.0, -0.3, 0.01), "water", segments=8, rings=4, scale=(1.2, 1.0, 0.06), smooth=True)  # the spill
    p.cylinder(0.04, 0.7, (-0.3, -0.35, 0.04), "tarred_wood", segments=6, rotation=(0, 90, 60))              # hose
    p.cylinder(0.045, 0.16, (-0.55, -0.15, 0.05), "gold_trim", segments=6, radius_top=0.02, rotation=(0, 90, 110))  # bent nozzle
    p.build()
    return tower
