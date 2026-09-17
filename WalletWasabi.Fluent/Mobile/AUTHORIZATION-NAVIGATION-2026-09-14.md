# Mobile authorization and navigation continuation — 2026-09-14

Repository: `wieslawsoltes/WalletWasabi`  
Branch: `feature/mobile-support`  
Upstream draft: `WalletWasabi/WalletWasabi#14803`

This continuation builds on the existing native mobile reference implementation. It does not replace the application with an HTML view or change the wallet's signing implementation.

## Committed implementation

| Commit | Change |
| --- | --- |
| `7641b49f02697dcb9bb7c30262095bfd576b1029` | Mobile application settings use the existing result-returning dialog lifecycle, preserve the wallet page, and restore the previous navigation tab. |
| `039a693ff04f4a95ee99bc447007f42d2b329323` | Native passphrase and hardware authorization views, explicit view factory, and shell navigation policy. |
| `5e5bc31efdea59f2e09ffc394c6810324dfa4c33` | Authorization routes installed in the mobile shell; inactive modal layers hidden and disabled; stale queued focus rejected; global navigation restricted to landing pages. |
| `9647a2b6db542f4d87e57a5446a31b775ae5e275` | Passphrase undo disabled, reveal disabled, and sensitive/no-suggestions/no-capitalization input-method hints applied to login and authorization. |
| `5571e4d16076e66d4e959a5ed8f0fd7132e405cb` | Post-send and search-driven transaction selection connected to the native history/details projection. |

### Settings and modal lifetime

`NavigateToMobileSettingsCommand` awaits the existing `SettingsPage` instance on the dialog stack instead of creating a result-returning view model as an unawaited home page. `Done` completes that navigation operation. The settings reset command is never used as mobile Back.

`MobileShellNavigation` centralizes modal precedence. Only the topmost host is visible and enabled; underlying view models remain alive. Application tabs are restricted to known landing-page types, not arbitrary setup or signing steps. Focus requests check both the current page and current shell data context before running. A blocked Back action displays a transient, non-interactive notice, whose timer is released on navigation or detachment.

### Authorization and sensitive input

`MobileAuthorizationViewLocator` maps `PasswordAuthDialogViewModel` and `HardwareWalletAuthDialogViewModel` to native views. The views bind the original commands and error state. They do not perform independent password checks, synthesize approval results, or sign transactions.

Passphrase input is masked, has undo disabled, and requests sensitive input with suggestions and auto-capitalization disabled. These are platform input-method hints, not a guarantee about third-party keyboards. Editable passphrase state is cleared when an authorization view loses its model or detaches; this does not claim secure erasure of managed strings.

The passphrase page uses its view model's title and generic wallet-action wording because the same authorization model can also protect non-payment operations.

### Transaction-selection handoff

`HistoryViewModel.SelectTransaction` now records a wallet-owned `TransactionSelectionRequest` in addition to its unchanged desktop-grid selection behavior. Requests have reference identity, so repeated requests for the same transaction are distinguishable and an old consumer cannot consume a newer request.

`MobileTransactionNavigation` prepares the native history projection, resolves the requested transaction against actual rows, and opens that row only after consuming its request. It handles requests that arrive before a view attaches or before history contains the transaction. A user changing the page or filter cancels a waiting selection synchronously; queued row refreshes cannot override that choice. Newer requests supersede older ones. Production scheduling always posts to Avalonia's UI context so row reconciliation finishes before navigation.

The subscription is owned by `MobileWalletView` and disposed before replacing or releasing the wallet projection. A detached view cannot consume queued requests. Request state belongs to an individual wallet history, not a global static cache.

## Validation performed here

Source-level checks were executed against the local copies of 15 edited/new C# and XAML files. They checked well-formed XML for four XAML files, duplicate named elements, shell named-element references, passphrase masking/undo/input-method attributes, class declarations, and file-format integrity. The reconstructed pre-change `HistoryViewModel.cs` matched Git blob `ac0a40c17971d6bcfa09f97475cea8c9173b7ba6`, verifying that the desktop-history implementation was retained outside the added selection notification.

These checks are **not** C# compilation, compiled-binding validation, or native rendering tests.

## Added .NET regression cases — not executed here

| Test class | Declared cases | Coverage |
| --- | ---: | --- |
| `MobileShellNavigationTests` | 22 | All modal-precedence combinations and explicit landing-page navigation policy. |
| `MobileAuthorizationViewTests` | 10 | Two authorization views at 320/390 DIP in both themes, sensitive-input attributes, login policy, and unsupported factory inputs. |
| `MobileTransactionNavigationTests` | 8 | Request identity, delayed rows, supersession, explicit-navigation cancellation, queued-refresh ordering, disposal/reattachment, and wallet isolation. |

Authorization screenshots are named `authorization-unbound-*`. Those tests intentionally construct unbound views without wallet or signing services and are not end-to-end authorization tests.

The editing environment has no .NET SDK. Attempts to reach SDK/package endpoints failed at DNS resolution. The GitHub Actions query returned no branch runs; this report does not infer the reason or assert that Actions is disabled. No successful native build or native test execution is claimed.

## Remaining validation and completion gates

The shared UI project must compile and the native regression suite must execute. Android/iOS startup, keyboard behavior, safe areas, accessibility, device authorization, and real transaction review must be verified in an appropriate test environment. Native bitmap parity has not been measured. Existing routes not replaced by a mobile view locator still use shared responsive views, and this continuation does not complete every operating-system integration or every prototype interaction.

Reproducible native checks from the repository root:

```sh
dotnet build WalletWasabi.Fluent/WalletWasabi.Fluent.csproj -c Release -p:UseCdp=false
dotnet test WalletWasabi.Fluent.Mobile.Tests/WalletWasabi.Fluent.Mobile.Tests.csproj -c Release -p:UseCdp=false --logger trx --results-directory TestResults
```

This branch should remain experimental until those gates and the remaining implementation gaps are resolved. No production payment or device signing operation was performed as part of this continuation.
