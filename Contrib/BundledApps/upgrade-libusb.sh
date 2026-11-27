#!/usr/bin/env bash
#!nix-shell -i bash -p _7zz gnupg jq gh xz
#
# Downloads libusb into WalletWasabi/BundledApps/Binaries, from the same kind of source HWI bundles it from:
#
# Linux:   the libusb-1.0-0 package of Debian bookworm, which HWI's Linux build bundles too. Checked like apt
#          does: the signed Release file, the hash of the package index in it, the hash of the package in that.
# Windows: the MinGW64 DLL of the official libusb release (HWI builds the same with MinGW-w64), checked against
#          the release manager's signature. It needs only system DLLs; the Visual Studio builds need VCRUNTIME140.dll.
# macOS:   Homebrew's libusb, which HWI's macOS build bundles too. libusb publishes no macOS binary. These are the
#          bottles built for macOS 14, the oldest macOS Wasabi supports, checked against the build attestation
#          Homebrew's CI publishes for every bottle.
#
# Usage:   ./upgrade-libusb.sh <version> [<homebrew version>]   e.g. ./upgrade-libusb.sh 1.0.30
#          <version> is for Windows and macOS. The Homebrew version differs only after a rebuild of the formula,
#          e.g. 1.0.30_1. Linux takes whatever version Debian bookworm has.
#
# Requirements: curl, gpg, 7zz, xz, tar, jq, gh (logged in, for `gh attestation verify`).

set -euo pipefail

VERSION="${1:?Usage: $0 <version> [<homebrew version>]}"
BREW_VERSION="${2:-$VERSION}"

# Debian Stable Release Key (12/bookworm), see https://ftp-master.debian.org/keys.html
DEBIAN_RELEASE_KEY="4D64FEC119C2029067D6E791F8D2585B8783D481"
# Tormod Volden, libusb release manager, see SIGNING_KEYS.txt of the release.
LIBUSB_RELEASE_KEY="C68187379B23DE9EFC46651E2C80FF56C6830A0E"

BINARIES_DIR=$(realpath "$(dirname "$0")/../../WalletWasabi/BundledApps/Binaries")
WORK_DIR=$(mktemp -d)
trap 'rm -rf "$WORK_DIR"' EXIT
cd "$WORK_DIR"

export GNUPGHOME="$WORK_DIR/gnupg"
mkdir -m 700 "$GNUPGHOME"

# verify <primary key> <gpg arguments>: fails unless gpg finds a good signature by that key, whichever of its
# subkeys made it. Other signatures on the file, by keys not imported here, do not count either way.
verify() {
    local key="$1"
    shift
    gpg --status-file status "$@" 2>/dev/null || true
    # VALIDSIG ends with the fingerprint of the primary key.
    grep -q "^\[GNUPG:\] VALIDSIG .* ${key}$" status || { echo "Not signed by ${key}: $*" >&2; exit 1; }
}

echo "Linux: Debian bookworm libusb-1.0-0"
DEBIAN_URI="https://deb.debian.org/debian"
curl -fsSL -o debian-release.asc "https://ftp-master.debian.org/keys/release-12.asc"
gpg --quiet --import debian-release.asc
curl -fsSL -o InRelease "${DEBIAN_URI}/dists/bookworm/InRelease"
# --decrypt writes out only the signed part, so nothing unsigned around it gets parsed.
verify "$DEBIAN_RELEASE_KEY" --output Release --decrypt InRelease

for platform in linux-x64:amd64:x86_64-linux-gnu linux-arm64:arm64:aarch64-linux-gnu; do
    IFS=: read -r target arch triplet <<< "$platform"

    index="main/binary-${arch}/Packages.xz"
    index_hash=$(awk -v file="$index" '/^SHA256:/ { inside = 1; next } /^[^ ]/ { inside = 0 } inside && $3 == file { print $1 }' Release)
    curl -fsSL -o "Packages-${arch}.xz" "${DEBIAN_URI}/dists/bookworm/${index}"
    echo "${index_hash}  Packages-${arch}.xz" | sha256sum --check --quiet

    stanza=$(xz -dc "Packages-${arch}.xz" | awk 'BEGIN { RS = "" } $0 ~ "^Package: libusb-1.0-0\n"')
    file=$(awk '/^Filename:/ { print $2 }' <<< "$stanza")
    hash=$(awk '/^SHA256:/ { print $2 }' <<< "$stanza")
    echo "  ${arch}: ${file}"
    curl -fsSL -o "libusb-${arch}.deb" "${DEBIAN_URI}/${file}"
    echo "${hash}  libusb-${arch}.deb" | sha256sum --check --quiet

    mkdir "$arch"
    # 7zz looks through the .deb and its data.tar.xz, as it does for HWI's .tar.gz files.
    (cd "$arch" && 7zz e -y "../libusb-${arch}.deb" data.tar >/dev/null && tar -xf data.tar)
    install -m 0644 "$(realpath "${arch}/usr/lib/${triplet}/libusb-1.0.so.0")" "${BINARIES_DIR}/${target}/libusb-1.0.so.0"
done

echo "Windows: libusb-${VERSION}.7z"
RELEASE_URI="https://github.com/libusb/libusb/releases/download/v${VERSION}"
curl -fsSLO "${RELEASE_URI}/libusb-${VERSION}.7z"
curl -fsSLO "${RELEASE_URI}/libusb-${VERSION}.7z.asc"
curl -fsSL -o KEYS "https://raw.githubusercontent.com/libusb/libusb/v${VERSION}/KEYS"
gpg --quiet --import KEYS
verify "$LIBUSB_RELEASE_KEY" --verify "libusb-${VERSION}.7z.asc" "libusb-${VERSION}.7z"

7zz x -y "libusb-${VERSION}.7z" MinGW64/dll/libusb-1.0.dll >/dev/null
install -m 0644 MinGW64/dll/libusb-1.0.dll "${BINARIES_DIR}/win-x64/libusb-1.0.dll"

TOKEN=$(curl -fsSL "https://ghcr.io/token?scope=repository:homebrew/core/libusb:pull" | jq -r .token)
curl -fsSL -H "Authorization: Bearer ${TOKEN}" -H "Accept: application/vnd.oci.image.index.v1+json" \
    -o bottles.json "https://ghcr.io/v2/homebrew/core/libusb/manifests/${BREW_VERSION}"

for platform in osx64:sonoma osx-arm64:arm64_sonoma; do
    target="${platform%%:*}"
    tag="${BREW_VERSION}.${platform#*:}"
    echo "macOS ${target}: Homebrew bottle ${tag}"

    digest=$(jq -r --arg tag "$tag" '.manifests[].annotations | select(."org.opencontainers.image.ref.name" == $tag) | ."sh.brew.bottle.digest"' bottles.json)
    [[ -n "$digest" ]] || { echo "No Homebrew bottle ${tag}" >&2; exit 1; }

    curl -fsSL -H "Authorization: Bearer ${TOKEN}" -o "${tag}.tar.gz" "https://ghcr.io/v2/homebrew/core/libusb/blobs/sha256:${digest}"
    echo "${digest}  ${tag}.tar.gz" | sha256sum --check --quiet
    gh attestation verify "${tag}.tar.gz" --repo Homebrew/homebrew-core >/dev/null

    mkdir "$tag"
    tar -xzf "${tag}.tar.gz" -C "$tag" "libusb/${BREW_VERSION}/lib/libusb-1.0.0.dylib"
    install -m 0644 "${tag}/libusb/${BREW_VERSION}/lib/libusb-1.0.0.dylib" "${BINARIES_DIR}/${target}/libusb-1.0.0.dylib"
done

echo "Done."
