#!/usr/bin/env python3
"""Validate native mobile resources and export their declarative design tokens.

Uses Python 3.10+ only. This does not compile C#, XAML bindings, or native views.
"""
from __future__ import annotations

import argparse
import json
import math
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path
from typing import Any

AVALONIA = "https://github.com/avaloniaui"
XAML = "http://schemas.microsoft.com/winfx/2006/xaml"
NS = {"a": AVALONIA, "x": XAML}
KEY = f"{{{XAML}}}Key"
CLASS = f"{{{XAML}}}Class"
TOKEN_REFERENCE = re.compile(r"\{(?:Static|Dynamic)Resource\s+(Mobile[A-Za-z0-9]+)\s*\}")
TOKEN_TYPES = frozenset({
    "SolidColorBrush", "LinearGradientBrush", "Color", "Double", "Int32", "Boolean",
    "String", "Thickness", "CornerRadius", "FontFamily", "FontWeight", "TimeSpan",
})


def local_name(tag: str) -> str:
    return tag.rsplit("}", 1)[-1]


def parse_color(value: str, key: str | None) -> str:
    if not re.fullmatch(r"#[0-9a-fA-F]{6}(?:[0-9a-fA-F]{2})?", value):
        raise ValueError(f"Invalid color for {key}: {value!r}; use #RRGGBB or #AARRGGBB")
    return value.upper()


def read_value(element: ET.Element) -> dict[str, Any]:
    kind = local_name(element.tag)
    key = element.get(KEY)
    value: Any = (element.text or "").strip()
    if kind in {"SolidColorBrush", "Color"}:
        color = parse_color(element.get("Color", value), key)
        return {"type": "color" if kind == "SolidColorBrush" else "Color", "value": color}
    if kind == "LinearGradientBrush":
        stops = [
            {"color": parse_color(stop.get("Color", ""), key), "offset": float(stop.get("Offset", ""))}
            for stop in element.findall("a:GradientStop", NS)
        ]
        if not stops or any(not math.isfinite(stop["offset"]) or not 0 <= stop["offset"] <= 1 for stop in stops):
            raise ValueError(f"Invalid gradient stops for {key}")
        if stops != sorted(stops, key=lambda stop: stop["offset"]):
            raise ValueError(f"Unsorted gradient stops for {key}")
        return {
            "type": "linearGradient",
            "value": {"start": element.get("StartPoint"), "end": element.get("EndPoint"), "stops": stops},
        }
    if kind == "Double":
        value = float(value)
        if not math.isfinite(value):
            raise ValueError(f"Nonfinite design token {key}")
    elif kind == "Int32":
        value = int(value)
        if not -(2 ** 31) <= value < 2 ** 31:
            raise ValueError(f"Int32 token out of range: {key}")
    elif kind == "Boolean":
        if value.lower() not in {"true", "false"}:
            raise ValueError(f"Invalid Boolean token {key}: {value!r}")
        value = value.lower() == "true"
    elif kind in {"Thickness", "CornerRadius"}:
        components = re.split(r"\s*,\s*|\s+", value)
        if len(components) not in {1, 2, 4} or any(not math.isfinite(float(part)) for part in components):
            raise ValueError(f"Invalid {kind} token {key}: {value!r}")
    return {"type": kind, "value": value}


