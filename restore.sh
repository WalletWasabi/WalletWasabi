#!/bin/bash

set -euo pipefail

# Requested target versions of bundled apps
REQUESTED_BITCOIN="30.2"
REQUESTED_TOR="16.0a4"
REQUESTED_HWI="3.2.0"

# Configuration
MANIFEST_VERSIONS="manifest.versions"
MANIFEST_SHA256="manifest.sha256"

# The folder where this script is located
SCRIPT_FOLDER="$(dirname "$0")"

# Color output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

# =============================================
# Show help message
# =============================================
show_help() {
    cat << EOF
Usage: $(basename "$0") [OPTIONS]

Restore and verify bundled applications (Bitcoin Core, Tor, HWI)
for WalletWasabi. Automatically upgrades components when versions
or file integrity don't match the manifests.

Options:
  -h, --help     Show this help message and exit
  -f, --force    Force full upgrade/restore (ignores version check)

Examples:
  $(basename "$0")                # Normal restore + verification
  $(basename "$0") --force        # Force upgrade of all components

Manifest files used:
  - $MANIFEST_VERSIONS   Stores target versions of bundled apps for the current git branch
  - $MANIFEST_SHA256     Stores SHA256 checksums of all expected bundled binary files

EOF
}

# =============================================
# Parse command-line arguments
# =============================================
parse_args() {
    while [[ $# -gt 0 ]]; do
        case "$1" in
            -h|--help)
                show_help
                exit 0
                ;;
            -f|--force)
                FORCE_RESTORE=true
                shift
                ;;
            *)
                echo -e "${RED}✗ Unknown option: $1${NC}" >&2
                echo ""
                show_help
                exit 1
                ;;
        esac
    done
}

# =============================================
# Load target versions from manifest.versions
# =============================================
load_target_versions() {
    if [ -f "$MANIFEST_VERSIONS" ]; then
        echo -e "${GREEN}✓ Loading target versions from $MANIFEST_VERSIONS${NC}"
        
        TARGET_BITCOIN=$(grep '^Bitcoin=' "$MANIFEST_VERSIONS" | cut -d'=' -f2 || echo "NULL")
        TARGET_TOR=$(grep '^Tor=' "$MANIFEST_VERSIONS" | cut -d'=' -f2 || echo "NULL")
        TARGET_HWI=$(grep '^HWI=' "$MANIFEST_VERSIONS" | cut -d'=' -f2 || echo "NULL")
    else
        echo -e "${YELLOW}⚠ $MANIFEST_VERSIONS not found. Creating with requested targets...${NC}"
        
        cat > "$MANIFEST_VERSIONS" << EOF
Bitcoin=$REQUESTED_BITCOIN
Tor=$REQUESTED_TOR
HWI=$REQUESTED_HWI
EOF
        TARGET_BITCOIN="$REQUESTED_BITCOIN"
        TARGET_TOR="$REQUESTED_TOR"
        TARGET_HWI="$REQUESTED_HWI"
        
        echo -e "${GREEN}✓ Created $MANIFEST_VERSIONS with requested targets${NC}"
    fi

    echo -e "${YELLOW}Target versions in manifest:${NC}"
    echo "  Bitcoin : $TARGET_BITCOIN"
    echo "  Tor     : $TARGET_TOR"
    echo "  HWI     : $TARGET_HWI"
}

# Finds all relevant binary files for the manifest, printing them null-terminated
find_bundled_binaries() {
    find \
        WalletWasabi/BundledApps/Binaries/ \
        WalletWasabi.Tests/BundledApps/Binaries/ \
        -type f \
        -not -path "WalletWasabi/BundledApps/Binaries/temp/*" \
        -not -path "WalletWasabi.Tests/BundledApps/Binaries/temp/*" \
        -not -path "*/.gitignore" \
        -not -path "*/*.md" \
        -print0
}

# Check if manifest.versions exists
check_manifest_versions() {
    if [ ! -f "$MANIFEST_VERSIONS" ]; then
        echo -e "${YELLOW}✗ $MANIFEST_VERSIONS not found${NC}"
        return 1
    fi
    echo -e "${GREEN}✓ $MANIFEST_VERSIONS found${NC}"
    return 0
}

# Create (deterministic) SHA256 manifest
create_manifest_sha256() {
    find_bundled_binaries | xargs -0 sha256sum -b | LC_ALL=C sort -k2 > "$MANIFEST_SHA256"

    echo -e "${GREEN}✓ $MANIFEST_SHA256 generated successfully${NC}"
}

# Verify files against manifest.sha256 + check for unexpected files
check_manifest_sha256() {
    if [[ ! -f "$MANIFEST_SHA256" ]]; then
        echo -e "${YELLOW}✗ $MANIFEST_SHA256 not found${NC}"
        return 1
    fi

    echo "Verifying files against $MANIFEST_SHA256..."
    echo "---"

    VERIFICATION_FAILED=false

    if ! sha256sum -c "$MANIFEST_SHA256"; then
        VERIFICATION_FAILED=true
    fi

    echo "---"
    echo ""

    echo "Checking for unexpected files..."

    NEW_FILES=$(comm -13 \
        <(awk '{sub(/^\*/, "", $2); print $2}' "$MANIFEST_SHA256" | LC_ALL=C sort -u) \
        <(find_bundled_binaries | xargs -0 printf '%s\n' | LC_ALL=C sort -u) \
        2>/dev/null || true)

    if [[ -n "$NEW_FILES" ]]; then
        echo -e "${RED}✗ Found files not present in the manifest:${NC}"
        echo "---"
        echo "$NEW_FILES"
        echo "---"
        VERIFICATION_FAILED=true
    fi

    if [[ "$VERIFICATION_FAILED" == false ]]; then
        echo -e "${GREEN}✓ All files verified successfully!${NC}"
        return 0
    else
        echo -e "${RED}✗ Verification failed.${NC}"
        return 1
    fi
}

