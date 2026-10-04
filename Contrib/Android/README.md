# Wasabi Wallet for Android

The Android app retains Wasabi's Bitcoin and WabiSabi engines and uses native
Android views, Camera2, Android Keystore and a bundled Tor process.

## Personal candidate status

Version **0.2.0 / version code 4** produces a personally signed APK. This is a
**qualification candidate, not a qualified real-funds release**. The implementation
and evidence are described in [VALIDATION.md](VALIDATION.md), with outstanding
acceptance gates in [IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md) and the
[source review](SECURITY_REVIEW.md).

The supported runtime selection is .NET 10 **Mono JIT**, with no trimming,
interpreter, assembly store, ReadyToRun or AOT. The minimal reproduction is in
`Contrib/Android/RuntimeProbe`. Published Bitcoin vectors and funded Bitcoin Core
acceptance pass on x86_64 emulators. ARM64 is packaged but has not executed on a
physical phone. Android 7/API 24 public TLS remains blocked. Strict 16 KB native
RELRO inspection also reports findings in bundled prebuilt libraries, despite
successful runtime tests on the 16 KB emulator. These findings remain visible;
tests are not an independent security audit.

## Build and signing

Use the exact baseline in [toolchain.json](toolchain.json): SDK 10.0.401, Android
workload 36.1.69, Mono 10.0.12, JDK 21, Android platform 36 and build-tools 36.0.0.
The root `global.json` pins the SDK without roll-forward. `MonoRuntime.props`
pins the .NET servicing runtime through framework-reference metadata while
retaining the installed Android workload. Initial Mono 10.0.9 evidence is
identified separately in the validation record.

```powershell
dotnet workload install android@36.1.69
./Contrib/Android/prepare-tor.ps1
dotnet build WalletWasabi.Android/WalletWasabi.Android.csproj -m:1 -p:WasabiSkipBundledApps=true
```

Development packages use `io.wasabiwallet.android.dev`, default to Testnet4, and
reject mainnet below the UI, including an attempted policy-object override. Debug
instrumentation is confined to this package. `WasabiUiTest=true` is rejected in
Release. Keep existing development installations and their data intact.

A personal build uses `io.wasabiwallet.android.personal`, Release configuration,
the dedicated external key and a bootstrap JSON containing **only** `Coordinator`
and `CoordinatorIdentifier`. Desktop wallet files, passwords and RPC credentials
are not build inputs. The personal candidate copies the selected desktop
coordinator URL and identifier; CoinJoin still requires an explicit start.

On the configured Windows workstation:

```powershell
./Contrib/Android/build-personal.ps1 -CoordinatorBootstrap artifacts/android/PersonalCoordinator.json
```

The APK and checksum appear in
`WalletWasabi.Android/bin/Release/net10.0-android36.0/`.
The key lives outside the repository under
`~/.codex/secrets/wasabi-android-personal/`; its password uses Windows DPAPI and
restricted local access. Keep both the key and a recoverable private password
backup. Losing the signing identity prevents in-place updates. Never uninstall a
wallet or delete its data to resolve a signature error.

Recorded personal certificate SHA-256:

```text
894b7317cd04fa09534d9bd0e8d09b08bf90e9cad6718e9020f33a6fd3f7db48
```

