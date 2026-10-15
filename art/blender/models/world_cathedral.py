"""A cathedral on the world map: a long stone nave, a square tower with a spire, a rose window, and a bishop who
thinks baths are the Devil's soup."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 3000
REQUIRED_PARTS = ("Building",)


def build():
    church = root("world_cathedral")
    b = Part("Building", parent=church)
    b.box((0.62, 1.5, 0.62), (0, 0.15, 0.37), "worn_stone", bevel=0.02)                                  # nave
    b.prism((0.74, 1.56, 0.42), (0, 0.15, 0.89), "fieldstone")                                          # roof
    b.box((1.3, 0.46, 0.5), (0, 0.45, 0.31), "worn_stone", bevel=0.02)                                   # transept
    b.prism((1.38, 0.52, 0.34), (0, 0.45, 0.73), "fieldstone", rotation=(0, 0, 90))
    for y in (-0.3, 0.05, 0.75):
        for x in (-0.36, 0.36):
            b.box((0.1, 0.12, 0.66), (x, y, 0.39), "fieldstone", bevel=0.01)                              # buttresses
            b.box((0.03, 0.12, 0.2), (x * 0.92, y + 0.17, 0.42), "window", bevel=0.004)
    b.box((0.5, 0.5, 1.2), (0, -0.78, 0.66), "worn_stone", bevel=0.02)                                   # tower
    for x in (-0.22, 0.22):
        for y in (-0.22, 0.22):
            b.box((0.1, 0.1, 0.14), (x, -0.78 + y, 1.32), "worn_stone", bevel=0.01)                      # pinnacles
    b.cylinder(0.3, 0.9, (0, -0.78, 1.7), "fieldstone", segments=8, radius_top=0.0)                      # spire
    b.sphere(0.04, (0, -0.78, 2.17), "gold_trim", segments=6, rings=4)
    b.cylinder(0.12, 0.03, (0, -1.04, 0.85), "window", segments=10, rotation=(90, 0, 0))                 # rose window
    b.box((0.2, 0.04, 0.34), (0, -1.04, 0.2), "tarred_wood", bevel=0.01)                                 # door
    b.box((0.04, 0.02, 0.18), (0.08, -1.06, 1.05), "gold_trim", bevel=0.003)                             # a bell slot's glint
    b.build()
    return church
