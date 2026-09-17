# Saved native UI patch import — 2026-09-14

This commit imports all 26 source files from the previously delivered
`Wasabi-Mobile-Native-UI-Patch.zip`, plus this import note. The prior branch history
is retained; the base is `4d8989a85c090c1fd3405af99a4e90eed761a310` on
`wieslawsoltes/WalletWasabi:feature/mobile-support`.

Archive SHA-256:
`759c8cee7a2e9dc18c2ae015dce0f2cf8ad098873ae550a8290c94979b062f92`.

All 26 source files were checked against the archive manifest's byte lengths and
SHA-256 hashes. The final Git tree references Git blob IDs calculated from those
exact saved files. No unrelated source or branch history is replaced.

The "local source overlay" delivery status in `VISUAL-TESTING.md` describes the
earlier blocked attempt. It is retained as historical delivery evidence; this
import supersedes that status, not the documented validation limitations.

## Checks rerun during import

- Visual comparator/report/approval tests: 22 passed using synthetic pixel fixtures.
- Guarded overlay installer tests: 14 passed in temporary local Git repositories.
- Source-structure checks: passed for 26 files, nine XAML documents, and 121
  presentation binding roots.

No C# compilation, compiled-XAML validation, native test execution, native
screenshot generation, or bitmap-parity review was performed during this retry.
The 68 added native cases are committed test source, not executed test results.
The visual gate still requires explicit approval of real native screenshot
baselines. No baseline was automatically approved or invented for this import.
