# 1. Final tests

1. Go to https://github.com/zkSNACKs/WalletWasabi/releases.
2. Check the exact **date** of the of the last release and the **name** of the last PR.
3. List the PR-s in order, open the [link and (adjust date!)](https://github.com/zkSNACKs/WalletWasabi/pulls?q=is%3Apr+merged%3A%3E%3D2019-07-07+sort%3Aupdated-asc).
4. Go trough all PR, create the Release Notes document and the Final Test issue. Create test cases according to PR-s and write a list.  [Release Notes format](https://github.com/zkSNACKs/WalletWasabi/releases/tag/v1.1.6) and [Final Test format](https://github.com/zkSNACKs/WalletWasabi/issues/2227).
5. Go trough all issues and pick the [important ones (adjust date!)](https://github.com/zkSNACKs/WalletWasabi/issues?utf8=%E2%9C%93&q=is%3Aissue+closed%3A%3E%3D2019-07-07+sort%3Aupdated-asc+) and add to Release Notes or to Final Tests if required.
6. At the end there will be a Final Test document and a Release Notes document.

# 2. Pre-releasing

1. Go to https://github.com/zkSNACKs/WalletWasabi/releases and press Draft a new release.
2. Tag version: add `pre` postfix e.g: 1.1.7pre.
3. Set release title e.g: `Wasabi v1.1.7pre: Community Edition - PRERELEASE`.
4. Set description use [previous releases](https://github.com/zkSNACKs/WalletWasabi/releases/tag/1.1.7pre) as a template. 
5. Add the Release Notes.
6. Make sure local .NET Core version is up to date.
7. Update the onion seed list to the most reliable ones: in packager run `dotnet run -- --reduceonions`.
8. Run tests.
9. Run packager in publish mode.
10. Create `.msi`.
11. Run packager in sign mode. (Set back to publish mode.).
12. Test asc file for `.msi`.
12. Final `.msi` test on own computer.
13. Upload the files to the pre-release.
14. Check `This is a pre-release` and press Publish Release.
15. Add the pre-release link to the Final Test issue.
16. Share the Final Test issue link with developers an test it for 24 hour. 

# 3. Packaging

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

# 4. Notify

1. Refresh website download and signature links.
2. Update InstallationGuide and DeterministicBuildGuide download links.
3. Make sure CI and CodeFactor checks out.
4. [Deploy testnet and mainnet backend.](https://github.com/zkSNACKs/WalletWasabi/blob/master/WalletWasabi.Documentation/BackendDeployment.md#update)

# 5. Announce

1. [Twitter](https://twitter.com) (tag @wasabiwallet #Bitcoin #Privacy).
2. Submit to [/r/WasabiWallet](https://old.reddit.com/r/WasabiWallet/) and [/r/Bitcoin](https://old.reddit.com/r/Bitcoin/).

