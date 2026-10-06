"""Inspect a personal APK without installing it or reading wallet data.

Requires Python 3.9+, lz4==4.4.4, JDK 21 and Android build-tools 36.0.0.
The LZ4 dependency is used only to inspect SDK-compressed managed assemblies.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import struct
import subprocess
import zipfile
import xml.etree.ElementTree as ElementTree
from datetime import datetime, timezone

import lz4.block


def sha(data):
    return hashlib.sha256(data).hexdigest()


def run(arguments, env=None):
    return subprocess.run(arguments, check=True, capture_output=True, text=True,
                          encoding="utf-8", errors="replace", env=env).stdout


def elf(data):
    if data[:6] != b"\x7fELF\x02\x01":
        raise ValueError("Expected a little-endian ELF64 library")
    offset = struct.unpack_from("<Q", data, 32)[0]
    size, count = struct.unpack_from("<HH", data, 54)
    segments = [struct.unpack_from("<IIQQQQQQ", data, offset + i * size) for i in range(count)]
    for kind, _, file_offset, address, _, _, memory_size, alignment in segments:
        if kind == 1 and (alignment < 16384 or file_offset % 16384 != address % 16384):
            raise ValueError("ELF LOAD segment is not aligned for 16 KB pages")
    section_offset = struct.unpack_from("<Q", data, 40)[0]
    section_size, section_count, names_index = struct.unpack_from("<HHH", data, 58)
    sections = [struct.unpack_from("<IIQQQQIIQQ", data, section_offset + i * section_size) for i in range(section_count)]
    names_section = sections[names_index]
    names = data[names_section[4]:names_section[4] + names_section[5]]
    payload = None
    for section in sections:
        name = names[section[0]:names.index(0, section[0])]
        if name == b"payload":
            payload = data[section[4]:section[4] + section[5]]
            if payload[:4] == b"XALZ":
                length = struct.unpack_from("<I", payload, 8)[0]
                if length > 64 * 1024 * 1024:
                    raise ValueError("Unexpectedly large assembly payload")
                payload = lz4.block.decompress(payload[12:], uncompressed_size=length)
    # Assembly wrappers are data containers read by Mono, not dlopen() code.
    # Native code needs page-aligned RELRO boundaries as well as LOAD alignment.
    if payload is None:
        relro = stack = now = False
        for kind, _, _, address, _, _, memory_size, _ in segments:
            if kind == 0x6474E552 and (address + memory_size) % 16384:
                raise ValueError("Native ELF RELRO end is not aligned for 16 KB pages")
        for kind, flags, file_offset, _, _, file_size, _, _ in segments:
            if kind == 0x6474E552:
                relro = True
            if kind == 0x6474E551:
                if flags & 1:
                    raise ValueError("Native ELF stack is executable")
                stack = True
            if kind == 2:
                for offset in range(file_offset, file_offset + file_size, 16):
                    tag, value = struct.unpack_from("<QQ", data, offset)
                    now |= tag == 24 or tag == 30 and bool(value & 8) or tag == 0x6FFFFFFB and bool(value & 1)
        if not (relro and stack and now):
            raise ValueError("Native ELF lacks RELRO, immediate binding or stack protection")
    return payload


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("apk", type=Path)
    parser.add_argument("--sdk", type=Path, required=True)
    parser.add_argument("--java", type=Path, required=True, help="JDK bin/java executable")
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--require-clean", action="store_true")
    parser.add_argument("--native-manifest", type=Path, required=True, help="Manifest of the selected pinned source build")
    args = parser.parse_args()
    repository = Path(__file__).resolve().parents[2]
    os.chdir(repository)
    dirty = bool(run(["git", "status", "--porcelain"]).strip())
    if args.require_clean and dirty:
        raise ValueError("Commit intended changes before recording delivery provenance")
    tools = args.sdk / "build-tools/36.0.0"
    executable = ".exe" if os.name == "nt" else ""
    manifest = run([str(tools / ("aapt2" + executable)), "dump", "xmltree", str(args.apk), "--file", "AndroidManifest.xml"])
    if 'package="io.wasabiwallet.android.personal"' not in manifest or "E: instrumentation" in manifest:
        raise ValueError("Wrong package or instrumentation in the personal APK")
    for name in ("allowBackup", "fullBackupContent"):
        if not re.search(rf":{name}\([^\n]*\)=false", manifest):
            raise ValueError(f"Release manifest must disable {name}")
    if re.search(r":debuggable\([^\n]*\)=true", manifest) or "targetSdkVersion(0x01010270)=36" not in manifest:
        raise ValueError("Debuggable APK or unexpected target SDK")
    badging = run([str(tools / ("aapt2" + executable)), "dump", "badging", str(args.apk)])
    version = re.search(r"package: name='io.wasabiwallet.android.personal' versionCode='([0-9]+)' versionName='([^']+)'", badging)
    project = ElementTree.parse(repository / "WalletWasabi.Android/WalletWasabi.Android.csproj").getroot()
    expected_version = tuple(project.findtext("./PropertyGroup/" + name) for name in ("ApplicationVersion", "ApplicationDisplayVersion"))
    if not version or None in expected_version or (version[1], version[2]) != expected_version:
        raise ValueError("Personal APK version differs from the recorded source project")
    certificate = run([str(args.java), "-jar", str(tools / "lib/apksigner.jar"), "verify", "--verbose", "--print-certs", str(args.apk)])
    digest = re.search(r"Signer #1 certificate SHA-256 digest: ([a-f0-9]+)", certificate)
    expected = "894b7317cd04fa09534d9bd0e8d09b08bf90e9cad6718e9020f33a6fd3f7db48"
    if not digest or digest[1] != expected or "Verified using v2 scheme (APK Signature Scheme v2): true" not in certificate or "Verified using v3 scheme (APK Signature Scheme v3): true" not in certificate:
        raise ValueError("APK does not have the recorded personal signing identity and v2/v3 signatures")
    run([str(tools / ("zipalign" + executable)), "-c", "-P", "16", "-v", "4", str(args.apk)])
    entries, assemblies, native, failed_checks = [], [], [], []
    forbidden = ("WalletInstrumentation", "WASABI_RELEASE_HARNESS", "StoreFixtureSecret", "RetrieveFixtureSecret", "synthetic-keystore-secret-", "wasabi-android-regtest", "WasabiAndroidRegtest", "public android test passphrase", "public native Android test passphrase", "RuntimeProbe", "coinjoin-interrupted-fixture.json", "disruption-ready.txt", "CHECKPOINT: synthetic CoinJoin signing", "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about")
    source_native = json.loads(args.native_manifest.read_text())
    recipes = repository / "Contrib/Android/NativeSources"
    if sha((recipes / "source.lock.json").read_bytes()) != source_native["sourceLockSha256"] or json.loads((recipes / "source.lock.json").read_text()) != source_native["sources"]:
        raise ValueError("Native sources differ from the checked-in lock")
    for name, expected in source_native["recipeSha256"].items():
        if sha((recipes / name).read_bytes()) != expected:
            raise ValueError("Native build recipe differs: " + name)
    with zipfile.ZipFile(args.apk) as archive:
        for item in source_native["files"]:
            if item["configuration"] == "Release" and sha(archive.read(item["archivePath"])) != item["sha256"]:
                raise ValueError("Packaged native code differs from the verified source artifact: " + item["archivePath"])
        bootstrap = json.loads(archive.read("assets/PersonalCoordinator.json"))
        if set(bootstrap) != {"Coordinator", "CoordinatorIdentifier"}:
            raise ValueError("Unapproved coordinator bootstrap fields")
        for item in archive.infolist():
            data = archive.read(item)
            entries.append({"path": item.filename, "size": len(data), "sha256": sha(data)})
            if item.filename.startswith("lib/") and item.filename.endswith(".so"):
                try:
                    payload = elf(data)
                except ValueError as error:
                    failed_checks.append(f"{item.filename}: {error}")
                    payload = None
                if payload is None:
                    native.append(item.filename)
                else:
                    for marker in forbidden:
                        if marker.encode() in payload or marker.encode("utf-16-le") in payload:
                            raise ValueError(f"Test marker {marker} found in {item.filename}")
                    name = item.filename.split("/")[-1][4:-3]
                    assemblies.append({"path": item.filename, "assembly": name, "sha256": sha(payload)})
                    if name in ("WalletWasabi.dll", "WalletWasabi.Client.dll", "WalletWasabi.Mobile.dll", "WalletWasabi.Android.dll", "NBitcoin.dll", "NBitcoin.Secp256k1.dll", "WabiSabi.dll", "System.Private.CoreLib.dll", "System.Net.Security.dll", "System.Security.Cryptography.dll", "Mono.Android.dll"):
                        # Compare the payload to the SDK's prepared asset, after
                        # its normal PE/runtime-reference processing.
                        abi = item.filename.split("/")[1]
                        original = repository / "WalletWasabi.Android/obj/Release/net10.0-android36.0/android/assets" / abi / name
                        if not original.is_file() or sha(original.read_bytes()) != sha(payload):
                            raise ValueError(f"Packaged assembly differs from build output: {name}")
    report = {
        "checkedUtc": datetime.now(timezone.utc).isoformat(),
        "sourceCommit": run(["git", "rev-parse", "HEAD"]).strip(),
        "sourceTree": run(["git", "rev-parse", "HEAD^{tree}"]).strip(),
        "workingTreeModified": dirty,
        "apk": args.apk.name, "apkSha256": sha(args.apk.read_bytes()),
        "version": version[2], "versionCode": int(version[1]),
        "screenshotsAllowed": True,
        "personalNodeSupported": False,
        "certificateSha256": digest[1], "signatures": ["v2", "v3"],
        "toolchain": json.loads((repository / "Contrib/Android/toolchain.json").read_text()),
        "coordinatorBootstrap": bootstrap,
        "tor": json.loads((repository / "WalletWasabi.Android/Native/tor.lock.json").read_text()),
        "nativeSourceBuild": source_native,
        "dependencies": json.loads((repository / "WalletWasabi.Android/packages.lock.json").read_text()),
        "nativeLibraries": native, "managedAssemblies": assemblies, "apkEntries": entries,
        "packageChecksPassed": not failed_checks, "failedChecks": failed_checks,
        "sourceFiles": [{"path": name, "sha256": sha((repository / name).read_bytes())}
                        for name in run(["git", "ls-files"]).splitlines() if (repository / name).is_file()],
        "qualification": "Package checks only. Consult VALIDATION.md and the phone handoff; not authorized for real funds."
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    args.output.with_suffix(".certificate.txt").write_text(certificate, encoding="utf-8")
    print(f"Recorded package/certificate, {len(native)} native libraries and {len(assemblies)} managed payloads; source manifest: {args.output}")
    if failed_checks:
        for finding in failed_checks:
            print("BLOCKED: " + finding)
        raise SystemExit(1)
    print("Package checks passed; device qualification is separate.")


if __name__ == "__main__":
    main()
