#!/usr/bin/env bash
#!nix-shell -i bash -p _7zz
#
# Downloads, extracts and upgrades bitcoind from Bitcoin Core archives for Wasabi Wallet
#
# Requirements:
#   - bash on linux or git bash on Windows
#   - curl
#   - 7zz (version 25.1+; 7-Zip command line; apt install 7zip-standalone / brew install sevenzip / winget install --id 7zip.7zip)
#   - git (only for chmod +x marking via git update-index)
#
# Usage:
#   ./upgrade-bitcoin-core.sh 30.2                                                # Download Bitcoin Core archives using curl, extract binaries, update them in the repository.
#   ./upgrade-bitcoin-core.sh 30.2 --skip-download                                # Work with Bitcoin Core archives from a previous script run.
#   ./upgrade-bitcoin-core.sh 30.2 --skip-download --skip-extract --skip-replace  # Do not extract Tor Browser archives. Continue with remaining steps.
#

set -euo pipefail
shopt -s extglob nullglob

VERSION="${1:-}"
if [[ -z "$VERSION" ]]; then
    echo "ERROR: Bitcoin Core version is required."
    echo "Usage: $0 <version> [--skip-download] [--skip-extract] [--skip-replace]"
    exit 1
fi

# Parse flags
SKIP_DOWNLOAD=false
SKIP_EXTRACT=false
SKIP_REPLACE=false

for arg in "$@"; do
    case "$arg" in
        --skip-download)  SKIP_DOWNLOAD=true ;;
        --skip-extract)   SKIP_EXTRACT=true ;;
        --skip-replace)   SKIP_REPLACE=true ;;
    esac
done

# ──────────────────────────────────────────────────────────────────────────────
#  Settings
# ──────────────────────────────────────────────────────────────────────────────

DIST_URI="https://bitcoincore.org/bin/bitcoin-core-${VERSION}"

declare -A FILES
FILES[linux-x64]="bitcoin-${VERSION}-x86_64-linux-gnu.tar.gz"
FILES[osx64]="bitcoin-${VERSION}-x86_64-apple-darwin.tar.gz"
FILES[win-x64]="bitcoin-${VERSION}-win64.zip"

SUPPORTED_PLATFORMS=("linux-x64" "osx64" "win-x64")

SEVEN_ZIP="7zz"

# ──────────────────────────────────────────────────────────────────────────────
#  Helpers
# ──────────────────────────────────────────────────────────────────────────────

info() {
    echo "[INFO]  $*"
}

error() {
    echo ""
    echo "[ERROR] $*" >&2
    echo ""
    exit 1
}

section() {
    echo ""
    echo "# $1"
    echo ""
}

require_command() {
    command -v "$1" >/dev/null 2>&1 || error "Required command not found: $1"
}

# ──────────────────────────────────────────────────────────────────────────────
#  Checks
# ──────────────────────────────────────────────────────────────────────────────

require_command curl

if [[ "$OSTYPE" == "msys" || "$OSTYPE" == "cygwin" ]]; then
    SEVEN_ZIP="/c/Program Files/7-Zip/7z.exe"
    if [[ ! -f "$SEVEN_ZIP" ]]; then
        error "Cannot find 7z.exe in /c/Program Files/7-Zip/\nInstall 7-Zip and verify the path."
    fi
    info "Git Bash detected — using full path to 7-Zip: $SEVEN_ZIP"
fi

require_command "$SEVEN_ZIP"
require_command gpg
require_command sha256sum

# ──────────────────────────────────────────────────────────────────────────────
#  Main logic
# ──────────────────────────────────────────────────────────────────────────────

basedir=$(dirname "$0")
BINARIES_DIR=$(realpath "${basedir}/../../WalletWasabi.Tests/BundledApps/Binaries")
cd $BINARIES_DIR || { echo "Error: Failed to change directory to '$BINARIES_DIR'" >&2; exit 1; }

info "Change directory to '$BINARIES_DIR'"

if [[ ! -d "linux-x64" || ! -d "osx64" || ! -d "win-x64" ]]; then
    error "Expected linux-x64, osx64, win-x64, etc. to be present in the folder 'WalletWasabi.Tests/BundledApps/Binaries'"
fi

TEMP_DIR="temp/bitcoin-${VERSION}"
mkdir -p "$TEMP_DIR" || error "Cannot create temp directory"

pushd "$TEMP_DIR" >/dev/null || exit 1
info "Change directory to $TEMP_DIR"

