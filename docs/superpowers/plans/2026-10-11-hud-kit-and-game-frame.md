# HUD Kit & Game Frame Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restyle the whole interface as layout C on parchment (Manuscript + Junk) with a full game frame, locked-feature sticky notes and demo-marked placeholder data.

**Architecture:** Three asset sources feed one UI: a Pillow script writes the kit textures, the Blender pipeline renders icons from models, and Google Fonts supplies three typefaces; all land in `Assets/Resources/UI/`. `HudView` (UI Toolkit, built in code) is rebuilt around a frame + strip + side columns + bottom bar, styled entirely by `Hud.uss`; two small pure-C# types (`LockedFeatures`, `DemoEconomy`/`FrameState`) feed it. `GameController` only changes how it calls the HUD.

**Tech Stack:** Unity 6.3 LTS (6000.3.26f1) UI Toolkit, C# 9; Python 3 + Pillow; Blender 4.5 LTS (bpy, EEVEE); NUnit (EditMode/PlayMode), Python unittest.

**Spec:** `docs/superpowers/specs/2026-10-11-hud-kit-and-game-frame-design.md`

## Global Constraints

- Simulation untouched: demo determinism hash stays `12c3fe1f0e23fb59`; all 56 .NET tests stay green.
- UI stays code-first: `HudView` builds the tree, `Resources/UI/Hud.uss` styles it; no UXML, no new packages.
- Every kit texture, icon and font is generated or downloaded by a committed script/command — reproducible.
- Icons: 128 px PNG, transparent background, built from palette-only Blender models within the asset's triangle budget.
- Fonts: IM Fell English, IM Fell English SC (OFL), Permanent Marker (Apache 2.0), each with its licence file next to it.
- Demo values carry a visible "demo" mark; Scrap and all Night values are real.
- 60 fps on the Pixel 10 in Dusk and a busy Night.
- Commits as adzius.lech@gmail.com (repo config), **no** `Co-Authored-By` or "Generated with" lines.

## Review Focus

1. A long announcement or rejection reason (e.g. 200 characters) must wrap inside the screen, not run off it — test in Task 5.
2. Tapping a second locked button while a note is showing replaces the note (one note, the newest text) — test in Task 4.
3. Taps on an open Settings sheet or a locked note must not reach the board (no accidental building) — `IsOverUi` test in Task 5.
4. Dawn → Back to Dusk must bring back the frame, Tension strip and shortcuts (full phase cycle) — test in Task 5.
5. Meters fed 0 or a negative Hearth value show an empty bar, never a negative width — test in Task 5.

---

## File Structure

| File | Responsibility |
|---|---|
| `art/ui/make_kit.py` (new) | Pillow generator: every kit texture, deterministic, `write_kit(out_dir)` |
| `art/ui/test_make_kit.py` (new) | Python unittest for the generator |
| `art/blender/lib/icon.py` (new) | Transparent ortho icon render of a built model (optionally framed on one part) |
| `art/blender/icons/*.py` (new, 18) | One icon model per file: `TRIANGLE_BUDGET`, `build()`, optional `FOCUS` |
| `art/blender/build.py` (modify) | `ICON_DIR`, `build_icon(name)`, `--icons` flag |
| `art/blender/tests/test_icons.py` (new) | Blender test: every icon builds in budget and renders transparent 128 px |
| `client/.../Assets/Resources/UI/Fonts/*` (new) | Three TTFs + `OFL.txt` + `LICENSE-PermanentMarker.txt` |
| `client/.../Assets/Resources/UI/Kit/*.png`, `UI/Icons/*.png` (generated) | Kit and icons |
| `client/.../Assets/Editor/ArtImportSettings.cs` (modify) | UI textures: bilinear, no mips, uncompressed, alpha is transparency |
| `client/.../Assets/Scripts/Hud/UiAssets.cs` (new) | The canonical name lists of kit textures, icons and fonts |
| `client/.../Assets/Scripts/Hud/LockedFeatures.cs` (new) | The six locked buttons and their notes |
| `client/.../Assets/Scripts/Hud/FrameState.cs` (new) | `Reading`, `FrameState`, `DemoEconomy` |
| `client/.../Assets/Scripts/Hud/HudView.cs` (rewrite) | The new tree, frame/strip/sides/bar/notes/sheet/card |
| `client/.../Assets/Resources/UI/Hud.uss` (rewrite) | All styling |
| `client/.../Assets/Scripts/GameController.cs` (modify) | Feeds `FrameState`, new `NightHudState` fields, prophet quips |
| `client/.../Assets/Tests/EditMode/UiAssetsTests.cs`, `HudModelTests.cs` (new) | Asset loading; locked/demo model |
| `client/.../Assets/Tests/PlayMode/HudFrameTests.cs` (new), `GameSmokeTests.cs` (modify) | HUD behaviour |

Paths below abbreviate `client/CultOfComfort.Unity/Assets` as `Assets`. Commands run from the repo root.
`UNITY=~/Unity/Hub/Editor/6000.3.26f1/Unity.app/Contents/MacOS/Unity`. Unity test runs:
`"$UNITY" -batchmode -projectPath client/CultOfComfort.Unity -runTests -testPlatform EditMode|PlayMode [-testFilter X] -testResults <file> -logFile <file>`
then read `result=… total=… passed=… failed=…` from the XML. The editor must not be open on the project.

---

### Task 1: Fonts and UI import rules

**Files:**
- Create: `Assets/Resources/UI/Fonts/ImFellEnglish.ttf`, `ImFellEnglishSC.ttf`, `PermanentMarker.ttf`, `OFL.txt`, `LICENSE-PermanentMarker.txt`
- Create: `Assets/Scripts/Hud/UiAssets.cs`
- Modify: `Assets/Editor/ArtImportSettings.cs`
- Test: `Assets/Tests/EditMode/UiAssetsTests.cs`

**Interfaces:**
- Produces: `UiAssets.Fonts` (`string[]` of Resources paths `"UI/Fonts/ImFellEnglish"`, `"UI/Fonts/ImFellEnglishSC"`, `"UI/Fonts/PermanentMarker"`), `UiAssets.KitTextures`, `UiAssets.Icons` (filled in Tasks 2–3; declared now as the final lists).

- [ ] **Step 1: Write the failing test**

```csharp
// Assets/Tests/EditMode/UiAssetsTests.cs
using NUnit.Framework;
using UnityEngine;

namespace CultOfComfort.Client.Tests
{
    public class UiAssetsTests
    {
        [Test]
        public void Fonts_EveryListedFont_LoadsFromResources()
        {
            foreach (var path in UiAssets.Fonts)
            {
                // Act
                var font = Resources.Load<Font>(path);

                // Assert
                Assert.IsNotNull(font, path);
            }
        }
    }
}
```

- [ ] **Step 2: Run it to verify it fails**

Run: EditMode with `-testFilter UiAssetsTests`. Expected: compile error / FAIL — `UiAssets` does not exist.

- [ ] **Step 3: Add the names and the fonts**

```csharp
// Assets/Scripts/Hud/UiAssets.cs
namespace CultOfComfort.Client
{
    /// <summary>The canonical lists of HUD assets under Resources/UI (generated by art/ui/make_kit.py and art/build.sh --icons).</summary>
    public static class UiAssets
    {
        public static readonly string[] Fonts = { "UI/Fonts/ImFellEnglish", "UI/Fonts/ImFellEnglishSC", "UI/Fonts/PermanentMarker" };

        public static readonly string[] KitTextures =
        {
            "parchment", "parchment_dark", "note_yellow", "note_pink", "note_green", "note_blue", "tape_a", "tape_b",
            "hazard", "scribble_underline", "scribble_circle", "padlock", "stamp_verified", "stamp_mismatch",
        };

        public static readonly string[] Icons =
        {
            "timber", "scrap", "peat", "devotion", "penny", "comfort", "sluice", "voice", "stones", "speed",
            "map", "research", "missionaries", "tithes", "shop", "cult", "settings", "portrait_prophet",
        };
    }
}
```

```bash
F=client/CultOfComfort.Unity/Assets/Resources/UI/Fonts; mkdir -p $F
R=https://raw.githubusercontent.com/google/fonts/main
curl -fsSL $R/ofl/imfellenglish/IMFeENrm28P.ttf -o $F/ImFellEnglish.ttf
curl -fsSL $R/ofl/imfellenglishsc/IMFeENsc28P.ttf -o $F/ImFellEnglishSC.ttf
curl -fsSL $R/ofl/imfellenglish/OFL.txt -o $F/OFL.txt
curl -fsSL $R/apache/permanentmarker/PermanentMarker-Regular.ttf -o $F/PermanentMarker.ttf
curl -fsSL $R/apache/permanentmarker/LICENSE.txt -o $F/LICENSE-PermanentMarker.txt
file $F/*.ttf   # each: TrueType Font data
```

Import rule for everything under `Resources/UI/` (textures only; fonts keep defaults). Add to `ArtImportSettings.OnPreprocessTexture`, before the palette check:

```csharp
        private const string UiRoot = "Assets/Resources/UI/";

        private void OnPreprocessTexture()
        {
            if (assetPath.StartsWith(UiRoot))
            {
                var ui = (TextureImporter)assetImporter;
                ui.textureType = TextureImporterType.Default;
                ui.filterMode = FilterMode.Bilinear;               // parchment and icons are painterly, not swatches
                ui.mipmapEnabled = false;                          // UI is drawn at ~1:1
                ui.textureCompression = TextureImporterCompression.Uncompressed;
                ui.alphaIsTransparency = true;
                ui.npotScale = TextureImporterNPOTScale.None;
                ui.wrapMode = TextureWrapMode.Clamp;
                ui.sRGBTexture = true;
                return;
            }

            if (assetPath != ArtRoot + "Palette.png")
            ... (existing body unchanged)
```

- [ ] **Step 4: Run it to verify it passes**

Run: EditMode `-testFilter UiAssetsTests`. Expected: PASS 1/1.

- [ ] **Step 5: Commit**

```bash
git add client/CultOfComfort.Unity/Assets/Resources/UI/Fonts client/CultOfComfort.Unity/Assets/Scripts/Hud/UiAssets.cs* \
  client/CultOfComfort.Unity/Assets/Editor/ArtImportSettings.cs client/CultOfComfort.Unity/Assets/Tests/EditMode/UiAssetsTests.cs*
git commit -m "HUD kit: bundle IM Fell English (SC) and Permanent Marker, UI texture import rules"
```

---

### Task 2: Kit texture generator

**Files:**
- Create: `art/ui/make_kit.py`, `art/ui/test_make_kit.py`
- Generate: `Assets/Resources/UI/Kit/*.png`
- Test: `Assets/Tests/EditMode/UiAssetsTests.cs` (add a test)

**Interfaces:**
- Consumes: `Assets/Resources/UI/Fonts/ImFellEnglishSC.ttf` (stamp lettering), `UiAssets.KitTextures`.
- Produces: `make_kit.NAMES` (tuple, same order as `UiAssets.KitTextures`), `make_kit.write_kit(out_dir) -> list[str]` (paths written). 9-slice margins for USS: parchment 32 px, parchment_dark 24 px, notes 16 px.

- [ ] **Step 1: Write the failing Python test**

