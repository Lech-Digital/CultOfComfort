"""Build-time guards from the spec: triangle budgets, palette-only colours, required named parts."""
import palette


class AssetCheckError(Exception):
    pass


def _meshes(root):
    return [o for o in [root, *root.children_recursive] if o.type == "MESH"]


def triangles(root):
    return sum(len(p.vertices) - 2 for o in _meshes(root) for p in o.data.polygons)


def off_palette(root):
    bad = []
    for obj in _meshes(root):
        layer = obj.data.uv_layers.active
        if layer is None or any(palette.swatch_at(d.uv[0], d.uv[1]) is None for d in layer.data):
            bad.append(obj.name)
    return bad


def missing_parts(root, required):
    names = {o.name for o in [root, *root.children_recursive]}
    return [name for name in required if name not in names]


def verify(root, budget, required):
    problems = []
    count = triangles(root)
    if count > budget:
        problems.append(f"{count} triangles > budget {budget}")
    off = off_palette(root)
    if off:
        problems.append(f"colours outside the palette on: {', '.join(off)}")
    missing = missing_parts(root, required)
    if missing:
        problems.append(f"missing parts: {', '.join(missing)}")
    if problems:
        raise AssetCheckError(f"{root.name}: " + "; ".join(problems))
