"""Turns frames rendered by Unity's AnimationShowcase (art/previews/anim/<clip>/*.png) into GIFs next to them."""
import os
import sys

from PIL import Image

ANIM = os.path.join(os.path.dirname(os.path.abspath(__file__)), "previews", "anim")
FRAME_MS = 50  # 20 fps, as rendered


def main(prefix):
    for clip in sorted(os.listdir(ANIM)):
        folder = os.path.join(ANIM, clip)
        if not os.path.isdir(folder):
            continue
        frames = [Image.open(os.path.join(folder, f)).convert("P", palette=Image.ADAPTIVE, colors=128)
                  for f in sorted(os.listdir(folder)) if f.endswith(".png")]
        out = os.path.join(ANIM, f"{prefix}_{clip}.gif")
        frames[0].save(out, save_all=True, append_images=frames[1:], duration=FRAME_MS, loop=0, optimize=True)
        print(f"{out} ({len(frames)} frames)")


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else "prophet")