```python
# art/ui/test_make_kit.py
import os
import tempfile
import unittest

from PIL import Image

import make_kit


class MakeKitTests(unittest.TestCase):
    def test_write_kit_writes_every_texture(self):
        out = tempfile.mkdtemp()
        paths = make_kit.write_kit(out)
        self.assertEqual(sorted(os.path.basename(p)[:-4] for p in paths), sorted(make_kit.NAMES))

    def test_write_kit_twice_is_byte_identical(self):
        a, b = tempfile.mkdtemp(), tempfile.mkdtemp()
        make_kit.write_kit(a)
        make_kit.write_kit(b)
        for name in make_kit.NAMES:
            with open(os.path.join(a, name + ".png"), "rb") as x, open(os.path.join(b, name + ".png"), "rb") as y:
                self.assertEqual(x.read(), y.read(), name)

    def test_parchment_has_transparent_corner_and_opaque_centre(self):
        out = tempfile.mkdtemp()
        make_kit.write_kit(out)
        im = Image.open(os.path.join(out, "parchment.png")).convert("RGBA")
        self.assertEqual(im.getpixel((0, 0))[3], 0)
        self.assertEqual(im.getpixel((im.width // 2, im.height // 2))[3], 255)

    def test_hazard_tiles_seamlessly(self):
        out = tempfile.mkdtemp()
        make_kit.write_kit(out)
        im = Image.open(os.path.join(out, "hazard.png")).convert("RGB")
        for y in range(im.height):
            self.assertEqual(im.getpixel((0, y)), im.getpixel((im.width - 1, (y + 1) % im.height)))


if __name__ == "__main__":
    unittest.main()
```

(The hazard stripes run along x + y, so the last column at row y+1 continues the first column at row y.)

- [ ] **Step 2: Run it to verify it fails**

Run: `python3 -m unittest discover -s art/ui -p "test_*.py"`. Expected: `ModuleNotFoundError: No module named 'make_kit'`.

- [ ] **Step 3: Write the generator**

```python
# art/ui/make_kit.py
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
    font = ImageFont.truetype(STAMP_FONT, 52)
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
```

- [ ] **Step 4: Run it to verify it passes, then generate the kit**

Run: `python3 -m unittest discover -s art/ui -p "test_*.py"`. Expected: `Ran 4 tests … OK`.
Run: `python3 art/ui/make_kit.py`. Expected: 14 paths printed under `Assets/Resources/UI/Kit/`.

- [ ] **Step 5: Unity loads the kit — failing test first**

Add to `UiAssetsTests`:

```csharp
        [Test]
        public void KitTextures_EveryListedTexture_LoadsBilinearWithoutMips()
        {
            foreach (var name in UiAssets.KitTextures)
            {
                // Act
                var texture = Resources.Load<Texture2D>("UI/Kit/" + name);

                // Assert
                Assert.IsNotNull(texture, name);
                Assert.AreEqual(FilterMode.Bilinear, texture.filterMode, name);
                Assert.AreEqual(1, texture.mipmapCount, name);
            }
        }
```

Run EditMode `-testFilter UiAssetsTests` before running `make_kit.py` would fail (missing); since the kit is generated in Step 4, verify RED by temporarily moving `Kit/` aside only if needed — otherwise record in the ledger that the test passed first because the assets already existed (generated output, not code). Expected now: PASS 2/2.

- [ ] **Step 6: Commit**

```bash
git add art/ui client/CultOfComfort.Unity/Assets/Resources/UI/Kit client/CultOfComfort.Unity/Assets/Tests/EditMode/UiAssetsTests.cs
git commit -m "HUD kit: deterministic Pillow generator for parchment, sticky notes, tape, hazard stripes, scribbles, padlock, stamps"
```

---

### Task 3: Icons rendered from Blender models

**Files:**
- Create: `art/blender/lib/icon.py`, `art/blender/icons/__init__.py`, 18 icon modules, `art/blender/tests/test_icons.py`
- Modify: `art/blender/build.py`, `art/build.sh` (usage comment only)
- Generate: `Assets/Resources/UI/Icons/*.png`
- Test: `Assets/Tests/EditMode/UiAssetsTests.cs` (add a test)

**Interfaces:**
- Consumes: `lib.mesh.Part/root`, `lib.checks.verify(root, budget, required)`, `lib.scene.reset()`, model modules' `build()`/`TRIANGLE_BUDGET`, `lib.preview._bounds/_engine/_light/_world`.
- Produces: `build.ICON_DIR`, `build.build_icon(name) -> str` (PNG path), `icon.render_icon(root, path, size=128, focus=None)`; CLI `./art/build.sh --icons [names…]`.

- [ ] **Step 1: Write the failing Blender test**

```python
# art/blender/tests/test_icons.py
import os
import tempfile
import unittest

import bpy

import build

ICONS = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "icons")
EXPECTED = ("timber", "scrap", "peat", "devotion", "penny", "comfort", "sluice", "voice", "stones", "speed",
            "map", "research", "missionaries", "tithes", "shop", "cult", "settings", "portrait_prophet")


class IconTests(unittest.TestCase):
    def setUp(self):
        self._dir = build.ICON_DIR
        build.ICON_DIR = tempfile.mkdtemp()

    def tearDown(self):
        build.ICON_DIR = self._dir

    def test_icon_modules_match_the_hud_list(self):
        names = sorted(f[:-3] for f in os.listdir(ICONS) if f.endswith(".py") and not f.startswith("_"))
        self.assertEqual(names, sorted(EXPECTED))

    def test_build_icon_every_icon_renders_transparent_128px(self):
        for name in EXPECTED:
            with self.subTest(name):
                # Act
                path = build.build_icon(name)

                # Assert
                image = bpy.data.images.load(path)
                self.assertEqual(tuple(image.size), (128, 128))
                pixels = list(image.pixels)
                self.assertEqual(pixels[3], 0.0, "corner must be transparent")
                self.assertGreater(max(pixels[3::4]), 0.99, "something must be drawn")
                bpy.data.images.remove(image)
```

- [ ] **Step 2: Run it to verify it fails**

Run: `./art/build.sh --test 2>&1 | tail -20`. Expected: `test_icons` errors — `build` has no `ICON_DIR` / icons folder missing.

- [ ] **Step 3: Write the renderer and the build entry**

```python
# art/blender/lib/icon.py
"""HUD icons: a model rendered small, three-quarter view, transparent background (spec: HUD Kit, Icons)."""
import math

import bpy
from mathutils import Vector

from lib.preview import _bounds, _engine, _light, _world


def _part_bounds(root, focus):
    part = next(o for o in [root, *root.children_recursive] if o.name.split(".")[0] == focus)
    points = [part.matrix_world @ Vector(c) for c in part.bound_box]
    lo = Vector((min(p.x for p in points), min(p.y for p in points), min(p.z for p in points)))
    hi = Vector((max(p.x for p in points), max(p.y for p in points), max(p.z for p in points)))
    return lo, hi


def render_icon(root, path, size=128, focus=None):
    """Frames the whole model, or one named part plus its surroundings (portraits: focus="Head")."""
    bpy.context.view_layer.update()
    scene = bpy.context.scene
    scene.render.engine = _engine()
    scene.render.resolution_x = scene.render.resolution_y = size
    scene.render.film_transparent = True
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.view_settings.view_transform = "Standard"

    lo, hi = _bounds(root) if focus is None else _part_bounds(root, focus)
    centre = (lo + hi) / 2
    extent = max(hi.x - lo.x, hi.y - lo.y, hi.z - lo.z)
    if focus is not None:
        centre.z -= extent * 0.25   # head and shoulders
        extent *= 1.5

    camera_data = bpy.data.cameras.new("IconCamera")
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = extent * 1.3
    camera = bpy.data.objects.new("IconCamera", camera_data)
    scene.collection.objects.link(camera)
    yaw, pitch = math.radians(25), math.radians(28)
    direction = Vector((math.sin(yaw) * math.cos(pitch), math.cos(yaw) * math.cos(pitch), math.sin(pitch)))
    camera.location = centre + direction * extent * 6
    camera.rotation_euler = (-direction).to_track_quat("-Z", "Y").to_euler()
    scene.camera = camera

    _world((0.62, 0.6, 0.58), 0.9)
    sun = _light("IconSun", "SUN", 3.0, (1.0, 0.9, 0.75), rotation=(55, 0, -50))
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    for obj in (sun, camera):
        bpy.data.objects.remove(obj, do_unlink=True)
```

Add to `art/blender/build.py`:

```python
from lib import icon  # noqa: E402   (next to the other lib imports)

ICON_DIR = os.path.join(os.path.dirname(palette.UNITY_ART_DIR), "UI", "Icons")


def build_icon(name):
    module = importlib.import_module(f"icons.{name}")
    scene.reset()
    root = module.build()
    checks.verify(root, module.TRIANGLE_BUDGET, ())
    os.makedirs(ICON_DIR, exist_ok=True)
    path = os.path.join(ICON_DIR, f"{name}.png")
    icon.render_icon(root, path, focus=getattr(module, "FOCUS", None))
    print(f"[art] icon {name} → {path}")
    return path


def _all_icons():
    folder = os.path.join(HERE, "icons")
    return sorted(f[:-3] for f in os.listdir(folder) if f.endswith(".py") and not f.startswith("_"))
```

and in `main`: `parser.add_argument("--icons", action="store_true")`; before the model branch:

```python
    if args.icons:
        for name in args.names or _all_icons():
            build_icon(name)
        return
```

Add `#   ./art/build.sh --icons [NAME…]    render HUD icons (all if none named)` to the usage comment in `art/build.sh`.

- [ ] **Step 4: Write the 18 icon modules**

`art/blender/icons/__init__.py`: empty. Reusing modules (one import line plus metadata):

```python
# icons/comfort.py
"""Comfort: the rubber duck, minor deity of bathing."""
from models.rubber_duck import TRIANGLE_BUDGET, build  # noqa: F401
```
```python
# icons/stones.py
"""Hot Stone Therapy."""
from models.fx_hot_stone import TRIANGLE_BUDGET, build  # noqa: F401
```
```python
# icons/research.py
"""Research: a hymnal, borrowed indefinitely."""
from models.fx_hymnal import TRIANGLE_BUDGET, build  # noqa: F401
```
```python
# icons/shop.py
"""Shop: a shopping trolley from 2024."""
from models.shopping_trolley import TRIANGLE_BUDGET, build  # noqa: F401
```
```python
# icons/cult.py
"""Your cult: a cultist, framed on the face."""
from models.cultist import TRIANGLE_BUDGET, build  # noqa: F401

FOCUS = "Head"
```
```python
# icons/portrait_prophet.py
"""The Prophet's portrait for the frame."""
from models.prophet import TRIANGLE_BUDGET, build  # noqa: F401

FOCUS = "Head"
```

New small models (authoring space: Z up, front faces −Y; budget 300 each):

