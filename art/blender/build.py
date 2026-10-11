"""Blender-side build driver. Run through ./art/build.sh, which passes arguments after `--`."""
import argparse
import importlib
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)

import palette  # noqa: E402
from lib import checks, export, scene  # noqa: E402
from lib import preview as previews  # noqa: E402

PREVIEW_DIR = os.path.abspath(os.path.join(HERE, "..", "previews"))
MODEL_DIR = os.path.join(palette.UNITY_ART_DIR, "Models")


def build_asset(name, preview=True):
    module = importlib.import_module(f"models.{name}")
    scene.reset()
    root = module.build()
    checks.verify(root, module.TRIANGLE_BUDGET, module.REQUIRED_PARTS)
    result = {"fbx": os.path.join(MODEL_DIR, f"{name}.fbx"), "triangles": checks.triangles(root)}
    export.export_fbx(root, result["fbx"])
    if preview:
        result["day"] = os.path.join(PREVIEW_DIR, f"{name}_day.png")
        result["night"] = os.path.join(PREVIEW_DIR, f"{name}_night.png")
        previews.render(root, result["day"], result["night"])
    print(f"[art] {name}: {result['triangles']} triangles → {result['fbx']}")
    return result


def _all_models():
    folder = os.path.join(HERE, "models")
    return sorted(f[:-3] for f in os.listdir(folder) if f.endswith(".py") and not f.startswith("_"))


def main(argv):
    parser = argparse.ArgumentParser(prog="art/build.sh")
    parser.add_argument("names", nargs="*")
    parser.add_argument("--all", action="store_true")
    parser.add_argument("--no-preview", action="store_true")
    args = parser.parse_args(argv)
    names = _all_models() if args.all else args.names
    if not names:
        parser.error("name an asset (see art/blender/models) or pass --all")
    for name in names:
        build_asset(name, preview=not args.no_preview)


if __name__ == "__main__":
    main(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else [])
