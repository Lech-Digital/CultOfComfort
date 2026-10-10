#!/usr/bin/env bash
# Cult of Comfort art pipeline. Usage:
#   ./art/build.sh prophet            build one asset (export + previews)
#   ./art/build.sh --all              build every asset in art/blender/models
#   ./art/build.sh --no-preview NAME  export only
#   ./art/build.sh --test             run the pipeline tests inside Blender
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
BLENDER="${BLENDER:-$HOME/Applications/Blender.app/Contents/MacOS/Blender}"

if [[ ! -x "$BLENDER" ]]; then
  echo "Blender not found at: $BLENDER" >&2
  echo "Install Blender 4.5 LTS (README → Art pipeline) or set BLENDER=/path/to/Blender." >&2
  exit 2
fi

if [[ "${1:-}" == "--test" ]]; then
  exec "$BLENDER" -b --factory-startup --python-exit-code 1 -P "$HERE/blender/tests/run.py"
fi

exec "$BLENDER" -b --factory-startup --python-exit-code 1 -P "$HERE/blender/build.py" -- "$@"
