"""Relic: the hi-vis vest from the boot (legally required, 2024). Worn by whoever the Prophet trusts most this week.
Reflective stripes; reflects torchlight back at the raiders, who find this upsetting."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 300
REQUIRED_PARTS = ("Prop",)


def build():
    relic = root("relic_hivis_vest")
    (Part("Prop", parent=relic)
     .box((0.3, 0.04, 0.34), (0, 0, 0.17), "hivis_yellow", bevel=0.02)
     .box((0.31, 0.045, 0.03), (0, 0, 0.1), "chrome", bevel=0.004)
     .box((0.31, 0.045, 0.03), (0, 0, 0.17), "chrome", bevel=0.004)
     .box((0.1, 0.046, 0.12), (0, 0, 0.3), "umbrella_black", bevel=0.01)                     # neck hole
     .build())
    return relic
