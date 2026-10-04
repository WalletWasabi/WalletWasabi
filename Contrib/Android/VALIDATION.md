# Android personal candidate verification

Recorded 2026-10-05 (Asia/Singapore). This record distinguishes implementation,
local emulator evidence, static package checks, and uncompleted release gates.
**Overall release qualification is BLOCKED. No physical phone or real mainnet
payment was tested. This work is not an independent security audit.**

## Runtime and host checks

The selected baseline is SDK 10.0.401, Android workload 36.1.69 and Mono 10.0.12
JIT. Trimming, interpreter, assembly store, ReadyToRun and AOT are disabled.
The original assembly-store initialization deadlock and interpreter proof failure
were isolated with `RuntimeProbe`. Individual assembly packaging and Mono JIT
execute the existing engine without replacing Bitcoin algorithms or serialization.
Release AOT with trimming disabled was separately attempted and rejected by the
workload with XA1030, including the Mono 10.0.12 reproduction recorded in
`mono12-untrimmed-aot-build.log`. AOT is unqualified; the candidate uses JIT. Experimental
Android CoreCLR is not selected.

The workload initially supplied Mono 10.0.9. Stable servicing runtime 10.0.12
passed the isolated Release reproduction before adoption. The pin applies only to
`Microsoft.NETCore.App`, leaving the Android runtime packs at 36.1.69. The
qualification results below distinguish these versions; an earlier pass is not
silently attributed to the updated runtime. Microsoft publishes the current
[.NET servicing releases](https://github.com/dotnet/core/blob/main/release-notes/10.0/10.0.12/10.0.12.md).

The runtime suite checks genesis/serialization, published BIP39, BIP32, BIP84,
BIP86 and BIP340 vectors, ECDSA verification, modified-message rejection, Android
marshalling and managed WabiSabi credential proofs. Funded suites additionally
require Bitcoin Core acceptance of Segwit ECDSA and Taproot payments.

| Host check | Local result | Evidence under `artifacts/android/` |
| --- | --- | --- |
| Mobile tests | 35 passed | `mobile-tests.log` |
| WabiSabi suite, including coordinator/client safeguards | 322 passed | `desktop-wabisabi-tests.log` |
| Relevant SafeFile, KeyManager, TransactionFactory, CPFP-provider and Tor settings checks | 52 passed | `desktop-relevant-tests.log` |
| RPC/P2P filter synchronization regressions | 15 passed | `filter-regression-tests.log` |
| Desktop Fluent build | Passed, zero warnings/errors | Local build output |
| Personal and qualification Release builds | Passed, zero warnings/errors | Local build output |
| NuGet transitive vulnerability query | No vulnerabilities reported by queried feed | `dependency-audit.json` |

A dependency query does not audit native components or establish absence of
vulnerabilities. A clean build does not establish wallet safety.

## Android execution matrix

Only **x86_64 actually executed**. Some emulator ABI lists include ARM64 translation
support; those lists do not prove an ARM64 execution. ARM64 remains a handset gate.

The expanded initial **Mono 10.0.9** matrix:

| Emulator | API / actual page size | Release runtime, funded wallet, faults and vault | Evidence directory |
| --- | --- | --- | --- |
| emulator-5582 | 24 / 4096 | Passed | `device-20b9f47c76b1481c9f4213d6314b5bcd` |
| emulator-5580 | 35 / 4096 | Passed in two runs | `device-a6d11a5adb6a4dab9144ff543ff361ca`, `device-91f2dd2dab0e411cbd748fb96efffa4b` |
| emulator-5584 | 36 / 4096 | Passed, including funded faults | `device-b1f6543700dd40f3beb0ece2d0df5907` |
| emulator-5586 | 36 / **16384** | Passed, including Tor | `device-3b14a96ec38b471684f22d85cc00c716` |

The updated **Mono 10.0.12** matrix:

| Emulator | API / actual page size | Release runtime, funded wallet, faults and vault | Evidence directory |
| --- | --- | --- | --- |
| emulator-5582 | 24 / 4096 | Passed | `device-bfc5872e62994b409aba67098295e28f` |
| emulator-5580 | 35 / 4096 | Passed | `device-87c9d47731504101a7307c12571fdf34` |
| emulator-5584 | 36 / 4096 | Passed | `device-d98d4d918e124e71996f38acb7505f1b` |
| emulator-5586 | 36 / **16384** | Passed | `device-8d8d9049094d4d91ae275e9f603aade2` |

Earlier Debug runtime/vault checks passed on the same four environments; Debug
funded-wallet and 20-round evidence also exist. This is not a claim that the entire
latest Debug/Release failure matrix ran everywhere. CI defines that larger matrix;
a current remote CI result must be read separately.

On Mono 10.0.12, the Debug runtime (including the session-level mainnet prohibition)
and vault suites passed on all four environments, recorded in
`mono12-debug-api35-runtime.log`, `mono12-debug-api35-vault.log` and
files matching `mono12-debug-emulator-*-runtime.log` and
`mono12-debug-emulator-*-vault.log` (one file per serial/mode). The funded Debug
runtime, wallet, faults and vault suites also passed on API 35 in
`device-4c8a3afdc0564618bbec51573c880aec/`.

The funded Release wallet suite checks persisted Taproot and Segwit addresses,
exact recipient amount, password/stale-review rejection, duplicate confirmation,
recipient-preserving RBF, cancellation returning outputs to the wallet, mining,
history, lock/key clearing, encrypted backup import into a fresh directory with no
previous database or device keys, restored signing, and both seed-derived accounts.
Vault checks cover encrypted RPC round-trip, reopening, tamper rejection, key loss
and removal. Hardware-backed per-use biometric success/cancellation remains untested.

The Release funded failure suite passed on API 35 in
`device-91f2dd2dab0e411cbd748fb96efffa4b/` and on API 36 in
`device-b1f6543700dd40f3beb0ece2d0df5907/`. It expires a proposal, cancels confirmation,
lets Core accept a transaction while deliberately discarding its RPC reply, checks
uncertain-input reservations and duplicate confirmation, reopens the wallet, and
reconciles/rebroadcasts the exact approved bytes. A same-height reorganization makes
the payment pending and then reconfirms the same transaction. A funded CPFP child
also passes preparation, authorization, Core acceptance, journaling and confirmation
without changing its parent's recipients. The regtest-only fixture supplies actual
Core mempool metadata to the existing fee-info interface; it does not qualify the
production public CPFP fee endpoint or replace the production transaction pipeline.

These tests found delayed RPC-batch continuations being classified as a reorg,
same-height RPC reorgs being missed, and stale P2P headers repeatedly rolling back
valid RPC filters. RPC filters now use the RPC chain consistently, await batch
completion and check the tip hash even when the height is unchanged. Five new
regression cases failed before their corrections; all 15 related cases now pass.
Failed Android attempts and their engine traces remain retained, including
`device-fef57b831e064a16ad9005418de71870/` and
`device-ed70c9acbdad43409a07f36d4d9e99f6/faults-engine-before-stop.log`.

## CoinJoin qualification

On initial Mono 10.0.9, twenty consecutive **fresh Release rounds passed** with an independently keyed
host wallet and the real Wasabi coordinator. Both participants completed every
round; Core accepted and mined the transactions. Evidence:
`device-ca93fb3dcb1b4ecdbf905aff0d618648/coinjoin-1.log` through `coinjoin-20.log`,
plus participant and coordinator logs for each round. A separate Debug 20/20 run
is retained in `device-2f951bfd1ba64bde83b9656bfdefd212/`. These counts do not qualify
the subsequent Mono 10.0.12 update. Its separate fresh-round run is retained in
`device-b441f50c6e2e40a39dce17b06f891bc1/`; consult the delivered
`verification.json` for the final verified count, round transaction identities and
evidence hashes. An unfinished run does not satisfy the twenty-round gate.

A deferred random script selection in denomination construction was reproduced
and fixed by materializing each choice once. Its regression failed against the
old behavior and passes against the change. Tests also verify minimum-input
signing refusal and that a failed durable checkpoint prevents signature submission.
The manager's stop/retry race was fixed; stops release credentials while locked.

An earlier historical output-registration timeout lacked private engine diagnostics.
The reproduced denomination defect is resolved, but that does not uniquely prove
the cause of the historical timeout. A later Release trial failed at round seven
because the host fixture called start while still catching up with the Android
funding block; retained logs establish that cause. Both fixture participants now
recheck the final funding height after their readiness barrier. The fresh 20/20
rerun passed after this correction.

These rounds do not establish real-world anonymity. A complete Android-specific
fault matrix for dropout, blame rounds, coordinator restart, process death during
signing and safe stopping at each protocol phase is still an open gate. Unknown
signed outcomes retain reservations until reconciliation; they are not inferred
rejected from an unavailable coordinator.

## Tor and native package findings

The 16 KB Release Tor suite passed public TLS verified by the destination as a
Tor exit, stopped-Tor fail-closed behavior, idempotent disposal, restart and another
verified public request on both runtimes. Evidence: initial Mono 10.0.9
`tor-api36-16k-final.log` and updated Mono 10.0.12 `mono12-tor-api36-16k.log`. GeoIP assets are
normalized to LF atomically; CRLF records had caused parser warnings during startup.

**API 24 public TLS is blocked.** Tor bootstraps and establishes a SOCKS connection,
but managed `SslStream` stalls during the platform handshake. The raw transport
reproduction reports TCP and Tor-tunnel success before the stall. A native dump
shows the worker in `SSL_do_handshake_bio`; this narrows the failure and does not
prove an upstream fix. Evidence includes `tor-api24-tls12.log`,
`tor-api24-native-stack.txt` and retained bounded Tor diagnostics. Cancellation did
not interrupt the native stall; the owned emulator fixture was force-stopped.
TLS certificate validation was not disabled and no direct-network fallback was added.
The original raw-transport reproduction also stalls on stable Mono 10.0.12 after
TCP and Tor-tunnel success. Its 45-second cancellation did not interrupt the
native operation; the owned process was stopped after more than four minutes.
Evidence: `mono12-api24-transport-engine.log`, `mono12-api24-native-stack.txt` and
`mono12-api24-transport-verification.json`. An API 35 attempt separately timed out
in Tor bootstrap (`mono12-tor-api35.log`); that network/bootstrap failure is not
misclassified as the API 24 TLS defect.

Strict package inspection verifies v2/v3 signatures and the dedicated certificate,
ZIP alignment and ELF LOAD alignment. It decompresses managed assembly payloads,
checks absence of test instrumentation/fixture markers, and verifies their hashes
against SDK-prepared outputs. The generated `libxamarin-app.so` omitted the common
page-size flag; a scoped target now relinks it with the SDK's exact response file
and the documented flag, retaining RELRO and the original security options.

However, static inspection still reports non-16-KB RELRO ends in prebuilt .NET
runtime, Tor and ARM64 SQLite libraries. Actual 16 KB emulator tests passed, but
that does **not** close these static findings or qualify other devices. They are
recorded in the manifest and the inspection tool returns failure. See
[Android's native page-size guidance](https://developer.android.com/guide/practices/page-sizes).
Do not patch those runtime binaries or disable RELRO to turn the check green.
The servicing update's personal APK still has 15 static RELRO findings across
the two architectures, recorded in `mono12-precommit-package-manifest.json`.

## Actual personal APK and update evidence

A separate pure-Java, co-signed harness drove the **actual personal APK** on API 36
with 16384-byte pages, without changing its security flags. Wrong-password
confirmation was rejected; one 0.1 BTC regtest payment survived duplicate taps.
Background lock, original-password unlocking, two-minute inactivity lock, process
death and a version-code 2 to 3 update passed. The wallet and encrypted RPC vault
remained usable and the pending journal hash stayed identical across restart/update.
Core accepted and confirmed the approved transaction.

Initial evidence: `native-ui-e811ce520ebb4fc39ea0d0b2a881c31f/verification.json`.
Baseline APK SHA-256: `c7c1f050903fdf59158e08962fe8b6add1db9c52b572acabe3ac58822d5f12b5`.
Tested update SHA-256: `38a82d7afaa444a49ee74e5272f3d9602946ccbb8124cafb1575d1e7631d2dfd`.
Subsequent source/build changes require final-package inspection and retesting;
those hashes must not be presented as the final delivery hash.

The implementing review reproduced a delayed-unlock completion reopening the
interface after background/resume. The original signed Mono 10.0.12 APK failed
the co-signed native lifecycle test (`late-unlock-before/instrumentation.log`).
UI lock generations now invalidate old completions, and busy operations and
recovery-word screens obey inactivity locking. The same native test passed all
three background/resume cycles after correction
(`late-unlock-after/instrumentation.log`). Hardware authentication remains a
separate handset gate; this test uses the synthetic wallet's original password.

The updated Mono 10.0.12 personal candidate also passed the complete native UI
suite and version-code 3 to 4 update. Evidence is retained in
`native-ui-version3-to4/verification.json` and
`native-ui-version4.log`; the original version 2 to 3 record is preserved in
`native-ui-version2-to3/`. The version 3 baseline hash is
`cbb879fbae5085fba88abb8aca46b8b7eb916462dd6dbf761c117663d1bea703`; the
tested version 4 update hash is
`5166cee2a8657ec099bc5ea8962383be71a04ab57669ed50610b775616050af0`.
This was a synthetic regtest payment, not a mainnet or physical-phone test.
These prepublication hashes are not the delivery checksum. The delivered
`package-manifest.json` and `verification.json` record the final source publication,
APK checksum and native retest against that exact package. The final native test
also checks in-place installation over version 4, separately from the retained
version 3 to 4 evidence.

The final delivery manifest records the exact APK checksum, certificate, source
commit/tree, source-file hashes, managed/native payload hashes and dependency locks.
Preserve failed findings alongside successful evidence. Local signing and testing
never imply physical-handset compatibility, successful mainnet spending or an audit.

## Unclosed acceptance gates

- Supported resolution and rerun of API 24 TLS and strict native RELRO findings.
- Complete Android failure scenarios, production CPFP fee-service qualification and CoinJoin
  phase-specific disruption/restart tests.
- Physical phone architecture/version, real biometric/credential authorization and
  cancellation, camera rotation/autofocus/scanning, accessibility/large text,
  background budgets, battery behavior, recovery and update preservation.
- New disposable mainnet wallet, privately retained backup and an explicitly
  authorized small receive/send/confirmation test. No funded existing seed is a fixture.
- Current production coordinator availability and any independent security review.

Hardware-wallet integration and Play Store publication are outside this release.
