# Final tests

1. Draft a new release.
2. Check the exact **date** of the last release and the **name** of the last PR.
3. List the PR-s in order, open the [link and (adjust date!)](https://github.com/zkSNACKs/WalletWasabi/pulls?q=is%3Apr+merged%3A%3E%3D2019-07-07+sort%3Aupdated-asc).
4. Go trough all PR, create the Release Notes document and the Final Test issue. Create test cases according to PR-s and write a list. [Release Notes format](https://github.com/zkSNACKs/WalletWasabi/releases/tag/v1.1.6) and [Final Test format](https://github.com/zkSNACKs/WalletWasabi/issues/2227).
5. Go trough all issues and pick the [important ones (adjust date!)](https://github.com/zkSNACKs/WalletWasabi/issues?utf8=%E2%9C%93&q=is%3Aissue+closed%3A%3E%3D2019-07-07+sort%3Aupdated-asc+) and add to Release Notes or to Final Tests if required.
6. Check Tor status. Never release during a Tor network disruption: https://status.torproject.org/
7. At the end there will be a Final Test document and a Release Notes document.

# Release candidate

1. Go to your own fork of Wasabi and press Draft a new release. Release candidates are not published in the main repository!
2. Tag version: add `rc1` postfix e.g: v1.1.7rc1.
3. Set release title e.g: `Wasabi v1.1.7: <Release Title> - Release candidate`.
4. Set description use [Release Notes Template]([https://github.com/molnard/WalletWasabi/releases](https://github.com/zkSNACKs/WalletWasabi/blob/master/WalletWasabi.Documentation/ClientRelease/ReleaseNotesTemplate.md)).
5. Add the Release Notes, the same as it will be at the final release (can be typofixed).
6. Do Packaging (see below).
16. Upload the files to the pre-release.
17. Check `This is a pre-release` and press Publish Release.
18. Add the pre-release link to the Final Test issue.
19. Share the Final Test issue link with developers and test it for 24 hours.
20. Every PR that is contained in the release must be at least 24 hours old.

Make sure to run a virus detection scan on one of the Release candidate's .msi installer (preferably the final one). You can use this site for example: https://www.virustotal.com/gui/home/upload.

# Packaging

0. Make sure local .NET Core version is up to date.
1. Make sure CI and CodeFactor check out.
2. Run tests.
3. Run the [script file](https://github.com/zkSNACKs/WalletWasabi/blob/master/WalletWasabi.Packager/scripts/Wasabi_release.ps1) on the **Windows Release Laptop** and follow the instructions.
4. At some point you will need to run [this script](https://github.com/zkSNACKs/WalletWasabi/blob/master/WalletWasabi.Packager/scripts/WasabiNoratize.scpt) file on Mac. Don't forget to open the script file on Mac and insert your Apple dev username and password. Guide how to setup it: [macOS release environment](https://github.com/zkSNACKs/WalletWasabi/blob/master/WalletWasabi.Documentation/Guides/MacOsSigning.md).
5. Finish the script on Windows. Now a folder should pop up with all the files that need to be uploaded to GitHub.
6. Test asc file for `.msi`.
7. Final `.msi` test on own computer.

# Final release

1. Draft a [new release at the main repo](https://github.com/zkSNACKs/WalletWasabi/releases/new).
2. Bump client version. (WalletWasabi/Helpers/Constants.cs).
3. Copy and paste the release notes from the RC releases. It should have been well-reviewed until now. Make sure the the recent changes are in the What's new section. 
2. Run tests.
3. Do Packaging (see above).
4. Upload the files to the main repo!
5. Download MSI from the draft release to your local computer, test it and verify the version number in about!
6. Do not set pre-release flag!
7. Publish the release.

# Notify

1. Refresh website download and signature links.
2. [Deploy testnet and mainnet backend](https://github.com/zkSNACKs/WalletWasabi/blob/master/WalletWasabi.Documentation/HowToDeploy.md).

# Announce

1. [Twitter](https://twitter.com) (tag @wasabiwallet #Bitcoin #Privacy).
2. Submit to [/r/WasabiWallet](https://old.reddit.com/r/WasabiWallet/) and [/r/Bitcoin](https://old.reddit.com/r/Bitcoin/).

# 8. Backporting

Backport is a branch. It is used for creating silent releases (hotfixes, small improvements) on top of the last released version. For this reason it has to be maintained with special care.

## Merge PR into backport

1. There is a PR which is merged to master and selected to backport.
2. Checkout the current backport branch to a new local branch like bp_whatever: `git checkout -b bp_whatever upstream/backport`.
3. Go to the merged PR / Commits and copy the hash of the commit.
4. Cherry pick: `git cherry-pick 35c4db348__hash of the commit__06abcd9278c`.
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
git push -u upstream backport
```

## Code signing certificate

Digicert holds our Code Signing Certificate under the name "zkSNACKs Limited".
- Issuing CA: DigiCert SHA2 Assured ID Code Signing CA
- Platform: Microsoft Authenticode
- Type: Code Signing
- CSR Key Size: 3072

**Renewal**

1. Create a new Certificate Signing Request (CSR) file with DigiCert® Certificate Utility application. 
   DigiCert® Certificate Utility is using the logged in user's public key to encrypt the file and only the same user can decrypt it after we receive the certificate.
   Make sure to create the CSR file in David's profile (or wherever the release script is located)!
2. Upload the CSR file to DigiCert.
3. Wait for DigiCert to issue a new `zksnacks_limited.p7b` file.
4. Import the `zksnacks_limited.p7b` file to DigiCert® Certificate Utility.
5. Choose a friendly name for the certificate and apply the default password to it.
6. Export the `zksnacks_limited.pfx` to `C:\zksnacks_limited.pfx`.
7. Rename `C:\zksnacks_limited.pfx` to `C:\digicert.pfx`, so the Packager can find it!!


## Packager environment setup

### WSL

You can disable WSL sudo password prompt with this oneliner: 

```
echo "`whoami` ALL=(ALL) NOPASSWD:ALL" | sudo tee /etc/sudoers.d/`whoami` && sudo chmod 0440 /etc/sudoers.d/`whoami`
```

Use WSL 1 otherwise you cannot enter anything to the console (sudo password, appleid). https://github.com/microsoft/WSL/issues/4424


