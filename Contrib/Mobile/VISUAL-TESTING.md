# Native mobile rendering and visual review

This patch targets `wieslawsoltes/WalletWasabi`, branch `feature/mobile-support`,
starting at commit `4d8989a85c090c1fd3405af99a4e90eed761a310`.

## What runs in production

`MobileWalletView` still owns its original `MobileWalletViewModel` and
`MobileTransactionNavigation`. Its named `Root` is retained. The inner
`MobileWalletSurface` adapts that live model through `IMobileWalletPresentation`.
The exact same surface is instantiated by headless tests with a test-assembly-only
fixture. There is no production mock wallet, alternate signing engine, or HTML view.

`MobileWalletPresentation` delegates every wallet action to the existing command.
It observes the current wallet's default send/receive choices, amounts, transactions,
coins and CoinJoin state. It owns and disposes its event subscriptions and row
adapters, not the wallet or its commands. Supplied rendering presentations remain
caller-owned. The source-owned `Root` and presentation-owned `PresentationRoot`
are intentionally separate compiled-binding scopes.

The native surface covers Home, History, Privacy, CoinJoin, Coins, Transaction
Details and Discover. Four vertical native settings panels replace the settings
host's embedded desktop General, Bitcoin, Coordinator and Connections layouts.
Existing settings view models continue to validate and save values. The node
verification action is still the original command. Desktop-only options are not
advertised as Android/iOS integrations.

## What the new tests prove when executed

`MobileWalletVisualTests` declares 50 cases:

- 42 populated production-surface cases: seven destinations, 320 × 568,
  390 × 844 and 430 × 932 device-independent pixels, light and dark themes.
- Two themed interaction cases for navigation, filter/search bindings, discreet
  amounts, coin selection and exclusion action bindings.
- Transaction row navigation, a light/dark/light pixel round trip, two critical
  CoinJoin-state presentation cases, 2,000-row virtualization, and context teardown.

`MobileSettingsPanelTests` adds 18 cases: 16 **unbound** settings-panel layout
captures, the RPC credential input policy, and content-context inheritance.
Those unbound cases are not settings persistence or RPC connection tests.

The fixture instantiates the real `MobileTransactionItem` and `TransactionModel`
presentation types using synthetic metadata. It never starts a wallet, signs,
broadcasts, connects to a coordinator or connects to an RPC endpoint. Its commands
exercise UI binding behavior, not the underlying payment implementation. The live
adapter delegates to existing production commands; that integration still requires
native and end-to-end validation.

`MobileScreenshot` captures actual Skia pixels using Avalonia Headless. Each PNG
has a `.frame.json` record containing its commit, theme, viewport, scale, culture,
timezone, content kind, dimensions and SHA-256. Populated tests assert binding
warnings are absent, verify horizontal scroll extents, and reject blank frames.

## Execute native tests

Prerequisites: the repository's .NET 10 SDK and restored packages. This shared UI
suite does not need Android/iOS workloads, a wallet or mainnet funds.

```sh
python3 -m pip install -r Contrib/Mobile/requirements-visual.txt
python3 -m unittest discover -s Contrib/Mobile -p test_visual_review.py -v

export TZ=UTC
export WASABI_MOBILE_TEST_ARTIFACTS="$PWD/TestResults/mobile-previews"
dotnet build WalletWasabi.Fluent/WalletWasabi.Fluent.csproj -c Release -p:UseCdp=false
dotnet test WalletWasabi.Fluent.Mobile.Tests/WalletWasabi.Fluent.Mobile.Tests.csproj \
  -c Release -p:UseCdp=false --logger trx --results-directory TestResults
```

The test application loads Inter through its existing NuGet dependency. No font
files are bundled with this patch. Run approvals and comparisons on the same OS,
rendering dependencies, font configuration, timezone and scale. Fixture wallet
captures use the invariant culture, matching the repository’s InvariantGlobalization setting.

## Review and explicitly approve

Create the first gallery without representing new images as approved:

```sh
python3 Contrib/Mobile/visual_review.py report \
  --actual TestResults/mobile-previews \
  --output TestResults/mobile-visual-review \
  --allow-unreviewed
```

Open `TestResults/mobile-visual-review/index.html` and inspect the native frames.
`--allow-unreviewed` is an explicit first-review convenience; the report still says
**unreviewed**, never **passed**, for images with no approved baseline.

After human review:

```sh
python3 Contrib/Mobile/visual_review.py approve \
  --actual TestResults/mobile-previews \
  --baseline Contrib/Mobile/Baselines --reviewed
```

Existing baseline files require the additional `--replace` option. CI does not run
`approve`, does not auto-update baselines, and does not use `--allow-unreviewed`.
Commit the reviewed PNGs and `approval.json` through the normal review process.
No baseline PNGs are supplied with this patch because no native capture ran here.

Subsequent comparison:

```sh
python3 Contrib/Mobile/visual_review.py report \
  --actual TestResults/mobile-previews \
  --baseline Contrib/Mobile/Baselines \
  --output TestResults/mobile-visual-review
```

The gate rejects changed images, dimension mismatches, missing baselines, corrupt
capture metadata and empty capture runs. It compares without resizing. Defaults:
maximum per-channel delta 8 for a changed pixel, changed fraction at most 0.5%,
and mean channel error at most 0.5. Differences are retained as diagnostic images,
not replacement UI screenshots. The actual/approved/difference gallery and JSON
report are uploaded even when the visual gate fails.

The default CI gate intentionally fails as **unreviewed** until native baselines
are actually reviewed. This is distinct from a C# build or test failure.

## Evidence and limitations for this delivery

Executed here: Python comparator/report/approval regression tests using synthetic
image fixtures; source-structure and integration checks; guarded overlay installer
tests. See the accompanying executed-results files for counts and output.

Not executed here: C# compilation, compiled-XAML validation, the 68 newly declared
native cases, native screenshots, or Android/iOS tests. This editing environment
has no .NET SDK and external SDK/package downloads failed. The GitHub write batch
was blocked twice and did not create a commit; this is a local source overlay.

The Python images used to test the comparator are not screenshots of the wallet.
Passing those tests does not prove the Avalonia views compile, render correctly,
or match the original bitmap designs. No bitmap-parity or release-readiness claim
is made. Some other routes still use the existing shared responsive views.
