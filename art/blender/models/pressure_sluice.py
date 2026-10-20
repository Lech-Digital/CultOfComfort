"""Pressure Sluice (tech tree: Comfort era, GDD 03): an elevated cistern on a timber frame, a leather hose down to a
hammered nozzle on a swivel. Every 10 m of head is a bar; the Prophet has about 1.5 m. `Nozzle` swings to aim."""
from lib.buildings import barrel
from lib.mesh import Part, root

TRIANGLE_BUDGET = 3500
REQUIRED_PARTS = ("Base", "Nozzle")


def build():
    tower = root("pressure_sluice")
    base = Part("Base", parent=tower)
    base.box((0.7, 0.7, 0.12), (0, 0, 0.06), "fieldstone", bevel=0.02)                              # stone footing
    for x, y in ((-0.24, -0.24), (0.24, -0.24), (-0.24, 0.24), (0.24, 0.24)):
        base.box((0.08, 0.08, 1.05), (x, y, 0.62), "old_wood", bevel=0.01)                           # the frame
    for z in (0.45, 0.95):
        base.box((0.56, 0.06, 0.05), (0, -0.24, z), "tarred_wood", bevel=0.008, rotation=(0, 18 if z < 0.6 else -18, 0))  # braces
    base.box((0.62, 0.62, 0.05), (0, 0, 1.15), "fresh_log", bevel=0.01)                              # platform
    base.cylinder(0.27, 0.42, (0, 0.04, 1.38), "old_wood", segments=12)                              # the cistern barrel
    for z in (1.22, 1.38, 1.54):
        base.cylinder(0.28, 0.03, (0, 0.04, z), "mail", segments=12)                                 # iron hoops
    base.cylinder(0.24, 0.02, (0, 0.04, 1.6), "water", segments=12)
    base.sphere(0.045, (0.12, -0.1, 1.62), "water", segments=6, rings=4, scale=(1, 1, 0.5))          # slop
    # The hose: leather (ochre) segments from the barrel's foot down the front post to the swivel.
    for k, (y, z, tilt) in enumerate(((-0.24, 1.08, 10), (-0.3, 0.86, 20), (-0.33, 0.62, 8), (-0.33, 0.4, 0))):
        base.cylinder(0.045, 0.24, (0.05, y, z), "ochre_clay", segments=6, rotation=(tilt, 0, 0))
    barrel(base, (0.38, 0.3, 0.27), radius=0.1, height=0.24)                                         # a spare bucket of water
    base.box((0.12, 0.12, 0.2), (0.05, -0.36, 0.22), "tarred_wood", bevel=0.01)                      # swivel post
    base_obj = base.build()

    (Part("Nozzle", pivot=(0.05, -0.36, 0.34), parent=base_obj)
     .cylinder(0.055, 0.32, (0.05, -0.52, 0.36), "gold_trim", segments=8, radius_top=0.025, rotation=(90, 0, 0))  # hammered brass
     .cylinder(0.07, 0.06, (0.05, -0.38, 0.36), "mail", segments=8, rotation=(90, 0, 0))
     .box((0.03, 0.18, 0.03), (0.13, -0.42, 0.36), "old_wood", bevel=0.004)                         # the handle he leans on
     .build())
    return tower
