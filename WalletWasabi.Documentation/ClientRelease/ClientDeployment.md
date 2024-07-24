# Final tests

- Check the exact **date** of the last release and the **name** of the last PR.
- List the PR-s in order, open the [link and (adjust date!)](https://github.com/WalletWasabi/WalletWasabi/pulls?q=is%3Apr+merged%3A%3E%3D2019-07-07+sort%3Aupdated-asc).
- Go through all PR, create the Final Test issue. Create test cases according to PR-s and write a list - [Final Test format](https://github.com/WalletWasabi/WalletWasabi/issues/2227).
- Go through all issues and pick the [important ones (adjust date!)](https://github.com/WalletWasabi/WalletWasabi/issues?utf8=%E2%9C%93&q=is%3Aissue+closed%3A%3E%3D2019-07-07+sort%3Aupdated-asc+) and add to Final Tests if required.
- Check Tor status. Never release during a Tor network disruption: https://status.torproject.org/
- At the end there will be a Final Test document.
- Do testing contribution game if needed. 

# Release candidate

- Go to your own fork of Wasabi and press Draft a new release. Release candidates are not published in the main repository!
- Tag version: add `rc1` postfix e.g: v1.1.7rc1.
- Set release title e.g: `Wasabi v1.1.7: <Release Title> - Release candidate`.
- Add "Do not use this" to the release notes. 
- Set as a pre-release. Save Draft.
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
- MAKE SURE YOU ARE ON THE RIGHT BRANCH AND UP TO DATE in GitHub Desktop on the release machine!
- Discard packages.lock changes if there are. Inserted USB drive name must contain the string USB! 
- Run the [script file](https://github.com/WalletWasabi/WalletWasabi/blob/master/WalletWasabi.Packager/scripts/Wasabi_release.ps1) on the **Windows Release Laptop** and follow the instructions.
- At some point you will need to run [this script](https://github.com/WalletWasabi/WalletWasabi/blob/master/WalletWasabi.Packager/scripts/WasabiNoratize.scpt) file on Mac. Don't forget to open the script file on Mac and insert your Apple dev username and password. Guide how to setup it: [macOS release environment](https://github.com/WalletWasabi/WalletWasabi/blob/master/WalletWasabi.Documentation/Guides/MacOsSigning.md).
- Finish the script on Windows. Now a folder should pop up with all the files that need to be uploaded to GitHub.
- Test asc file for `.msi`.
- Final `.msi` test on own computer. Check the About dialog and optionally the BUILDINFO.json next to the wasabi executable, the commit ID should match with the one on GitHub. 

# Final release

- Draft a [new release at the main repo](https://github.com/WalletWasabi/WalletWasabi/releases/new).
- Bump client version. (WalletWasabi/Helpers/Constants.cs) - maybe you already did this.
- Create the release notes by using [the template](https://github.com/WalletWasabi/WalletWasabi/blob/master/WalletWasabi.Documentation/ClientRelease/ReleaseNotesTemplate.md). Make sure the the recent changes are in the What's new section. 
- Run tests.
- Do Packaging (see above).
- Upload the files to the main repo!
- Download MSI from the draft release to your local computer, test it, and verify the version number in about!
- Do not set pre-release flag!
- Publish the release.

# Notify

- Refresh website download and signature links.
- [Deploy testnet and mainnet backend](https://github.com/WalletWasabi/WalletWasabi/blob/master/WalletWasabi.Documentation/HowToDeploy.md). Make sure the client version number is bumped here as well. If it is a hotfix you do not need to update the backend, but you need to update the website!

# Announce

- [Twitter](https://twitter.com) (tag @wasabiwallet #Bitcoin #Privacy).
- Submit to [/r/WasabiWallet](https://old.reddit.com/r/WasabiWallet/) and [/r/Bitcoin](https://old.reddit.com/r/Bitcoin/).

# Backporting

Backport is a branch. It is used for creating silent releases (hotfixes, small improvements) on top of the last released version. For this reason it has to be maintained with special care.

## Merge PR into backport (release branch)

- There is a PR which is merged to master and selected to backport.
- Checkout the current backport branch to a new local branch like bp_whatever: `git checkout -b bp_whatever upstream/backport`.
- Go to the merged PR / Commits and copy the hash of the commit.
- Cherry-pick: `git cherry-pick 35c4db348__hash of the commit__06abcd9278c`.
- git push origin bp_whatever.
- Create a PR into upstream/backport and name it as [Backport] Whatever.

## Code signing certificate

Digicert holds our Code Signing Certificate under the name "zkSNACKs Limited".
- Issuing CA: DigiCert SHA2 Assured ID Code Signing CA
- Platform: Microsoft Authenticode
- Type: Code Signing
- CSR Key Size: 3072

**Renewal**

- Create a new Certificate Signing Request (CSR) file with DigiCert® Certificate Utility application. 
   DigiCert® Certificate Utility is using the logged in user's public key to encrypt the file and only the same user can decrypt it after we receive the certificate.
   Make sure to create the CSR file in David's profile (or wherever the release script is located)!
- Upload the CSR file to DigiCert.
- Wait for DigiCert to issue a new `zksnacks_limited.p7b` file.
- Import the `zksnacks_limited.p7b` file to DigiCert® Certificate Utility.
- Choose a friendly name for the certificate and apply the default password to it.
- Export the `zksnacks_limited.pfx` to `C:\zksnacks_limited.pfx`.
- Rename `C:\zksnacks_limited.pfx` to `C:\digicert.pfx`, so the Packager can find it!!


## Packager environment setup

### WSL

You can disable WSL sudo password prompt with this oneliner: 

```
echo "`whoami` ALL=(ALL) NOPASSWD:ALL" | sudo tee /etc/sudoers.d/`whoami` && sudo chmod 0440 /etc/sudoers.d/`whoami`
```

Use WSL 1 otherwise you cannot enter anything to the console (sudo password, appleid). https://github.com/microsoft/WSL/issues/4424


