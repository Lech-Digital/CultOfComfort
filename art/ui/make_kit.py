"""HUD kit textures (spec: HUD Kit & Game Frame). Deterministic: every texture uses its own seeded RNG and Pillow
writes no timestamps, so the PNGs are byte-for-byte reproducible.  Usage: python3 art/ui/make_kit.py [out_dir]"""
import math
import os
import random
import sys

from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
UI_DIR = os.path.join(REPO, "client", "CultOfComfort.Unity", "Assets", "Resources", "UI")
KIT_DIR = os.path.join(UI_DIR, "Kit")
STAMP_FONT = os.path.join(UI_DIR, "Fonts", "ImFellEnglishSC.ttf")

NAMES = ("parchment", "parchment_dark", "note_yellow", "note_pink", "note_green", "note_blue", "tape_a", "tape_b",
         "hazard", "scribble_underline", "scribble_circle", "padlock", "stamp_verified", "stamp_mismatch")


def _rounded_mask(size, radius):
    mask = Image.new("L", size, 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, size[0] - 1, size[1] - 1), radius, fill=255)
    return mask


def _sheet(seed, base, edge, rule, size=256, inset=14):
    """Paper: base colour darkening towards the edges, fibres, and a thin ink rule inset from the border."""
    rng = random.Random(seed)
    w = h = size
    im = Image.new("RGB", (w, h), base)
    px = im.load()
    for y in range(h):
        for x in range(w):
            d = min(x, y, w - 1 - x, h - 1 - y) / (size * 0.18)
            t = max(0.0, 1.0 - min(1.0, d)) ** 2
            px[x, y] = tuple(int(base[i] + (edge[i] - base[i]) * t) for i in range(3))
    fibres = ImageDraw.Draw(im, "RGBA")
    for _ in range(260):
        x, y = rng.uniform(0, w), rng.uniform(0, h)
        a, length = rng.uniform(0, math.pi), rng.uniform(3, 11)
        tone = rng.choice((-1, 1)) * rng.randint(8, 18)
        colour = tuple(max(0, min(255, c + tone)) for c in base) + (90,)
        fibres.line((x, y, x + math.cos(a) * length, y + math.sin(a) * length), fill=colour, width=1)
    ink = ImageDraw.Draw(im)
    ink.rectangle((inset, inset, w - 1 - inset, h - 1 - inset), outline=rule, width=2)
    ink.rectangle((inset + 5, inset + 5, w - 1 - inset - 5, h - 1 - inset - 5), outline=rule, width=1)
    out = im.convert("RGBA")
    out.putalpha(_rounded_mask((w, h), 12))
    return out


def _note(colour, seed):
    """A sticky note with a soft baked shadow and a slightly darker, curling bottom edge."""
    rng = random.Random(seed)
    size, pad = 128, 8
    shadow = Image.new("L", (size + pad * 2, size + pad * 2), 0)
    ImageDraw.Draw(shadow).rectangle((pad + 3, pad + 5, pad + size + 2, pad + size + 4), fill=110)
    shadow = shadow.filter(ImageFilter.GaussianBlur(3))
    im = Image.new("RGBA", shadow.size, (0, 0, 0, 0))
    im.putalpha(shadow)
    paper = Image.new("RGB", (size, size), colour)
    draw = ImageDraw.Draw(paper)
    for y in range(size):
        k = 1.06 - 0.1 * y / size - (0.08 if y > size - 12 else 0)
        draw.line((0, y, size, y), fill=tuple(min(255, int(c * k)) for c in colour))
    for _ in range(40):
        x, y = rng.randrange(size), rng.randrange(size)
        draw.point((x, y), fill=tuple(max(0, c - 12) for c in colour))
    im.paste(paper, (pad, pad))
    return im


def _tape(seed):
    rng = random.Random(seed)
    w, h = 128, 40
    im = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    left = [(rng.uniform(0, 6), y) for y in range(0, h + 1, 5)]
    right = [(w - rng.uniform(0, 6), y) for y in range(h, -1, -5)]
    ImageDraw.Draw(im).polygon(left + right, fill=(236, 230, 208, 175))
    return im


