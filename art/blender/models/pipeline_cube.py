"""Smoke asset: proves palette → mesh → checks → FBX → Unity works end to end. Not used by the game."""
from lib.mesh import Part, marker, root

TRIANGLE_BUDGET = 300
REQUIRED_PARTS = ("Cube", "Front")


def build():
    cube = root("pipeline_cube")
    (Part("Cube", parent=cube)
     .box((0.8, 0.8, 0.8), (0, 0, 0.4), "worn_stone")
     .cylinder(0.12, 0.3, (0, 0, 0.95), "cone_orange", radius_top=0.0)
     .build())
    marker("Front", (0, -0.6, 0.4), cube)
    return cube
