# 1. Final tests

1. Go to https://github.com/zkSNACKs/WalletWasabi/releases.
2. Check the exact **date** of the of the last release and the **name** of the last PR.
3. List the PR-s in order, open the [link and (adjust date!)](https://github.com/zkSNACKs/WalletWasabi/pulls?q=is%3Apr+merged%3A%3E%3D2019-07-07+sort%3Aupdated-asc).
4. Go trough all PR, create the Release Notes document and the Final Test issue. Create test cases according to PR-s and write a list.  [Release Notes format](https://github.com/zkSNACKs/WalletWasabi/releases/tag/v1.1.6) and [Final Test format](https://github.com/zkSNACKs/WalletWasabi/issues/2227).
5. Go trough all issues and pick the [important ones (adjust date!)](https://github.com/zkSNACKs/WalletWasabi/issues?utf8=%E2%9C%93&q=is%3Aissue+closed%3A%3E%3D2019-07-07+sort%3Aupdated-asc+) and add to Release Notes or to Final Tests if required.
6. At the end there will be a Final Test document and a Release Notes document.

# 2. Pre-releasing

1. Go to https://github.com/zkSNACKs/WalletWasabi/releases and press Draft a new release.
2. Tag version: add `pre` postfix e.g: v1.1.7pre.
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
17. Every PR which contained by the release must be at least 24 hours old.

# 3. Packaging

0. Make sure local .NET Core version is up to date.
1. Update the onion seed list to the most reliable ones: `dotnet run -- --reduceonions`
2. Run tests.
3. Retest every PR since last release on Windows, macOS and Linux.
4. Dump client version. (WalletWasabi/Helpers/Constants.cs)
5. Run packager in publish mode.
6. Create `.msi`
7. Run packager in sign mode. (Set back to publish mode.)
8. Final `.msi` test on own computer.

# 4. GitHub Release

1. Create GitHub Release (Use the previous release as template.)
2. Write Release notes based on commits since last release.
3. Download and test the binaries on all VMs.

# 5. Notify

1. Refresh website download and signature links.
2. Update InstallationGuide and DeterministicBuildGuide download links, [here](https://github.com/zkSNACKs/WasabiDoc/blob/master/docs/.vuepress/variables.js)
3. Make sure CI and CodeFactor checks out.
4. [Deploy testnet and mainnet backend.](https://github.com/zkSNACKs/WalletWasabi/blob/master/WalletWasabi.Documentation/BackendDeployment.md#update)

# 6. Announce

1. [Twitter](https://twitter.com) (tag @wasabiwallet #Bitcoin #Privacy).
2. Submit to [/r/WasabiWallet](https://old.reddit.com/r/WasabiWallet/) and [/r/Bitcoin](https://old.reddit.com/r/Bitcoin/).

# 7. Backporting

Backport is a branch. It is used for creating silent releases (hotfixes, small improvements) on top of the last released version. For this reason it has to be maintained with special care. 

## Merge PR into backport

1. There is a PR which is merged to master and selected to backport. 
2. Checkout the current backport branch to a new local branch like bp_whatever.
`git checkout -b bp_whatever upstream/backport`
3. Go to the merged PR / Commits and copy the hash of the commit.
4. Cherry pick.
`git cherry-pick 35c4db348__hash of the commit__06abcd9278c`
5. git push origin bp_whatever.
6. Create a PR into upstream/backport name it as: [Backport] Whatever.

Notes:
- In Backport the index.html does not need to be maintained.

## Create squash commit
Squash commit makes cherry-picking easier as you only need to do that for one commit. Squash commit "merge" together multiple commits. GitHub has an easy solution to do this. 

1. Merge the PR into the master as usually.
2. Revert the merge.
3. Revert the reverted merge, so the original PR will be in the master.
4. Go to the PR which reverted the revert you will find the only one commit there - it will be the squash commit.
5. Cherry pick the squash commit into backport.

## Rebase backport after release

If it's a major release, then the backport branch must be rebased, so we can start backporting stuff.

```sh
git checkout --track upstream/backport
git rebase upstream/master
git push -u upstream/backport
```

