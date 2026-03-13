#!/usr/bin/env bash
#!nix-shell -i bash -p _7zz
#
# Downloads, extracts and upgrades Hardware Wallet Interface (HWI) binaries for Wasabi Wallet
#
# Requirements:
#   - bash on linux or git bash on Windows
#   - curl
#   - 7zz (version 25.1+; 7-Zip command line; apt install 7zip-standalone / brew install sevenzip / winget install --id 7zip.7zip)
#   - git (only for chmod +x marking via git update-index)
#
# Usage:
#   ./upgrade-hwi.sh 3.1.0                                   # Download HWI archives using curl, extract HWI binaries, update them in the repository.
#   ./upgrade-hwi.sh 3.1.0 --skip-download                   # Work with HWI archives from a previous script run.
#   ./upgrade-hwi.sh 3.1.0 --skip-download --skip-extract    # Do not extract HWI archives. Continue with remaining steps.
#
# See:
# https://github.com/bitcoin-core/HWI/

set -euo pipefail
shopt -s extglob nullglob

VERSION="${1:-}"
if [[ -z "$VERSION" ]]; then
    echo "ERROR: HWI version is required."
    echo "Usage: $0 <version> [--skip-download] [--skip-extract] [--skip-replace]"
    exit 1
fi

# Parse flags
SKIP_DOWNLOAD=false
SKIP_EXTRACT=false
SKIP_REPLACE=false

for arg in "$@"; do
    case "$arg" in
        --skip-download) SKIP_DOWNLOAD=true ;;
        --skip-extract)  SKIP_EXTRACT=true ;;
        --skip-replace)  SKIP_REPLACE=true ;;
    esac
done

# ──────────────────────────────────────────────────────────────────────────────
#  Settings
# ──────────────────────────────────────────────────────────────────────────────

DIST_URI="https://github.com/bitcoin-core/HWI/releases/download/${VERSION}"

declare -A FILES
FILES[linux-x64]="hwi-${VERSION}-linux-x86_64.tar.gz"
FILES[osx64]="hwi-${VERSION}-mac-x86_64.tar.gz"
FILES[win-x64]="hwi-${VERSION}-windows-x86_64.zip"

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

# ──────────────────────────────────────────────────────────────────────────────
#  Main logic
# ──────────────────────────────────────────────────────────────────────────────

basedir=$(dirname "$0")
BINARIES_DIR=$(realpath "${basedir}/../../WalletWasabi/BundledApps/Binaries")
cd $BINARIES_DIR || { echo "Error: Failed to change directory to '$BINARIES_DIR'" >&2; exit 1; }

info "Change directory to '$BINARIES_DIR'"

if [[ ! -d "linux-x64" || ! -d "osx64" || ! -d "win-x64" ]]; then
    error "Expected linux-x64, osx64, win-x64, etc. to be present in the folder 'WalletWasabi/BundledApps/Binaries'"
fi

TEMP_DIR="temp/hwi-${VERSION}"
mkdir -p "$TEMP_DIR" || error "Cannot create temp directory"

pushd "$TEMP_DIR" >/dev/null || exit 1
info "Change directory to $TEMP_DIR"

# ─── Download ────────────────────────────────────────────────────────────────
if [[ "$SKIP_DOWNLOAD" != true ]]; then
    section "Downloading HWI packages"

    CHECKSUM_FILE="SHA256SUMS.txt.asc"

    url="${DIST_URI}/$CHECKSUM_FILE"
    info "Downloading $CHECKSUM_FILE from '$url' ..."
    curl -fL --progress-bar -o "$CHECKSUM_FILE" "$url" || error "Download of '$CHECKSUM_FILE' failed: $url"

    # See https://achow101.com/contact/
    url="http://achow101.com/achow101.pgp"
    info "Downloading 'achow101.pgp' from '$url' ..."    
    curl -fsSL --output achow101.pgp "${url}"
    gpg --import ./achow101.pgp
    gpg --list-keys --fingerprint 17565732E08E5E41
    gpg --verify "${CHECKSUM_FILE}"
    info "Signature OK for $CHECKSUM_FILE"

    for platform in "${SUPPORTED_PLATFORMS[@]}"; do
        fname="${FILES[$platform]}"
        url="${DIST_URI}/${fname}"

        info "Downloading $fname from '$url' ..."
        curl -fL --progress-bar -o "$fname" "$url" || error "Download of '$fname' failed: $url"

        # Verify downloaded .gz/.zip file's SHA256 hash against hashes in SHA256SUMS.txt.asc.
        actual_line=$(sha256sum --text $fname)
        found=$(grep --fixed-strings --line-regexp --quiet "$actual_line" "$CHECKSUM_FILE" && echo true || echo false)
        if ! $found; then
            error "Line '$actual_line' not present in $CHECKSUM_FILE"
            exit 1
        fi
        
        info "SHA256 hash is OK for $fname"
    done
else
    section "Skipping download"
fi

# ─── Extract only HWI archives ────────────────────────────────────────────────
if [[ "$SKIP_EXTRACT" != true ]]; then
    section "Extracting HWI archives"

    rm -rf HWI

    # Linux x64
    info "Extracting Linux x86_64 tar.xz (hwi-${VERSION}-linux-x86_64.tar.gz)"
    mkdir -p HWI/linux-x64
    "$SEVEN_ZIP" x -y "hwi-${VERSION}-linux-x86_64.tar.gz" >/dev/null
    "$SEVEN_ZIP" x -y -oHWI/linux-x64 "hwi-${VERSION}-linux-x86_64.tar" >/dev/null

    # macOS
    info "Extracting macOS (hwi-${VERSION}-mac-x86_64.tar.gz)"
    mkdir -p HWI/osx64
    "$SEVEN_ZIP" x -y "hwi-${VERSION}-mac-x86_64.tar.gz" >/dev/null
    "$SEVEN_ZIP" x -y -oHWI/osx64 "hwi-${VERSION}-mac-x86_64.tar" >/dev/null

    # Windows
    info "Extracting Windows (hwi-${VERSION}-windows-x86_64.zip)"
    mkdir -p HWI/win-x64
    "$SEVEN_ZIP" x -y "hwi-${VERSION}-windows-x86_64.zip" -oHWI/win-x64 >/dev/null

    # Remove HWI-QT if copied
    rm -rf HWI/*/hwi-qt 2>/dev/null || true
    rm -rf HWI/*/hwi-qt.exe 2>/dev/null || true
else
    section "Skipping HWI archive extraction"
fi

popd >/dev/null # Working directory is 'WalletWasabi/BundledApps/Binaries' again.

# ─── Replace binaries in repository ──────────────────────────────────────────
if [[ "$SKIP_REPLACE" != true ]]; then
    section "Replacing HWI binaries in repository"

    for platform in "${SUPPORTED_PLATFORMS[@]}"; do
        target_dir="${platform}"
        rm -rf "${target_dir:?}"/hwi
        cp -a "${TEMP_DIR}/HWI/${platform}/"* "${BINARIES_DIR}/${target_dir}/"
        info "Updated ${target_dir}"
    done
else
    section "Skipping HWI binary replacement"
fi

# ─── Make executables +x (git friendly) ──────────────────────────────────────
section "Marking HWI binaries executable in the git repository"

chmod +x ./{linux-x64,osx64,win-x64}/hwi{,.exe} 2>/dev/null || true

git update-index --chmod=+x ./{linux-x64,osx64}/hwi 2>/dev/null || true
git update-index --chmod=+x ./win-x64/hwi.exe 2>/dev/null || true

echo ""
echo "Done."
echo ""