```python
# icons/timber.py
"""Timber: two stacked logs with pale cut ends."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 300


def build():
    icon = root("icon_timber")
    part = Part("Prop", parent=icon)
    for x, z in ((-0.11, 0.1), (0.11, 0.1), (0.0, 0.28)):
        part.cylinder(0.1, 0.5, (x, 0, z), "fresh_log", segments=10, rotation=(90, 0, 0))
        part.cylinder(0.085, 0.01, (x, -0.252, z), "daub", segments=10, rotation=(90, 0, 0))
    part.build()
    return icon
```
```python
# icons/scrap.py
"""Scrap: a bent horseshoe and a nail."""
import math

from lib.mesh import Part, root

TRIANGLE_BUDGET = 300


def build():
    icon = root("icon_scrap")
    part = Part("Prop", parent=icon)
    for k in range(9):
        a = math.radians(-20 + k * 220 / 8)
        part.box((0.08, 0.06, 0.07), (math.cos(a) * 0.2, 0, 0.22 + math.sin(a) * 0.2), "mail", bevel=0.01,
                 rotation=(0, -math.degrees(a) + 90, 0))
    part.cylinder(0.012, 0.3, (0.05, -0.06, 0.12), "fieldstone", segments=5, rotation=(0, 70, 0))
    part.build()
    return icon
```
```python
# icons/peat.py
"""Peat: a cut brick of bog."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 300


def build():
    icon = root("icon_peat")
    part = Part("Prop", parent=icon)
    part.box((0.42, 0.26, 0.2), (0, 0, 0.1), "wet_mud", bevel=0.03, rotation=(0, 0, 8))
    part.box((0.36, 0.22, 0.04), (0.02, 0, 0.215), "tarred_wood", bevel=0.01, rotation=(0, 3, 8))
    part.build()
    return icon
```
```python
# icons/devotion.py
"""Devotion: a lit candle on a dish."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 300


def build():
    icon = root("icon_devotion")
    part = Part("Prop", parent=icon)
    part.cylinder(0.16, 0.03, (0, 0, 0.015), "gold_trim", segments=12)
    part.cylinder(0.06, 0.32, (0, 0, 0.19), "linen", segments=10, smooth=True)
    part.cylinder(0.006, 0.04, (0, 0, 0.37), "tarred_wood", segments=4)
    part.sphere(0.04, (0, 0, 0.42), "fire", segments=8, rings=6, scale=(0.8, 0.8, 1.6), smooth=True)
    part.build()
    return icon
```
```python
# icons/penny.py
"""Silver Penny: a coin, face to the viewer."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 300


def build():
    icon = root("icon_penny")
    part = Part("Prop", parent=icon)
    part.cylinder(0.2, 0.04, (0, 0, 0.2), "mail", segments=16, rotation=(90, 0, 0))
    part.cylinder(0.15, 0.01, (0, -0.022, 0.2), "worn_stone", segments=16, rotation=(90, 0, 0))
    part.box((0.03, 0.01, 0.16), (0, -0.03, 0.2), "fieldstone", bevel=0)
    part.box((0.16, 0.01, 0.03), (0, -0.03, 0.2), "fieldstone", bevel=0)
    part.build()
    return icon
```
```python
# icons/sluice.py
"""Open Sluice: a plank gate raised between two posts over water."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 300


def build():
    icon = root("icon_sluice")
    part = Part("Prop", parent=icon)
    part.box((0.5, 0.2, 0.06), (0, 0, 0.03), "water", bevel=0)
    for x in (-0.2, 0.2):
        part.box((0.06, 0.06, 0.5), (x, 0, 0.25), "old_wood", bevel=0.01)
    part.box((0.36, 0.04, 0.2), (0, 0, 0.32), "fresh_log", bevel=0.01)
    part.box((0.5, 0.07, 0.05), (0, 0, 0.52), "old_wood", bevel=0.01)
    part.build()
    return icon
```
```python
# icons/voice.py
"""Phantom Voice: a tin-can telephone."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 300


def build():
    icon = root("icon_voice")
    part = Part("Prop", parent=icon)
    part.cylinder(0.07, 0.14, (-0.18, 0, 0.12), "mail", segments=10, rotation=(0, 90, 0))
    part.cylinder(0.07, 0.14, (0.18, 0, 0.3), "mail", segments=10, rotation=(0, 90, 0))
    part.cylinder(0.006, 0.32, (0, 0, 0.21), "linen", segments=4, rotation=(0, 61, 0))
    part.build()
    return icon
```
```python
# icons/speed.py
"""Speed: a snail, unhurried."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 300


def build():
    icon = root("icon_speed")
    part = Part("Prop", parent=icon)
    part.sphere(0.06, (0, 0, 0.05), "daub", segments=8, rings=5, scale=(4.0, 1.0, 0.8), smooth=True)
    part.sphere(0.13, (0.05, 0, 0.18), "levy_wool", segments=10, rings=7, smooth=True)
    part.sphere(0.07, (0.07, -0.08, 0.19), "thatch", segments=8, rings=5, smooth=True)
    for x in (-0.2, -0.16):
        part.cylinder(0.008, 0.1, (x, 0, 0.13), "daub", segments=4, rotation=(0, -15, 0))
        part.sphere(0.015, (x - 0.01, 0, 0.18), "umbrella_black", segments=5, rings=3)
    part.build()
    return icon
```
```python
# icons/map.py
"""World map: a half-unrolled chart with a red route."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 300


def build():
    icon = root("icon_map")
    part = Part("Prop", parent=icon)
    part.box((0.42, 0.3, 0.01), (0, 0, 0.01), "daub", bevel=0)
    part.cylinder(0.04, 0.32, (0.22, 0, 0.04), "daub", segments=10, rotation=(90, 0, 0))
    part.box((0.22, 0.015, 0.012), (-0.05, 0.02, 0.02), "zealot_red", bevel=0, rotation=(0, 0, 30))
    part.box((0.05, 0.012, 0.012), (-0.15, -0.06, 0.02), "zealot_red", bevel=0, rotation=(0, 0, 45))
    part.box((0.05, 0.012, 0.012), (-0.15, -0.06, 0.02), "zealot_red", bevel=0, rotation=(0, 0, -45))
    part.build()
    return icon
```
```python
# icons/missionaries.py
"""Missionaries: a neon sandal and a walking staff."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 300


def build():
    icon = root("icon_missionaries")
    part = Part("Prop", parent=icon)
    part.box((0.14, 0.34, 0.03), (-0.06, 0, 0.015), "flipflop_pink", bevel=0.01)
    part.box((0.15, 0.03, 0.05), (-0.06, -0.06, 0.05), "tarred_wood", bevel=0.005)
    part.box((0.03, 0.12, 0.04), (-0.06, -0.12, 0.045), "tarred_wood", bevel=0.005)
    part.cylinder(0.018, 0.6, (0.12, 0.04, 0.3), "old_wood", segments=6, rotation=(0, 12, 0))
    part.build()
    return icon
```
```python
# icons/tithes.py
"""Tithes: a coin pouch, tied, with coins spilling out."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 300


def build():
    icon = root("icon_tithes")
    part = Part("Prop", parent=icon)
    part.sphere(0.15, (0, 0, 0.14), "levy_wool", segments=10, rings=7, scale=(1.0, 1.0, 0.9), smooth=True)
    part.cylinder(0.05, 0.08, (0, 0, 0.3), "levy_wool", segments=8, smooth=True)
    part.cylinder(0.056, 0.02, (0, 0, 0.28), "tarred_wood", segments=8)
    for x, y in ((0.18, -0.08), (0.24, 0.02), (0.13, -0.16)):
        part.cylinder(0.04, 0.012, (x, y, 0.006), "gold_trim", segments=10)
    part.build()
    return icon
```
```python
# icons/settings.py
"""Settings: an iron cog."""
import math

from lib.mesh import Part, root

TRIANGLE_BUDGET = 300


def build():
    icon = root("icon_settings")
    part = Part("Prop", parent=icon)
    part.cylinder(0.16, 0.06, (0, 0, 0.2), "mail", segments=16, rotation=(90, 0, 0))
    part.cylinder(0.05, 0.065, (0, 0, 0.2), "umbrella_black", segments=10, rotation=(90, 0, 0))
    for k in range(8):
        a = math.radians(k * 45)
        part.box((0.07, 0.06, 0.06), (math.cos(a) * 0.18, 0, 0.2 + math.sin(a) * 0.18), "mail", bevel=0.005,
                 rotation=(0, -k * 45, 0))
    part.build()
    return icon
```

- [ ] **Step 5: Run the Blender tests, then render the icons**

Run: `./art/build.sh --test 2>&1 | tail -5`. Expected: `OK` (previous 36 + 2 new = 38 tests).
Run: `./art/build.sh --icons`. Expected: 18 `[art] icon … →` lines. Open a few PNGs (Read tool) and check each reads as its object; fix framing only if an icon is unreadable (one pass — see spec Intent).

- [ ] **Step 6: Unity loads the icons**

Add to `UiAssetsTests`:

```csharp
        [Test]
        public void Icons_EveryListedIcon_LoadsWithAlpha()
        {
            foreach (var name in UiAssets.Icons)
            {
                // Act
                var texture = Resources.Load<Texture2D>("UI/Icons/" + name);

                // Assert
                Assert.IsNotNull(texture, name);
                Assert.AreEqual(128, texture.width, name);
            }
        }
```

Run EditMode `-testFilter UiAssetsTests`. Expected: PASS 3/3.

- [ ] **Step 7: Commit**

```bash
git add art/blender art/build.sh client/CultOfComfort.Unity/Assets/Resources/UI/Icons client/CultOfComfort.Unity/Assets/Tests/EditMode/UiAssetsTests.cs
git commit -m "HUD icons: render 18 icons from Blender models (resources, skills, shortcuts, Prophet portrait)"
```

---

### Task 4: Locked features and demo economy (pure C#)

**Files:**
- Create: `Assets/Scripts/Hud/LockedFeatures.cs`, `Assets/Scripts/Hud/FrameState.cs`
- Test: `Assets/Tests/EditMode/HudModelTests.cs`

**Interfaces:**
- Produces:
  - `readonly struct LockedFeature(string id, string label, string icon, string note)` with properties `Id, Label, Icon, Note`.
  - `static class LockedFeatures { IReadOnlyList<LockedFeature> All; LockedFeature Get(string id) /* throws KeyNotFoundException */ }`; ids: `map, research, missionaries, tithes, shop, cult`.
  - `readonly struct Reading(int value, int cap, bool isDemo)` with `Value, Cap, IsDemo`.
  - `readonly struct FrameState(Reading timber, Reading scrap, Reading peat, Reading devotion, Reading pennies, Reading comfort, int hearthLevel, float tension, string tensionNote, bool tensionIsDemo)`.
  - `static class DemoEconomy { const int StorageCap = 1000; FrameState Frame(int scrap, int hearthLevel) }`.

- [ ] **Step 1: Write the failing tests**

```csharp
// Assets/Tests/EditMode/HudModelTests.cs
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace CultOfComfort.Client.Tests
{
    public class HudModelTests
    {
        [Test]
        public void LockedFeatures_All_HasTheSixShortcutsWithNotes()
        {
            // Act
            var ids = LockedFeatures.All.Select(f => f.Id).ToArray();

            // Assert
            CollectionAssert.AreEquivalent(new[] { "map", "research", "missionaries", "tithes", "shop", "cult" }, ids);
            Assert.IsTrue(LockedFeatures.All.All(f => f.Note.Length > 20 && f.Label.Length > 0 && f.Icon.Length > 0));
        }

        [Test]
        public void LockedFeatures_Get_ResearchNamesItsHearthLevel()
        {
            // Act
            var research = LockedFeatures.Get("research");

            // Assert
            StringAssert.Contains("Hearth 3", research.Note);
        }

        [Test]
        public void LockedFeatures_GetUnknownId_Throws()
        {
            Assert.Throws<KeyNotFoundException>(() => LockedFeatures.Get("time_machine"));
        }

        [Test]
        public void DemoEconomy_Frame_ScrapIsRealAndTheRestIsDemo()
        {
            // Act
            var frame = DemoEconomy.Frame(scrap: 380, hearthLevel: 1);

            // Assert
            Assert.AreEqual(380, frame.Scrap.Value);
            Assert.IsFalse(frame.Scrap.IsDemo);
            Assert.IsTrue(frame.Timber.IsDemo && frame.Peat.IsDemo && frame.Devotion.IsDemo && frame.Pennies.IsDemo && frame.Comfort.IsDemo);
            Assert.IsTrue(frame.TensionIsDemo);
            Assert.AreEqual(1, frame.HearthLevel);
        }

        [Test]
        public void DemoEconomy_FrameWithNegativeScrap_ClampsToZero()
        {
            // Act
            var frame = DemoEconomy.Frame(scrap: -5, hearthLevel: 1);

            // Assert
            Assert.AreEqual(0, frame.Scrap.Value);
        }
    }
}
```

- [ ] **Step 2: Run them to verify they fail**

Run: EditMode `-testFilter HudModelTests`. Expected: compile error — types missing.

