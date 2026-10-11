import os

import bpy


def export_fbx(root, path):
    """Exports `root` and its children for Unity: metres, Blender axes (Unity converts on import), flat normals."""
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    for obj in [root, *root.children_recursive]:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        object_types={"EMPTY", "MESH"},
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        # Write Blender's own axes (Z up) and let Unity convert the whole file on import (bakeAxisConversion in
        # ArtImportSettings). Converting here either puts a rotation on the root or, with bake_space_transform,
        # skips child positions; both break part animation.
        axis_forward="Y",
        axis_up="Z",
        bake_space_transform=False,
        mesh_smooth_type="FACE",
        add_leaf_bones=False,
        bake_anim=False,
        path_mode="STRIP",
    )
