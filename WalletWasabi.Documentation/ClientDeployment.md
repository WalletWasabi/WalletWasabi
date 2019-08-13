# 1. Packaging

0. Make sure local .NET Core version is up to date.
1. Update the onion seed list to the most reliable ones: `dotnet run -- --reduceonions`
2. Run tests.
3. Retest every PR since last release on Windows, macOS and Linux.
4. Dump client version.
5. Run packager in publish mode.
6. Create `.msi`
7. Run packager in sign mode. (Set back to publish mode.)
8. Final `.msi` test on own computer.

# 2. GitHub Release

1. Create GitHub Release (Use the previous release as template.)
2. Write Release notes based on commits since last release.
3. Download and test the binaries on all VMs.

# 3. Notify

1. Refresh website download and signature links.
2. Update InstallationGuide and DeterministicBuildGuide download links.
3. Make sure CI and CodeFactor checks out.
4. [Deploy testnet and mainnet backend.](https://github.com/zkSNACKs/WalletWasabi/blob/master/WalletWasabi.Documentation/BackendDeployment.md#update)

# 4. Announce

1. [Twitter](https://twitter.com) (tag @wasabiwallet #Bitcoin #Privacy).
2. Submit to [/r/WasabiWallet](https://old.reddit.com/r/WasabiWallet/) and [/r/Bitcoin](https://old.reddit.com/r/Bitcoin/).
