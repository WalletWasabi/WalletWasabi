# Final tests

- Draft a new release.
- Check the exact **date** of the last release and the **name** of the last PR.
- List the PR-s in order, open the [link and (adjust date!)](https://github.com/zkSNACKs/WalletWasabi/pulls?q=is%3Apr+merged%3A%3E%3D2019-07-07+sort%3Aupdated-asc).
- Go through all PR, create the Release Notes document, and the Final Test issue. Create test cases according to PR-s and write a list. [Release Notes format](https://github.com/zkSNACKs/WalletWasabi/releases/tag/v1.1.6) and [Final Test format](https://github.com/zkSNACKs/WalletWasabi/issues/2227).
- Go through all issues and pick the [important ones (adjust date!)](https://github.com/zkSNACKs/WalletWasabi/issues?utf8=%E2%9C%93&q=is%3Aissue+closed%3A%3E%3D2019-07-07+sort%3Aupdated-asc+) and add to Release Notes or to Final Tests if required.
- Check Tor status. Never release during a Tor network disruption: https://status.torproject.org/
- At the end there will be a Final Test document and a Release Notes document.

# Release candidate

- Go to your own fork of Wasabi and press Draft a new release. Release candidates are not published in the main repository!
- Tag version: add `rc1` postfix e.g: v1.1.7rc1.
- Set release title e.g: `Wasabi v1.1.7: <Release Title> - Release candidate`.
- Set description use [Release Notes Template]([https://github.com/molnard/WalletWasabi/releases](https://github.com/zkSNACKs/WalletWasabi/blob/master/WalletWasabi.Documentation/ClientRelease/ReleaseNotesTemplate.md)).
- Add the Release Notes, the same as it will be at the final release (can be typofixed).
- Do Packaging (see below).
- Upload the files to the pre-release.
- Check `This is a pre-release` and press Publish Release.
- Add the pre-release link to the Final Test issue.
- Share the Final Test issue link with developers and test it for 24 hours.
- Every PR that is contained in the release must be at least 24 hours old.

Make sure to run a virus detection scan on one of the Release candidate's .msi installer (preferably the final one). You can use this site for example: https://www.virustotal.com/gui/home/upload.

# Packaging

- Make sure the local .NET Core version is up to date.
- Make sure CI and CodeFactor check out.
- Run tests.
- Run the [script file](https://github.com/zkSNACKs/WalletWasabi/blob/master/WalletWasabi.Packager/scripts/Wasabi_release.ps1) on the **Windows Release Laptop** and follow the instructions.
- At some point you will need to run [this script](https://github.com/zkSNACKs/WalletWasabi/blob/master/WalletWasabi.Packager/scripts/WasabiNoratize.scpt) file on Mac. Don't forget to open the script file on Mac and insert your Apple dev username and password. Guide how to setup it: [macOS release environment](https://github.com/zkSNACKs/WalletWasabi/blob/master/WalletWasabi.Documentation/Guides/MacOsSigning.md).
- Finish the script on Windows. Now a folder should pop up with all the files that need to be uploaded to GitHub.
- Test asc file for `.msi`.
- Final `.msi` test on own computer.

# Final release

- Draft a [new release at the main repo](https://github.com/zkSNACKs/WalletWasabi/releases/new).
- Bump client version. (WalletWasabi/Helpers/Constants.cs).
- Copy and paste the release notes from the RC releases. It should have been well-reviewed until now. Make sure the the recent changes are in the What's new section. 
- Run tests.
- Do Packaging (see above).
- Upload the files to the main repo!
- Download MSI from the draft release to your local computer, test it, and verify the version number in about!
- Do not set pre-release flag!
- Publish the release.

# Notify

- Refresh website download and signature links.
- [Deploy testnet and mainnet backend](https://github.com/zkSNACKs/WalletWasabi/blob/master/WalletWasabi.Documentation/HowToDeploy.md).

# Announce

- [Twitter](https://twitter.com) (tag @wasabiwallet #Bitcoin #Privacy).
- Submit to [/r/WasabiWallet](https://old.reddit.com/r/WasabiWallet/) and [/r/Bitcoin](https://old.reddit.com/r/Bitcoin/).

# 8. Backporting

Backport is a branch. It is used for creating silent releases (hotfixes, small improvements) on top of the last released version. For this reason it has to be maintained with special care.

## Merge PR into backport

- There is a PR which is merged to master and selected to backport.
- Checkout the current backport branch to a new local branch like bp_whatever: `git checkout -b bp_whatever upstream/backport`.
- Go to the merged PR / Commits and copy the hash of the commit.
- Cherry-pick: `git cherry-pick 35c4db348__hash of the commit__06abcd9278c`.
- git push origin bp_whatever.
- Create a PR into upstream/backport and name it as [Backport] Whatever.

Notes:
- In Backport the index.html does not need to be maintained.

## Create squash commit
Squash commit makes cherry-picking easier as you only need to do that for one commit. Squash commit "merge" together multiple commits. GitHub has an easy solution to do this.

1. Merge the PR into the master as usual.
2. Revert the merge.
3. Revert the reverted merge, so the original PR will be in the master.
4. Go to the PR which reverted the revert you will find the only one commit there - it will be the squash commit.
5. Cherry-pick the squash commit into the backport.

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


