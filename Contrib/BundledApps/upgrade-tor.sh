#!/usr/bin/env bash
#!nix-shell -i bash -p _7zz
#
# Downloads, extracts and upgrades Tor from Tor Browser binaries for Wasabi Wallet
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
Downloads, extracts and upgrades Tor from Tor Browser binaries for Wasabi Wallet

Usage:
    ./upgrade-tor.sh <version> [OPTIONS]

Arguments:
    <version>               Tor Browser version (required)  e.g. 15.0.7

Options:
    -h, --help              Show this help message and exit
    --force-download        Force download Tor Browser archives even if they already exist
    --skip-extract-browser  Skip extracting the Tor Browser archive
    --skip-extract-tor      Skip extracting the embedded Tor from browser
    --skip-replace-tor      Skip replacing the Tor binaries in the repository
    --skip-replace-geoip    Skip replacing the GeoIP files

Examples:
    ./upgrade-tor.sh 15.0.7
    ./upgrade-tor.sh 15.0.7 --force-download
    ./upgrade-tor.sh 15.0.7 --skip-extract-browser --skip-replace-geoip
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
    echo "ERROR: Tor Browser version is required." >&2
    echo "Use --help for usage information." >&2
    echo "Usage: $0 <version> [--force-download] [--skip-extract-browser] [--skip-extract-tor] [--skip-replace-tor] [--skip-replace-geoip]" >&2
    exit 1
fi

# Parse flags
FORCE_DOWNLOAD=false
SKIP_EXTRACT_BROWSER=false
SKIP_EXTRACT_TOR=false
SKIP_REPLACE_TOR=false
SKIP_REPLACE_GEOIP=false

for arg in "$@"; do
    case "$arg" in
        --force-download)       FORCE_DOWNLOAD=true ;;
        --skip-extract-browser) SKIP_EXTRACT_BROWSER=true ;;
        --skip-extract-tor)     SKIP_EXTRACT_TOR=true ;;
        --skip-replace-tor)     SKIP_REPLACE_TOR=true ;;
        --skip-replace-geoip)   SKIP_REPLACE_GEOIP=true ;;
    esac
done

# ──────────────────────────────────────────────────────────────────────────────
#  Settings
# ──────────────────────────────────────────────────────────────────────────────

DIST_URI="https://www.torproject.org/dist/torbrowser/${VERSION}"

declare -A FILES
FILES[linux-arm64]="tor-browser-linux-aarch64-${VERSION}.tar.xz"
FILES[linux-x64]="tor-browser-linux-x86_64-${VERSION}.tar.xz"
FILES[osx64]="tor-browser-macos-${VERSION}.dmg"
FILES[win-x64]="tor-browser-windows-x86_64-portable-${VERSION}.exe"

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
require_command rsync

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

TEMP_DIR="temp/${VERSION}"
mkdir -p "$TEMP_DIR" || error "Cannot create temp directory"

pushd "$TEMP_DIR" >/dev/null || exit 1
info "Change directory to $TEMP_DIR"

# ─── Download ────────────────────────────────────────────────────────────────
section "Downloading Tor Browser packages (if needed)"

if [[ "$FORCE_DOWNLOAD" == true ]] || [[ ! -f "./tor.keyring" ]]; then
    info "Fetching the Tor Developers key"
    
    # https://support.torproject.org/tor-browser/getting-started/verifying-tor-browser/
    gpg --auto-key-locate nodefault,wkd --locate-keys torbrowser@torproject.org    # Import key from Web Key Directory (WKD)
    gpg --output ./tor.keyring --export 0xEF6E286DDA85EA2A4BA7DE684E2C6E8793298290 # Export the specific key to a local keyring file

    info "Tor Developers keyring created/updated"
else
    info "Tor keyring already exists. Skipping fetch."
fi

for platform in "${SUPPORTED_PLATFORMS[@]}"; do
    fname="${FILES[$platform]}"
    url="${DIST_URI}/${fname}"

    asc_fname="${fname}.asc"
    asc_url="${url}.asc"

    # Download only if file doesn't exist, unless --force-download is used.
    if [[ "$FORCE_DOWNLOAD" == true ]] || [[ ! -f "$fname" ]] || [[ ! -f "$asc_fname" ]]; then
        info "Downloading $fname from '$url' ..."
        curl -fL --progress-bar -o "$fname" "$url" || error "Download of '$fname' failed: $url"

        info "Downloading $asc_fname from '$asc_url' ..."
        curl -fL --progress-bar -o "$asc_fname" "$asc_url" || error "Download of '$asc_fname' failed: $asc_url"
    else
        info "File already exists: $fname (skipping download)"
    fi

    # Verify signature
    if gpgv --keyring ./tor.keyring "$asc_fname" "$fname" >gpg-verify.log 2>&1; then
        info "Signature OK for $fname"
    else
        gpgLog=$(cat ./gpg-verify.log)
        error "Verification FAILED for $fname" "$gpgLog"
    fi
done