# ─── Download ────────────────────────────────────────────────────────────────
if [[ "$SKIP_DOWNLOAD" != true ]]; then
    section "Downloading Bitcoin Core packages"

    rm -rf BitcoinCore

    # Download checksums first
    info "Downloading SHA256SUMS and its signature..."
    echo "${DIST_URI}/SHA256SUMS"
    curl -fL -o SHA256SUMS "${DIST_URI}/SHA256SUMS" || error "Failed to download SHA256SUMS"
    curl -fL -o SHA256SUMS.asc "${DIST_URI}/SHA256SUMS.asc" || error "Failed to download SHA256SUMS.asc"

    # Ava Chow + Michael Ford (fanquake)
    curl -fL -o achow101.gpg https://keys.openpgp.org/vks/v1/by-fingerprint/152812300785C96444D3334D17565732E08E5E41 || error "Failed to download achow101's gpg"
    curl -fL -o fanquake.gpg https://keys.openpgp.org/vks/v1/by-fingerprint/E777299FC265DD04793070EB944D35F9AC3DB76A || error "Failed to download fanquake's gpg"
    gpg --import achow101.gpg || true
    gpg --import fanquake.gpg || true

    gpg --verify SHA256SUMS.asc SHA256SUMS >gpg_result.log 2>&1 || true

    if grep --fixed-strings -q "gpg: Good signature" gpg_result.log; then
        info "GPG verification PASSED: at least one good signature found on SHA256SUMS"
    else
        error "GPG verification FAILED.\n\nOutput from gpg: $(gpg --verify SHA256SUMS.asc SHA256SUMS 2>&1)"
    fi

    for platform in "${SUPPORTED_PLATFORMS[@]}"; do
        fname="${FILES[$platform]}"
        url="${DIST_URI}/${fname}"

        info "Downloading $fname from '$url' ..."
        curl -fL --progress-bar -o "$fname" "$url" || error "Download of '$fname' failed: $url"
    done

    if sha256sum -c --ignore-missing SHA256SUMS | grep -v "OK$"; then
        error "Checksum verification FAILED — see output above"
    fi

    info "All checksums OK"
else
    section "Skipping download"
fi

# ─── Extract Bitcoin Core archives ────────────────────────────────────────────────
if [[ "$SKIP_EXTRACT" != true ]]; then
    section "Extracting Bitcoin binaries"

    # Remove WalletWasabi.Tests/BundledApps/Binaries/temp/$VERSION/BitcoinCore
    rm -rf BitcoinCore

    # Linux x64
    info "Extracting Linux x86_64 tar.xz (tor-browser-linux-x86_64-${VERSION}.tar.xz)"
    mkdir -p BitcoinCore/linux-x64
    "$SEVEN_ZIP" x -y "bitcoin-${VERSION}-x86_64-linux-gnu.tar.gz" >/dev/null
    "$SEVEN_ZIP" x -y -oBitcoinCore/linux-x64 "bitcoin-${VERSION}-x86_64-linux-gnu.tar" >/dev/null

    # macOS (x86-64)
    info "Extracting macOS archive (bitcoin-${VERSION}-x86_64-apple-darwin.tar.gz)"
    mkdir -p BitcoinCore/osx64
    "$SEVEN_ZIP" x -y "bitcoin-${VERSION}-x86_64-apple-darwin.tar.gz" >/dev/null
    "$SEVEN_ZIP" x -y -oBitcoinCore/osx64 "bitcoin-${VERSION}-x86_64-apple-darwin.tar" >/dev/null

    # Windows (win-x64)
    info "Extracting Windows archive (bitcoin-${VERSION}-win64.zip)"
    mkdir -p BitcoinCore/win-x64
    "$SEVEN_ZIP" x -y -oBitcoinCore/win-x64 "bitcoin-${VERSION}-win64.zip" >/dev/null
else
    section "Skipping Bitcoin Core archive extraction"
fi

popd >/dev/null # Working directory is 'WalletWasabi.Tests/BundledApps/Binaries' again.

# ─── Replace binaries in repository ─────────────────────────────────────────
if [[ "$SKIP_REPLACE" != true ]]; then
    section "Replacing Bitcoin Core binaries in platform folders"

    for platform in "${SUPPORTED_PLATFORMS[@]}"; do
        target_dir="${platform}"
        mkdir -p "${target_dir}"
        rm -rf "${target_dir:?}"/*

        bin_dir="${TEMP_DIR}/BitcoinCore/${platform}/bitcoin-${VERSION}/bin"

        if [[ "$platform" == win-* ]]; then
            cp -a "${bin_dir}/bitcoind.exe" "${target_dir}/"
        else
            cp -a "${bin_dir}/bitcoind" "${target_dir}/"
        fi

        info "Updated ${target_dir}"
    done
else
    section "Skipping Bitcoin Core binary replacement"
fi

# ─── Make executables +x (git friendly) ──────────────────────────────────────
section "Marking Bitcoin Core binaries executable in the git repository"

chmod +x ./{linux-x64,osx64,win-x64}/bitcoind{,.exe} 2>/dev/null || true

git update-index --chmod=+x ./{linux-x64,osx64}/bitcoind 2>/dev/null || true
git update-index --chmod=+x ./win-x64/bitcoind.exe 2>/dev/null || true

echo ""
echo "Done."
echo ""
