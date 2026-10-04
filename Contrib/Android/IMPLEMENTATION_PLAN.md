# Android personal implementation and acceptance gates

This is the execution record for the accepted Complete Wasabi Wallet for Android
plan. Source implementation is present; **the release remains blocked**. The final
APK is a qualification candidate. The original target remains new wallets, Wasabi
recovery, receive/send, fee management, history, Tor, opt-in CoinJoin and device
unlocking. Hardware wallets and Play Store publication remain excluded.

| Workstream | Implemented | Acceptance still required |
| --- | --- | --- |
| 1. Supported runtime | Pinned .NET 10 Mono JIT; retained minimal reproduction; fixed packaging/interpreter selection; published vectors and Core acceptance on x64 Debug/Release | ARM64 execution; supported resolution of API 24 TLS and native page findings; AOT remains separately unqualified |
| 2. Package separation | `.dev` and `.personal`; Testnet4 development default; session-level mainnet prohibition; Release build guards; two-field coordinator bootstrap | Final delivered-package record and handset installation |
| 3. Authorization and vault | Authenticated Keystore AES-GCM, StrongBox preference/hardware requirement for wallet convenience, per-operation auth, separate RPC encryption, invalidation fallback, background/inactivity lock and CoinJoin key release | Real hardware success/cancellation, device-key loss and OS credential changes on the phone |
| 4. Recovery and persistence | Original backup/password handling; both account public keys; fresh import rescan; atomic/fsynced saves; persisted addresses; wrong-network/watch-only guards | Handset fresh recovery and storage/document-picker interruption tests |
| 5. Transaction pipeline | Immutable opaque proposals; five-minute monotonic expiry; serialized confirmation/revalidation; internal unsigned PSBT; exact-byte durable journal; uncertainty/reservations; funded RBF/cancel/CPFP; lost-reply/restart/reorg tests; explicit receipt/history states | Complete fault matrix, production CPFP fee-service and final-package qualification |
| 6. Lifecycle/network/camera | Serialized runtime states; Tor-only public factories; fail-closed transport; corrected RPC batch/reorg handling; reconnect/stop handling; foreground service limits/wake-lock release; Camera2 bounded decoding, focus/orientation and generation guards; common parser | API 24 TLS fix; final native page checks; physical camera, accessibility, small-screen and background-budget checks |
| 7. CoinJoin | Denomination regression fixed; durable address/input/signing checkpoints; minimum-input/refusal tests; stop/retry race fixed; keys released on stop; initial-runtime 20 fresh Release rounds and 20 Debug rounds; updated-runtime results recorded separately in delivery evidence | Full Android dropout/blame/restart/signing-interruption matrix; production coordinator availability |
| 8. Qualification/delivery | Host checks, emulator matrix, dependency query, source review, external stable signing identity, package inspector, pure-Java actual-APK test/update tooling and installation instructions | Close runtime/native findings, final package checks and publication record, then phone and authorized mainnet gates |

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

1. API 24 establishes Tor transport but stalls in platform TLS on both initial
   Mono 10.0.9 and stable servicing 10.0.12. The supported pinned
   runtime needs a verified remedy. Certificate checks and transport privacy cannot
   be weakened to bypass it.
2. Static 16 KB RELRO checks still flag prebuilt runtime/Tor/SQLite binaries after
   the servicing update. The generated
   app-library omission is corrected using documented linker flags, while prebuilt
   binaries remain unchanged. Successful 16 KB x64 execution is narrower evidence
   than universal native compatibility.
3. No actual handset is connected. ARM64 execution, hardware-backed authorization,
   physical camera decoding and handset background behavior cannot be inferred from
   emulator results.
4. A real mainnet transaction requires a new disposable wallet and an explicitly
   authorized amount and destination. Existing funded seeds are excluded from tests.

Finish the remaining deterministic Android fault scenarios after the runtime/native
baseline passes, then execute `PHONE_HANDOFF.md` on the connected, authorized phone.
Retain its backup privately. Independent security review and production coordinator
availability remain external dependencies and are never claimed by local tests.
