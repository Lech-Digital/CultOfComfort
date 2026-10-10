"""A signpost with two arrow boards (one points to the baths; the other has been graffitied with a duck)."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 300
REQUIRED_PARTS = ("Prop",)


def build():
    sign = root("signpost")
    (Part("Prop", parent=sign)
     .box((0.06, 0.06, 1.1), (0, 0, 0.55), "old_wood", bevel=0.01, rotation=(2, 0, 0))
     .box((0.5, 0.03, 0.14), (0.16, 0, 0.92), "fresh_log", bevel=0.01, rotation=(0, 0, 8))
     .prism((0.14, 0.03, 0.14), (0.44, 0, 0.94), "fresh_log", rotation=(0, -90, 8))
     .box((0.45, 0.03, 0.13), (-0.14, 0.01, 0.72), "old_wood", bevel=0.01, rotation=(0, 0, -12))
     .sphere(0.035, (-0.14, -0.02, 0.73), "hivis_yellow", segments=6, rings=4, smooth=True)                    # duck graffiti
     .build())
    return sign
