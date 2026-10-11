"""A plank walk tile: four boards on the mud, one of them slightly warped (medieval carpentry)."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 300
REQUIRED_PARTS = ("Prop",)


def build():
    tile = root("road_planks")
    part = Part("Prop", parent=tile).box((0.98, 0.98, 0.03), (0, 0, 0.015), "wet_mud", bevel=0.0)
    for i, (swatch, tilt) in enumerate((("fresh_log", 0), ("old_wood", 1.5), ("fresh_log", 0), ("old_wood", -1))):
        part.box((0.96, 0.22, 0.05), (0, -0.36 + i * 0.24, 0.055), swatch, bevel=0.01, rotation=(tilt, 0, 0))
    part.build()
    return tile