- [ ] **Step 3: Implement**

```csharp
// Assets/Scripts/Hud/LockedFeatures.cs
using System.Collections.Generic;

namespace CultOfComfort.Client
{
    /// <summary>A shortcut whose screen does not exist yet. Tapping it shows <see cref="Note"/> on a sticky note.</summary>
    public readonly struct LockedFeature
    {
        public LockedFeature(string id, string label, string icon, string note)
        {
            Id = id;
            Label = label;
            Icon = icon;
            Note = note;
        }

        public string Id { get; }
        public string Label { get; }
        public string Icon { get; }
        public string Note { get; }
    }

    /// <summary>The frame's not-yet-built shortcuts (spec: Locked features). An entry leaves this table when its screen lands.</summary>
    public static class LockedFeatures
    {
        public static readonly IReadOnlyList<LockedFeature> All = new[]
        {
            new LockedFeature("map", "Map", "map", "World map: the Prophet is still arguing with the cartographer. Coming soon."),
            new LockedFeature("research", "Research", "research", "Research table unlocks at Hearth 3. He's still reading the manual."),
            new LockedFeature("missionaries", "Missionaries", "missionaries", "Missionaries: nobody has volunteered yet. Coming soon."),
            new LockedFeature("tithes", "Tithes", "tithes", "Tithes unlock at Hearth 4. Wulfric hasn't sent the invoice yet."),
            new LockedFeature("shop", "Shop", "shop", "Shop: the till is a bucket. Coming soon."),
            new LockedFeature("cult", "Cult", "cult", "Your cult: five people and a duck. Coming with buildings."),
        };

        public static LockedFeature Get(string id)
        {
            foreach (var feature in All)
            {
                if (feature.Id == id)
                {
                    return feature;
                }
            }

            throw new KeyNotFoundException($"No locked feature '{id}'.");
        }
    }
}
```

```csharp
// Assets/Scripts/Hud/FrameState.cs
using UnityEngine;

namespace CultOfComfort.Client
{
    /// <summary>One number on the frame. <see cref="IsDemo"/> values are placeholders and get a visible "demo" mark.</summary>
    public readonly struct Reading
    {
        public Reading(int value, int cap, bool isDemo)
        {
            Value = value;
            Cap = cap;
            IsDemo = isDemo;
        }

        public int Value { get; }
        public int Cap { get; }
        public bool IsDemo { get; }
    }

    /// <summary>What the game frame shows: resources, Hearth level, Comfort and Tension.</summary>
    public readonly struct FrameState
    {
        public FrameState(Reading timber, Reading scrap, Reading peat, Reading devotion, Reading pennies, Reading comfort,
            int hearthLevel, float tension, string tensionNote, bool tensionIsDemo)
        {
            Timber = timber;
            Scrap = scrap;
            Peat = peat;
            Devotion = devotion;
            Pennies = pennies;
            Comfort = comfort;
            HearthLevel = hearthLevel;
            Tension = tension;
            TensionNote = tensionNote;
            TensionIsDemo = tensionIsDemo;
        }

        public Reading Timber { get; }
        public Reading Scrap { get; }
        public Reading Peat { get; }
        public Reading Devotion { get; }
        public Reading Pennies { get; }
        public Reading Comfort { get; }
        public int HearthLevel { get; }

        /// <summary>0..1: how close the next raid is.</summary>
        public float Tension { get; }
        public string TensionNote { get; }
        public bool TensionIsDemo { get; }
    }

    /// <summary>
    /// Placeholder economy until base state exists (spec: Placeholder data). Only Scrap is real. The buildings work
    /// replaces this class; nothing else should invent resource numbers.
    /// </summary>
    public static class DemoEconomy
    {
        /// <summary>Hearth 1 storage cap per resource (docs/gdd/02-economy.md).</summary>
        public const int StorageCap = 1000;

        private const string TensionNote = "Wulfric's coming in ~3h";

        public static FrameState Frame(int scrap, int hearthLevel) => new FrameState(
            new Reading(480, StorageCap, true),
            new Reading(Mathf.Max(0, scrap), StorageCap, false),
            new Reading(120, StorageCap, true),
            new Reading(15, StorageCap, true),
            new Reading(50, 0, true),
            new Reading(42, 0, true),
            hearthLevel,
            0.62f,
            TensionNote,
            true);
    }
}
```

- [ ] **Step 4: Run them to verify they pass**

Run: EditMode `-testFilter HudModelTests`. Expected: PASS 5/5.

- [ ] **Step 5: Commit**

```bash
git add client/CultOfComfort.Unity/Assets/Scripts/Hud/LockedFeatures.cs* client/CultOfComfort.Unity/Assets/Scripts/Hud/FrameState.cs* \
  client/CultOfComfort.Unity/Assets/Tests/EditMode/HudModelTests.cs*
git commit -m "HUD: locked-feature table and demo economy for the game frame"
```

---

### Task 5: The new HUD (frame, strips, sides, bar, notes, sheet, card)

**Files:**
- Rewrite: `Assets/Scripts/Hud/HudView.cs`, `Assets/Resources/UI/Hud.uss`
- Modify: `Assets/Scripts/GameController.cs` (`SetUpHud`, `UpdateHud`, `Announce`, line 147 rejection → prophet quip)
- Test: `Assets/Tests/PlayMode/HudFrameTests.cs` (new), `Assets/Tests/PlayMode/GameSmokeTests.cs` (modify `Hud_DuskThenNight_SwapsBuildBarForSkillBar`, `StartNight_UndefendedBase_PlaysToDawnWithVerifiedReplay`)

**Interfaces:**
- Consumes: `FrameState`, `Reading`, `DemoEconomy.Frame(int, int)`, `LockedFeatures.All/Get`, `UiAssets` names (USS references `resource("UI/Kit/…")`, `resource("UI/Icons/…")`, `resource("UI/Fonts/…")`).
- Produces (HudView public API):
  - unchanged: ctor `(VisualElement root, IReadOnlyDictionary<TowerType,(string Name,int Cost)> towers)`, events `TowerSelected, StartNightClicked, SluiceClicked, VoiceClicked, StonesClicked, SpeedClicked, RecenterClicked, BackToDuskClicked`, `SetPortrait(bool)`, `ShowPhase(GamePhase)`, `UpdateDusk(int night, int scrap, TowerType? selected, IReadOnlyDictionary<TowerType,int> costs, string probe)`, `ShowDawn(BattleResult, BattleResult, int)`, `Tick(float now)`, `IsOverUi(Vector2)`, `ClearViewport()`.
  - new: `UpdateFrame(in FrameState frame)`; `Announce(string text, float now, float seconds = 3.5f, bool prophet = false)`; `ShowLockedNote(string id, float now)`.
  - `NightHudState` gains `int Wave, int WaveCount, int Alive` (ctor params after `speed`, before `status`); `Status` is now the hint only.
  - Element names tests rely on: `frame`, `pill-timber|scrap|peat|devotion|pennies`, `portrait`, `hearth-badge`, `comfort`, `settings-button`, `settings-sheet`, `recenter`, `tension-strip`, `wave-strip`, `wave-label`, `side-left`, `side-right`, `locked-<id>`, `locked-note`, `dusk-bar`, `night-bar`, `tower-<TowerType>`, `start-night`, `sluice`, `voice`, `stones`, `speed`, `toast`, `card`, `stamp`, `hearth-fill`, `prophet-fill`. Demo pills/labels carry class `demo`.

- [ ] **Step 1: Write the failing PlayMode tests**

```csharp
// Assets/Tests/PlayMode/HudFrameTests.cs
using System.Collections;
using System.Collections.Generic;
using CultOfComfort.Simulation.Balance;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace CultOfComfort.Client.Tests
{
    public class HudFrameTests
    {
        private GameObject _host;

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            Time.captureDeltaTime = 0f;
            if (_host != null)
            {
                Object.Destroy(_host);
            }
        }

        private static IEnumerator LoadGame()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene");
            yield return null;
            yield return null;
        }

        private static VisualElement Root() => Object.FindAnyObjectByType<UIDocument>().rootVisualElement;

        private static bool Visible(VisualElement e) => e.resolvedStyle.display == DisplayStyle.Flex && e.worldBound.width > 0f;

        private static void Click(Button button)
        {
            using (var submit = NavigationSubmitEvent.GetPooled())
            {
                submit.target = button;
                button.SendEvent(submit);
            }
        }

        /// <summary>A bare HudView on its own panel, for tests that drive it directly.</summary>
        private (HudView Hud, VisualElement Root) StandaloneHud()
        {
            _host = new GameObject("HudHost");
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UI/CocTheme");
            var document = _host.AddComponent<UIDocument>();
            document.panelSettings = settings;
            var root = document.rootVisualElement;
            root.styleSheets.Add(Resources.Load<StyleSheet>("UI/Hud"));
            var hud = new HudView(root, new Dictionary<TowerType, (string, int)> { [TowerType.SaplingFlinger] = ("Sapling Flinger", 70) });
            return (hud, root);
        }

        [UnityTest]
        public IEnumerator Frame_Dusk_ShowsPillsPortraitTensionAndShortcuts()
        {
            // Arrange & Act
            yield return LoadGame();
            var root = Root();

            // Assert
            foreach (var name in new[] { "frame", "pill-timber", "pill-scrap", "pill-peat", "pill-devotion", "pill-pennies", "portrait",
                         "settings-button", "tension-strip", "side-left", "side-right", "locked-map", "dusk-bar", "start-night" })
            {
                Assert.IsTrue(Visible(root.Q(name)), name);
            }

            Assert.IsFalse(Visible(root.Q("wave-strip")), "No wave strip in Dusk.");
        }

        [UnityTest]
        public IEnumerator DemoPills_Dusk_MarkedDemoExceptScrap()
        {
            // Arrange & Act
            yield return LoadGame();
            var root = Root();

            // Assert
            foreach (var name in new[] { "pill-timber", "pill-peat", "pill-devotion", "pill-pennies", "comfort" })
            {
                Assert.IsTrue(root.Q(name).ClassListContains("demo"), name);
            }

            Assert.IsFalse(root.Q("pill-scrap").ClassListContains("demo"));
        }

        [UnityTest]
        public IEnumerator LockedButton_Tapped_ShowsItsNote()
        {
            // Arrange
            yield return LoadGame();
            var root = Root();

            // Act
            Click(root.Q<Button>("locked-research"));
            yield return null;

            // Assert
            var note = root.Q<Label>("locked-note");
            Assert.IsTrue(Visible(note));
            StringAssert.Contains("Hearth 3", note.text);
        }

        [UnityTest]
        public IEnumerator LockedButton_SecondTapOnAnother_ReplacesTheNote()
        {
            // Arrange
            yield return LoadGame();
            var root = Root();
            Click(root.Q<Button>("locked-research"));
            yield return null;

            // Act
            Click(root.Q<Button>("locked-tithes"));
            yield return null;

            // Assert
            Assert.AreEqual(1, root.Query<Label>("locked-note").ToList().Count);
            StringAssert.Contains("Hearth 4", root.Q<Label>("locked-note").text);
        }

        [UnityTest]
        public IEnumerator Night_StartNight_SwapsTensionForWaveAndHidesShortcuts()
        {
            // Arrange
            yield return LoadGame();
            var game = Object.FindAnyObjectByType<GameController>();
            var root = Root();

            // Act
            game.StartNight();
            yield return null;
            yield return null;

            // Assert
            Assert.IsTrue(Visible(root.Q("wave-strip")));
            Assert.IsFalse(Visible(root.Q("tension-strip")));
            Assert.IsFalse(Visible(root.Q("side-left")));
            StringAssert.StartsWith("Wave", root.Q<Label>("wave-label").text);
            Assert.IsTrue(Visible(root.Q("frame")), "The frame stays at night.");
        }

        [UnityTest]
        public IEnumerator Settings_Opened_ShowsRecenterAndBlocksWorldTaps()
        {
            // Arrange
            yield return LoadGame();
            var root = Root();

            // Act
            Click(root.Q<Button>("settings-button"));
            yield return null;
            yield return null;

            // Assert
            var sheet = root.Q("settings-sheet");
            Assert.IsTrue(Visible(sheet));
            Assert.IsTrue(Visible(root.Q("recenter")));
            var centre = sheet.worldBound.center;
            var panelToScreen = new Vector2(centre.x / root.worldBound.width * Screen.width, Screen.height - centre.y / root.worldBound.height * Screen.height);
            var hud = typeof(GameController).GetField("_hud", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(Object.FindAnyObjectByType<GameController>()) as HudView;
            Assert.IsTrue(hud.IsOverUi(panelToScreen), "Taps on the sheet must not reach the board.");
        }

        [UnityTest]
        public IEnumerator Orientation_PortraitAndLandscape_RestackTheDuskBar()
        {
            // Arrange
            var (hud, root) = StandaloneHud();
            hud.ShowPhase(GamePhase.Dusk);

            // Act
            hud.SetPortrait(true);
            yield return null;
            var portrait = root.Q("dusk-bar").resolvedStyle.flexDirection;
            hud.SetPortrait(false);
            yield return null;
            var landscape = root.Q("dusk-bar").resolvedStyle.flexDirection;

            // Assert
            Assert.AreEqual(FlexDirection.Column, portrait);
            Assert.AreEqual(FlexDirection.Row, landscape);
        }

        [UnityTest]
        public IEnumerator Announce_VeryLongText_WrapsInsideTheScreen()
        {
            // Arrange
            var (hud, root) = StandaloneHud();
            hud.ShowPhase(GamePhase.Dusk);

            // Act
            hud.Announce(new string('x', 40) + " " + string.Join(" ", new string[30]).Replace(" ", "mud ") , Time.time, 10f, prophet: true);
            hud.Tick(Time.time);
            yield return null;
            yield return null;

            // Assert
            var toast = root.Q("toast");
            Assert.IsTrue(Visible(toast));
            Assert.LessOrEqual(toast.worldBound.xMax, root.worldBound.xMax + 0.5f);
            Assert.GreaterOrEqual(toast.worldBound.xMin, root.worldBound.xMin - 0.5f);
        }

        [UnityTest]
        public IEnumerator Meters_NegativeOrZeroHearth_ShowAnEmptyBar()
        {
            // Arrange
            var (hud, root) = StandaloneHud();
            hud.ShowPhase(GamePhase.Night);

            // Act
            hud.UpdateNight(new NightHudState(1, -5, 100, 0, 110, "Open Sluice · 2", true, "Phantom Voice", true, "Hot Stones", true, false, 1, 1, 4, 0, "hint"));
            yield return null;
            yield return null;

            // Assert
            Assert.AreEqual(0f, root.Q("hearth-fill").resolvedStyle.width, 0.01f);
            Assert.AreEqual(0f, root.Q("prophet-fill").resolvedStyle.width, 0.01f);
        }
    }
}
```

