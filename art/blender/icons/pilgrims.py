"""The Pilgrims' Way: a walking staff with a gourd tied on and a scallop shell."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 400


def build():
    icon = root("icon_pilgrims")
    part = Part("Prop", parent=icon)
    part.cylinder(0.018, 0.5, (0, 0, 0.02), "old_wood", segments=6, rotation=(0, 70, 25))                     # the staff
    part.sphere(0.06, (0.05, 0.03, 0.05), "ochre_clay", segments=8, rings=5, scale=(1, 1, 1.25))           # gourd
    part.sphere(0.035, (0.05, 0.03, 0.12), "ochre_clay", segments=6, rings=4)
    part.sphere(0.08, (-0.12, -0.04, 0.03), "linen", segments=8, rings=4, scale=(1.2, 1.0, 0.35))          # scallop shell
    for k in range(4):
        part.box((0.008, 0.08, 0.01), (-0.12 + (k - 1.5) * 0.03, -0.04, 0.06), "daub", bevel=0, rotation=(0, 0, (k - 1.5) * 12))
    part.build()
    return icon
