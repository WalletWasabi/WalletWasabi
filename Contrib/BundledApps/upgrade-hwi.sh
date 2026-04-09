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

set -euo pipefail
shopt -s extglob nullglob

# ──────────────────────────────────────────────────────────────────────────────
# Show help and exit
# ──────────────────────────────────────────────────────────────────────────────
show_help() {
    cat << 'EOF'
Downloads, extracts and upgrades Hardware Wallet Interface (HWI) binaries for Wasabi Wallet

Usage:
    ./upgrade-hwi.sh <version> [OPTIONS]

Arguments:
    <version>          HWI version (required)  e.g. 3.2.0 or 3.1.0

Options:
    -h, --help         Show this help message and exit
    --force-download   Force download even if files already exist
    --skip-extract     Skip extracting the downloaded archives
    --skip-replace     Skip replacing the HWI binaries in the repository

Examples:
    ./upgrade-hwi.sh 3.2.0
    ./upgrade-hwi.sh 3.2.0 --force-download
    ./upgrade-hwi.sh 3.1.0 --skip-extract
    ./upgrade-hwi.sh 3.2.0 --force-download --skip-extract

See also:
    https://github.com/bitcoin-core/HWI/
    https://github.com/bitcoin-core/HWI/releases
EOF
    exit 0
}

# Handle help flags early
case "${1:-}" in
    -h|--help)
        show_help
        ;;
esac

VERSION="${1:-}"
if [[ -z "$VERSION" ]]; then
    echo "ERROR: HWI version is required." >&2
    echo "Use -h or --help for usage information." >&2
    echo "Usage: $0 <version> [--force-download] [--skip-extract] [--skip-replace]" >&2
    exit 1
fi

# Parse flags
FORCE_DOWNLOAD=false
SKIP_EXTRACT=false
SKIP_REPLACE=false

for arg in "$@"; do
    case "$arg" in
        --force-download) FORCE_DOWNLOAD=true ;;
        --skip-extract)   SKIP_EXTRACT=true ;;
        --skip-replace)   SKIP_REPLACE=true ;;
    esac
done

# ──────────────────────────────────────────────────────────────────────────────
#  Settings
# ──────────────────────────────────────────────────────────────────────────────

DIST_URI="https://github.com/bitcoin-core/HWI/releases/download/${VERSION}"

declare -A FILES
FILES[linux-arm64]="hwi-${VERSION}-linux-aarch64.tar.gz"
FILES[linux-x64]="hwi-${VERSION}-linux-x86_64.tar.gz"
FILES[osx64]="hwi-${VERSION}-mac-x86_64.tar.gz"
FILES[win-x64]="hwi-${VERSION}-windows-x86_64.zip"

CHECKSUM_FILE="SHA256SUMS.txt.asc"

SUPPORTED_PLATFORMS=("linux-arm64" "linux-x64" "osx64" "win-x64")

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
#  Functions
# ──────────────────────────────────────────────────────────────────────────────

download_checksums_file_and_pgp() {
    # Download checksum file and import key only if needed
    info "Downloading checksum file and signing key..."

    # Download SHA256SUMS.txt.asc
    local url="${DIST_URI}/$CHECKSUM_FILE"
    info "Downloading $CHECKSUM_FILE from '$url' ..."
    curl -fL --progress-bar -o "$CHECKSUM_FILE" "$url" || error "Download of '$CHECKSUM_FILE' failed"

    # Download achow101.pgp
    # See https://achow101.com/contact/
    url="http://achow101.com/achow101.pgp"
    info "Downloading 'achow101.pgp' from '$url' ..."
    curl -fsSL --output achow101.pgp "${url}"
    gpg --import ./achow101.pgp
    gpg --list-keys --fingerprint 17565732E08E5E41
    gpg --verify "${CHECKSUM_FILE}"
    info "Signature OK for $CHECKSUM_FILE"
}

verify_checksums() {
    # Verify the checksum signature
    gpg --verify "${CHECKSUM_FILE}" 2>&1 | grep -q "Good signature" || error "GPG verification of $CHECKSUM_FILE failed"

    info "Signature verification passed for $CHECKSUM_FILE"
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
section "Downloading HWI packages"

downloadChecksumsFile=true

# Download platform binaries only if missing or forced
for platform in "${SUPPORTED_PLATFORMS[@]}"; do
    fname="${FILES[$platform]}"
    url="${DIST_URI}/${fname}"

    if [[ "$FORCE_DOWNLOAD" == true ]] || [[ ! -f "$fname" ]]; then
        info "Downloading $fname ..."

        # Call the function only when we are actually downloading a file
        if [[ "$downloadChecksumsFile" == true ]]; then
            download_checksums_file_and_pgp
            downloadChecksumsFile=false
        fi

        curl -fL --progress-bar -o "$fname" "$url" || error "Download of '$fname' failed"
    else
        info "File already exists: $fname (skipping download)"
    fi

    # Verify SHA256 hash against the checksum file
    actual_line=$(sha256sum --text "$fname")
    if ! grep -qF "$actual_line" "$CHECKSUM_FILE"; then
        error "SHA256 hash verification FAILED for $fname"
    fi

    info "SHA256 hash is OK for $fname"
done

verify_checksums

# ─── Extract only HWI archives ────────────────────────────────────────────────
if [[ "$SKIP_EXTRACT" != true ]]; then
    section "Extracting HWI archives"

    rm -rf HWI

    # Linux arm64
    info "Extracting Linux arm64 tar.xz (hwi-${VERSION}-linux-aarch64.tar.gz)"
    mkdir -p HWI/linux-x64
    "$SEVEN_ZIP" x -y "hwi-${VERSION}-linux-aarch64.tar.gz" >/dev/null
    "$SEVEN_ZIP" x -y -oHWI/linux-arm64 "hwi-${VERSION}-linux-aarch64.tar" >/dev/null

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

chmod +x ./{linux-arm64,linux-x64,osx64,win-x64}/hwi{,.exe} 2>/dev/null || true

git update-index --chmod=+x ./{linux-arm64,linux-x64,osx64}/hwi 2>/dev/null || true
git update-index --chmod=+x ./win-x64/hwi.exe 2>/dev/null || true

echo ""
echo "Done."
echo ""
