"""A rubber duck (2024). The cult believes it is a minor deity of bathing. It is."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 300
REQUIRED_PARTS = ("Prop",)


def build():
    duck = root("rubber_duck")
    (Part("Prop", parent=duck)
     .sphere(0.12, (0, 0.02, 0.1), "hivis_yellow", segments=9, rings=6, scale=(1.0, 1.3, 0.8), smooth=True)
     .sphere(0.075, (0, -0.1, 0.22), "hivis_yellow", segments=8, rings=6, smooth=True)
     .cylinder(0.03, 0.06, (0, -0.18, 0.21), "cone_orange", segments=8, radius_top=0.02, rotation=(90, 0, 0))
     .sphere(0.014, (0.04, -0.16, 0.25), "umbrella_black", segments=6, rings=4, smooth=True)
     .sphere(0.014, (-0.04, -0.16, 0.25), "umbrella_black", segments=6, rings=4, smooth=True)
     .build())
    return duck
