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