# ─── Extract browser archives ────────────────────────────────────────────────
if [[ "$SKIP_EXTRACT_BROWSER" != true ]]; then
    rm -rf TorBrowser Tor

    section "Extracting Tor Browser archives"

    # Remove WalletWasabi/BundledApps/Binaries/temp/$VERSION/TorBrowser
    rm -rf TorBrowser

    # Linux arm64
    info "Extracting Linux aarch64 tar.xz (tor-browser-linux-aarch64-${VERSION}.tar.xz)"
    mkdir -p TorBrowser/linux-arm64
    "$SEVEN_ZIP" x -y "tor-browser-linux-aarch64-${VERSION}.tar.xz" >/dev/null
    "$SEVEN_ZIP" x -y -oTorBrowser/linux-arm64 "tor-browser-linux-aarch64-${VERSION}.tar" >/dev/null

    # Linux x64
    info "Extracting Linux x86_64 tar.xz (tor-browser-linux-x86_64-${VERSION}.tar.xz)"
    mkdir -p TorBrowser/linux-x64
    "$SEVEN_ZIP" x -y "tor-browser-linux-x86_64-${VERSION}.tar.xz" >/dev/null
    "$SEVEN_ZIP" x -y -oTorBrowser/linux-x64 "tor-browser-linux-x86_64-${VERSION}.tar" >/dev/null

    # macOS → .dmg
    info "Extracting macOS DMG (tor-browser-macos-${VERSION}.dmg)"
    mkdir -p TorBrowser/macOS
    "$SEVEN_ZIP" x -y -oTorBrowser/macOS "tor-browser-macos-${VERSION}.dmg" >/dev/null

    # Windows → .exe
    info "Extracting Windows installer (tor-browser-windows-x86_64-portable-${VERSION}.exe)"
    mkdir -p TorBrowser/Windows
    "$SEVEN_ZIP" x -y -oTorBrowser/Windows "tor-browser-windows-x86_64-portable-${VERSION}.exe" >/dev/null
else
    section "Skipping browser archive extraction"
fi

# ─── Extract only Tor folder ─────────────────────────────────────────────────
if [[ "$SKIP_EXTRACT_TOR" != true ]]; then
    section "Extracting Tor binaries only"

    rm -rf Tor
    mkdir -p Tor/{linux-arm64,linux-x64,osx64,win-x64}

    # Linux arm64
    cp -a TorBrowser/linux-arm64/tor-browser/Browser/TorBrowser/Tor/* Tor/linux-arm64/ 2>/dev/null || true

    # Linux x64
    cp -a TorBrowser/linux-x64/tor-browser/Browser/TorBrowser/Tor/* Tor/linux-x64/ 2>/dev/null || true

    # macOS
    # Note: path can vary slightly between versions — "Tor Browser.app" or "Tor Browser Alpha.app"
    cp -a "TorBrowser/macOS/Tor Browser"*"/Tor Browser"*".app/Contents/MacOS/Tor/"* Tor/osx64/ 2>/dev/null || true

    # Windows
    cp -a TorBrowser/Windows/Browser/TorBrowser/Tor/* Tor/win-x64/ 2>/dev/null || true

    # Remove PluggableTransports if accidentally copied
    rm -rf Tor/*/PluggableTransports 2>/dev/null || true
else
    section "Skipping Tor binary extraction"
fi

popd >/dev/null # Working directory is 'WalletWasabi/BundledApps/Binaries' again.

# ─── Replace binaries in repository ──────────────────────────────────────────
if [[ "$SKIP_REPLACE_TOR" != true ]]; then
    section "Replacing Tor binaries in platform folders"

    for platform in "${SUPPORTED_PLATFORMS[@]}"; do
        target_dir="${platform}/Tor"
        mkdir -p "${target_dir}"
        rm -rf "${target_dir:?}"/!(LICENSE|.gitattributes)
        rsync -a --exclude='geoip' --exclude='geoip6' --exclude='torrc' --exclude='torrc-defaults' "${TEMP_DIR}/Tor/${platform}/" "${BINARIES_DIR}/${target_dir}/"
        info "Updated ${target_dir}"
    done
else
    section "Skipping Tor binary replacement"
fi

# ─── Replace GeoIP files ─────────────────────────────────────────────────────
if [[ "$SKIP_REPLACE_GEOIP" != true ]]; then
    section "Replacing GeoIP files"

    geoip_target="../../Tor/Geoip"
    mkdir -p "$geoip_target"

    # Usually taken from one of the Linux extractions
    cp -f "${TEMP_DIR}/TorBrowser/linux-x64/tor-browser/Browser/TorBrowser/Data/Tor/geoip"* "$geoip_target/" 2>/dev/null

    info "GeoIP files updated in ${geoip_target}"
else
    section "Skipping GeoIP replacement"
fi

# ─── Make executables +x (git friendly) ──────────────────────────────────────
section "Marking Tor binaries executable in the git repository"

chmod +x ./{linux-arm64,linux-x64,osx64,win-x64}/Tor/tor{,.exe} 2>/dev/null || true

git update-index --chmod=+x ./{linux-arm64,linux-x64,osx64}/Tor/tor 2>/dev/null || true
git update-index --chmod=+x ./win-x64/Tor/tor.exe 2>/dev/null || true

echo ""
echo "Done."
echo ""
