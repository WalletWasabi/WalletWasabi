# Android development-build validation

Verified on 2026-10-04 against the existing Wasabi source at
`5e26b4f83a27b6d5e70d5db7fe68a7e1a3b5113c`, plus this Android implementation.

## Environment and results

- Windows, .NET SDK 10.0.401, Android workload 36.1.69, JDK 21, SDK platform 36.
- Dedicated Android API 35 x86_64 emulator. No physical phone was used.
- 29 mobile tests passed; 49 existing key-manager, transaction-factory, CPFP,
  and Tor-settings tests passed.
- Debug and Release APK builds completed with zero warnings and zero errors.
- The existing desktop application built successfully with locked package restore,
  zero warnings, and zero errors.
- Pinned Bitcoin Core 31.1 ran in a fresh, isolated regtest directory.
- On-device **wallet** suite passed funded Taproot receive, exact reviewed send,
  invalid-password/stale-review rejection, Bitcoin Core acceptance, confirmation,
  history, credential clearing, persistence, seed recovery and rescan, WabiSabi
  credential issuance/validation, and QR encoding/decoding.
- On-device **coinjoin** suite passed a full round with an independent host wallet
  using different keys. Both participants completed the same round. Bitcoin Core
  accepted the transaction, which was mined and observed by the Android wallet.
  Safe stop and signing-key clearing passed. Solo-CoinJoin protection stayed enabled.
- On-device **tor** suite passed process startup, 100% bootstrap, SOCKS negotiation,
  and a public TLS request confirmed by the destination as a Tor exit connection.
- Native UI checks passed wallet recovery, receive QR display, amount/address display,
  send entry, camera permission and active preview, scanner return, and background lock.
  The QR camera path was opened on the emulator; physical-camera decoding is unverified.
- The app connected through Tor to six mainnet peers and to Testnet4 peers. These
  checks establish public-network connectivity, not a funded mainnet payment test.
- The installed Release package launched successfully with `SECURE` window flags,
  no debug instrumentation, and disabled backup. Its v2/v3 signatures and APK alignment
  passed. Both architectures are packaged; all 26 native ELF libraries have load
  segments aligned for 16 KB pages. ARM64 execution and a 16 KB device were not tested.

Local device-suite output is in
`artifacts/android/device-004a25667251493889171f69b2e42ec9/`.
Visual QA screenshots are in `artifacts/android/launcher.png` and `receive.png`.
The screenshots come from a dedicated Debug build with test-only screenshot access;
the distributed Release package blocks screenshots.

## Independent CI verification

[GitHub run 37173987041](https://github.com/nopara73/WalletWasabi/actions/runs/37173987041)
passed on Linux at source commit `419332cfbe4e5161d38cdf85cb6d8ad2a599dacc`.
It passed all 29 mobile tests, built both APK configurations, and passed all three
device suites on a fresh API 35 x86_64 emulator. The CoinJoin transaction was
broadcast and confirmed; both independent participants completed the round.
The development APK and device logs are retained as workflow artifacts.

Earlier run 37171537798 failed before tests because the runner lacked KVM access;
the workflow now verifies hardware acceleration. Run 37172782782 passed the wallet
suite but timed out after the Android participant exited during output registration.
That trial did not retain the private engine log, so its underlying cause remains
unverified. The next run passed without changing wallet or protocol code. The
fixture now retains private engine diagnostics for future failures; the passing run
does not erase the earlier timeout or establish production reliability.

## Release limits

This is a development-signed 0.1.0 APK, not an independently audited production
release. .NET 10 CoreCLR on Android is experimental. Physical ARM devices, store
distribution, hardware-wallet signing, real-funds mainnet payments, and real-world
CoinJoin anonymity have not been verified. See [README.md](README.md) for build, operation,
test commands, supported features, and additional platform limits.
