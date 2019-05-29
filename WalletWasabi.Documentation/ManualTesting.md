# Testing

The goal of this document is to establish a manual testing workflow and checklist for Wasabi Wallet in order to make sure larger pull requests and releases don't accidentally ruin vital functionality.

# Environment

Someone must go through this document before every release on Windows 10, Ubuntu 18.04 (Bionic Beaver) and macOS 10.14 (Mojave).

## Setup

### How to get the latest release?

#### Get The Requirements

1. Get Git: https://git-scm.com/downloads
2. Get .NET Core 2.2 SDK: https://www.microsoft.com/net/download (Note, you can disable .NET's telemetry by typing `export DOTNET_CLI_TELEMETRY_OPTOUT=1` on Linux and OSX or `set DOTNET_CLI_TELEMETRY_OPTOUT=1` on Windows.)
  
#### Get Wasabi

Clone & Restore & Build

```sh
git clone https://github.com/zkSNACKs/WalletWasabi.git --recursive
cd WalletWasabi/WalletWasabi.Gui
dotnet build -c Release
```

#### Run Wasabi

Run Wasabi with `dotnet run -c Release` from the `WalletWasabi.Gui` folder.

#### Update Wasabi

```sh
git pull
git submodule update --init --recursive 
```

### How to checkout a pull request?

Check the id of the pull requesest.
```sh
git fetch origin/ID/branch:yourbranchname
git checkout yourbranchname
git submodule update --init --recursive
```

#### Updating a pull request

If someone made a change to the pull request and you want to go through the tests again, first checkout the master branch `git checkout master` then continue with the same procedure described in the "How to checkout a pull request?" part. It will update your branch.

# Workflow

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

- foo
- bar
- buz

---TEMPLATE END---