# Upgrades Bitcoin, Tor, HWI, if needed, or if enforced
run_upgrades() {
    echo ""
    echo -e "${YELLOW}Running scripts from $SCRIPT_FOLDER/Contrib/BundledApps/...${NC}"
    echo ""
    local upgrades_performed=false

    if [[ "${FORCE_RESTORE:-false}" == true ]]; then
        echo -e "${YELLOW}→ Forcing full restore of all components${NC}"
        
        echo "1. Upgrading Bitcoin to $REQUESTED_BITCOIN..."
        "$SCRIPT_FOLDER/Contrib/BundledApps/upgrade-bitcoin.sh" "$REQUESTED_BITCOIN"
        echo -e "${GREEN}✓ Bitcoin upgrade completed${NC}"
        
        echo "2. Upgrading Tor to $REQUESTED_TOR..."
        "$SCRIPT_FOLDER/Contrib/BundledApps/upgrade-tor.sh" "$REQUESTED_TOR"
        echo -e "${GREEN}✓ Tor upgrade completed${NC}"
        
        echo "3. Upgrading HWI to $REQUESTED_HWI..."
        "$SCRIPT_FOLDER/Contrib/BundledApps/upgrade-hwi.sh" "$REQUESTED_HWI"
        echo -e "${GREEN}✓ HWI upgrade completed${NC}"
        
        upgrades_performed=true
    else
        # Normal selective upgrade
        if [[ "$TARGET_BITCOIN" != "$REQUESTED_BITCOIN" ]]; then
            echo "1. Upgrading Bitcoin to $REQUESTED_BITCOIN..."
            "$SCRIPT_FOLDER/Contrib/BundledApps/upgrade-bitcoin.sh" "$REQUESTED_BITCOIN"
            echo -e "${GREEN}✓ Bitcoin upgrade completed${NC}"
            upgrades_performed=true
        else
            echo -e "${GREEN}✓ Bitcoin is already at requested version. Skipping.${NC}"
        fi

        if [[ "$TARGET_TOR" != "$REQUESTED_TOR" ]]; then
            echo "2. Upgrading Tor to $REQUESTED_TOR..."
            "$SCRIPT_FOLDER/Contrib/BundledApps/upgrade-tor.sh" "$REQUESTED_TOR"
            echo -e "${GREEN}✓ Tor upgrade completed${NC}"
            upgrades_performed=true
        else
            echo -e "${GREEN}✓ Tor is already at requested version. Skipping.${NC}"
        fi

        if [[ "$TARGET_HWI" != "$REQUESTED_HWI" ]]; then
            echo "3. Upgrading HWI to $REQUESTED_HWI..."
            "$SCRIPT_FOLDER/Contrib/BundledApps/upgrade-hwi.sh" "$REQUESTED_HWI"
            echo -e "${GREEN}✓ HWI upgrade completed${NC}"
            upgrades_performed=true
        else
            echo -e "${GREEN}✓ HWI is already at requested version. Skipping.${NC}"
        fi
    fi

    # Update manifests after upgrades
    if [[ "$upgrades_performed" == true ]]; then
        echo ""
        echo -e "${YELLOW}Upgrades completed. Updating manifests...${NC}"

        cat > "$MANIFEST_VERSIONS" << EOF
Bitcoin=$REQUESTED_BITCOIN
Tor=$REQUESTED_TOR
HWI=$REQUESTED_HWI
EOF
        echo -e "${GREEN}✓ $MANIFEST_VERSIONS updated${NC}"

        create_manifest_sha256
    else
        echo -e "${GREEN}✓ No upgrades were needed.${NC}"
    fi
}

main() {
    parse_args "$@"

    load_target_versions

    # If force flag was used, skip checks and run upgrades directly
    if [[ "${FORCE_RESTORE:-false}" == true ]]; then
        run_upgrades
        return
    fi

    # Normal flow: check manifests
    if ! check_manifest_versions; then
        echo -e "${YELLOW}manifest.versions missing → forcing restore...${NC}"
        FORCE_RESTORE=true
        run_upgrades
        return
    fi

    if ! check_manifest_sha256; then
        echo -e "${YELLOW}SHA256 verification failed → forcing full restore...${NC}"
        FORCE_RESTORE=true
        run_upgrades
        return
    fi

    echo ""
    echo -e "${GREEN}✓ All manifests are valid and files match the restored state.${NC}"
    echo -e "${GREEN}✓ No restore action needed.${NC}"
}

# Run the script
main "$@"

echo ""
echo -e "${GREEN}Restore process completed successfully.${NC}"
exit 0