`NativePageAlignment.targets` reuses the SDK's linker response file and adds the
[documented common-page-size flag](https://developer.android.com/guide/practices/page-sizes)
for the generated application metadata library, retaining RELRO, immediate symbol
binding and non-executable stacks. It does not patch prebuilt runtime or Tor code.

## Wallet operation

- Create a new wallet, recover using the original words and Wasabi password, or
  import an encrypted Wasabi JSON backup. Both BIP84 and BIP86 account public keys
  are checked at unlock. The password remains the original recovery passphrase.
- Import resets the scan height and rescans with a fresh database. Wallet saves,
  receive/change allocations and journals use atomic publication and file flushes;
  Android/Linux also flush the containing directory.
- Receive a fresh address and share a BIP21 request. Camera and payment intents use
  the same parser; scanning fills payment fields without authorizing a payment.
- Prepare a payment or replacement, review recipients, amount, fee and total, then
  authorize that operation. The unsigned PSBT remains private. Proposals expire
  after five minutes and are bound to the wallet, network and selected transaction.
- Signed bytes are journaled before broadcast. An uncertain result reserves inputs;
  restart reconciliation recognizes or rebroadcasts those same bytes. Duplicate
  confirmation returns the recorded outcome. It does not construct another payment.
- Speed-up preserves recipient amounts through RBF or eligible CPFP. Cancellation
  requires its own review and authorization and competes with confirmation of the
  original transaction. Unsupported or ineligible modifiers report unavailability.
- Enable device unlocking only on Android 11+ with supported hardware-backed
  Keystore authentication. StrongBox is preferred. Software-only devices retain
  password access. Payments, replacements, backup disclosure and CoinJoin start
  require fresh authorization. RPC credentials use a separate encrypted vault.
- Backgrounding and two minutes of inactivity lock the UI. Only explicitly started
  CoinJoin retains signing credentials while locked; stopping releases them.
  Device-key invalidation requests the original password and preserves the wallet.
- Public peers, filters, fees, coordinator requests and external broadcast use Tor.
  RPC accepts only loopback or onion addresses; remote onion RPC uses Tor. Tor loss
  never enables direct public networking. Foreground work and wake locks remain
  subject to Android's background limits.

Backups and device-transfer backup are disabled. Release windows retain screenshot
protection; secret fields disable autofill and personalized keyboard learning.
This cannot guarantee removal of every immutable managed string from process memory.
Hardware-wallet integration and Play Store publication are excluded.

## Verification tools

```powershell
dotnet test --project WalletWasabi.Mobile.Tests/WalletWasabi.Mobile.Tests.csproj -p:WasabiSkipBundledApps=true --no-progress --no-ansi
./Contrib/Android/test-device.ps1 -Serial emulator-5580 -DownloadBitcoinCore
./Contrib/Android/build-personal.ps1 -CoordinatorBootstrap artifacts/android/PersonalCoordinator.json -QualificationHarness
./Contrib/Android/test-device.ps1 -Serial emulator-5580 -DownloadBitcoinCore -ReleaseEngine -Modes runtime,coinjoin -CoinJoinRounds 20
```

`test-device.ps1` only accepts a selected emulator, owns a new regtest directory,
verifies its pinned Bitcoin Core download, rejects occupied fixture ports, and
cleans up its own processes. Its runtime, wallet, faults, vault, Tor and CoinJoin
suites retain synthetic diagnostics. `-ExpectedPageSize 16384` verifies execution
on a real 16 KB kernel; an AVD name alone is not evidence.

`ReleaseHarness` is a separate, emulator-only package compiled in Release with the
personal engine policy. **Never distribute it or install it on a real-funds phone.**
`NativeUiHarness` is pure Java and separately co-signed; it drives the actual personal
APK with its security flags intact. `test-release-ui.ps1` tests funded UI sending,
wrong-password rejection, duplicate taps, locking, restart and data-preserving
updates. It refuses unrelated wallets and never clears app data.

`inspect-package.py` needs Python 3.9+ and `lz4==4.4.4` for qualification only. It
verifies the certificate, manifest, ZIP and ELF alignment, decompresses every
managed payload to check for test markers, compares engine payloads to SDK-prepared
build outputs, and writes source/dependency/package hashes. Failed native checks
are recorded and produce a nonzero exit; they are not silently waived.

The CI workflow defines API 24, 35, 36 and 36/16 KB Debug and Release matrix jobs,
plus a separate 20-round Release CoinJoin job. A configured workflow does not prove
that a remote run passed. Current local checks and limitations are recorded in
[VALIDATION.md](VALIDATION.md). Use [PHONE_HANDOFF.md](PHONE_HANDOFF.md) for the
remaining handset gate; do not use an existing funded seed as a fixture.
