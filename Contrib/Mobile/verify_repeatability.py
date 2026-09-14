#!/usr/bin/env python3
"""Fail on cross-process native raster drift. This never enrolls a baseline."""
from __future__ import annotations

import argparse
import json
from pathlib import Path
import sys
from PIL import Image
from visual_review import collect_frames, raster_fingerprint

CONTEXT = ("engine", "contentKind", "scenario", "width", "height", "dipWidth", "dipHeight",
           "scale", "theme", "culture", "timezone", "commit", "motion", "caret")


def verify(first: Path, second: Path, output: Path) -> dict:
    if first.resolve() == second.resolve():
        raise ValueError("Repeatability requires two independent capture directories")
    left = {f["image"]: f for f in collect_frames(first)}
    right = {f["image"]: f for f in collect_frames(second)}
    differences = []
    for name in sorted(left.keys() | right.keys()):
        if name not in left or name not in right:
            differences.append({"image": name, "reason": "missing-capture"})
            continue
        context_changes = [key for key in CONTEXT if left[name].get(key) != right[name].get(key)]
        with Image.open(first / name) as a, Image.open(second / name) as b:
            a_hash, b_hash = raster_fingerprint(a), raster_fingerprint(b)
        if context_changes or a_hash != b_hash:
            differences.append({"image": name, "reason": "context-drift" if context_changes else "pixel-drift",
                                "contextChanges": context_changes, "firstRaster": a_hash, "secondRaster": b_hash})
    result = {"gatePassed": not differences, "firstCount": len(left), "secondCount": len(right),
              "comparison": "exact-rgba-and-context", "differences": differences}
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    return result


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("first", type=Path)
    parser.add_argument("second", type=Path)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    try:
        result = verify(args.first, args.second, args.output)
    except (OSError, ValueError) as error:
        print(f"Repeatability validation failed: {error}", file=sys.stderr)
        return 1
    print(f"Cross-process raster comparison: {result['firstCount']} / {result['secondCount']} frames; "
          f"{len(result['differences'])} differences")
    return 0 if result["gatePassed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
