# Wasabi Hardware Wallet Integration Testing

|                | Windows | macOS   | Linux   |
|----------------|---------|---------|---------|
| Coldcard       | pass    | pass    | pass    |
| Digital BitBox |         |         |         |
| KeepKey        |         |         |         |
| Ledger Nano S  | pass    | pass    | pass    |
| Ledger Nano X  |         |         |         |
| Trezor One     | pass    | pass    | pass    |
| Trezor Model T | pass    | pass    | pass    |

# How to test?

## (Linux Only) Step 0: Add `udev` rules

If you are on Linux you must add some `udev` rules if you have not already, so your OS (and Wasabi) can recognize your hardware wallet:

```sh
git clone https://github.com/bitcoin-core/HWI.git
cd HWI/hwilib/
sudo cp udev/*.rules /etc/udev/rules.d/
sudo udevadm trigger
sudo udevadm control --reload-rules
sudo groupadd plugdev
sudo usermod -aG plugdev `whoami`
```

More info here: [https://github.com/bitcoin-core/HWI/tree/master/hwilib/udev](https://github.com/bitcoin-core/HWI/tree/master/hwilib/udev)

## Step 1: Build From Source Code

Follow these [steps](https://github.com/zkSNACKs/WalletWasabi#build-from-source-code).

## Step 2: Test

### 1. Does Wasabi recognize your hardware wallet?
### 2. Does Wasabi load your hardware wallet?
### 3. Can you send transaction using Wasabi and your hardware wallet?

## Step 3: Report Results

Report the results on GitHub by commenting under one of the following pull requests: [PR #1341](https://github.com/zkSNACKs/WalletWasabi/pull/1341) or [PR #1905](https://github.com/zkSNACKs/WalletWasabi/pull/1905).

Please include your OS version and your hardware wallet type.
