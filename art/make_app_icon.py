"""Composes the app icon from the Blender render (./art/build.sh --brand app_icon): a woad field with the Hearth's
glow and the cult's sun rays behind the hooded duck. Writes Android adaptive layers and a full legacy icon into
the Unity project (Assets/Branding), and a preview into art/branding. Run with python3 (needs Pillow)."""
import math
import os

from PIL import Image, ImageDraw, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "..", "client", "CultOfComfort.Unity", "Assets", "Branding")
WOAD_DEEP, WOAD = (22, 32, 56), (52, 78, 128)
EMBER, FIRE = (200, 84, 44), (255, 196, 110)


def background(size):
    img = Image.new("RGB", (size, size), WOAD_DEEP)
    px = img.load()
    c = size / 2
    for y in range(size):
        for x in range(size):
            d = math.hypot(x - c, y - c * 1.05) / (size * 0.72)
            t = max(0.0, min(1.0, d))
            # fire at the heart, woad at the rim
            if t < 0.35:
                k = t / 0.35
                col = tuple(int(FIRE[i] + (EMBER[i] - FIRE[i]) * k) for i in range(3))
            else:
                k = min(1.0, (t - 0.35) / 0.65)
                col = tuple(int(EMBER[i] + (WOAD[i] - EMBER[i]) * min(1.0, k * 1.6) + (WOAD_DEEP[i] - WOAD[i]) * max(0.0, k * 1.6 - 0.6) / 1.0) for i in range(3))
                col = tuple(max(0, min(255, v)) for v in col)
            px[x, y] = col
    # The cult's sign: soft sun rays fanning out from the glow.
    rays = Image.new("L", (size, size), 0)
    draw = ImageDraw.Draw(rays)
    for k in range(16):
        a0 = 2 * math.pi * k / 16
        a1 = a0 + math.pi / 32
        r = size
        draw.polygon([(c, c), (c + r * math.cos(a0), c + r * math.sin(a0)), (c + r * math.cos(a1), c + r * math.sin(a1))], fill=36)
    rays = rays.filter(ImageFilter.GaussianBlur(size / 90))
    img = Image.composite(Image.new("RGB", (size, size), FIRE), img, rays)
    return img


def duck(size, fill):
    """The render, trimmed and scaled so it fills `fill` of the square, a touch below centre, with a soft shadow."""
    render = Image.open(os.path.join(HERE, "branding", "app_icon_render.png")).convert("RGBA")
    render = render.crop(render.getbbox())
    scale = fill * size / max(render.size)
    render = render.resize((int(render.width * scale), int(render.height * scale)), Image.LANCZOS)
    layer = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    at = ((size - render.width) // 2, int((size - render.height) / 2 + size * 0.03))
    shadow = Image.new("RGBA", render.size, (10, 12, 24, 0))
    shadow.putalpha(render.getchannel("A").point(lambda a: int(a * 0.55)))
    shadow = shadow.filter(ImageFilter.GaussianBlur(size / 60))
    layer.alpha_composite(shadow, (at[0] + size // 80, at[1] + size // 50))
    layer.alpha_composite(render, at)
    return layer


def main():
    os.makedirs(OUT, exist_ok=True)
    # Adaptive: 432 px layers; Android shows the middle 66 %, so the duck fills ~60 % of the foreground.
    background(432).save(os.path.join(OUT, "app_icon_background.png"))
    duck(432, 0.6).save(os.path.join(OUT, "app_icon_foreground.png"))
    legacy = background(1024).convert("RGBA")
    legacy.alpha_composite(duck(1024, 0.84))
    legacy.save(os.path.join(OUT, "app_icon.png"))
    legacy.resize((512, 512), Image.LANCZOS).save(os.path.join(HERE, "branding", "app_icon.png"))
    print("[art] app icon →", os.path.abspath(OUT))


if __name__ == "__main__":
    main()