Update `GameSmokeTests.Hud_DuskThenNight_SwapsBuildBarForSkillBar`'s two display assertions to the named containers:

```csharp
            Assert.AreEqual(DisplayStyle.None, root.Q("dusk-bar").resolvedStyle.display, "The build bar hides at night.");
            Assert.AreEqual(DisplayStyle.Flex, root.Q("night-bar").resolvedStyle.display, "The skill bar shows at night.");
            StringAssert.StartsWith("Open Sluice", sluice.text);
```

and in `StartNight_UndefendedBase_PlaysToDawnWithVerifiedReplay` add, after the existing asserts:

```csharp
            yield return null;
            var root = Object.FindAnyObjectByType<UIDocument>().rootVisualElement;
            Assert.AreEqual(DisplayStyle.Flex, root.Q("stamp").resolvedStyle.display, "Dawn shows the verdict stamp.");

            // Back to Dusk restores the frame (Review Focus 4).
            game.GetType().GetMethod("BackToDusk", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(game, null);
            yield return null;
            yield return null;
            Assert.AreEqual(GamePhase.Dusk, game.Phase);
            Assert.AreEqual(DisplayStyle.Flex, root.Q("tension-strip").resolvedStyle.display);
            Assert.AreEqual(DisplayStyle.Flex, root.Q("side-left").resolvedStyle.display);
```

- [ ] **Step 2: Run them to verify they fail**

Run: PlayMode (all). Expected: compile error (`NightHudState` ctor arity, `Announce` named arg `prophet`) — then, once Step 3 is half done, element-not-found failures. Record the RED output.

- [ ] **Step 3: Rewrite `HudView.cs`**

Replace the file with:

