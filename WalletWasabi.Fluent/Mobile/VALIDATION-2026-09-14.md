# Native mobile reference implementation — 2026-09-14

Target branch: `feature/mobile-support` in `wieslawsoltes/WalletWasabi`.
Upstream draft pull request: `WalletWasabi/WalletWasabi#14803`.

## Changes in this continuation

The existing native mobile implementation is preserved. This continuation extends it rather than embedding the HTML prototype.

### Native secondary flows

`MobileFlowViewLocator`, installed only in `MobileShell`, maps the existing view models to these additional native views:

| View | Existing model and behavior |
| --- | --- |
| `MobileCustomFeeView` | `CustomFeeRateDialogViewModel`: validated custom rate and dialog result |
| `MobileReceiveAddressesView` | `ReceiveAddressesViewModel`: live unused-address collection; QR, copy, edit and hide commands |
| `MobileAddressLabelEditView` | `AddressLabelEditViewModel`: wallet label suggestions and validation |
| `MobileConfirmHideAddressView` | `ConfirmHideAddressViewModel`: explicit hide confirmation; no address revocation claim |
| `MobileSendSuccessView` | `SendSuccessViewModel`: existing completion and history-selection behavior; sent is not presented as confirmed |
| `MobileSuccessView` | `SuccessViewModel`: generic operation completion, not a broadcast assertion |
| `MobileTransactionDetailsView` | `TransactionDetailsViewModel`: amounts, destination addresses, fees, confirmations and explicit clipboard actions |

`MobileDetailRow` and its control theme provide consistent wrapping detail fields. Clipboard actions use the existing Wasabi clipboard control. `MobileSensitiveContent` accepts an explicit discreet-mode binding, starts hidden, and does not initialize global wallet services or reveal information on hover.

### Theme composition and rendering

The main wallet now uses five light-theme navigation actions and four dark-theme actions. Nested transaction and coin-management pages retain their parent tab selection. The light theme's privacy gauge uses a 270-degree arc; the dark theme uses a full ring. CoinJoin progress remains a separate full-circle control in either theme.

`MobileArcGeometry` uses native circular arcs, including two half-arcs for full circles. `MobilePrivacyRing` caches its track and progress geometry by size, stroke, angles and value. No synthetic prices or privacy probabilities are introduced.

### Command and binding correctness

`MobileSendFeeSelection` highlights a card only when its target agrees with the saved confirmation target. Refreshing estimates cannot silently select or persist a different target. A failed save preserves the previous committed selection; ambiguous duplicate quotes disable the affected card.

`MobileCommandActivity` combines the view-model busy flag with confirm and alternate-command execution. `MobileBusyBehavior` binds this state on the UI scheduler. The page template disables header, body and footer actions during activity, and system Back checks the topmost native page rather than a busy page beneath an authorization dialog.

Adapter-typed roots for wallet, receive request and fee selection have an explicit null data context until their projection is installed. They do not temporarily inherit the incompatible parent route model. Subscriptions remain view-lifetime owned.

### Design-token export

`Contrib/Mobile/mobile_design.py` now gathers declarative tokens from all mobile style files, including nested style folders and the composition dictionaries. The existing JSON schema name and core fields are retained. Additional source provenance identifies each token's declaration file.

The validator detects duplicate declarations, missing resource names, light/dark key and type mismatches, malformed Boolean/numeric values, invalid gradient colors and offsets, and missing view-class declarations. Helpers and control themes are not misrepresented as scalar design tokens.

## Validation evidence

**Executed in the editing environment:** all 18 tests in `Contrib/Mobile/test_mobile_design.py` passed. These are dependency-free Python tests against generated resource fixtures. They verify the exporter and structural validator; they do not establish that the complete native application compiles. The locally tested exporter matched committed Git blob `d7942eabcd7c8d32445f2cc42ed7a5428fe58944`.

**Added but not executed in that environment:** native tests for fee-preference consistency, command-activity composition, seven secondary flows at two viewport widths in two themes, privacy defaults, navigation-state mapping, dynamic theme changes, busy-control inheritance, arc geometry and adapter context isolation.

The secondary-flow rendering tests intentionally use unbound views and name their image artifacts `secondary-unbound-*`. They are construction/layout smoke tests, not end-to-end payment or wallet tests.

The environment did not provide a .NET SDK, and the GitHub workflow queries returned no runs for this branch. No successful C# build, compiled-XAML check, native rendering test run, Android build, iOS build, or device test is claimed by this report.

## Reproducible checks

From the repository root:

```sh
python3 -m unittest discover -s Contrib/Mobile -p test_mobile_design.py -v
python3 Contrib/Mobile/mobile_design.py --check --export TestResults/mobile-design-tokens.json
dotnet build WalletWasabi.Fluent/WalletWasabi.Fluent.csproj -c Release -p:UseCdp=false
dotnet test WalletWasabi.Fluent.Mobile.Tests/WalletWasabi.Fluent.Mobile.Tests.csproj -c Release -p:UseCdp=false --logger trx --results-directory TestResults
```

The mobile validation workflow runs these stages and retains diagnostics and rendering artifacts. A workflow definition is not evidence that a run has completed.

## Remaining release gates

Successful native compilation and execution of the regression suite are still required. Device validation must include Android/iOS startup, safe areas, virtual-keyboard resizing, screen-reader navigation, background/resume behavior, receiving and QR interoperability, and payment review/authorization using test funds. Existing routes not explicitly replaced by a mobile view locator continue to use their shared responsive views.

Full bitmap parity has not been established by native screenshot comparison. The implementation must not be treated as release-ready or fully validated on the strength of the Python tests alone.
