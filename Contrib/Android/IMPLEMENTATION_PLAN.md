# Android personal implementation and acceptance gates

This is the execution record for the accepted Complete Wasabi Wallet for Android
plan. Source implementation is present; **the release remains blocked**. The final
APK is a qualification candidate. The original target remains new wallets, Wasabi
recovery, receive/send, fee management, history, Tor, opt-in CoinJoin and device
unlocking. Hardware wallets and Play Store publication remain excluded.

| Workstream | Implemented | Acceptance still required |
| --- | --- | --- |
| 1. Supported runtime | Pinned .NET 10 Mono JIT; retained minimal reproduction; published vectors and Core acceptance; source-native Debug/Release matrix passes API 24, 35 and 36, including verified 16 KB pages, at recorded source | Corrected source-native execution on ARM64 phone; untrimmed AOT attempt is separately recorded as unsupported |
| 2. Package separation | `.dev` and `.personal`; Testnet4 development default; session-level mainnet prohibition; Release build guards; two-field coordinator bootstrap | Final delivered-package record and handset installation |
| 3. Authorization and vault | Authenticated Keystore AES-GCM, StrongBox preference/hardware requirement for wallet convenience, per-operation auth, separate RPC encryption, invalidation fallback, background/inactivity lock and CoinJoin key release | Real hardware success/cancellation, device-key loss and OS credential changes on the phone |
| 4. Recovery and persistence | Original backup/password handling; both account public keys; fresh import rescan; atomic/fsynced saves; persisted addresses; wrong-network/watch-only guards | Handset fresh recovery and storage/document-picker interruption tests |
| 5. Transaction pipeline | Immutable proposals; five-minute expiry; serialized confirmation/revalidation; private PSBT; exact-byte durable journal; uncertainty/reservations; funded RBF/cancel/CPFP; lost-reply/restart/reorg tests; production Main/Testnet4 CPFP through Tor | Final personal APK payment and update qualification; handset recovery and update |
| 6. Lifecycle/network/camera | Serialized runtime states; Tor-only public factories; fail-closed transport; RPC batch/reorg handling; reconnect/stop; foreground/wake-lock limits; Camera2 bounded decoding, focus/orientation and generation guards; common parser; API 24 TLS and native matrix pass | Fresh Main/Testnet4 synchronization through Tor after filter-peer discovery correction; physical camera, accessibility, small-screen and background-budget checks |
| 7. CoinJoin | Denomination and credential cancellation regressions fixed; durable reservations; minimum-input/refusal tests; stop/retry serialization; keys released on stop; all nine fault scenarios and 20 consecutive fresh Release rounds pass in CI at clean `24abf94c` | New-source evidence for subsequent changes; desktop single-CPU blame correction; production coordinator availability |
| 8. Qualification/delivery | Host and emulator checks, dependency/source review, stable external signing identity, package inspector, actual-APK UI/update tooling and instructions | Final clean-source package and publication record, complete delivery evidence, then phone and explicitly authorized mainnet gates |

`VALIDATION.md` identifies the actual local checks and retained failures. A passing
round or rerun never erases an unexplained historical failure. The historical
output-registration timeout had insufficient diagnostics for a unique attribution;
the newly reproduced denomination failure has a verified cause and regression.

## Interfaces delivered

- `PaymentProposal` and `BroadcastReceipt` expose immutable review details and an
  explicit uncertain/pending/confirmed/replaced/conflicted outcome. The PSBT and
  signing keys remain internal.
- `WalletSession` serializes preparation, confirmation, replacement and reconciliation.
  It checks synchronization, authorization, wallet/network identity, expiry,
  immutable review integrity and input eligibility before signing.
- `ICredentialVault` exposes per-use authorized password retrieval, optional enrollment
  and removal, and separately encrypted RPC storage. Settings serialization ignores
  plaintext credentials.
- Transaction modifiers accept unsigned preparation while retaining desktop signing
  defaults. Mobile speed-up additionally requires recipient preservation.
- `RuntimeStatus` exposes an immutable lifecycle, synchronization, pending-submission
  and CoinJoin snapshot.
- CoinJoin checkpoint stores are optional in the desktop engine and durable in the
  mobile host. A failed signing checkpoint prevents witness submission.

## Blocking findings and boundaries

1. API 24 TLS rejection re-enters the same native handshake while legacy Conscrypt
   holds its mutex. The retained reproduction, native trace and narrow reentrancy
   guard pass valid/untrusted/wrong-hostname localhost tests. Source Release
   funded wallet/vault and public Tor stop/restart checks pass on API 24 x64 after
   supplementing its missing public CA root; the complete x64 matrix passed in
   Android CI `37285067882` at clean source `24abf94c`.
2. The old candidate's prebuilt runtime/Tor/SQLite libraries fail strict RELRO-end
   checks. Pinned source rebuilds now pass 38 native protection checks, and a
   packaging path verifies their hashes and assembly-container layout. This
   qualifies the recorded x64 execution matrix, while corrected ARM64 execution
   is still required. A first loader build failed on the
   phone because its container constants differed from the SDK; the build now uses
   the pinned SDK stub and retains a regression gate for that mismatch.
3. The actual ARM64 phone passed the isolated stock-runtime
   cryptographic probe. Hardware-backed authorization, physical camera decoding,
   full wallet recovery/updates and handset background behavior remain unverified.
   It disconnected before corrected source-runtime installation.
4. A real mainnet transaction requires a new disposable wallet and an explicitly
   authorized amount and destination. Existing funded seeds are excluded from tests.

All nine deterministic Android fault scenarios passed in that CI run. Finish
current-source public synchronization and final APK checks, then execute
`PHONE_HANDOFF.md` on the connected, authorized phone.
Retain its backup privately. Independent security review and production coordinator
availability remain external dependencies and are never claimed by local tests.

Version 0.3.1/code 6 additionally corrects unsigned cancellation fee sizing and
the engine's occasionally non-final/underflowed locktimes. Retained host/Core
reproductions identify these defects, while earlier API 36 rejection logs lack
the precise node reasons. The broadcaster and synthetic qualification now
retain those reasons. A new current-source Android matrix is required; a
successful earlier rerun alone does not close an unresolved failure.

Version 0.3.2/code 7 prevents zero-credential dependency cycles across amount and
vsize requests, rejects incomplete graphs before issuing requests, and verifies
complete one-use credential allocation. Retained seed-23 production denominations
reproduce the prior graph deadlock; qualification includes the real cryptographic
blame fixture. Public Main/Testnet4 synchronization can be executed independently
and logs no generated receive addresses. Complete the new source's automated
matrix, personally signed APK payment/update checks and independent public-network
results before closing those software gates. Physical-phone and real-bitcoin
authorization gates remain separate.

Version 0.3.3/code 8 corrects two public synchronization defects: P2P progress can
no longer erase a higher peer target, and a stalled compact-filter socket send
cannot block timeout or detach cleanup while holding the assignment lock.
Controlled pre-correction reproductions and retained public stall diagnostics are
qualified separately. Cold Tor startup now uses bounded progress/idle deadlines
after the three-minute cutoff failed on a progressing API 36/16 KB bootstrap.
Complete new-source public Main/Testnet4 synchronization, matrix checks and actual
personal APK payment/update tests before closing these software gates. The selected
desktop coordinator currently redirects its WabiSabi status endpoint; retain its
configuration until the user supplies a working replacement.