def _hazard(size=64, band=16):
    im = Image.new("RGB", (size, size))
    px = im.load()
    for y in range(size):
        for x in range(size):
            px[x, y] = (242, 194, 48) if ((x + y) // band) % 2 == 0 else (34, 34, 34)
    return im.convert("RGBA")


def _stroke(points, size, width):
    im = Image.new("RGBA", size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(im)
    draw.line(points, fill=(255, 255, 255, 235), width=width, joint="curve")
    for x, y in (points[0], points[-1]):
        r = width / 2
        draw.ellipse((x - r, y - r, x + r, y + r), fill=(255, 255, 255, 235))
    return im


def _underline(seed):
    rng = random.Random(seed)
    pts = [(10 + x * 2.3, 22 + math.sin(x / 9) * 4 + rng.uniform(-1, 1)) for x in range(103)]
    return _stroke(pts, (256, 40), 7)


def _circle(seed):
    rng = random.Random(seed)
    pts = []
    for k in range(80):
        a = -0.4 + k / 79 * (2 * math.pi + 0.7)
        r = 52 + rng.uniform(-2, 2) + k * 0.05
        pts.append((64 + math.cos(a) * r * 1.05, 64 + math.sin(a) * r * 0.9))
    return _stroke(pts, (128, 128), 6)


def _padlock():
    im = Image.new("RGBA", (64, 64), (0, 0, 0, 0))
    draw = ImageDraw.Draw(im)
    draw.arc((18, 6, 46, 38), 180, 360, fill=(60, 60, 64, 255), width=6)
    draw.line((18, 22, 18, 30), fill=(60, 60, 64, 255), width=6)
    draw.line((46, 22, 46, 30), fill=(60, 60, 64, 255), width=6)
    draw.rounded_rectangle((10, 28, 54, 58), 6, fill=(217, 180, 90, 255), outline=(90, 60, 20, 255), width=3)
    draw.ellipse((28, 37, 36, 45), fill=(60, 40, 15, 255))
    draw.rectangle((30, 43, 34, 51), fill=(60, 40, 15, 255))
    return im


def _stamp(text, colour, seed):
    rng = random.Random(seed)
    w, h = 300, 110
    im = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    draw = ImageDraw.Draw(im)
    draw.rounded_rectangle((6, 6, w - 7, h - 7), 10, outline=colour + (255,), width=6)
    draw.rounded_rectangle((16, 16, w - 17, h - 17), 6, outline=colour + (255,), width=2)
    size = 52
    font = ImageFont.truetype(STAMP_FONT, size)
    box = draw.textbbox((0, 0), text, font=font)
    while box[2] - box[0] > w - 56:  # shrink until the word sits inside the inner border
        size -= 2
        font = ImageFont.truetype(STAMP_FONT, size)
        box = draw.textbbox((0, 0), text, font=font)
    draw.text(((w - (box[2] - box[0])) / 2 - box[0], (h - (box[3] - box[1])) / 2 - box[1]), text, font=font, fill=colour + (255,))
    wear = Image.new("L", (w, h), 255)
    specks = ImageDraw.Draw(wear)
    for _ in range(420):
        x, y, r = rng.uniform(0, w), rng.uniform(0, h), rng.uniform(0.6, 2.2)
        specks.ellipse((x - r, y - r, x + r, y + r), fill=0)
    im.putalpha(ImageChops.multiply(im.getchannel("A"), wear))
    return im.rotate(-7, resample=Image.BICUBIC, expand=True)


def _build():
    return {
        "parchment": _sheet(1, (241, 226, 191), (205, 175, 120), (138, 59, 34)),
        "parchment_dark": _sheet(2, (92, 66, 40), (52, 36, 22), (201, 162, 74), size=192, inset=10),
        "note_yellow": _note((255, 224, 102), 3),
        "note_pink": _note((255, 159, 196), 4),
        "note_green": _note((182, 240, 138), 5),
        "note_blue": _note((159, 216, 255), 6),
        "tape_a": _tape(7),
        "tape_b": _tape(8),
        "hazard": _hazard(),
        "scribble_underline": _underline(9),
        "scribble_circle": _circle(10),
        "padlock": _padlock(),
        "stamp_verified": _stamp("VERIFIED", (47, 122, 58), 11),
        "stamp_mismatch": _stamp("MISMATCH", (168, 50, 42), 12),
    }


def write_kit(out_dir=KIT_DIR):
    os.makedirs(out_dir, exist_ok=True)
    paths = []
    for name, image in _build().items():
        path = os.path.join(out_dir, name + ".png")
        image.save(path, optimize=False)
        paths.append(path)
    return paths


if __name__ == "__main__":
    for p in write_kit(sys.argv[1] if len(sys.argv) > 1 else KIT_DIR):
        print(p)
