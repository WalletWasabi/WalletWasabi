# Testing

The goal of this document is to establish a manual testing workflow and checklist for Wasabi Wallet in order to make sure larger pull requests and releases don't accidentally ruin vital functionality.

# Environment

Someone must go through this document before every release on Windows 10, Ubuntu 18.04 (Bionic Beaver) and macOS 10.14 (Mojave).

## Setup

See this quick tutorial for getting the latest release and for updating:

https://github.com/zkSNACKs/WalletWasabi#build-from-source-code

#### Run Wasabi in Release Mode

Run Wasabi with `dotnet run -c Release` from the `WalletWasabi.Gui` folder.

### How to checkout a pull request?

Check the id of the pull requesest.
```sh
git fetch origin/ID/branch:yourbranchname
git checkout yourbranchname
git submodule update --init --recursive
```

#### Updating a pull request

If someone made a change to the pull request and you want to go through the tests again, first checkout the master branch `git checkout master` then continue with the same procedure described in the "How to checkout a pull request?" part. It will update your branch.

### DataFolder location?

Open Wasabi and go to Main Menu / File / Open / Data Folder.

### How to check error?

Standart procedure: look at the terminal. If there is something ERROR or WARNING that is probably an error.
Special case: always defined at the specific test case.

# Workflow

## Wasabi GUI exit test

1.
  * Run Wasabi.
  * Immediately after the UI pops up, press the X button.
2.
  * Run Wasabi.
  * Wait until Backend connected.
  * Press the X button.
3.
  * Delete all files from DataFolder/Client/BitcoinStore/Main
  * Run Wasabi.
  * Wait until Missing Filter less than 50000.
  * Press the X button.

# Checklist

This is the template one can fill out and copypaste under a pull request.

---TEMPLATE START---

**Operating System**:

- **pass** foo - Test passed, no unusual things noticed.
- **fail** bar - Test failed or something unusual noticed around it.
- **?** buz - Test was omitted.

---TEMPLATE END---

---TEMPLATE START---

**Operating System**:

- **pass** Wasabi GUI exit test

---TEMPLATE END---
