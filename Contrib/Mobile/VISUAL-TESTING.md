# Native mobile rendering and acceptance gates

The production mobile UI is Avalonia C# and compiled XAML. The headless suite loads
those same controls and styles and captures actual Skia framebuffers. It does not
render HTML or paint substitute screenshots.

## Gate contract

`Mobile design validation / shared-ui` must succeed before accepting a mobile UI
change. It builds both the shared application and native test assembly, executes
the complete native suite twice in independent processes, compares the first run
with explicitly reviewed baselines, and compares both runs with each other.

The workflow pins Ubuntu 24.04, .NET SDK 10.0.401, Python 3.13 and the visual-tool
dependencies. It uses UTC and the application's invariant-globalization setting.
The native test application loads the existing Inter dependency and real mobile
resources. No Android/iOS workload or physical device is required for this suite.

A successful job requires all of the following:

- Every discovered native test executed and passed. Empty, failed, skipped,
  incomplete and inconsistent TRX results are rejected.
- Every emitted PNG has a validated `.frame.json` manifest; its image name,
  dimensions, engine and PNG-byte hash agree with the actual file. Unmanifested
  PNGs, including nested or differently cased filenames, cannot bypass the gate.
- Every canonical frame matches its independently committed approval. Missing,
  extra/unreviewed, corrupt or changed captures fail the job. Previously approved
  cases cannot silently disappear from the suite.
- Every frame and its rendering context match the second fresh process exactly.
  Comparing a directory with itself is rejected.

The checked-in approvals use `comparison: rgba-sha256`: SHA-256 of the dimensions
and decoded RGBA8 raster, independent of PNG compression and metadata. **Any changed
channel fails. There are no masks, resizes or allowed pixel differences for these
approvals.** The comparator also supports PNG baselines with diagnostic tolerances;
those options do not apply to fingerprint approvals. Inspect each frame's
`verification` field rather than treating the generic tolerance defaults as the
policy for exact-hash records.

`MobileSnapshotState` prepares only the current test window for static capture. It
settles transitions, uses Fluent's declared final expander-chevron angle and hides
the blinking insertion caret. It preserves focus, focus outlines, bindings,
commands, layout and content. Production motion is not disabled. Animation timing
and touch delivery are separate validation concerns.

## Reproduce locally

Use the pinned CI environment when comparing exact pixels. Other operating
systems, font configurations or Skia versions may legitimately produce different
rasters and must not be enrolled as replacements without review.

```sh
set -eu
python3 -m pip install -r Contrib/Mobile/requirements-visual.txt
python3 -m unittest discover -s Contrib/Mobile -p 'test_*.py' -v
python3 Contrib/Mobile/mobile_design.py --check

export TZ=UTC
export GITHUB_SHA="$(git rev-parse HEAD)"
export WASABI_MOBILE_TEST_ARTIFACTS="$PWD/TestResults/mobile-previews"

dotnet build WalletWasabi.Fluent/WalletWasabi.Fluent.csproj \
  -c Release -p:UseCdp=false -bl:TestResults/mobile-ui-build.binlog
dotnet build WalletWasabi.Fluent.Mobile.Tests/WalletWasabi.Fluent.Mobile.Tests.csproj \
  -c Release -p:UseCdp=false -bl:TestResults/mobile-tests-build.binlog

dotnet vstest WalletWasabi.Fluent.Mobile.Tests/bin/Release/net10.0/WalletWasabi.Fluent.Mobile.Tests.dll \
  --logger:"trx;LogFileName=mobile-ui.trx" --ResultsDirectory:TestResults
python3 Contrib/Mobile/verify_test_results.py TestResults/mobile-ui.trx

export WASABI_MOBILE_TEST_ARTIFACTS="$PWD/TestResults/repeat/mobile-previews"
dotnet vstest WalletWasabi.Fluent.Mobile.Tests/bin/Release/net10.0/WalletWasabi.Fluent.Mobile.Tests.dll \
  --logger:"trx;LogFileName=mobile-ui-repeat.trx" --ResultsDirectory:TestResults/repeat
python3 Contrib/Mobile/verify_test_results.py TestResults/repeat/mobile-ui-repeat.trx

python3 Contrib/Mobile/verify_repeatability.py \
  TestResults/mobile-previews TestResults/repeat/mobile-previews \
  --output TestResults/repeatability.json
python3 Contrib/Mobile/visual_review.py report \
  --actual TestResults/mobile-previews --baseline Contrib/Mobile/Baselines \
  --output TestResults/mobile-visual-review
```

