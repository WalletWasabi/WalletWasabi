#!/usr/bin/env python3
"""Review actual Avalonia/Skia captures; never creates replacement UI screenshots.

report: verify capture manifests, generate a gallery and compare approved baselines.
approve: explicitly enroll captures after visual review. CI never runs this command.
"""
from __future__ import annotations

import argparse
from dataclasses import asdict, dataclass
import hashlib
import html
import json
import math
from pathlib import Path
import re
import shutil
import sys
from typing import Any

from PIL import Image, ImageChops

NAME = re.compile(r"^[a-z0-9-]+\.png$")
SHA256 = re.compile(r"^[a-f0-9]{64}$")


@dataclass(frozen=True)
class Difference:
    status: str
    changed_fraction: float
    mean_error: float
    maximum_delta: int
    bounds: tuple[int, int, int, int] | None


def compare_images(actual: Image.Image, expected: Image.Image, *, pixel_tolerance: int = 8,
                   maximum_changed_fraction: float = 0.005, maximum_mean_error: float = 0.5) -> tuple[Difference, Image.Image]:
    if not 0 <= pixel_tolerance <= 255:
        raise ValueError("pixel_tolerance must be between 0 and 255")
    if not math.isfinite(maximum_changed_fraction) or not 0 <= maximum_changed_fraction <= 1:
        raise ValueError("maximum_changed_fraction must be between 0 and 1")
    if not math.isfinite(maximum_mean_error) or not 0 <= maximum_mean_error <= 255:
        raise ValueError("maximum_mean_error must be between 0 and 255")
    if actual.size != expected.size:
        return Difference("dimension-mismatch", 1.0, 255.0, 255, None), actual.convert("RGB")
    rgba = ImageChops.difference(actual.convert("RGBA"), expected.convert("RGBA"))
    channels = rgba.split()
    strongest = channels[0]
    for channel in channels[1:]:
        strongest = ImageChops.lighter(strongest, channel)
    mask = strongest.point(lambda value: 255 if value > pixel_tolerance else 0)
    pixels = actual.width * actual.height
    if pixels == 0:
        raise ValueError("Cannot compare an empty image")
    changed = mask.histogram()[255] / pixels
    mean = sum(sum(index * count for index, count in enumerate(channel.histogram())) for channel in channels) / (pixels * 4)
    maximum = strongest.getextrema()[1]
    passed = changed <= maximum_changed_fraction and mean <= maximum_mean_error
    result = Difference("passed" if passed else "different", changed, mean, maximum, mask.getbbox())
    # A diagnostic delta image, not an altered or synthesized UI capture.
    diff = strongest.point(lambda value: min(255, value * 4)).convert("RGB")
    return result, diff


def collect_frames(directory: Path) -> list[dict[str, Any]]:
    directory = directory.resolve()
    manifests = sorted(directory.glob("*.frame.json"))
    if not manifests:
        raise ValueError(f"No native frame manifests in {directory}; native rendering did not produce evidence")
    frames: list[dict[str, Any]] = []
    seen: set[str] = set()
    for manifest in manifests:
        record = json.loads(manifest.read_text(encoding="utf-8"))
        name = record.get("image", "")
        if not isinstance(name, str) or not NAME.fullmatch(name) or name in seen:
            raise ValueError(f"Invalid or duplicate image name in {manifest.name}")
        if manifest.name != name[:-4] + ".frame.json":
            raise ValueError(f"Image/manifest identity mismatch in {manifest.name}")
        seen.add(name)
        image_path = (directory / name).resolve()
        if image_path.parent != directory:
            raise ValueError(f"Capture path escapes its directory: {name}")
        if record.get("engine") != "avalonia-headless-skia":
            raise ValueError(f"{name} is not marked as an Avalonia/Skia capture")
        if hashlib.sha256(image_path.read_bytes()).hexdigest() != record.get("sha256"):
            raise ValueError(f"Capture hash mismatch for {name}")
        with Image.open(image_path) as image:
            if image.format != "PNG":
                raise ValueError(f"Capture is not a PNG: {name}")
            image.load()
            if image.size != (record.get("width"), record.get("height")):
                raise ValueError(f"Capture dimensions disagree with metadata: {name}")
            if image.width <= 0 or image.height <= 0:
                raise ValueError(f"Empty capture: {name}")
        frames.append(record)
    return frames


