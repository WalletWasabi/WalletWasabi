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

## Create release docs and before-release test list

1. Go to https://github.com/zkSNACKs/WalletWasabi/releases
2. Check the exact date and time of the of the last release and the name of the last PR.
3. Go to this site and adjust the [date](https://github.com/zkSNACKs/WalletWasabi/pulls?q=is%3Apr+merged%3A%3E%3D2019-07-07+sort%3Aupdated-asc).
4. Find the last PR which was merged and start from there.
5. Create a [draft release](https://github.com/zkSNACKs/WalletWasabi/releases/new)
5. Start writing the Release Notes into the draft in similar [format](https://github.com/zkSNACKs/WalletWasabi/releases/tag/v1.1.6).
6. Open a new Issue with the name 'Final tests before releasing 1.x.x' and start writing the tests according to the PR-s with similar format like [here](https://github.com/zkSNACKs/WalletWasabi/issues/2227)
7. Pick important [issues (adjust date)](https://github.com/zkSNACKs/WalletWasabi/issues?utf8=%E2%9C%93&q=is%3Aissue+closed%3A%3E%3D2019-07-07+sort%3Aupdated-asc+) and add to Release Notes or tests if neccessary.

