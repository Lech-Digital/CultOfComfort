"""Relic: a wind-up dynamo torch, the only light in the Marchlands that does not need fire, oil or a candle. It
needs cranking. Somebody is always cranking. `Crank` turns; `Light` glows."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 400
REQUIRED_PARTS = ("Prop", "Crank", "Light")


def build():
    relic = root("relic_dynamo_torch")
    body = (Part("Prop", parent=relic)
            .box((0.08, 0.2, 0.07), (0, 0, 0.04), "hivis_yellow", bevel=0.02, bevel_segments=2)
            .cylinder(0.045, 0.04, (0, -0.115, 0.045), "umbrella_black", segments=10, rotation=(90, 0, 0))
            .build())
    Part("Light", pivot=(0, -0.14, 0.045), parent=body).cylinder(0.035, 0.012, (0, -0.138, 0.045), "window", segments=10, rotation=(90, 0, 0)).build()
    (Part("Crank", pivot=(0.045, 0.03, 0.04), parent=body)
     .box((0.012, 0.012, 0.07), (0.05, 0.03, 0.07), "umbrella_black", bevel=0.003)
     .cylinder(0.012, 0.03, (0.06, 0.03, 0.105), "umbrella_black", segments=6, rotation=(0, 90, 0))
     .build())
    return relic
