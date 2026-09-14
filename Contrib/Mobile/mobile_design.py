#!/usr/bin/env python3
"""Validate native mobile resource structure and export its source-of-truth tokens.

No dependencies beyond Python 3.10+. This is not a C# or compiled-XAML validator.
"""
from __future__ import annotations

import argparse
import json
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


def local_name(tag: str) -> str:
    return tag.rsplit("}", 1)[-1]


def read_value(element: ET.Element) -> dict[str, Any]:
    kind = local_name(element.tag)
    if kind == "SolidColorBrush":
        color = element.get("Color", (element.text or "").strip())
        if not re.fullmatch(r"#[0-9a-fA-F]{6}(?:[0-9a-fA-F]{2})?", color):
            raise ValueError(f"Invalid solid color for {element.get(KEY)}: {color!r}")
        return {"type": "color", "value": color.upper()}
    if kind == "LinearGradientBrush":
        stops = [
            {"color": stop.attrib["Color"].upper(), "offset": float(stop.attrib["Offset"])}
            for stop in element.findall("a:GradientStop", NS)
        ]
        if not stops or any(not 0 <= stop["offset"] <= 1 for stop in stops):
            raise ValueError(f"Invalid gradient stops for {element.get(KEY)}")
        if stops != sorted(stops, key=lambda stop: stop["offset"]):
            raise ValueError(f"Unsorted gradient stops for {element.get(KEY)}")
        return {
            "type": "linearGradient",
            "value": {"start": element.get("StartPoint"), "end": element.get("EndPoint"), "stops": stops},
        }
    value: Any = (element.text or "").strip()
    if kind == "Double":
        value = float(value)
    elif kind == "Int32":
        value = int(value)
    elif kind == "Boolean":
        value = value.lower() == "true"
    return {"type": kind, "value": value}


def tokens_from_dictionary(dictionary: ET.Element) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for element in dictionary:
        key = element.get(KEY)
        if key is None:
            continue
        if key in result:
            raise ValueError(f"Duplicate token {key}")
        result[key] = read_value(element)
    return result


def validate(root: Path) -> dict[str, Any]:
    mobile = root / "WalletWasabi.Fluent" / "Mobile"
    theme_path = mobile / "Styles" / "MobileTheme.axaml"
    theme = ET.parse(theme_path).getroot()
    resources = theme.find("a:Styles.Resources/a:ResourceDictionary", NS)
    if resources is None:
        raise ValueError("Native theme resource dictionary is missing")
    variants = resources.find("a:ResourceDictionary.ThemeDictionaries", NS)
    if variants is None:
        raise ValueError("Native theme variants are missing")
    themes = {variant.attrib[KEY]: tokens_from_dictionary(variant) for variant in variants}
    if set(themes) != {"Light", "Dark"}:
        raise ValueError("Both Light and Dark variants are required")
    if set(themes["Light"]) != set(themes["Dark"]):
        raise ValueError("Light and Dark semantic token keys differ")
    shared = tokens_from_dictionary(resources)
    sources = sorted(mobile.rglob("*.axaml"))
    available: set[str] = set()
    references: list[tuple[Path, str]] = []
    view_classes: list[str] = []
    for path in sources:
        text = path.read_text(encoding="utf-8")
        document = ET.fromstring(text)
        available.update(key for node in document.iter() if (key := node.get(KEY)) is not None)
        references.extend((path, key) for key in TOKEN_REFERENCE.findall(text))
        if document.get(CLASS):
            view_classes.append(document.attrib[CLASS])
    missing = [(str(path.relative_to(root)), key) for path, key in references if key not in available]
    if missing:
        raise ValueError(f"Undefined mobile resource references: {missing}")
    cs_sources = "\n".join(path.read_text(encoding="utf-8") for path in mobile.rglob("*.cs"))
    for name in view_classes:
        simple_name = name.rsplit(".", 1)[-1]
        if not re.search(rf"\bclass\s+{re.escape(simple_name)}\b", cs_sources):
            raise ValueError(f"No native C# view class for {name}")
    if "WebView" in cs_sources or "WebView" in "\n".join(path.read_text(encoding="utf-8") for path in sources):
        raise ValueError("The mobile design must remain native; WebView content was found")
    return {
        "schema": "wasabi.mobile.native-tokens.v1",
        "source": str(theme_path.relative_to(root)),
        "dimensionUnits": "Avalonia device-independent pixels",
        "shared": shared,
        "themes": themes,
        "structure": {"xamlFiles": len(sources), "viewClasses": sorted(view_classes), "resourceReferences": len(references)},
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument("--check", action="store_true", help="Validate without creating an export")
    parser.add_argument("--export", type=Path, help="Write deterministic native-token JSON")
    args = parser.parse_args()
    try:
        result = validate(args.root.resolve())
        if args.export:
            args.export.parent.mkdir(parents=True, exist_ok=True)
            args.export.write_text(json.dumps(result, indent=2, sort_keys=True) + "\n", encoding="utf-8")
        print(f"Resource structure valid: {len(result['shared'])} shared tokens, {len(result['themes']['Light'])} per theme, {result['structure']['xamlFiles']} XAML files.")
        print("C# compilation, compiled binding validation, rendering and device tests are separate checks.")
        return 0
    except (OSError, ValueError, KeyError, ET.ParseError) as error:
        print(f"Mobile design validation failed: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
