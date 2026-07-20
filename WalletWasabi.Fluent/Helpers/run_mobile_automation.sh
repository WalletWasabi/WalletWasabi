#!/bin/bash
set -e

SIM_ID="B3687A55-89AA-403D-9BA4-7229ABBA1B5A"
BUNDLE_ID="WasabiWallet"
ART_DIR="${1:-./screenshots}"

echo "Creating screenshot output directory: $ART_DIR"
mkdir -p "$ART_DIR"

LOG_PATH="$ART_DIR/wasabi_automation.log"
# Remove old log if it exists
rm -f "$LOG_PATH"

echo "Uninstalling app to clear all wallets/settings..."
xcrun simctl uninstall $SIM_ID $BUNDLE_ID || true

echo "Installing app..."
xcrun simctl install $SIM_ID WalletWasabi.Fluent.iOS/bin/Debug/net10.0-ios/iossimulator-arm64/WalletWasabi.Fluent.iOS.app

echo "Activating Simulator..."
osascript -e 'tell application "Simulator" to activate'
sleep 2

echo "Launching app with automation and CDP env variables..."
export SIMCTL_CHILD_WASABI_AUTOMATE_MOBILE=1
export SIMCTL_CHILD_WASABI_USE_CDP=1
export SIMCTL_CHILD_WASABI_AUTOMATE_LOG_PATH="$LOG_PATH"
xcrun simctl launch --stdout="$ART_DIR/wasabi_stdout.log" --stderr="$ART_DIR/wasabi_stderr.log" $SIM_ID $BUNDLE_ID

echo "Waiting for CDP server to start on port 9222..."
for i in {1..60}; do
    if nc -z 127.0.0.1 9222 2>/dev/null; then
        echo "CDP server is up and listening on port 9222!"
        break
    fi
    sleep 0.5
done

echo "Running CDP CLI runner in background to drive YAML test flow..."
dotnet /Users/wieslawsoltes/GitHub/CDP/src/CDP.Inspector.CLI/bin/Debug/net10.0/CDP.Inspector.CLI.dll run WalletWasabi.Fluent/Helpers/wasabi_mobile.flow.yaml --timeout 360000 --output-dir "$ART_DIR/CDP_Reports" &
CDP_PID=$!

wait_and_screenshot() {
    local pattern="$1"
    local filename="$2"
    echo "Waiting for log pattern: '$pattern'..."
    while true; do
        if grep -q "$pattern" "$LOG_PATH" 2>/dev/null; then
            # Wait another 0.5s for UI layout to settle
            sleep 0.5
            echo "Pattern found! Capturing screenshot to $filename..."
            xcrun simctl io $SIM_ID screenshot "$ART_DIR/$filename"
            break
        fi
        sleep 0.5
    done
}

# Capture screenshots dynamically as pages are entered
wait_and_screenshot "Entered WelcomePageViewModel" "automation_1_welcome.png" &
wait_and_screenshot "Entered AddWalletPageViewModel" "automation_2_addwallet.png" &
wait_and_screenshot "Entered WalletBackupTypeViewModel" "automation_3_backup_type.png" &
wait_and_screenshot "Entered RecoveryWordsViewModel" "automation_4_recovery_words.png" &
wait_and_screenshot "Entered ConfirmRecoveryWordsViewModel" "automation_5_confirm_words.png" &
wait_and_screenshot "Entered CreatePasswordDialogViewModel" "automation_6_passphrase.png" &
wait_and_screenshot "Entered AddedWalletPageViewModel" "automation_7_success.png" &
wait_and_screenshot "Entered WalletPage - Init" "automation_8_main_wallet.png" &
wait_and_screenshot "Entered ReceiveViewModel" "automation_9_receive.png" &
wait_and_screenshot "Entered ReceiveAddressViewModel" "automation_10_receive_address.png" &
wait_and_screenshot "Entered WalletPage - Return" "automation_11_main_wallet_return.png" &
wait_and_screenshot "Entered SendViewModel" "automation_12_send.png" &
wait_and_screenshot "Entered PrivacyControlViewModel" "automation_13_privacy_control.png" &
wait_and_screenshot "Entered SendFeeViewModel" "automation_14_send_fee.png" &
wait_and_screenshot "Entered TransactionPreviewViewModel" "automation_15_transaction_preview.png" &
wait_and_screenshot "Entered SendSuccessViewModel" "automation_16_send_success.png" &
wait_and_screenshot "Entered SettingsPageViewModel" "automation_17_settings.png" &

echo "Waiting for CDP CLI runner to finish..."
wait $CDP_PID

echo "Automation flow completed successfully!"