def raster_fingerprint(image: Image.Image) -> str:
    """Hash dimensions and decoded RGBA8 pixels, independent of PNG metadata/compression."""
    with image.convert("RGBA") as rgba:
        header = b"wasabi-rgba8-v1\0" + rgba.width.to_bytes(4, "big") + rgba.height.to_bytes(4, "big")
        return hashlib.sha256(header + rgba.tobytes()).hexdigest()


def compare_fingerprint(actual: Image.Image, approval: dict[str, Any]) -> dict[str, Any]:
    digest = approval.get("rgbaSha256")
    width, height = approval.get("width"), approval.get("height")
    if not isinstance(digest, str) or not SHA256.fullmatch(digest):
        raise ValueError("Invalid reviewed RGBA fingerprint")
    if type(width) is not int or type(height) is not int or width <= 0 or height <= 0:
        raise ValueError("Invalid reviewed fingerprint dimensions")
    actual_digest = raster_fingerprint(actual)
    status = "dimension-mismatch" if actual.size != (width, height) else "passed" if actual_digest == digest else "different"
    return {"status": status, "verification": "rgba-sha256", "actualFingerprint": actual_digest,
            "approvedFingerprint": digest, "approvedDimensions": [width, height]}


def report(actual_directory: Path, output: Path, baseline: Path | None = None, *, allow_unreviewed: bool = False,
           pixel_tolerance: int = 8, maximum_changed_fraction: float = 0.005, maximum_mean_error: float = 0.5) -> dict[str, Any]:
    frames = collect_frames(actual_directory)
    approvals: dict[str, dict[str, Any]] = {}
    if baseline is not None and (baseline / "approval.json").is_file():
        approval = json.loads((baseline / "approval.json").read_text(encoding="utf-8"))
        if approval.get("reviewed") is not True:
            raise ValueError("Baseline approval is not marked as reviewed")
        for record in approval.get("frames", []):
            name = record.get("image", "")
            if not isinstance(name, str) or not NAME.fullmatch(name) or name in approvals:
                raise ValueError("Invalid or duplicate baseline approval entry")
            if record.get("comparison") not in {None, "rgba-sha256"}:
                raise ValueError("Unknown baseline comparison method")
            approvals[name] = record
    output.mkdir(parents=True, exist_ok=True)
    for child in ("actual", "expected", "diff"):
        (output / child).mkdir(exist_ok=True)
    results: list[dict[str, Any]] = []
    for frame in frames:
        name = frame["image"]
        source = actual_directory / name
        shutil.copyfile(source, output / "actual" / name)
        result: dict[str, Any] = {"capture": frame, "status": "unreviewed"}
        expected = baseline / name if baseline else None
        if name in approvals and approvals[name].get("comparison") == "rgba-sha256":
            # Deliberately stricter than image tolerances: even one changed channel fails.
            # Approval is committed independently; this command never enrolls current output.
            with Image.open(source) as actual:
                result.update(compare_fingerprint(actual, approvals[name]))
        elif expected is not None and expected.is_file() and name in approvals:
            if expected.resolve().parent != baseline.resolve():
                raise ValueError(f"Baseline path escapes its directory: {name}")
            if hashlib.sha256(expected.read_bytes()).hexdigest() != approvals[name].get("sha256"):
                raise ValueError(f"Reviewed baseline hash mismatch for {name}")
            shutil.copyfile(expected, output / "expected" / name)
            with Image.open(source) as actual, Image.open(expected) as original:
                difference, delta = compare_images(actual, original, pixel_tolerance=pixel_tolerance,
                    maximum_changed_fraction=maximum_changed_fraction, maximum_mean_error=maximum_mean_error)
                result.update(asdict(difference))
                delta.save(output / "diff" / name)
                delta.close()
        elif expected is not None and expected.is_file():
            result["status"] = "unreviewed-baseline"
        elif baseline is not None:
            result["status"] = "missing-baseline"
        results.append(result)
    captured_names = {frame["image"] for frame in frames}
    missing = sorted(set(approvals) - captured_names)
    for name in missing:
        results.append({"capture": approvals[name], "status": "missing-capture"})
    changed = sum(item["status"] in {"different", "dimension-mismatch"} for item in results)
    unreviewed = sum(item["status"] in {"unreviewed", "missing-baseline", "unreviewed-baseline"} for item in results)
    summary = {
        "engine": "avalonia-headless-skia", "total": len(results), "captured": len(frames), "missingCaptured": len(missing), "changed": changed,
        "unreviewed": unreviewed, "status": "missing-captures" if missing else "different" if changed else "unreviewed" if unreviewed else "passed",
        "gatePassed": changed == 0 and not missing and (unreviewed == 0 or allow_unreviewed),
        "allowUnreviewed": allow_unreviewed,
        "thresholds": {"pixelTolerance": pixel_tolerance, "changedFraction": maximum_changed_fraction, "meanError": maximum_mean_error},
        "frames": results,
    }
    (output / "report.json").write_text(json.dumps(summary, indent=2, allow_nan=False) + "\n", encoding="utf-8")
    rows: list[str] = []
    for item in results:
        capture = item["capture"]
        name = html.escape(capture["image"], quote=True)
        status = html.escape(item["status"])
        reviewed = item["status"] not in {"unreviewed", "missing-baseline", "unreviewed-baseline", "missing-capture"}
        columns = f'<figure><figcaption>Native capture</figcaption><img loading="lazy" src="actual/{name}"></figure>'
        metrics = "No approved baseline. This is not a parity pass."
        if item["status"] == "missing-capture":
            columns = "<p>The test run did not produce this previously approved capture.</p>"
            metrics = "Missing required capture; the gate fails even in render-only mode."
        elif item.get("verification") == "rgba-sha256":
            matched = item["status"] == "passed"
            metrics = ("Exact decoded RGBA pixels and dimensions match the reviewed capture." if matched
                       else "Decoded pixels or dimensions differ from the reviewed capture. No tolerance is applied.")
            metrics += " Approved raster: " + item["approvedFingerprint"] + "; actual: " + item["actualFingerprint"]
        elif reviewed:
            columns += f'<figure><figcaption>Approved baseline</figcaption><img loading="lazy" src="expected/{name}"></figure>'
            columns += f'<figure><figcaption>Difference ×4</figcaption><img loading="lazy" src="diff/{name}"></figure>'
            metrics = f"Changed pixels: {item['changed_fraction']:.3%} · Mean channel error: {item['mean_error']:.3f} · Maximum: {item['maximum_delta']}"
        rows.append(f'<article data-status="{status}"><h2>{name}<span>{status}</span></h2><p>{html.escape(metrics)}</p>'
                    f'<p>{html.escape(str(capture.get("contentKind", "unknown")))} · {capture["width"]} × {capture["height"]} · '
                    f'{html.escape(str(capture.get("theme", "")))} · {html.escape(str(capture.get("commit", "")))}</p><div class="images">{columns}</div></article>')
    page = '''<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Wasabi native mobile visual review</title><style>
:root{color-scheme:dark;font:15px/1.5 system-ui;background:#101817;color:#eaf2ed}body{margin:0 auto;padding:32px;max-width:1400px}
h1{font-size:32px;margin-bottom:8px}h2{font-size:17px;overflow-wrap:anywhere}p{color:#adbbb3;overflow-wrap:anywhere}article{border:1px solid #34453b;border-radius:16px;padding:20px;margin:24px 0}
span{float:right;font-size:12px;padding:4px 10px;border-radius:24px;background:#2c3d33}.images{display:flex;gap:20px;flex-wrap:wrap}
figure{margin:0;flex:0 1 320px;min-width:200px}img{display:block;max-width:100%;height:auto;border-radius:12px}figcaption{margin:8px 0;color:#96bb9e}
button{padding:9px 16px;margin:10px 8px 0 0;border-radius:8px;border:1px solid #52675b;background:#1c2b22;color:inherit;cursor:pointer}
[data-status="different"],[data-status="dimension-mismatch"],[data-status="missing-capture"]{border-color:#c58372}.notice{border-left:3px solid #ddbd74;padding-left:16px}
</style><h1>Wasabi native mobile visual review</h1>
<p>Actual Avalonia/Skia frames. Fixture captures validate production controls and bindings, not live wallet operations.</p>
<div class="notice">SUMMARY</div><nav><button onclick="filter('all')">All frames</button><button onclick="filter('different')">Differences</button><button onclick="filter('unreviewed')">Unreviewed</button></nav>
ROWS
<script>function filter(s){document.querySelectorAll('article').forEach(a=>a.hidden=s!=='all'&&(s==='different'?!['different','dimension-mismatch','missing-capture'].includes(a.dataset.status):!['unreviewed','missing-baseline','unreviewed-baseline'].includes(a.dataset.status)))}</script></html>'''
    description = f"{len(frames)} captures · {changed} changed · {len(missing)} missing captures · {unreviewed} unreviewed. Status: {summary['status']}."
    (output / "index.html").write_text(page.replace("SUMMARY", html.escape(description)).replace("ROWS", "\n".join(rows)), encoding="utf-8")
    return summary


