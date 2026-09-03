# Guide for deterministic builds

The term *deterministic builds* is [defined](https://reproducible-builds.org/) as follows:

> Reproducible [or deterministic] builds are a set of software development practices that create an independently-verifiable path from source to binary code.

This guide describes how to reproduce Wasabi's release packages. If you get stuck with these instructions, take a look at [how to build Wasabi from source code](https://docs.wasabiwallet.io/using-wasabi/BuildSource.html).

**Warning:** these instructions describe the release procedure used since [2.8.2](https://github.com/WalletWasabi/WalletWasabi/releases/tag/v2.8.2), when the last known source of non-determinism in the Linux packages was removed. Older releases were produced by earlier versions of the same script and may not reproduce byte for byte.

## 1. How releases are produced

Every release artifact is produced by [`Contrib/release.sh`](../../Contrib/release.sh), run by the [release workflow](../../.github/workflows/release.yml) on GitHub-hosted runners when a `v*` tag is pushed:

| Job | Runner | Command | Artifacts |
| --- | --- | --- | --- |
| `debian-package-and-zips` | `ubuntu-latest` | `sudo bash -x ./Contrib/release.sh debian` | `Wasabi-<version>.deb`, `Wasabi-<version>-arm64.deb`, `Wasabi-<version>-linux-{x64,arm64}.{tar.gz,zip}` |
| `appimage-package` | `ubuntu-latest` | `sudo bash -x ./Contrib/release.sh appimage` | `Wasabi-<version>.AppImage`, `Wasabi-<version>-arm64.AppImage` |
| `installer-for-windows` | `windows-latest` | `./Contrib/release.sh wininstaller` | `Wasabi-<version>.msi`, `Wasabi-<version>-win-x64.zip` |
| `macos-packages-and-zips` | `macos-latest` | `./Contrib/release.sh dmg` | `Wasabi-<version>.dmg`, `Wasabi-<version>-arm64.dmg`, `Wasabi-<version>-macOS-{x64,arm64}.zip` |
| `sign-all-packages` | `ubuntu-latest` | `bash -x ./Contrib/release.sh gpgsign` | `SHA256SUMS.asc` (used in section 4) |

To reproduce a release, run **that release's own** `Contrib/release.sh`, checked out from the release tag. The script changes between releases (2.8.2, for example, added `--property:PathMap` and an apparent-size `Installed-Size`), so instructions pinned to one version go stale; the script at the tag does not.

The rest of this guide covers the Linux packages, which are the ones that reproduce byte for byte. Windows and macOS are discussed in section 4.

## 2. Assert correct environment

You need:

* a Linux system (the release workflow uses `ubuntu-latest`) with `git`, `zip`, `tar`, `dpkg-deb` (package `dpkg`), `file` and `curl`;
* the .NET SDK that ships the **same runtime pack** the release was built against (see below);
* root privileges, or one small adaptation (see below).

### Which .NET SDK?

`global.json` pins only the major version and uses `rollForward: latestFeature`, so the release workflow builds with whatever .NET 10 SDK the runner image provides on release day. Do not guess it: the shipped package records the runtime it was built against. Read it from the artifact you want to reproduce:

```sh
# from the .deb
dpkg-deb --fsys-tarfile Wasabi-2.8.2.deb | tar -xO --wildcards '*WalletWasabi.Fluent.Desktop.runtimeconfig.json'
# or from the tar.gz
tar -xzO --wildcards -f Wasabi-2.8.2-linux-x64.tar.gz '*WalletWasabi.Fluent.Desktop.runtimeconfig.json'
```

The `"version"` field under `runtimeOptions.framework` is the runtime, for example `10.0.11` for 2.8.2. Then install the **highest** SDK that ships that runtime, which is what `latestFeature` selects; the mapping is published in [dotnet/core `releases.json`](https://github.com/dotnet/core/blob/main/release-notes/10.0/releases.json). For runtime 10.0.11 that is SDK 10.0.400; for runtime 10.0.9 it is 10.0.301.

If several SDKs are installed, pin the one you chose in `global.json` before building:

```json
{
  "sdk": {
    "version": "10.0.400",
    "allowPrerelease": false,
    "rollForward": "disable"
  }
}
```

A convenient way to get exactly one SDK is the official container image, `mcr.microsoft.com/dotnet/sdk:<sdk-version>-noble`; the 2.8.2 verification below was done that way.

### File ownership

The release workflow runs the Linux jobs under `sudo`, so every file inside the `.deb` is owned by `root:root`. Either run the script as root as well, or, if you prefer not to, add `--root-owner-group` to the `dpkg-deb --build` call in a local copy of the script. Nothing else in the package depends on the user running the build.

### Timestamps

You do not need to set anything. `Contrib/release.sh` exports `SOURCE_DATE_EPOCH` from the commit date of the checked-out tag (`git log -1 --pretty=%ct`) if it is not already set; `tar` and `dpkg-deb` use it, so the archives carry the commit time rather than your build time. The `.zip` files are the exception: they are created with a plain `zip -r` and carry real file times, so they are not expected to match.

## 3. Reproduce the Linux packages

You can see the list of Wasabi releases here: https://github.com/WalletWasabi/WalletWasabi/releases. Each release has a git tag `v<version>`.

```sh
# Fetch exactly the tag (a shallow clone by tag name would silently prefer a same-named branch).
git init WalletWasabi && cd WalletWasabi
git remote add origin https://github.com/WalletWasabi/WalletWasabi.git
git fetch --depth 1 origin refs/tags/v2.8.2:refs/tags/v2.8.2
git checkout refs/tags/v2.8.2

# Same command the release workflow runs.
sudo bash -x ./Contrib/release.sh debian
```

The packages land in `packages/`:

```
packages/Wasabi-2.8.2.deb
packages/Wasabi-2.8.2-arm64.deb
packages/Wasabi-2.8.2-linux-x64.tar.gz
packages/Wasabi-2.8.2-linux-x64.zip
packages/Wasabi-2.8.2-linux-arm64.tar.gz
packages/Wasabi-2.8.2-linux-arm64.zip
```

The `debian` target builds both `linux-x64` and `linux-arm64` in one run.

## 4. Verify builds

Every release ships `SHA256SUMS.asc`, the list of package hashes clearsigned by the zkSNACKs key (`6FB3 872B 5D42 292F 5992 0797 8563 4832 8949 861E`). Verify the signature, then compare your packages against the signed list:

```sh
gpg --keyserver hkps://keys.openpgp.org --recv-keys 6FB3872B5D42292F59920797856348328949861E
gpg --decrypt SHA256SUMS.asc > SHA256SUMS.verified   # prints "Good signature from zkSNACKs" and writes the signed list
cd packages && sha256sum -c --ignore-missing ../SHA256SUMS.verified
```

`sha256sum -c` prints `OK` for every package whose hash matches the published one. If a hash differs, extract both packages and compare them to find out why:

```sh
dpkg-deb -R Wasabi-2.8.2.deb official
dpkg-deb -R packages/Wasabi-2.8.2.deb built
diff -r official built
```

### What to expect

* **`.deb`: byte-identical.** [WalletScrutiny](https://walletscrutiny.com/) rebuilt `Wasabi-2.8.2.deb` from tag `v2.8.2` (commit `0b483fbc4206963df7a026e8da08fa0f417c7fb5`) inside `mcr.microsoft.com/dotnet/sdk:10.0.400-noble` with the procedure above and obtained the published SHA-256 `c68029ddf360dcc9e77b536e431a8d908d916430cabb52644959637754dbba01`.
* **`.tar.gz`: byte-identical.** The tarball is created with `--sort=name`, `--mtime=@$SOURCE_DATE_EPOCH`, `--owner=0 --group=0 --numeric-owner` and the atime/ctime PAX headers removed; WalletScrutiny found the 2.8.0 and 2.8.1 tarballs identical to the published ones.
* **`.zip`: differs**, because `zip -r` stores the files' modification times (see *Timestamps* above); it contains the same files that go into the `.tar.gz`.
* **Before 2.8.2**, the `.deb` `Installed-Size` field was computed with `du -s`, whose result depends on the filesystem, so those packages differ from a rebuild in that one control-file line while the payload matches.

### Windows

`Wasabi-<version>.msi` is Authenticode-signed after it is built, so the installer file itself cannot match a rebuild. The files it installs can be compared: install it, build the `wininstaller` target on Windows, and diff your `build\win-x64` directory against the installation directory:

```sh
git diff --no-index build\win-x64 "C:\Program Files\WasabiWallet"
```

### macOS

According to Apple documentation, the signature that is used to ensure the integrity of the software is added into the binary itself - so it will manipulate the content of the files.

> If the code is universal, the object code for each slice (architecture) is signed separately. This signature is stored within the binary file itself.

[Source](https://developer.apple.com/library/archive/documentation/Security/Conceptual/CodeSigningGuide/AboutCS/AboutCS.html#//apple_ref/doc/uid/TP40005929-CH3-SW3)

According to this, it is impossible to have both deterministic build and code signature on macOS. macOS Gatekeeper won't let you run software without it. Thus, Wasabi only applies code signature, but no deterministic build for macOS.

There is an issue [here](https://github.com/WalletWasabi/WalletWasabi/issues/4110) for further discussion.

With the following method you can check the differences by yourself. You will need `7z` to extract the `.dmg` (`sudo apt install p7zip-full`):

```sh
7z x Wasabi-2.8.2.dmg -oWasabiOsx
git diff --no-index osx-x64/ WasabiOsx/Wasabi\ Wallet.App/Contents/MacOS/
```
