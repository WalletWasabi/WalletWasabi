# 1. Packaging

0. Make sure local .NET Core version is up to date.
1. Run tests.
2. Dump client version.
3. Run packager in publish mode.
4. Create .msi (Release/x64)
5. Run packager in sign mode. (Set back to publish mode.)
6. Final .msi test on own computer.

# 2. GitHub Release

1. Create GitHub Release (Use the previous release as template.)
2. Write Release notes based on commits since last release.
3. Download and test the binaries on all VMs.

# 3. Notify

1. Refresh website download and signature links.
2. Refresh InstallationGuide download links.
3. Make sure CI and CodeFactor checks out.
4. [Deploy testnet and mainnet backend.](https://github.com/zkSNACKs/WalletWasabi/blob/master/WalletWasabi.Documentation/BackendDeployment.md#update)

# 4. Announce

1. Tweet about it.
2. Submit to /r/WasabiWallet and /r/Bitcoin.