def approve(actual_directory: Path, baseline: Path, *, reviewed: bool, replace: bool = False,
            fingerprints_only: bool = False) -> int:
    if not reviewed:
        raise ValueError("Review the native captures before enrolling them; --reviewed is required")
    frames = collect_frames(actual_directory)
    previous: dict[str, dict[str, Any]] = {}
    if (baseline / "approval.json").is_file():
        approval = json.loads((baseline / "approval.json").read_text(encoding="utf-8"))
        if approval.get("reviewed") is not True:
            raise ValueError("Existing baseline approval is invalid")
        previous = {frame["image"]: frame for frame in approval.get("frames", [])}
    conflicts = [frame["image"] for frame in frames if frame["image"] in previous or (baseline / frame["image"]).exists()]
    if conflicts and not replace:
        raise ValueError("Existing baselines are protected. Use --replace only after reviewing the changes")
    baseline.mkdir(parents=True, exist_ok=True)
    for frame in frames:
        destination = baseline / frame["image"]
        if destination.is_symlink():
            raise ValueError(f"Refusing baseline symlink: {frame['image']}")
        enrolled = dict(frame)
        if fingerprints_only:
            with Image.open(actual_directory / frame["image"]) as image:
                enrolled.update(comparison="rgba-sha256", rgbaSha256=raster_fingerprint(image))
        else:
            shutil.copyfile(actual_directory / frame["image"], destination)
        previous[frame["image"]] = enrolled
    (baseline / "approval.json").write_text(json.dumps({"reviewed": True,
        "reviewScope": "Native rendering regression; not a certification of bitmap-reference parity or wallet security.",
        "frames": [previous[name] for name in sorted(previous)]}, indent=2) + "\n", encoding="utf-8")
    return len(frames)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="command", required=True)
    review = sub.add_parser("report")
    review.add_argument("--actual", type=Path, required=True)
    review.add_argument("--output", type=Path, required=True)
    review.add_argument("--baseline", type=Path)
    review.add_argument("--allow-unreviewed", action="store_true")
    review.add_argument("--pixel-tolerance", type=int, default=8)
    review.add_argument("--maximum-changed-fraction", type=float, default=0.005)
    review.add_argument("--maximum-mean-error", type=float, default=0.5)
    enroll = sub.add_parser("approve")
    enroll.add_argument("--actual", type=Path, required=True)
    enroll.add_argument("--baseline", type=Path, required=True)
    enroll.add_argument("--reviewed", action="store_true")
    enroll.add_argument("--replace", action="store_true")
    enroll.add_argument("--fingerprints-only", action="store_true", help="Store exact decoded-pixel hashes instead of binary PNG baselines")
    args = parser.parse_args()
    try:
        if args.command == "approve":
            print(f"Enrolled {approve(args.actual, args.baseline, reviewed=args.reviewed, replace=args.replace, fingerprints_only=args.fingerprints_only)} reviewed captures")
            return 0
        result = report(args.actual, args.output, args.baseline, allow_unreviewed=args.allow_unreviewed,
                        pixel_tolerance=args.pixel_tolerance, maximum_changed_fraction=args.maximum_changed_fraction,
                        maximum_mean_error=args.maximum_mean_error)
        print(json.dumps({key: value for key, value in result.items() if key != "frames"}, indent=2))
        return 0 if result["gatePassed"] else 1
    except (OSError, ValueError, KeyError, TypeError) as error:
        print(f"Visual review failed: {error}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
