"""Stage pinned native builds and check ELF protections before APK packaging.

Run on Linux after build.py. Never modifies SDK packs or ELF program headers.
"""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import re
import struct
import subprocess
import xml.etree.ElementTree as ET


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def check_elf(path, abi):
    data = path.read_bytes()
    if data[:6] != b"\x7fELF\x02\x01" or struct.unpack_from("<HH", data, 16) != (3, 183 if abi == "arm64-v8a" else 62):
        raise ValueError(f"Wrong ELF architecture/type: {path}")
    offset = struct.unpack_from("<Q", data, 32)[0]
    size, count = struct.unpack_from("<HH", data, 54)
    segments = [struct.unpack_from("<IIQQQQQQ", data, offset + i * size) for i in range(count)]
    relro = stack = now = False
    for kind, flags, file_offset, address, _, file_size, memory_size, alignment in segments:
        if kind == 1 and (alignment < 16384 or file_offset % 16384 != address % 16384):
            raise ValueError(f"LOAD alignment: {path}")
        if kind == 0x6474E552:
            if (address + memory_size) % 16384:
                raise ValueError(f"RELRO alignment: {path}")
            relro = True
        if kind == 0x6474E551:
            if flags & 1:
                raise ValueError(f"Executable stack: {path}")
            stack = True
        if kind == 2:
            for i in range(file_offset, file_offset + file_size, 16):
                tag, value = struct.unpack_from("<QQ", data, i)
                now |= tag == 24 or tag == 30 and bool(value & 8) or tag == 0x6FFFFFFB and bool(value & 1)
    if not (relro and stack and now):
        raise ValueError(f"Missing RELRO/NOW/nonexecutable stack: {path}")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--work", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    directory = Path(__file__).resolve().parent
    lock_path = directory / "source.lock.json"
    lock = json.loads(lock_path.read_text())
    if digest(directory / "runtime-tls-reentrancy.patch") != lock["tlsPatchSha256"]:
        raise ValueError("TLS patch differs from the source lock")
    root, output = args.work.resolve(), args.output.resolve()
    layouts = json.loads((root / "archive-layout.json").read_text())
    output.mkdir(parents=True, exist_ok=True)
    strip = root / "android-ndk-r29/toolchains/llvm/prebuilt/linux-x86_64/bin/llvm-strip"
    props = ET.Element("Project")
    files = []
    libraries = ["libSystem.Globalization.Native.so", "libSystem.IO.Compression.Native.so", "libSystem.Native.so", "libSystem.Security.Cryptography.Native.Android.so"]
    for configuration in ("Debug", "Release"):
        group = ET.SubElement(props, "ItemGroup", Condition=f"'$(Configuration)' == '{configuration}'")
        for arch, abi in (("x64", "x86_64"), ("arm64", "arm64-v8a")):
            header = (root / f"sdk-native-{configuration}-android-{arch}/mono/monodroid/include/archive-dso-stub-config.hh").read_text()
            for name, expected in (("SectionHeaderEntryCount", layouts[abi]["sectionHeaderEntryCount"]), ("SectionHeaderEntrySize", layouts[abi]["sectionHeaderEntrySize"]), ("PayloadSectionOffset", layouts[abi]["payloadSectionOffset"])):
                match = re.search(name + r" = (\d+)uz", header)
                if match is None or int(match.group(1)) != expected:
                    raise ValueError("Loader/SDK assembly container layout mismatch: " + str(abi))
            framework = [(root / f"runtime/artifacts/bin/native/net10.0-android-Release-{arch}" / name, name) for name in libraries]
            mono = root / f"runtime/artifacts/bin/mono/android.{arch}.Release"
            framework += [(mono / name, name) for name in ("libmonosgen-2.0.so", "libmono-component-marshal-ilgen.so")]
            if configuration == "Debug":
                framework.append((mono / "libmono-component-debugger.so", "libmono-component-debugger.so"))
            name = f"libmono-android.{configuration.lower()}.so"
            framework.append((root / f"android-clean-out/{configuration}/android-{arch}" / name, "libmonodroid.so"))
            native = [(root / "tor-built" / abi / "libtor.so", "libtor.so"), (root / "sqlite-built" / abi / "libe_sqlite3.so", "libe_sqlite3.so")]
            for kind, sources in (("Framework", framework), ("Native", native)):
                for source, archive_name in sources:
                    target = output / configuration / abi / source.name
                    check_elf(source, abi)
                    target.parent.mkdir(parents=True, exist_ok=True)
                    shutil.copyfile(source, target)
                    subprocess.run([str(strip), "--strip-unneeded", str(target)], check=True)
                    check_elf(target, abi)
                    relative = target.relative_to(output).as_posix()
                    item = ET.SubElement(group, "WasabiSource" + kind, Include="$(MSBuildThisFileDirectory)" + relative)
                    ET.SubElement(item, "Abi").text = abi
                    ET.SubElement(item, "ArchiveFileName").text = archive_name
                    ET.SubElement(item, "ExpectedSha256").text = digest(target)
                    files.append({"path": relative, "archivePath": f"lib/{abi}/{archive_name}", "configuration": configuration,
                                  "sha256": digest(target), "unstrippedSha256": digest(source), "size": target.stat().st_size})
    manifest = {"schema": 1, "sourceLockSha256": digest(lock_path), "sources": lock, "assemblyContainerLayouts": layouts,
                "recipeSha256": {name: digest(directory / name) for name in ("build.py", "stage.py", "verify-package.py", "runtime-tls-reentrancy.patch")}, "files": files}
    (output / "native-manifest.json").write_text(json.dumps(manifest, indent=2) + "\n")
    if hasattr(ET, "indent"):
        ET.indent(props)
    ET.ElementTree(props).write(output / "native-libraries.props", encoding="utf-8", xml_declaration=True)
    print(f"PASS: staged and checked {len(files)} native libraries")


if __name__ == "__main__":
    main()