Upstream uses MTP/xUnit v3, while Avalonia 11's headless adapter uses VSTest/xUnit v2.
The explicit build plus `dotnet vstest` sequence is intentional; do not replace it
with an incompatible invocation or accept a zero-test result.

## Review and enrollment

Download the `mobile-ui-validation` artifact. Read both TRX files and
`repeatability.json`, then open `mobile-visual-review/index.html`. Inspect actual
native frames at every affected viewport and theme, including error, empty,
privacy-hidden and compact-phone scrolled states. Fix rendering or binding defects
before approving. A repeatable defect is still a defect.

New captures intentionally fail the baseline gate until reviewed. Enrollment is an
explicit local operation, never a CI step:

```sh
python3 Contrib/Mobile/visual_review.py approve \
  --actual ReviewedCaptures --baseline Contrib/Mobile/Baselines \
  --fingerprints-only --reviewed
```

Put only the reviewed new cases in `ReviewedCaptures`, with both PNGs and manifests,
when extending the existing corpus. Replacing existing approvals additionally
requires `--replace` and review of each changed image. Preserve source run, artifact
and commit provenance in the review record. The low-level enrollment command writes
an approval manifest; it does not create or certify that provenance for you.

Commit the approval separately from the producing implementation. A subsequent
independent CI run must pass against it. Never use `--allow-unreviewed`, automated
approval, changed thresholds or `continue-on-error` to make CI green. Fingerprints
identify reviewed rasters but do not reconstruct approved images; retain the
producing artifacts for inspection and future comparison. A fingerprint mismatch
must be investigated, not regenerated blindly.

## What each capture proves

`contentKind` separates evidence that must not be conflated:

| Kind | Scope |
| --- | --- |
| `bound-fixture` | Populated production wallet surfaces and real presentation types, using test-only wallet metadata and command fixtures. |
| `bound-regtest-presentation` | Production Receive/Request Amount surface, deterministic regtest address, real QR encoder and injected clipboard boundary. Scannable cases also decode the rendered framebuffer through the production reader's ZXing/Skia pipeline. |
| `bound-regtest-authorization` | Production passphrase dialog, view model, command and `WalletAuthModel`/`PasswordHelper`, using an isolated in-memory regtest wallet. Incorrect input must not complete authorization; a correct retry returns the real dialog result. |
| `unbound-layout` | View construction, template layout, resource resolution and safe unavailable-state defaults. Not a successful wallet operation or persistence test. |
| `synthetic-components` | Deliberately synthetic native component reference scene. Not wallet data. |

Receive amount changes cancel obsolete QR work and clear stale matrices. Invalid
amounts cannot be copied as valid requests. Returning from Request Amount to Receive
clears the hidden amount before exposing the bare address. QR failure permits
copying a valid payload and retrying QR generation; clipboard failure remains
visible without discarding a valid QR. Source navigation and hardware-address
verification commands remain owned by the existing wallet view model.

The passphrase tests create no files or funded wallet, never start wallet services,
never render recovery words, and never sign or broadcast. Unrelated wallet-model
capabilities fail if accessed. Editable passphrases are cleared on success, rejection,
teardown and verifier exceptions; immutable managed strings are not claimed to be
securely zeroized.

## Other CI and release validation

The standard Build job covers the Nix build/test path. Android, iOS and all three
desktop Native AOT publication jobs are independent checks. Shared linker roots are
in `Roots.xml`; host-only descriptors live in `Contrib/Aot/<project>.xml`, so a
desktop publish never requires a mobile host assembly.

A green headless job is a native UI regression gate, **not** a certification of
pixel parity with the original multi-screen design boards, physical-device touch,
OS keyboard or accessibility behavior, hardware-wallet transport, funded signing
and broadcast, or release readiness. Original design conformance still requires
review alongside those references. Device and transaction evidence must be recorded
separately rather than inferred from successful screenshot comparisons.
