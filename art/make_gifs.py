"""Turns frames rendered by Unity's AnimationShowcase (art/previews/anim/<model>_<clip>/*.png) into <model>_<clip>.gif.

Only folders named after a model in art/blender/models are converted, so stale scratch folders are ignored.
"""
import os
import sys

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ANIM = os.path.join(HERE, "previews", "anim")
MODELS = {f[:-3] for f in os.listdir(os.path.join(HERE, "blender", "models")) if f.endswith(".py") and not f.startswith("_")}
FRAME_MS = 50  # 20 fps, as rendered


def main():
    for clip in sorted(os.listdir(ANIM)):
        folder = os.path.join(ANIM, clip)
        if not os.path.isdir(folder) or not any(clip.startswith(model + "_") for model in MODELS):
            continue
        frames = [Image.open(os.path.join(folder, f)).convert("P", palette=Image.ADAPTIVE, colors=128)
                  for f in sorted(os.listdir(folder)) if f.endswith(".png")]
        if not frames:
            print(f"skipping empty {folder}")
            continue
        out = os.path.join(ANIM, f"{clip}.gif")
        frames[0].save(out, save_all=True, append_images=frames[1:], duration=FRAME_MS, loop=0, optimize=True)
        print(f"{out} ({len(frames)} frames)")


if __name__ == "__main__":
    if not os.path.isdir(ANIM):
        sys.exit("No frames yet: run Unity's AnimationShowcase (see its class comment) first.")
    main()