def collect_tokens(documents: dict[Path, ET.Element], root: Path) -> dict[str, Any]:
    shared: dict[str, Any] = {}
    themes: dict[str, dict[str, Any]] = {"Light": {}, "Dark": {}}
    origins: dict[str, dict[str, str]] = {"shared": {}, "Light": {}, "Dark": {}}
    sources: set[str] = set()

    def visit(dictionary: ET.Element, scope: str, path: Path) -> None:
        for element in dictionary:
            kind = local_name(element.tag)
            if kind == "ResourceDictionary.ThemeDictionaries":
                for variant in element:
                    name = variant.get(KEY)
                    if name not in themes:
                        raise ValueError(f"Unsupported mobile theme {name!r} in {path.name}")
                    visit(variant, name, path)
            elif kind in {"ResourceDictionary", "ResourceDictionary.MergedDictionaries"}:
                visit(element, scope, path)
            elif kind in TOKEN_TYPES and (key := element.get(KEY, "")).startswith("Mobile"):
                target = shared if scope == "shared" else themes[scope]
                source = path.relative_to(root).as_posix()
                if key in target:
                    raise ValueError(f"Duplicate {scope} token {key}: {origins[scope][key]} and {source}")
                target[key] = read_value(element)
                origins[scope][key] = source
                sources.add(source)

    for path, document in sorted(documents.items()):
        if not path.is_relative_to(root / "WalletWasabi.Fluent" / "Mobile" / "Styles"):
            continue
        if local_name(document.tag) == "ResourceDictionary":
            visit(document, "shared", path)
        else:
            for resources in document.findall("a:Styles.Resources", NS):
                visit(resources, "shared", path)
    if not themes["Light"] or not themes["Dark"]:
        raise ValueError("Both Light and Dark native theme tokens are required")
    if set(themes["Light"]) != set(themes["Dark"]):
        raise ValueError("Light and Dark semantic token keys differ")
    for key in themes["Light"]:
        if themes["Light"][key]["type"] != themes["Dark"][key]["type"]:
            raise ValueError(f"Light and Dark token types differ for {key}")
    return {"shared": shared, "themes": themes, "tokenSources": origins, "sources": sorted(sources)}


def validate(root: Path) -> dict[str, Any]:
    root = root.resolve()
    mobile = root / "WalletWasabi.Fluent" / "Mobile"
    theme_path = mobile / "Styles" / "MobileTheme.axaml"
    if not theme_path.is_file():
        raise ValueError("Native MobileTheme.axaml is missing")
    texts = {path: path.read_text(encoding="utf-8") for path in sorted(mobile.rglob("*.axaml"))}
    documents = {path: ET.fromstring(text) for path, text in texts.items()}
    tokens = collect_tokens(documents, root)
    available: set[str] = set()
    references: list[tuple[Path, str]] = []
    view_classes: list[str] = []
    for path, document in documents.items():
        available.update(key for node in document.iter() if (key := node.get(KEY)) is not None)
        references.extend((path, key) for key in TOKEN_REFERENCE.findall(texts[path]))
        if name := document.get(CLASS):
            view_classes.append(name)
    missing = [(path.relative_to(root).as_posix(), key) for path, key in references if key not in available]
    if missing:
        raise ValueError(f"Undefined mobile resource references: {missing}")
    cs_sources = "\n".join(path.read_text(encoding="utf-8") for path in sorted(mobile.rglob("*.cs")))
    for name in view_classes:
        simple_name = name.rsplit(".", 1)[-1]
        if not re.search(rf"\bclass\s+{re.escape(simple_name)}\b", cs_sources):
            raise ValueError(f"No native C# view class for {name}")
    if "WebView" in cs_sources or any("WebView" in text for text in texts.values()):
        raise ValueError("The mobile design must remain native; WebView content was found")
    return {
        "schema": "wasabi.mobile.native-tokens.v1",
        "source": theme_path.relative_to(root).as_posix(),
        "dimensionUnits": "Avalonia device-independent pixels",
        **tokens,
        "structure": {"xamlFiles": len(documents), "viewClasses": sorted(view_classes), "resourceReferences": len(references)},
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument("--check", action="store_true", help="Validate without requiring an export")
    parser.add_argument("--export", type=Path, help="Write deterministic native-token JSON")
    args = parser.parse_args()
    try:
        result = validate(args.root)
        if args.export:
            serialized = json.dumps(result, indent=2, sort_keys=True, allow_nan=False) + "\n"
            args.export.parent.mkdir(parents=True, exist_ok=True)
            args.export.write_text(serialized, encoding="utf-8")
        print(f"Resource structure valid: {len(result['shared'])} shared tokens, {len(result['themes']['Light'])} per theme, {result['structure']['xamlFiles']} XAML files.")
        print("C# compilation, compiled binding validation, rendering and device tests are separate checks.")
        return 0
    except (OSError, ValueError, KeyError, ET.ParseError) as error:
        print(f"Mobile design validation failed: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
