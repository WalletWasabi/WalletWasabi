"""Bind an inspected APK to published source and explicitly selected evidence.

This copies only named public/synthetic verification files, never fixture folders,
wallets, backups, vaults, databases or signing material. It cannot authorize funds.
"""
import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import zipfile


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def git(root, *arguments):
    return subprocess.check_output(["git", *arguments], cwd=root, text=True).strip()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--delivery", type=Path, required=True)
    parser.add_argument("--evidence-index", type=Path, required=True,
                        help="Explicit assertions and public/synthetic paths under artifacts/android")
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[2]
    artifacts = (root / "artifacts/android").resolve()
    delivery = args.delivery.resolve()
    if not delivery.is_relative_to(artifacts) or delivery == artifacts:
        raise ValueError("Choose a dedicated delivery directory under artifacts/android")
    manifest = json.loads((delivery / "package-manifest.json").read_text(encoding="utf-8-sig"))
    commit = git(root, "rev-parse", "HEAD")
    if git(root, "status", "--porcelain") or manifest["workingTreeModified"] or manifest["sourceCommit"] != commit:
        raise ValueError("The inspected package must match a clean, committed checkout")
    if not manifest["packageChecksPassed"] or manifest["failedChecks"]:
        raise ValueError("Resolve package findings before assembling delivery")
    apk = (delivery / manifest["apk"]).resolve()
    if apk.parent != delivery or sha(apk) != manifest["apkSha256"]:
        raise ValueError("The candidate APK changed after inspection")
    for item in manifest["sourceFiles"]:
        source = (root / item["path"]).resolve()
        if not source.is_relative_to(root) or not source.is_file() or sha(source) != item["sha256"]:
            raise ValueError("Recorded source changed: " + item["path"])
    published = git(root, "ls-remote", "origin", "refs/heads/master").split()[0]
    if published != commit:
        git(root, "fetch", "origin", "master")
        if git(root, "rev-parse", "origin/master") != published:
            raise ValueError("Remote master changed while verifying publication; retry")
        subprocess.run(["git", "merge-base", "--is-ancestor", commit, "origin/master"], cwd=root, check=True)

    index = json.loads(args.evidence_index.read_text(encoding="utf-8-sig"))
    if index["sourceCommit"] != commit or index["apkSha256"].lower() != manifest["apkSha256"]:
        raise ValueError("Evidence selection belongs to a different candidate")
    if index.get("syntheticOnly") is not True or index.get("realBitcoinAuthorized") is not False:
        raise ValueError("This software delivery must contain only public/synthetic checks")
    retained = []
    forbidden_parts = {"wallets", "walletbackups", "backups", "vault", "regtest", "chainstate", "blocks", "indexes"}
    for relative in index["evidenceFiles"]:
        path = Path(relative)
        source = (artifacts / path).resolve()
        if (path.is_absolute() or ".." in path.parts or not source.is_relative_to(artifacts) or not source.is_file()
                or source.suffix.lower() not in {".log", ".txt", ".json"}
                or forbidden_parts.intersection(part.lower() for part in path.parts)
                or "fixture.json" in source.name or source.name.lower() in {"config.json", "mobile-settings.json", "bitcoin.conf"}):
            raise ValueError("Unsafe or missing evidence path: " + relative)
        target = (delivery / "evidence" / path).resolve()
        if not target.is_relative_to(delivery / "evidence"):
            raise ValueError("Evidence destination leaves the delivery directory: " + relative)
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, target)
        retained.append({"path": target.relative_to(delivery).as_posix(), "sha256": sha(target)})
    for name in ("README.md", "VALIDATION.md", "SECURITY_REVIEW.md", "PHONE_HANDOFF.md", "IMPLEMENTATION_PLAN.md", "toolchain.json"):
        shutil.copy2(root / "Contrib/Android" / name, delivery / name)
    (delivery / (apk.name + ".sha256")).write_text(f"{sha(apk)}  {apk.name}\n", encoding="utf-8")
    dependencies = {name: manifest[name] for name in ("sourceCommit", "sourceTree", "sourceFiles", "toolchain", "dependencies", "nativeSourceBuild", "managedAssemblies", "nativeLibraries")}
    (delivery / "source-dependency-manifest.json").write_text(json.dumps(dependencies, indent=2) + "\n", encoding="utf-8")
    report = {"recordedUtc": datetime.now(timezone.utc).isoformat(), "overallQualification": "BLOCKED", "releaseReady": False,
              "sourceCommit": commit, "remoteMasterVerified": published, "publishedCommitVerified": True,
              "apk": apk.name, "apkSha256": sha(apk), "version": manifest["version"], "versionCode": manifest["versionCode"],
              "certificateSha256": manifest["certificateSha256"], "packageChecksPassed": True,
              "checks": index["checks"], "openGates": index["openGates"], "evidence": retained,
              "realBitcoinAuthorized": False, "independentSecurityAudit": False, "realWorldAnonymityVerified": False}
    if not report["openGates"]:
        raise ValueError("Handset and explicitly authorized mainnet checks must be recorded separately")
    (delivery / "verification.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    bundle = delivery.parent / (delivery.name + ".zip")
    with zipfile.ZipFile(bundle, "w", compression=zipfile.ZIP_DEFLATED) as archive:
        # Do not sweep the directory: an explicitly named set prevents a stray
        # wallet backup or secret from being included in a later rerun.
        names = [apk.name, apk.name + ".sha256", "package-manifest.json", "package-manifest.certificate.txt",
                 "source-dependency-manifest.json", "verification.json", "README.md", "VALIDATION.md", "SECURITY_REVIEW.md",
                 "PHONE_HANDOFF.md", "IMPLEMENTATION_PLAN.md", "toolchain.json"]
        for name in names + [item["path"] for item in retained]:
            archive.write(delivery / name, name)
    bundle.with_suffix(".zip.sha256").write_text(f"{sha(bundle)}  {bundle.name}\n", encoding="utf-8")
    with zipfile.ZipFile(bundle) as archive:
        if archive.testzip() is not None or hashlib.sha256(archive.read(apk.name)).hexdigest() != sha(apk):
            raise ValueError("Delivery archive failed verification")
    print(f"Verified published source {commit}; candidate {sha(apk)}; bundle {bundle}")
    print("Software candidate recorded. Required handset/mainnet gates remain open.")


if __name__ == "__main__":
    main()
