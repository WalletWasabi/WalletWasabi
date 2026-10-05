"""Build pinned Android native dependencies on Linux, with two compiler workers.

Prerequisites: git, curl, unzip, Python 3.8+, gcc, clang 10+, ninja, autoconf,
automake, libtool, gettext, pkg-config, JDK 17+ and the pinned Android workload.
Use a dedicated cache with at least 24 GiB free on its backing physical disk.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import struct
import tarfile
import zipfile


DIRECTORY = Path(__file__).resolve().parent
LOCK = json.loads((DIRECTORY / "source.lock.json").read_text())
FLAGS = LOCK["linkerFlags"]


def run(arguments, cwd=None, log=None, env=None):
    if log:
        print("Building " + log.name, flush=True)
        with log.open("w") as stream:
            subprocess.run([str(a) for a in arguments], cwd=cwd, env=env, stdout=stream, stderr=subprocess.STDOUT, check=True)
    else:
        return subprocess.check_output([str(a) for a in arguments], cwd=cwd, env=env, text=True).strip()


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def checkout(path, url, commit, tag=None):
    if not (path / ".git").exists():
        path.mkdir(parents=True, exist_ok=True)
        run(["git", "init", path])
        run(["git", "-C", path, "remote", "add", "origin", url])
        run(["git", "-C", path, "fetch", "--depth=1", "origin", tag or commit])
        run(["git", "-C", path, "checkout", "--detach", commit])
    if run(["git", "-C", path, "rev-parse", "HEAD"]) != commit:
        raise ValueError("Unexpected checkout: " + str(path))
    run(["git", "-C", path, "fsck", "--full"])
    # Native output directories are ignored by the respective upstream projects.
    # Every compiled vendor is independently checked by dependencies(). Nested
    # upstream test-only submodules (e.g. OpenSSL interoperability fixtures) are
    # not compiled or initialized by this recipe.
    if run(["git", "-C", path, "diff", "--ignore-submodules=all", "--name-only"]):
        raise ValueError("Modified upstream sources: " + str(path))


def dependencies(path, expected):
    for name, commit in expected.items():
        key = "submodule.external/" + name + ".url"
        url = run(["git", "-C", path, "config", "-f", path / ".gitmodules", "--get", key])
        checkout(path / "external" / name, url, commit)


def restore_generated_zlib(path):
    if not (path / ".git").exists():
        return
    if run(["git", "-C", path, "rev-parse", "HEAD"]) != LOCK["tor"]["dependencies"]["zlib"]:
        raise ValueError("Unexpected zlib checkout")
    changed = set(run(["git", "-C", path, "diff", "--name-only"]).splitlines())
    if not changed:
        return
    if not changed <= {"Makefile", "zconf.h"}:
        raise ValueError("Unexpected zlib source modifications")
    if "Makefile" in changed and not (path / "Makefile").read_text().startswith("# Makefile for zlib\n"):
        raise ValueError("zlib Makefile is not an upstream-generated configure output")
    if "zconf.h" in changed:
        current = (path / "zconf.h").read_text()
        for define in ("UNISTD_H", "STDARG_H"):
            current = current.replace("#if 1     /* was set to #if 1 by ./configure */", "#if HAVE_" + define + "-0     /* may be set to #if 1 by ./configure */", 1)
        if current.strip() != run(["git", "-C", path, "show", "HEAD:zconf.h"]):
            raise ValueError("zconf.h differs beyond its generated platform header checks")
    run(["git", "-C", path, "restore", "--source=HEAD", "--worktree", "--", "Makefile", "zconf.h"])


def download(url, target, expected=None):
    if target.is_file() and expected and sha(target) == expected:
        return
    target.parent.mkdir(parents=True, exist_ok=True)
    # Retain partial bytes and resume them rather than restarting a slow download.
    for attempt in range(4):
        status = subprocess.run(["curl", "--fail", "--location", "--silent", "--show-error", "--connect-timeout", "20",
                                 "--max-time", "600", "--continue-at", "-", "--output", str(target), url]).returncode
        if status == 0:
            if expected and sha(target) != expected:
                raise ValueError("Downloaded file failed its pinned SHA-256: " + str(target))
            return
        if status not in (18, 28, 35, 56):
            raise RuntimeError("Download failed with curl status " + str(status))
    raise RuntimeError("Download incomplete; retained partial file: " + str(target))


def extract(archive, destination):
    destination.mkdir(parents=True, exist_ok=True)
    root = destination.resolve()
    with (zipfile.ZipFile(archive) if archive.suffix == ".zip" else tarfile.open(archive)) as source:
        names = source.namelist() if isinstance(source, zipfile.ZipFile) else source.getnames()
        for name in names:
            resolved = (root / name).resolve()
            if resolved != root and root not in resolved.parents:
                raise ValueError("Archive path escapes destination")
        if isinstance(source, zipfile.ZipFile):
            if source.testzip() is not None:
                raise ValueError("Archive CRC failure")
            # NDK toolchains contain executable modes and symlinks; Python's ZIP
            # extraction does not preserve those required Unix attributes.
            subprocess.run(["unzip", "-q", "-o", str(archive), "-d", str(root)], check=True)
        else:
            source.extractall(root)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--work", type=Path, required=True, help="Dedicated Linux source/build cache")
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--runtime-packs", type=Path, required=True, help="dotnet/packs directory containing Mono Android 10.0.12")
    parser.add_argument("--steps", nargs="+", choices=("runtime", "sdk", "tor", "sqlite", "stage"), default=["runtime", "sdk", "tor", "sqlite", "stage"])
    args = parser.parse_args()
    if os.name != "posix" or os.uname().machine != "x86_64":
        raise ValueError("Build on Linux x86_64 using the pinned Linux NDK")
    root = args.work.resolve()
    root.mkdir(parents=True, exist_ok=True)
    # Both ABI/configuration builds measured 8.4 GiB of source/toolchain outputs;
    # reserve further room for verified archives, runtime packs and intermediates.
    if shutil.disk_usage(root).free < 24 * 1024**3:
        raise ValueError("Native cache requires 24 GiB free; check the host disk too when using a WSL VHD")
    os.environ["PATH"] = "/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin"
    ndk = root / "android-ndk-r29"
    if not ndk.is_dir():
        archive = root / "downloads/android-ndk-r29-linux.zip"
        download(LOCK["ndk"]["url"], archive, LOCK["ndk"]["sha256"])
        if archive.stat().st_size != LOCK["ndk"]["size"]:
            raise ValueError("NDK archive size mismatch")
        extract(archive, root)
    if "Pkg.Revision = " + LOCK["ndk"]["version"] not in (ndk / "source.properties").read_text():
        raise ValueError("Incorrect NDK revision")
    cmake_root = root / "cmake-3.31.8-linux-x86_64"
    if not cmake_root.is_dir():
        archive = root / "downloads/cmake-3.31.8-linux-x86_64.tar.gz"
        download(LOCK["cmake"]["url"], archive, LOCK["cmake"]["sha256"])
        extract(archive, root)
    os.environ["PATH"] = str(cmake_root / "bin") + ":" + os.environ["PATH"]
    os.environ["ANDROID_NDK_ROOT"] = os.environ["ANDROID_NDK_HOME"] = str(ndk)
    os.environ["CMAKE_BUILD_PARALLEL_LEVEL"] = "2"
    if not os.environ.get("JAVA_HOME") or not (Path(os.environ["JAVA_HOME"]) / "bin/javac").is_file():
        raise ValueError("Set JAVA_HOME to an installed JDK, including javac")

    runtime = root / "runtime"
    if "runtime" in args.steps:
        patch = DIRECTORY / "runtime-tls-reentrancy.patch"
        if sha(patch) != LOCK["tlsPatchSha256"]:
            raise ValueError("Changed TLS patch")
        if not (runtime / ".git").exists():
            checkout(runtime, LOCK["runtime"]["url"], LOCK["runtime"]["commit"], LOCK["runtime"]["tag"])
        elif run(["git", "-C", runtime, "rev-parse", "HEAD"]) != LOCK["runtime"]["commit"]:
            raise ValueError("Unexpected runtime commit")
        run(["git", "-C", runtime, "fsck", "--full"])
        diff = run(["git", "-C", runtime, "diff", "--no-ext-diff"])
        if not diff:
            run(["git", "-C", runtime, "apply", patch])
        else:
            normalize = lambda value: re.sub(r"^index .*\n", "", value.strip(), flags=re.M)
            if normalize(diff) != normalize(patch.read_text()):
                raise ValueError("Runtime changes differ from the single pinned TLS patch")
        for arch in ("x64", "arm64"):
            run([runtime / "build.sh", "mono.runtime", "-os", "android", "-arch", arch, "-c", "Release", "-p:BuildTests=false", "-p:BuildMonoAOTCrossCompiler=false", "/m:1",
                 "-p:MonoCMakeExtraArgs=-DCMAKE_SHARED_LINKER_FLAGS=-Wl%2C-z%2Ccommon-page-size=16384"], cwd=runtime, log=root / f"build-runtime-{arch}.log")
            run([runtime / "src/native/libs/build-native.sh", "-os", "android", "-arch", arch, "-configuration", "Release", "-outconfig", f"net10.0-android-Release-{arch}",
                 "-numproc", "2", "-cmakeargs", "-DCMAKE_SHARED_LINKER_FLAGS=" + FLAGS], cwd=runtime, log=root / f"build-libraries-{arch}.log")

    android = root / "android-clean"
    if "sdk" in args.steps:
        checkout(android, LOCK["android"]["url"], LOCK["android"]["commit"], LOCK["android"]["tag"])
        dependencies(android, LOCK["android"]["nativeDependencies"])
        layouts = {}
        for configuration in ("Release", "Debug"):
            generated = android / "bin" / ("Build" + configuration)
            generated.mkdir(parents=True, exist_ok=True)
            lines = []
            for arch, suffix in (("arm64", "ARM64"), ("x64", "X86_64")):
                pack = args.runtime_packs.resolve() / f"Microsoft.NETCore.App.Runtime.Mono.android-{arch}/10.0.12/runtimes/android-{arch}"
                if not (pack / "native/include/mono-2.0/mono/jit/jit.h").is_file():
                    raise ValueError("Missing pinned Mono headers: " + str(pack))
                lines.append(f'set(NETCORE_APP_RUNTIME_DIR_{suffix} "{pack.as_posix()}")')
            (generated / "xa_build_configuration.cmake").write_text("\n".join(lines) + "\n")
            for arch, abi in (("x64", "x86_64"), ("arm64", "arm64-v8a")):
                rid = "android-" + arch
                output = root / "android-clean-out" / configuration
                options = ["-G", "Ninja", "-DCMAKE_TOOLCHAIN_FILE=" + str(ndk / "build/cmake/android.toolchain.cmake"), "-DANDROID_ABI=" + abi,
                           "-DANDROID_PLATFORM=24", "-DNATIVE_API_LEVEL=24", "-DANDROID_RID=" + rid, "-DANDROID_STL=c++_static", "-DANDROID_CPP_FEATURES=no-rtti no-exceptions",
                           "-DANDROID_USE_LEGACY_TOOLCHAIN_FILE=OFF", "-DANDROID_SUPPORT_FLEXIBLE_PAGE_SIZES=ON", "-DCMAKE_BUILD_TYPE=" + configuration,
                           "-DOUTPUT_PATH=" + str(output), "-DXA_LIB_TOP_DIR=" + str(output), "-DXA_BUILD_CONFIGURATION=" + configuration, "-DRUNTIME_FLAVOR=MonoVM",
                           "-DXA_TEST_OUTPUT_DIR=" + str(output / "tests"), "-DCMAKE_SHARED_LINKER_FLAGS=" + FLAGS]
                build = root / f"sdk-native-{configuration}-{rid}"
                cwd = android / "src/native"
                # The app SDK still generates assembly data containers using its
                # own stub. Derive the loader's layout constants from that exact
                # pinned stub, rather than a differently linked local container.
                # This is metadata generation, not an ELF security-header patch.
                stub = args.runtime_packs.resolve() / f"Microsoft.Android.Runtime.Mono.36.{rid}/36.1.69/runtimes/{rid}/native/libarchive-dso-stub.so"
                if sha(stub) != LOCK["android"]["assemblyContainerStubs"][arch]:
                    raise ValueError("Assembly container stub differs from the pinned SDK")
                destination = output / rid / "libarchive-dso-stub.so"
                destination.parent.mkdir(parents=True, exist_ok=True)
                shutil.copyfile(stub, destination)
                data = stub.read_bytes()
                entry_size, count = struct.unpack_from("<HH", data, 58)
                layouts[abi] = {"stubSha256": sha(stub), "sectionHeaderEntrySize": entry_size, "sectionHeaderEntryCount": count + 1, "payloadSectionOffset": 16384}
                run(["cmake", "-S", cwd, "-B", build] + options, cwd=cwd, log=root / f"configure-sdk-{configuration}-{rid}.log")
                run(["cmake", "--build", build, "--target", "mono-android." + configuration.lower(), "-j", "2"], cwd=cwd, log=root / f"build-sdk-{configuration}-{rid}.log")
        (root / "archive-layout.json").write_text(json.dumps(layouts, indent=2) + "\n")

    if "tor" in args.steps:
        tor = root / "tor-android"
        checkout(tor, LOCK["tor"]["url"], LOCK["tor"]["commit"], LOCK["tor"]["tag"])
        restore_generated_zlib(tor / "external/zlib")
        dependencies(tor, LOCK["tor"]["dependencies"])
        environment = dict(os.environ, LDFLAGS=FLAGS)
        # Upstream recursive makes also use nproc. Bound its visible CPU set,
        # rather than relying only on the top-level make's -j option.
        affinity = ",".join(str(cpu) for cpu in sorted(os.sched_getaffinity(0))[:2])
        make = ["taskset", "-c", affinity, "make"]
        for abi in ("x86_64", "arm64-v8a"):
            external = tor / "external"
            options = ["APP_ABI=" + abi, "NDK_REQUIRED_REVISION=" + LOCK["ndk"]["version"], "MIN_NDK_VERSION=29", "DEBUG=1"]
            # Verified task-owned source tree; upstream clean discards build products only.
            run(make + ["-j1"] + options + ["clean"], cwd=external, log=root / f"clean-tor-{abi}.log", env=environment)
            run(make + ["-j2"] + options + ["openssl-build-stamp", "libevent-build-stamp", "zlib-build-stamp", "zstd-build-stamp"], cwd=external, log=root / f"dependencies-tor-{abi}.log", env=environment)
            # DEBUG=1 skips the upstream ELF-header cleaner; normal LLVM stripping
            # occurs in stage.py. It does not change Tor's optimized build flags.
            run(make + ["-j2"] + options, cwd=external, log=root / f"build-tor-{abi}.log", env=environment)
            destination = root / "tor-built" / abi
            destination.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(external / "lib" / abi / "libtor.so", destination / "libtor.so")
        restore_generated_zlib(tor / "external/zlib")

    if "sqlite" in args.steps:
        archive = root / "sqlite-source/sqlite-amalgamation-3530300.zip"
        if not archive.is_file() or not zipfile.is_zipfile(archive):
            download(LOCK["sqlite"]["url"], archive)
        extract(archive, root / "sqlite-source")
        source = root / "sqlite-source/sqlite-amalgamation-3530300/sqlite3.c"
        if hashlib.sha3_256(source.read_bytes()).hexdigest() != LOCK["sqlite"]["amalgamationSha3_256"]:
            raise ValueError("SQLite amalgamation does not match the published SHA3-256")
        if LOCK["sqlite"]["sourceId"] not in source.read_text():
            raise ValueError("SQLite source ID differs from the currently packaged version")
        tools = ndk / "toolchains/llvm/prebuilt/linux-x86_64/bin"
        for triple, abi in (("x86_64-linux-android", "x86_64"), ("aarch64-linux-android", "arm64-v8a")):
            target = root / "sqlite-built" / abi / "libe_sqlite3.so"
            target.parent.mkdir(parents=True, exist_ok=True)
            options = ["-O2", "-fPIC", "-shared", "-fvisibility=default", "-Wl,-soname,libe_sqlite3.so", FLAGS]
            if abi == "x86_64":
                options += ["-maes", "-msse4.2"]
            run([tools / (triple + "24-clang")] + options + ["-D" + define for define in LOCK["sqlite"]["defines"]] + [source, "-llog", "-lm", "-o", target], log=root / f"build-sqlite-{abi}.log")

    if "stage" in args.steps:
        subprocess.run(["python3", str(DIRECTORY / "stage.py"), "--work", str(root), "--output", str(args.output.resolve())], check=True)


if __name__ == "__main__":
    main()
