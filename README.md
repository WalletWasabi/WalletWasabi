<p align="center">
  <a href="https://wasabiwallet.io">
    <img src="https://github.com/WalletWasabi/WalletWasabi/blob/master/ui-ww.png">
  </a>
</p>

<h3 align="center">
    An open-source, non-custodial, privacy-focused Bitcoin wallet for desktop.
</h3>

<h3 align="center">
  <a href="https://wasabiwallet.io">
    Website
  </a>
  <span> | </span>
  <a href="https://docs.wasabiwallet.io/">
    Documentation
  </a>
  <span> | </span>
  <a href="https://github.com/WalletWasabi/WalletWasabi/discussions/5185">
    Support
  </a>
  <span> | </span>
  <a href="https://www.youtube.com/c/WasabiWallet">
    YouTube
  </a>
  <span> | </span>
  <a href="https://github.com/WalletWasabi/WalletWasabi/blob/master/PGP.txt">
    PGP
  </a>
</h3>
<br>

# [Download Wasabi](https://github.com/WalletWasabi/WalletWasabi/releases)

<br>

# Build From Source Code

### Get The Requirements

1. Get Git: https://git-scm.com/downloads
2. Get .NET 10.0 SDK: https://dotnet.microsoft.com/download
3. Optionally disable .NET's telemetry by executing in the terminal `export DOTNET_CLI_TELEMETRY_OPTOUT=1` on Linux and macOS or `setx DOTNET_CLI_TELEMETRY_OPTOUT 1` on Windows.

### Get Wasabi

Clone & Restore & Build

```sh
git clone --depth=1 --single-branch --branch=master https://github.com/WalletWasabi/WalletWasabi.git
cd WalletWasabi/WalletWasabi.Fluent.Desktop
dotnet build
```

### Run Wasabi

Run Wasabi with `dotnet run` from the `WalletWasabi.Fluent.Desktop` folder.

### Update Wasabi

```sh
git pull
```
