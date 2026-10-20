"""A mountain ridge: a row of faceted peaks of different heights, snow on the tall ones, scree at the foot. The edge of
the world, or at least of the Marchlands. Several of these in a line make a range."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 1800
REQUIRED_PARTS = ("Prop",)

# (x, y, base radius, height, swatch, snow?)
PEAKS = (
    (-2.6, 0.2, 1.5, 3.2, "fieldstone", True),
    (-1.0, -0.3, 1.9, 4.6, "worn_stone", True),
    (0.8, 0.25, 1.7, 3.9, "fieldstone", True),
    (2.4, -0.2, 1.4, 2.7, "worn_stone", False),
    (3.6, 0.3, 1.0, 1.8, "fieldstone", False),
    (-3.8, -0.1, 0.9, 1.6, "worn_stone", False),
)


def build():
    ridge = root("peak_ridge")
    p = Part("Prop", parent=ridge)
    for k, (x, y, r, h, swatch, snow) in enumerate(PEAKS):
        twist = 13 * k
        p.cylinder(r, h, (x, y, h / 2), swatch, segments=6, radius_top=0.1, rotation=(2 - k % 3, k % 2 * 3 - 1, twist))
        if snow:
            cap = h * 0.3
            p.cylinder(r * 0.33, cap, (x, y, h - cap / 2 + 0.02), "linen", segments=6, radius_top=0.02, rotation=(2 - k % 3, k % 2 * 3 - 1, twist))
    for x in (-3.0, -0.2, 1.7, 3.2):
        p.sphere(0.6, (x, -1.2, 0.15), "worn_stone", segments=6, rings=4, scale=(1.4, 0.9, 0.45))                     # scree
    p.build()
    return ridge
