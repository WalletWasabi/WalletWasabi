"""Verify packaged native hashes and SDK/loader data-container compatibility."""
import argparse
import hashlib
import json
from pathlib import Path
import struct
import zipfile


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--apk", type=Path, required=True)
    parser.add_argument("--manifest", type=Path, required=True)
    parser.add_argument("--configuration", choices=("Debug", "Release"), required=True)
    args = parser.parse_args()
    manifest = json.loads(args.manifest.read_text())
    checked = containers = 0
    with zipfile.ZipFile(args.apk) as archive:
        abis = {name.split("/")[1] for name in archive.namelist() if name.startswith("lib/")}
        for item in manifest["files"]:
            if item["configuration"] != args.configuration or item["archivePath"].split("/")[1] not in abis:
                continue
            if hashlib.sha256(archive.read(item["archivePath"])).hexdigest() != item["sha256"]:
                raise ValueError("Packaged native library differs: " + item["archivePath"])
            checked += 1
        for name in archive.namelist():
            if not name.startswith("lib/") or not name.endswith(".so"):
                continue
            data = archive.read(name)
            offset = struct.unpack_from("<Q", data, 40)[0]
            size, count, names_index = struct.unpack_from("<HHH", data, 58)
            sections = [struct.unpack_from("<IIQQQQIIQQ", data, offset + i * size) for i in range(count)]
            names_section = sections[names_index]
            names = data[names_section[4]:names_section[4] + names_section[5]]
            payloads = [(i, section) for i, section in enumerate(sections) if names[section[0]:names.index(0, section[0])] == b"payload"]
            if not payloads:
                continue
            layout = manifest["assemblyContainerLayouts"][name.split("/")[1]]
            if len(payloads) != 1 or payloads[0][0] != count - 1 or count != layout["sectionHeaderEntryCount"] or size != layout["sectionHeaderEntrySize"] or payloads[0][1][4] != layout["payloadSectionOffset"]:
                raise ValueError("Assembly data container does not match native loader: " + name)
            containers += 1
    if checked == 0 or containers == 0:
        raise ValueError("Native or assembly payload verification did not execute")
    print(f"PASS: {checked} packaged native hashes and {containers} assembly container layouts")


if __name__ == "__main__":
    main()