```csharp
using System;
using System.Collections.Generic;
using CultOfComfort.Simulation.Balance;
using CultOfComfort.Simulation.Combat;
using UnityEngine;
using UnityEngine.UIElements;

namespace CultOfComfort.Client
{
    /// <summary>What the Night bar shows. Built by the controller each frame from the live battle.</summary>
    public readonly struct NightHudState
    {
        public NightHudState(
            int night, int hearth, int hearthMax, int prophet, int prophetMax, string sluice, bool sluiceReady, string voice, bool voiceReady,
            string stones, bool stonesReady, bool aiming, int speed, int wave, int waveCount, int alive, string status)
        {
            Night = night;
            Hearth = hearth;
            HearthMax = hearthMax;
            Prophet = prophet;
            ProphetMax = prophetMax;
            Sluice = sluice;
            SluiceReady = sluiceReady;
            Voice = voice;
            VoiceReady = voiceReady;
            Stones = stones;
            StonesReady = stonesReady;
            Aiming = aiming;
            Speed = speed;
            Wave = wave;
            WaveCount = waveCount;
            Alive = alive;
            Status = status;
        }

        public int Night { get; }
        public int Hearth { get; }
        public int HearthMax { get; }
        public int Prophet { get; }
        public int ProphetMax { get; }
        public string Sluice { get; }
        public bool SluiceReady { get; }
        public string Voice { get; }
        public bool VoiceReady { get; }
        public string Stones { get; }
        public bool StonesReady { get; }

        /// <summary>Hot Stones is armed: the next tap on the board picks where they land.</summary>
        public bool Aiming { get; }
        public int Speed { get; }
        public int Wave { get; }
        public int WaveCount { get; }
        public int Alive { get; }

        /// <summary>The one-line hint under the skills.</summary>
        public string Status { get; }
    }

    /// <summary>
    /// The HUD (spec: HUD Kit & Game Frame), built in code with UI Toolkit and styled by Resources/UI/Hud.uss.
    /// Layout C on parchment: a frame on top (resource pills, portrait, settings) with a strip under it (Tension at
    /// Dusk, waves + health at Night), shortcut columns at the sides, and a bottom bar. A <c>portrait</c> /
    /// <c>landscape</c> class on the root restacks it. It only displays state and raises events; the
    /// <see cref="GameController"/> decides what they mean.
    /// </summary>
    public sealed class HudView
    {
        /// <summary>Extra inset beyond the OS safe area, for rounded display corners (panel points).</summary>
        private const float CornerMargin = 14f;
        private const float NoteSeconds = 3f;

        private readonly VisualElement _root;
        private readonly VisualElement _top;
        private readonly Dictionary<string, (VisualElement Pill, Label Value)> _pills = new Dictionary<string, (VisualElement, Label)>();
        private readonly Label _hearthBadge;
        private readonly Label _hearthLine;
        private readonly Label _comfort;
        private readonly VisualElement _tensionStrip;
        private readonly VisualElement _tensionCover;
        private readonly Label _tensionNote;
        private readonly VisualElement _waveStrip;
        private readonly Label _waveLabel;
        private readonly VisualElement _hearthFill;
        private readonly Label _hearthValue;
        private readonly VisualElement _prophetFill;
        private readonly Label _prophetValue;
        private readonly VisualElement _sideLeft;
        private readonly VisualElement _sideRight;
        private readonly Label _lockedNote;
        private readonly VisualElement _settings;
        private readonly Label _probe;
        private readonly Label _toast;
        private readonly VisualElement _card;
        private readonly Label _cardTitle;
        private readonly Label _cardSummary;
        private readonly Label _cardFlavour;
        private readonly Label _cardStats;
        private readonly VisualElement _stamp;
        private readonly VisualElement _bar;
        private readonly VisualElement _duskBar;
        private readonly VisualElement _nightBar;
        private readonly Dictionary<TowerType, Button> _towerButtons = new Dictionary<TowerType, Button>();
        private readonly Button _sluice;
        private readonly Button _voice;
        private readonly Button _stones;
        private readonly Button _speed;
        private readonly Label _nightStatus;
        private readonly Dictionary<Button, Label> _skillText = new Dictionary<Button, Label>();
        private float _toastUntil;
        private float _noteUntil;
        private Vector4 _padding = new Vector4(float.NaN, 0f, 0f, 0f);

        public HudView(VisualElement root, IReadOnlyDictionary<TowerType, (string Name, int Cost)> towers)
        {
            _root = root;
            _root.AddToClassList("hud");
            _root.pickingMode = PickingMode.Ignore;

            // ---------- top: frame + strip ----------
            _top = Container("top");
            var frame = Container("frame");
            frame.name = "frame";
            var pills = Container("pills");
            foreach (var (key, icon) in new[] { ("timber", "timber"), ("scrap", "scrap"), ("peat", "peat"), ("devotion", "devotion"), ("pennies", "penny") })
            {
                var pill = Container(key == "pennies" ? "pill-note" : "pill");
                pill.name = $"pill-{key}";
                pill.Add(Icon(icon));
                var value = Text(string.Empty, "pill-value");
                pill.Add(value);
                pill.Add(Text("demo", "demo-tag"));
                _pills[key] = (pill, value);
                pills.Add(pill);
            }

            frame.Add(pills);
            var identity = Container("identity");
            var portrait = Container("portrait");
            portrait.name = "portrait";
            portrait.Add(Icon("portrait_prophet", "portrait-face"));
            _hearthBadge = Text("1", "hearth-badge");
            _hearthBadge.name = "hearth-badge";
            portrait.Add(_hearthBadge);
            identity.Add(portrait);
            var label = Container("id-label");
            label.Add(Container("tape"));
            _hearthLine = Text("Hearth Lv 1", "id-title");
            _comfort = Text(string.Empty, "id-line");
            _comfort.name = "comfort";
            label.Add(_hearthLine);
            label.Add(_comfort);
            identity.Add(label);
            identity.Add(Container("spacer"));
            var settingsButton = IconButton("settings", () => ToggleSettings(), "round");
            settingsButton.name = "settings-button";
            identity.Add(settingsButton);
            frame.Add(identity);
            _top.Add(frame);

            _tensionStrip = Container("strip");
            _tensionStrip.name = "tension-strip";
            var tape = Container("hazard-track");
            _tensionCover = Container("hazard-cover");
            tape.Add(_tensionCover);
            _tensionStrip.Add(tape);
            _tensionNote = Text(string.Empty, "marker-note");
            _tensionStrip.Add(_tensionNote);
            _top.Add(_tensionStrip);

            _waveStrip = Container("strip");
            _waveStrip.name = "wave-strip";
            _waveLabel = Text(string.Empty, "wave-label");
            _waveLabel.name = "wave-label";
            _waveStrip.Add(_waveLabel);
            (_hearthFill, _hearthValue) = Meter(_waveStrip, "Hearth", "hearth-fill");
            (_prophetFill, _prophetValue) = Meter(_waveStrip, "Prophet", "prophet-fill");
            _top.Add(_waveStrip);

            // ---------- middle: side columns + centre stack ----------
            var middle = Container("middle");
            _sideLeft = Container("side");
            _sideLeft.name = "side-left";
            _sideRight = Container("side");
            _sideRight.name = "side-right";
            _sideLeft.Add(Shortcut(LockedFeatures.Get("map")));
            _sideLeft.Add(Shortcut(LockedFeatures.Get("research")));
            _sideRight.Add(Shortcut(LockedFeatures.Get("missionaries")));
            _sideRight.Add(Shortcut(LockedFeatures.Get("tithes")));
            var centre = Container("centre");
            _toast = Text(string.Empty, "toast", "hidden");
            _toast.name = "toast";
            _toast.pickingMode = PickingMode.Ignore;
            _lockedNote = Text(string.Empty, "locked-note", "hidden");
            _lockedNote.name = "locked-note";
            _settings = BuildSettings(out _probe);
            _card = BuildCard(out _cardTitle, out _cardSummary, out _cardFlavour, out _cardStats, out _stamp);
            centre.Add(_toast);
            centre.Add(_lockedNote);
            centre.Add(_settings);
            centre.Add(_card);
            middle.Add(_sideLeft);
            middle.Add(centre);
            middle.Add(_sideRight);

            // ---------- bottom bar ----------
            _bar = Panel("bar");
            _duskBar = Container("dusk-bar");
            _duskBar.name = "dusk-bar";
            var picker = Container("towers");
            var notes = new[] { "note-yellow", "note-pink", "note-green", "note-blue" };
            foreach (var tower in towers)
            {
                var button = new Button(() => TowerSelected?.Invoke(tower.Key)) { name = $"tower-{tower.Key}" };
                button.AddToClassList("btn");
                button.AddToClassList("tower");
                button.AddToClassList(notes[_towerButtons.Count % notes.Length]);
                var towerName = Text(tower.Value.Name, "tower-name");
                var towerCost = Text($"{tower.Value.Cost} Scrap", "tower-cost");
                towerName.pickingMode = towerCost.pickingMode = PickingMode.Ignore;
                button.Add(towerName);
                button.Add(towerCost);
                _towerButtons[tower.Key] = button;
                picker.Add(button);
            }

            _duskBar.Add(picker);
            var actions = Container("actions");
            actions.Add(Shortcut(LockedFeatures.Get("shop"), "bar-shortcut"));
            var start = ButtonWith("Light the Lamps", () => StartNightClicked?.Invoke(), "primary");
            start.name = "start-night";
            actions.Add(start);
            actions.Add(Shortcut(LockedFeatures.Get("cult"), "bar-shortcut"));
            _duskBar.Add(actions);

            _nightBar = Container("night-bar");
            _nightBar.name = "night-bar";
            var skills = Container("skills");
            _sluice = SkillButton("sluice", "note-blue", () => SluiceClicked?.Invoke());
            _voice = SkillButton("voice", "note-pink", () => VoiceClicked?.Invoke());
            _stones = SkillButton("stones", "note-yellow", () => StonesClicked?.Invoke());
            _speed = SkillButton("speed", "note-green", () => SpeedClicked?.Invoke());
            _speed.AddToClassList("speed");
            skills.Add(_sluice);
            skills.Add(_voice);
            skills.Add(_stones);
            skills.Add(_speed);
            _nightStatus = Text(string.Empty, "hint");
            _nightBar.Add(skills);
            _nightBar.Add(_nightStatus);
            _bar.Add(_duskBar);
            _bar.Add(_nightBar);

            _root.Add(_top);
            _root.Add(middle);
            _root.Add(_bar);
            SetPortrait(false);
            Show(_settings, false);
        }

        public event Action<TowerType> TowerSelected;
        public event Action StartNightClicked;
        public event Action SluiceClicked;
        public event Action VoiceClicked;
        public event Action StonesClicked;
        public event Action SpeedClicked;
        public event Action RecenterClicked;
        public event Action BackToDuskClicked;

        public void SetPortrait(bool portrait)
        {
            _root.EnableInClassList("portrait", portrait);
            _root.EnableInClassList("landscape", !portrait);
        }

        public void ShowPhase(GamePhase phase)
        {
            Show(_tensionStrip, phase == GamePhase.Dusk);
            Show(_waveStrip, phase != GamePhase.Dusk);
            Show(_sideLeft, phase == GamePhase.Dusk);
            Show(_sideRight, phase == GamePhase.Dusk);
            Show(_duskBar, phase == GamePhase.Dusk);
            Show(_nightBar, phase == GamePhase.Night);
            Show(_bar, phase != GamePhase.Dawn);
            Show(_card, phase == GamePhase.Dawn);
            Show(_prophetFill.parent.parent, phase == GamePhase.Night);
            if (phase != GamePhase.Dusk)
            {
                Show(_settings, false);
                _noteUntil = 0f;
            }
        }

        public void UpdateFrame(in FrameState frame)
        {
            SetPill("timber", frame.Timber);
            SetPill("scrap", frame.Scrap);
            SetPill("peat", frame.Peat);
            SetPill("devotion", frame.Devotion);
            SetPill("pennies", frame.Pennies);
            _hearthBadge.text = frame.HearthLevel.ToString();
            _hearthLine.text = $"Hearth Lv {frame.HearthLevel}";
            _comfort.text = $"Comfort {frame.Comfort.Value}";
            _comfort.EnableInClassList("demo", frame.Comfort.IsDemo);
            _tensionCover.style.width = Length.Percent((1f - Mathf.Clamp01(frame.Tension)) * 100f);
            _tensionNote.text = frame.TensionIsDemo ? $"TENSION — {frame.TensionNote} (demo)" : $"TENSION — {frame.TensionNote}";
        }

        public void UpdateDusk(int night, int scrap, TowerType? selected, IReadOnlyDictionary<TowerType, int> costs, string probe)
        {
            _probe.text = string.IsNullOrEmpty(probe) ? $"Night {night}" : $"Night {night} · {probe}";
            foreach (var pair in _towerButtons)
            {
                pair.Value.EnableInClassList("selected", pair.Key == selected);
                pair.Value.EnableInClassList("unaffordable", scrap < costs[pair.Key]);
            }
        }

        public void UpdateNight(in NightHudState state)
        {
            _waveLabel.text = $"Wave {Mathf.Max(1, state.Wave)}/{state.WaveCount} · {state.Alive} on the road";
            SetMeter(_hearthValue, _hearthFill, state.Hearth, state.HearthMax);
            SetMeter(_prophetValue, _prophetFill, state.Prophet, state.ProphetMax);
            SetSkill(_sluice, state.Sluice, state.SluiceReady);
            SetSkill(_voice, state.Voice, state.VoiceReady);
            SetSkill(_stones, state.Stones, state.StonesReady);
            _stones.EnableInClassList("aiming", state.Aiming);
            SetSkill(_speed, $"{state.Speed}×", true);
            _nightStatus.text = state.Status;
        }

        public void ShowDawn(BattleResult result, BattleResult serverReplay, int commands)
        {
            var won = result.Outcome == BattleOutcome.Won;
            var match = serverReplay == result;
            SetMeter(_hearthValue, _hearthFill, result.HearthHealth, result.HearthMaxHealth);
            _waveLabel.text = "Dawn";
            _cardTitle.text = won ? "The Hearth stands" : "The Hearth went cold";
            _cardSummary.text = $"Stars {result.Stars}/3 · Hearth {result.HearthHealth}/{result.HearthMaxHealth}";
            _cardFlavour.text = won
                ? "The attackers left damp, confused and noticeably cleaner."
                : "Wulfric's men warmed their hands on the embers and left. Tomorrow: repairs, and a strongly worded diary entry.";
            _cardStats.text = $"{result.EnemiesDefeated} defeated · {result.EnemiesLeaked} reached the Hearth · {commands} commands logged\n" +
                              $"Client hash {result.StateHashHex} · server replay {serverReplay.StateHashHex}";
            _stamp.EnableInClassList("verified", match);
            _stamp.EnableInClassList("mismatch", !match);
        }

        /// <summary>The world speaks on a parchment ribbon; the Prophet (<paramref name="prophet"/>) scribbles on a sticky note.</summary>
        public void Announce(string text, float now, float seconds = 3.5f, bool prophet = false)
        {
            _toast.text = text;
            _toast.EnableInClassList("prophet", prophet);
            _toastUntil = now + seconds;
        }

        public void ShowLockedNote(string id, float now)
        {
            _lockedNote.text = LockedFeatures.Get(id).Note;
            _noteUntil = now + NoteSeconds;
            Show(_settings, false);
        }

        /// <summary>Per frame: hides expired notes and keeps the root padded to the safe area.</summary>
        public void Tick(float now)
        {
            _toast.EnableInClassList("hidden", now >= _toastUntil || string.IsNullOrEmpty(_toast.text));
            _lockedNote.EnableInClassList("hidden", now >= _noteUntil);
            ApplySafeArea();
        }

        /// <summary>True when a screen position (pixels, bottom-left origin) is over a panel or button.</summary>
        public bool IsOverUi(Vector2 screen)
        {
            var panel = _root.panel;
            if (panel == null)
            {
                return false;
            }

            var local = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screen.x, Screen.height - screen.y));
            return panel.Pick(local) != null;
        }

        /// <summary>
        /// The part of the screen between the top (frame + strip) and the bottom bar, in viewport coordinates (0..1,
        /// bottom-left origin). The camera fits the board in here. Null until UI Toolkit has done a layout pass.
        /// </summary>
        public Rect? ClearViewport()
        {
            var size = _root.worldBound;
            if (float.IsNaN(size.height) || size.height < 1f || float.IsNaN(_top.worldBound.yMax))
            {
                return null;
            }

            var top = _top.worldBound.yMax / size.height;
            var bottom = _bar.resolvedStyle.display == DisplayStyle.None ? 1f - _padding.w / size.height : _bar.worldBound.yMin / size.height;
            var left = float.IsNaN(_padding.x) ? 0f : _padding.x / size.width;
            var right = float.IsNaN(_padding.x) ? 0f : _padding.z / size.width;
            const float gap = 0.015f;
            return Rect.MinMaxRect(left + gap, 1f - bottom + gap, 1f - right - gap, 1f - top - gap);
        }

        private void ToggleSettings()
        {
            Show(_settings, _settings.style.display == DisplayStyle.None);
            _noteUntil = 0f;
        }

        private VisualElement BuildSettings(out Label probe)
        {
            var sheet = Panel("sheet");
            sheet.name = "settings-sheet";
            sheet.Add(Text("Settings", "sheet-title"));
            var recenter = ButtonWith("Recenter camera", () => RecenterClicked?.Invoke(), "plain");
            recenter.name = "recenter";
            sheet.Add(recenter);
            sheet.Add(Text("Sound: there is no sound yet.", "sheet-line"));
            sheet.Add(Text($"Build {Application.version}", "sheet-line", "small"));
            probe = Text(string.Empty, "sheet-line", "small");
            probe.style.display = Debug.isDebugBuild ? DisplayStyle.Flex : DisplayStyle.None;
            sheet.Add(probe);
            sheet.Add(ButtonWith("Close", () => Show(_settings, false), "plain"));
            return sheet;
        }

        private VisualElement BuildCard(out Label title, out Label summary, out Label flavour, out Label stats, out VisualElement stamp)
        {
            var card = Panel("card");
            card.name = "card";
            title = Text(string.Empty, "card-title");
            summary = Text(string.Empty, "card-line");
            flavour = Text(string.Empty, "card-line");
            stats = Text(string.Empty, "card-line", "small");
            stamp = Container("stamp");
            stamp.name = "stamp";
            card.Add(title);
            card.Add(summary);
            card.Add(flavour);
            card.Add(stats);
            card.Add(stamp);
            card.Add(ButtonWith("Back to Dusk", () => BackToDuskClicked?.Invoke(), "primary"));
            return card;
        }

        private VisualElement Shortcut(LockedFeature feature, string extraClass = null)
        {
            var holder = Container("shortcut");
            if (extraClass != null)
            {
                holder.AddToClassList(extraClass);
            }

            var button = IconButton(feature.Icon, () => ShowLockedNote(feature.Id, Time.time), "round", "locked");
            button.name = $"locked-{feature.Id}";
            button.Add(Container("padlock"));
            holder.Add(button);
            holder.Add(Text(feature.Label, "shortcut-label"));
            return holder;
        }

        private Button SkillButton(string icon, string note, Action clicked)
        {
            var button = new Button(clicked) { name = icon };
            button.AddToClassList("btn");
            button.AddToClassList("skill");
            button.AddToClassList(note);
            button.Add(Icon(icon, "skill-icon"));
            var text = Text(string.Empty, "skill-text");
            text.pickingMode = PickingMode.Ignore;
            button.Add(text);
            _skillText[button] = text;
            return button;
        }

        /// <summary>Per frame, so no queries: the label was remembered when the button was built.</summary>
        private void SetSkill(Button button, string text, bool ready)
        {
            _skillText[button].text = text;
            button.SetEnabled(ready);
        }

        private void SetPill(string key, Reading reading)
        {
            var (pill, value) = _pills[key];
            value.text = reading.Cap > 0 ? $"{reading.Value}/{Short(reading.Cap)}" : reading.Value.ToString();
            pill.EnableInClassList("demo", reading.IsDemo);
        }

        private static string Short(int cap) => cap >= 1000 && cap % 1000 == 0 ? $"{cap / 1000}k" : cap.ToString();

        private static void SetMeter(Label value, VisualElement fill, int current, int max)
        {
            var fraction = max > 0 ? Mathf.Clamp01(current / (float)max) : 0f;
            value.text = Mathf.Max(0, current).ToString();
            fill.style.width = Length.Percent(fraction * 100f);
        }

        private static (VisualElement Fill, Label Value) Meter(VisualElement parent, string name, string fillName)
        {
            var row = Container("meter");
            row.Add(Text(name, "meter-label"));
            var track = Container("meter-track");
            var fill = Container("meter-fill");
            fill.name = fillName;
            track.Add(fill);
            row.Add(track);
            var value = Text(string.Empty, "meter-value");
            row.Add(value);
            parent.Add(row);
            return (fill, value);
        }

        /// <summary>UI Toolkit ignores Screen.safeArea, so pad the root ourselves (cutouts, rounded corners, gesture bar).</summary>
        private void ApplySafeArea()
        {
            var panel = _root.panel;
            if (panel == null)
            {
                return;
            }

            var area = Screen.safeArea;
            var topLeft = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(area.xMin, Screen.height - area.yMax));
            var bottomRight = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(area.xMax, Screen.height - area.yMin));
            var full = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(Screen.width, Screen.height));
            var padding = new Vector4(
                topLeft.x + CornerMargin,
                topLeft.y + CornerMargin * 0.6f,
                full.x - bottomRight.x + CornerMargin,
                full.y - bottomRight.y + CornerMargin * 0.6f);
            if (padding == _padding)
            {
                return;
            }

            _padding = padding;
            _root.style.paddingLeft = padding.x;
            _root.style.paddingTop = padding.y;
            _root.style.paddingRight = padding.z;
            _root.style.paddingBottom = padding.w;
        }

        private static void Show(VisualElement element, bool visible) =>
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        private static VisualElement Container(string className)
        {
            var element = new VisualElement { pickingMode = PickingMode.Ignore };
            element.AddToClassList(className);
            return element;
        }

        /// <summary>A panel blocks world taps (picking on), unlike plain layout containers.</summary>
        private static VisualElement Panel(string className)
        {
            var element = new VisualElement();
            element.AddToClassList("panel");
            element.AddToClassList(className);
            return element;
        }

        private static VisualElement Icon(string icon, string className = "icon")
        {
            var element = Container(className);
            element.AddToClassList("icon");
            element.AddToClassList($"icon-{icon}");
            return element;
        }

        private static Label Text(string text, params string[] classNames)
        {
            var label = new Label(text);
            foreach (var className in classNames)
            {
                label.AddToClassList(className);
            }

            return label;
        }

        private static Button IconButton(string icon, Action clicked, params string[] classNames)
        {
            var button = ButtonWith(string.Empty, clicked, classNames);
            button.Add(Icon(icon));
            return button;
        }

        private static Button ButtonWith(string text, Action clicked, params string[] classNames)
        {
            var button = new Button(clicked) { text = text };
            button.AddToClassList("btn");
            foreach (var className in classNames)
            {
                button.AddToClassList(className);
            }

            return button;
        }
    }
}
```

