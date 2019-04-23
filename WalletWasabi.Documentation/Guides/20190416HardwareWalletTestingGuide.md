# Wasabi Hardware Wallet Integration Testing

|                | Windows | OSX     | Linux   |
|----------------|---------|---------|---------|
| Coldcard       | pass    | pass    | pass    |
| Digital BitBox |         |         |         |
| KeepKey        |         |         |         |
| Ledger Nano S  | pass    | pass    | pass    |
| Trezor One     |         |         |         |
| Trezor Model T | pass    | pass    | pass    |

# How to test?

## (Linux Only) Step 0: Add `udev` rules

If you are on Linux you must add some `udev` rules if you haven't already so your OS (and Wasabi) can recognize your hardware wallet:

```sh
git clone https://github.com/bitcoin-core/HWI.git
cd HWI/
sudo cp udev/*.rules /etc/udev/rules.d/
sudo udevadm trigger
sudo udevadm control --reload-rules
sudo groupadd plugdev
sudo usermod -aG plugdev `whoami`
```

More info here: https://github.com/bitcoin-core/HWI/tree/master/udev

## Step 2: Build From Source Code

### Get The Requirements

1. Get Git: https://git-scm.com/downloads
2. Get .NET Core 2.2 SDK: https://www.microsoft.com/net/download (Note, you can disable .NET's telemetry by typing `export DOTNET_CLI_TELEMETRY_OPTOUT=1` on Linux and OSX or `set DOTNET_CLI_TELEMETRY_OPTOUT=1` on Windows.)
  
### Get Wasabi

Clone & Restore & Build

```sh
git clone https://github.com/zkSNACKs/WalletWasabi.git --recursive
cd WalletWasabi/WalletWasabi.Gui
dotnet build
```

### Run Wasabi

Run Wasabi with `dotnet run` from the `WalletWasabi.Gui` folder.

More info here: https://github.com/zkSNACKs/WalletWasabi/blob/master/README.md

## Step 3: Test

### 1. Does Wasabi recognizes your hardware wallet?
### 2. Does Wasabi loads your hardware wallet?
### 3. Can you send transaction using Wasabi and your hardware wallet?

## Step 4: Report Results

Report results on GitHub or Reddit.  
On GitHub by commenting under this pull request: https://github.com/zkSNACKs/WalletWasabi/pull/1341  
On Reddit by commenting under this thread: https://old.reddit.com/r/WasabiWallet/comments/bdyz84/wasabi_wallet_hardware_wallet_integration_testing/

Please include your OS version and your hardware wallet type.
