# Personal APK phone handoff

**Version 0.3.3 / version code 8: qualification blocked.** Close all uncompleted
software and handset gates in the delivered `verification.json` before treating this APK
as a real-bitcoin wallet. The APK can be inspected as a signed candidate. The
SM-S948B phone passed the isolated stock-runtime ARM64 cryptographic probe on
Android 16/API 36 with 4096-byte pages. Wallet installation and full handset
qualification remain outstanding; no real-bitcoin amount or destination is authorized.
First rerun the corrected source-native runtime, TLS and SQLite probes on that
phone. The earlier stock-runtime pass does not qualify its new native libraries.

## Files and signing identity

Delivery contains the personal APK, its `.sha256`, `package-manifest.json`, the
certificate verification output, source/dependency hashes and the verification
record. Only `io.wasabiwallet.android.personal-Signed.apk` is the wallet installer.
Neither `.qualification` nor `.uiqualification` is a phone deliverable.

Certificate SHA-256:

```text
894b7317cd04fa09534d9bd0e8d09b08bf90e9cad6718e9020f33a6fd3f7db48
```

Keep the signing identity stable for updates. Never delete wallet data or uninstall
an existing package to fix a signing mismatch. The development package has another
identity and remains independently installed.

## Remaining handset qualification

1. Connect the phone, enable USB debugging and authorize this computer on the phone.
   Record the exact serial, Android version, ABI and page size using read-only ADB
   queries. ARMv7-only phones are outside the provisional ARM64 target.
2. Verify the APK checksum/certificate against the final manifest. Install the
   candidate using an ordinary in-place installation on the selected serial. Example:

   ```powershell
   adb -s PHONE_SERIAL install --no-incremental -r io.wasabiwallet.android.personal-Signed.apk
   ```

   A failed install preserves data. Investigate the reported signature/version or
   compatibility error instead of removing the app.
3. Qualify with a **new disposable test wallet**, initially using test coins. Test
   password access; enroll device convenience only if hardware Keystore supports it.
   Check biometric and device-credential success, cancellation, backgrounding during
   authorization and key invalidation. The original password must still recover it.
4. Scan known BIP21 requests with the physical camera in supported orientations and
   after pause/resume. Confirm the displayed destination and amount match the QR.
   Scanning must never sign. Check small-screen scrolling, keyboard behavior,
   large text, accessibility navigation and screenshot protection.
5. Check background lock, two-minute inactivity lock, network changes, Tor-child
   loss/restart, process death, OS foreground-service limits and wake-lock/battery
   behavior. Reconcile pending signed bytes before permitting conflicting operations.
6. Export an encrypted backup with fresh authorization and retain it privately with
   the original words/password. Recover into a fresh test installation/device state
   without its database or Keystore keys. Do not erase a live wallet to perform this
   test. Verify both accounts, addresses, balance and restored signing.
7. Install an update signed with the same certificate. Verify wallet files, encrypted
   RPC credentials, device authorization and pending transaction identity persist.

Record what actually executed, including failed checks. Emulator logs do not satisfy
physical camera, hardware authorization or handset compatibility requirements.

## Final mainnet gate

After the preceding checks pass, create a **new disposable mainnet wallet**. Privately
retain its recovery words and original Wasabi password; do not put them in this chat,
source control, logs or build inputs. An existing funded seed is never a fixture.

The user must explicitly authorize the small receive/send test amount, fee limit and
recipient destination. Wait for that authorization before a real-bitcoin transfer.
Verify the received output, exact sent recipient/amount/fee, broadcast identity,
confirmation and restored-wallet observation. There is no assumed default amount,
destination or fee budget. A technical test is not independent security review or
proof of real-world CoinJoin anonymity.