Notes for the implementer: frame pills and the strip are layout containers (`pickingMode = Ignore`) except buttons, so taps between pills reach the board — intended (they sit over the forest apron). The settings sheet, card, bar and locked note are panels / labels with picking on. Make `_lockedNote.pickingMode = PickingMode.Position` (Labels default to Position — keep).

- [ ] **Step 4: Rewrite `Hud.uss`**

```css
/* Cult of Comfort HUD — layout C on parchment (spec: HUD Kit & Game Frame).
   Units are density-independent points (PanelSettings: constant physical size, 160 dpi).
   Fonts: IM Fell English (world voice), IM Fell English SC (titles), Permanent Marker (the Prophet's voice). */

.hud {
    position: absolute; left: 0; top: 0; right: 0; bottom: 0;
    flex-direction: column;
    justify-content: space-between;
    color: #3a2a18;
    font-size: 14px;
    -unity-font-definition: resource("UI/Fonts/ImFellEnglish");
}

.panel {
    background-image: resource("UI/Kit/parchment");
    -unity-slice-left: 32; -unity-slice-right: 32; -unity-slice-top: 32; -unity-slice-bottom: 32;
    -unity-slice-scale: 0.5px;
    padding: 12px 16px;
}

.hidden { display: none; }
.spacer { flex-grow: 1; }
.small { font-size: 11px; color: #6b5a44; }
.icon { width: 22px; height: 22px; -unity-background-scale-mode: scale-to-fit; flex-shrink: 0; }

.icon-timber { background-image: resource("UI/Icons/timber"); }
.icon-scrap { background-image: resource("UI/Icons/scrap"); }
.icon-peat { background-image: resource("UI/Icons/peat"); }
.icon-devotion { background-image: resource("UI/Icons/devotion"); }
.icon-penny { background-image: resource("UI/Icons/penny"); }
.icon-comfort { background-image: resource("UI/Icons/comfort"); }
.icon-sluice { background-image: resource("UI/Icons/sluice"); }
.icon-voice { background-image: resource("UI/Icons/voice"); }
.icon-stones { background-image: resource("UI/Icons/stones"); }
.icon-speed { background-image: resource("UI/Icons/speed"); }
.icon-map { background-image: resource("UI/Icons/map"); }
.icon-research { background-image: resource("UI/Icons/research"); }
.icon-missionaries { background-image: resource("UI/Icons/missionaries"); }
.icon-tithes { background-image: resource("UI/Icons/tithes"); }
.icon-shop { background-image: resource("UI/Icons/shop"); }
.icon-cult { background-image: resource("UI/Icons/cult"); }
.icon-settings { background-image: resource("UI/Icons/settings"); }
.icon-portrait_prophet { background-image: resource("UI/Icons/portrait_prophet"); }

/* ---------- frame ---------- */

.top { flex-direction: column; }
.frame { flex-direction: column; }
.pills { flex-direction: row; flex-wrap: wrap; }

.pill, .pill-note {
    flex-direction: row; align-items: center;
    height: 28px; padding: 0 9px 0 4px; margin: 0 5px 5px 0;
    background-image: resource("UI/Kit/parchment_dark");
    -unity-slice-left: 24; -unity-slice-right: 24; -unity-slice-top: 24; -unity-slice-bottom: 24; -unity-slice-scale: 0.5px;
}
.pill-note {
    background-image: resource("UI/Kit/note_yellow");
    -unity-slice-left: 16; -unity-slice-right: 16; -unity-slice-top: 16; -unity-slice-bottom: 16;
    rotate: 2deg;
}
.pill-value { color: #f6e7c4; -unity-font-style: bold; font-size: 13px; margin-left: 3px; padding: 0; }
.pill-note .pill-value { color: #2b2215; -unity-font-definition: resource("UI/Fonts/PermanentMarker"); }
.demo-tag {
    display: none; font-size: 8px; color: #ffcf80; margin-left: 3px; padding: 0;
    -unity-font-definition: resource("UI/Fonts/PermanentMarker");
}
.demo > .demo-tag { display: flex; }
.pill-note.demo > .demo-tag { color: #a8432c; }

.identity { flex-direction: row; align-items: center; margin-top: 2px; }
.portrait {
    width: 58px; height: 58px; border-radius: 29px;
    background-color: #6d4c2f; border-color: #d9b45a; border-width: 3px;
    overflow: hidden;
}
.portrait-face { width: 100%; height: 100%; }
.hearth-badge {
    position: absolute; right: -2px; bottom: -2px;
    width: 24px; height: 24px; border-radius: 12px;
    background-color: #7a1f1a; border-color: #e3b34a; border-width: 2px;
    color: #ffd98a; font-size: 13px; -unity-text-align: middle-center; padding: 0;
    -unity-font-definition: resource("UI/Fonts/ImFellEnglishSC");
}
.id-label {
    margin-left: 8px; padding: 6px 12px 6px 12px;
    background-image: resource("UI/Kit/parchment");
    -unity-slice-left: 32; -unity-slice-right: 32; -unity-slice-top: 32; -unity-slice-bottom: 32; -unity-slice-scale: 0.5px;
}
.tape {
    position: absolute; top: -8px; left: 22px; width: 46px; height: 14px; rotate: -8deg;
    background-image: resource("UI/Kit/tape_a");
}
.id-title { -unity-font-definition: resource("UI/Fonts/ImFellEnglishSC"); font-size: 14px; padding: 0; margin: 0; }
.id-line { font-size: 12px; padding: 0; margin: 0; }
.id-line.demo { color: #8a6a47; }

.round {
    width: 48px; height: 48px; border-radius: 24px; padding: 6px; margin: 0;
    background-image: resource("UI/Kit/parchment");
    -unity-slice-left: 32; -unity-slice-right: 32; -unity-slice-top: 32; -unity-slice-bottom: 32; -unity-slice-scale: 0.5px;
    border-width: 0; align-items: center; justify-content: center;
}
.round > .icon { width: 34px; height: 34px; }

/* ---------- strips ---------- */

.strip { margin: 8px -16px 0 -16px; rotate: -1.5deg; }
.hazard-track {
    height: 20px;
    background-image: resource("UI/Kit/hazard"); background-repeat: repeat; background-size: 32px 32px;
    border-color: #222; border-top-width: 2px; border-bottom-width: 2px;
}
.hazard-cover { position: absolute; right: 0; top: 0; bottom: 0; background-color: rgba(30, 26, 20, 0.72); }
.marker-note, .wave-label {
    -unity-font-definition: resource("UI/Fonts/PermanentMarker");
    color: #ffe066; font-size: 14px; margin: 2px 24px 0 24px; padding: 0;
    text-shadow: 0 1px 2px rgba(0, 0, 0, 0.9);
}
.meter { flex-direction: row; align-items: center; margin: 3px 24px 0 24px; }
.meter-label { width: 60px; color: #f6e7c4; -unity-font-definition: resource("UI/Fonts/ImFellEnglishSC"); font-size: 13px; padding: 0; text-shadow: 0 1px 2px rgba(0,0,0,0.9); }
.meter-track { flex-grow: 1; height: 14px; background-color: rgba(30, 26, 20, 0.8); border-color: #222; border-width: 2px; }
.meter-fill { height: 100%; background-image: resource("UI/Kit/hazard"); background-repeat: repeat; background-size: 24px 24px; }
.meter-value { width: 36px; color: #f6e7c4; font-size: 12px; -unity-text-align: middle-right; padding: 0; }

/* ---------- middle ---------- */

.middle { flex-grow: 1; flex-direction: row; justify-content: space-between; align-items: flex-start; margin-top: 10px; }
.side { flex-direction: column; }
.shortcut { align-items: center; margin-bottom: 10px; }
.shortcut-label { font-size: 10px; color: #fff3d6; text-shadow: 0 1px 2px #000; padding: 0; margin-top: 2px; }
.locked > .icon { opacity: 0.55; }
.padlock { position: absolute; right: -2px; bottom: -2px; width: 18px; height: 18px; background-image: resource("UI/Kit/padlock"); }

.centre { flex-grow: 1; flex-shrink: 1; align-items: center; padding: 0 8px; }
.toast {
    max-width: 92%; margin-top: 4px; padding: 8px 18px; white-space: normal; -unity-text-align: middle-center;
    background-image: resource("UI/Kit/parchment");
    -unity-slice-left: 32; -unity-slice-right: 32; -unity-slice-top: 32; -unity-slice-bottom: 32; -unity-slice-scale: 0.5px;
}
.toast.prophet, .locked-note {
    background-image: resource("UI/Kit/note_yellow");
    -unity-slice-left: 16; -unity-slice-right: 16; -unity-slice-top: 16; -unity-slice-bottom: 16;
    -unity-font-definition: resource("UI/Fonts/PermanentMarker");
    color: #2b2215; rotate: -1.5deg;
}
.locked-note {
    max-width: 78%; margin-top: 10px; padding: 14px 16px; white-space: normal; font-size: 14px;
    background-image: resource("UI/Kit/note_pink");
    rotate: 2deg;
}

.sheet { width: 260px; margin-top: 10px; align-items: stretch; }
.sheet-title, .card-title { -unity-font-definition: resource("UI/Fonts/ImFellEnglishSC"); color: #7a1f1a; font-size: 22px; margin: 0 0 6px 0; padding: 0; }
.sheet-line, .card-line { white-space: normal; margin: 3px 0 0 0; padding: 0; }

.card { max-width: 420px; margin-top: 30px; align-items: stretch; }
.stamp { height: 74px; margin: 8px 0; -unity-background-scale-mode: scale-to-fit; }
.stamp.verified { background-image: resource("UI/Kit/stamp_verified"); }
.stamp.mismatch { background-image: resource("UI/Kit/stamp_mismatch"); }

/* ---------- buttons ---------- */

.btn { margin: 0; border-width: 0; background-color: rgba(0, 0, 0, 0); color: #2b2215; }
.primary {
    height: 50px; padding: 0 18px; border-radius: 12px;
    background-color: #c97a2c; border-color: #6b3a12; border-width: 2px;
    color: #fff4dc; font-size: 20px; -unity-font-definition: resource("UI/Fonts/ImFellEnglishSC");
    text-shadow: 0 1px 0 #6b3a12;
}
.primary:hover { background-color: #d98a38; }
.primary:active { background-color: #a9621f; }
.plain { height: 36px; margin-top: 8px; border-radius: 8px; background-color: rgba(122, 31, 26, 0.12); border-color: #8a6a3a; border-width: 1px; font-size: 15px; }

.note-yellow, .note-pink, .note-green, .note-blue {
    -unity-slice-left: 16; -unity-slice-right: 16; -unity-slice-top: 16; -unity-slice-bottom: 16;
    -unity-font-definition: resource("UI/Fonts/PermanentMarker");
}
.note-yellow { background-image: resource("UI/Kit/note_yellow"); }
.note-pink { background-image: resource("UI/Kit/note_pink"); }
.note-green { background-image: resource("UI/Kit/note_green"); }
.note-blue { background-image: resource("UI/Kit/note_blue"); }

/* ---------- bottom bar ---------- */

.bar {
    background-image: resource("UI/Kit/parchment_dark");
    -unity-slice-left: 24; -unity-slice-right: 24; -unity-slice-top: 24; -unity-slice-bottom: 24; -unity-slice-scale: 0.5px;
    padding: 10px 12px;
}
.dusk-bar, .night-bar { flex-direction: column; }
.landscape .dusk-bar { flex-direction: row; align-items: center; }
.towers { flex-direction: row; }
.portrait .towers { margin-bottom: 8px; }
.tower { flex-grow: 1; height: 58px; margin-right: 8px; padding: 6px 8px; justify-content: center; }
.tower:nth-child(odd) { rotate: -2deg; }
.tower:nth-child(even) { rotate: 2deg; }
.tower-name { font-size: 15px; padding: 0; -unity-text-align: middle-center; }
.tower-cost { font-size: 12px; padding: 0; -unity-text-align: middle-center; color: #5a3d1e; }
.tower.selected { scale: 1.06 1.06; border-color: #c9862e; border-width: 3px; }
.tower.unaffordable .tower-cost { color: #a8322a; }
.actions { flex-direction: row; align-items: center; flex-grow: 1; }
.actions > .primary { flex-grow: 1; margin: 0 8px; }
.bar-shortcut { margin-bottom: 0; }
.bar-shortcut .shortcut-label { color: #f6e7c4; }

.skills { flex-direction: row; }
.skill { flex-grow: 1; height: 62px; margin-right: 8px; padding: 4px; align-items: center; justify-content: center; }
.skill:disabled { opacity: 0.55; }
.skill.aiming { scale: 1.08 1.08; border-color: #ff5a00; border-width: 3px; }
.skill-icon { width: 30px; height: 30px; }
.skill-text { font-size: 12px; padding: 0; -unity-text-align: middle-center; white-space: normal; }
.speed { flex-grow: 0; width: 64px; margin-right: 0; }
.hint { color: #f6e7c4; font-size: 12px; white-space: normal; margin-top: 6px; padding: 0; }
```

