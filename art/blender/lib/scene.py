import bpy

import palette


def reset():
    """Empty scene with the current palette texture on disk (the material loads it)."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    palette.write_png(palette.UNITY_PALETTE_PATH)
