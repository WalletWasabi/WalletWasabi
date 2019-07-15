# Testing

The goal of this document is to establish a manual testing workflow and checklist for Wasabi Wallet in order to make sure larger pull requests and releases do not accidentally ruin vital functionality.

# Environment

Someone must go through this document before every release on Windows 10, Ubuntu 18.04 (Bionic Beaver) and macOS 10.14 (Mojave).

## Setup

See this quick tutorial for getting the latest release and for updating:

https://github.com/zkSNACKs/WalletWasabi#build-from-source-code

#### Run Wasabi in Release Mode

Run Wasabi with `dotnet run -c Release` from the `WalletWasabi.Gui` folder.

### How to checkout a pull request?

Check the id of the pull request.
```sh
git fetch origin pull/ID/head:yourbranchname
git checkout yourbranchname
```

#### Updating a pull request

If someone made a change to the pull request and you want to go through the tests again, first checkout the master branch `git checkout master` then continue with the same procedure described in the "How to checkout a pull request?" part. It will update your branch.

### DataFolder location?

Open Wasabi and go to Main Menu / File / Open / Data Folder.

### How to check for errors?

Standard procedure: look at the terminal. If there is something saying ERROR or WARNING, that is probably an error.
Special case: always defined at the specific test case.

### How to determine if the application has exited?

Look at the terminal. Wait until log messages stop and the blinking cursor reappears. If nothing happens, try to press enter. If the application hanged, you can also check it in process manager. If it is still running, there might be an endless loop: an error which does not let the application close.

# Workflow

## GUI exit tests

1.
  * Run Wasabi.
  * Immediately after the UI pops up, press the X button.
  * Wait until exit.
2.
  * Run Wasabi.
  * Wait until Backend is connected.
  * Press the X button.
  * Wait until exit.
3.
  * Delete all files from DataFolder/Client/BitcoinStore/Main
  * Run Wasabi.
  * Wait until Missing Filter less than 50000.
  * Press the X button.
  * Wait until exit.
4.
  * Run Wasabi.
  * Wait until Backend is connected.
  * Press the File / Exit.
  * Wait until exit.
5.
  * Run Wasabi.
  * Wait until Backend is connected.
  * Go to Terminal and press Ctrl-C.
  * Wait until exit.
  
## Filter downloading tests

1.
  * Delete all files from DataFolder/Client/BitcoinStore/Main
  * Wait until Backend is connected and all filter is downloaded.
  * Restart the application.
  * Filter download should not start again.
  
## Context menu and selection tests

1.
  * Run Wasabi.
  * Load one of your wallets. 
  * Go to the "Receive" tab.
  * If you do not have any addresses, then generate a couple.
  * Select an address by right clicking on it. Make sure that the context menu pops up, and the address is highlighted.
  * Select another address by right clicking on it. Make sure again that the context menu pops up, and the address is highlighted.
 

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
- **pass** Filter downloading tests
- **pass** Context menu and selection tests

---TEMPLATE END---