USS gotchas to check while implementing: `rotate`, `scale`, `background-repeat`, `background-size`, `text-shadow` and `:nth-child` — Unity 6 USS supports `rotate`/`scale`/`text-shadow`/`background-repeat`/`background-size`; it does **not** support `:nth-child`. Replace the two `.tower:nth-child` rules by adding class `tilt-left`/`tilt-right` in `HudView` (alternate per tower) with `rotate: -2deg` / `2deg`, and ledger the ruling. Check the Unity console/log for "USS" warnings after the first run and fix any unsupported property the same way.

- [ ] **Step 5: Wire `GameController`**

In `UpdateHud`, before the phase branches: `_hud.UpdateFrame(DemoEconomy.Frame(_scrap, 1));`
In the Night branch, replace the `NightHudState` construction's last argument and insert the wave numbers:

```csharp
                _hud.UpdateNight(new NightHudState(
                    _night, _battle.HearthHealth, _battle.HearthMaxHealth, leader.Health, leader.MaxHealth,
                    sluice, _battle.IsSluiceReady, voice, _battle.IsVoiceReady, stones, _battle.IsHotStonesReady || _aimingStones, _aimingStones, _speed,
                    _battle.CurrentWave, _battle.WaveCount, alive, hint));
```

Line 147 (`Announce(reason)` for a rejected build/input) becomes a Prophet quip: `_hud.Announce(reason, Time.time, prophet: true);`.

- [ ] **Step 6: Run the tests to verify they pass**

Run: PlayMode (all). Expected: PASS 13/13 (4 existing + 9 new). Run EditMode (all). Expected: PASS 48/48 (40 + 3 UiAssets + 5 HudModel).
If `Click()` via `NavigationSubmitEvent` does not trigger `Button.clicked` in this Unity version, switch the helper to
`button.clickable.GetType()` reflection-free alternative: send `PointerDownEvent`/`PointerUpEvent` at `button.worldBound.center` via `using (var e = PointerDownEvent.GetPooled(...))` — ledger it.

- [ ] **Step 7: Look at it**

Render the HUD in the editor isn't possible headless (Camera.Render excludes UI Toolkit), so build and look on the device in Task 6. Fix only blocking layout breakage there (one pass).

- [ ] **Step 8: Commit**

```bash
git add client/CultOfComfort.Unity/Assets/Scripts/Hud/HudView.cs client/CultOfComfort.Unity/Assets/Resources/UI/Hud.uss \
  client/CultOfComfort.Unity/Assets/Scripts/GameController.cs client/CultOfComfort.Unity/Assets/Tests/PlayMode
git commit -m "HUD: layout C on parchment — game frame, Tension/wave strips, locked shortcuts, sticky-note actions, settings sheet, stamped Dawn card"
```

---

### Task 6: Device check, screenshots, docs

**Files:**
- Modify: `README.md` (HUD paragraph + kit/icon commands), `docs/backlog.md`
- Create: `art/previews/hud/*.png` (device screenshots, committed for review)

**Interfaces:**
- Consumes: everything above.

- [ ] **Step 1: Full suites**

Run: `dotnet test` (solution root as in README) → 56/56; `./art/build.sh --test` → OK (38); `python3 -m unittest discover -s art/ui -p "test_*.py"` → OK (4); EditMode → 48/48; PlayMode → 13/13.

- [ ] **Step 2: Build, install, measure**

```bash
"$UNITY" -batchmode -quit -projectPath client/CultOfComfort.Unity -buildTarget Android -executeMethod CultOfComfort.Editor.BuildScripts.BuildAndroid -logFile <scratch>/build.log
adb install -r client/CultOfComfort.Unity/Builds/Android/CultOfComfort.apk && adb logcat -c && adb shell monkey -p com.lechdigital.cultofcomfort 1
```

Read `[CoC] fps` lines in Dusk (≥ 15 s) and in a Night with towers built and abilities used. Expected: ~60 fps average in both; determinism probe MATCH.

- [ ] **Step 3: Screenshots, portrait and landscape**

```bash
mkdir -p art/previews/hud
adb shell settings put system accelerometer_rotation 0
adb shell settings put system user_rotation 0   # portrait
adb exec-out screencap -p > art/previews/hud/dusk_portrait.png
# tap a locked shortcut → locked_note_portrait.png; Light the Lamps → night_portrait.png; let it end → dawn_portrait.png
adb shell settings put system user_rotation 1   # landscape; repeat the four shots as *_landscape.png
adb shell settings put system accelerometer_rotation 1   # restore auto-rotate
```

Downscale each to ≤ 900 px tall (`sips -Z 900`), look at every one, and fix only clipped/overlapping/unreadable elements (one pass, per the spec Intent). Re-run PlayMode if code changed.

- [ ] **Step 4: Docs**

README `HUD` bullet becomes: "UI Toolkit, built in code (`Assets/Scripts/Hud/HudView.cs`) and styled by `Assets/Resources/UI/Hud.uss`: layout C on parchment (frame, Tension/wave strip, locked shortcuts, sticky-note actions). Kit textures: `python3 art/ui/make_kit.py`; icons: `./art/build.sh --icons`; fonts in `Resources/UI/Fonts` (OFL / Apache 2.0)." Add `python3 -m unittest discover -s art/ui -p "test_*.py"` to the test commands.
Backlog: mark "Spec step 3: HUD kit" done (date, one line, screenshot folder); add "Replace `DemoEconomy` with real base state (buildings work)".

- [ ] **Step 5: Commit and push**

```bash
git add README.md docs/backlog.md art/previews/hud
git commit -m "HUD kit verified on the Pixel 10: screenshots, docs"
git push -u origin ui/hud-kit
```
