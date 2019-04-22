# 1. Packaging

0. Make sure local .NET Core version is up to date.
1. Update onion seed list: `dotnet run -- --getonions`
2. Run tests.
3. Dump client version.
4. Run packager in publish mode.
5. Create `.msi`
6. Run packager in sign mode. (Set back to publish mode.)
7. Final `.msi` test on own computer.

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

1. [Twitter](https://twitter.com) (tag @wasabiwallet #Bitcoin #Privacy).
2. Submit to [/r/WasabiWallet](https://old.reddit.com/r/WasabiWallet/) and [/r/Bitcoin](https://old.reddit.com/r/Bitcoin/).
