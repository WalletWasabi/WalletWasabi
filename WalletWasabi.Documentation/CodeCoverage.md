# Code coverage (How to)

Wasabi wallet is built using dotnet core. Given there is no cross-platform Profiling API like the one available on Windows, we
use [AltCover](https://github.com/SteveGilham/altcover) package for instrumenting the assemblies and recording the execution
coverage.

So, first of all we need to install the package in the `WalletWasabi.Tests` project as follow:

```sh
dotnet add WalletWasabi.Tests/WalletWasabi.Tests.csproj package AltCover
```


Next, run:

```sh
dotnet test /p:AltCover=true /p:AltCoverLcovReport=lcov.info
```

As a result we get a `lcov.info` file containing the covered lines. In order to be able to see what lines

Run vscode and click on "Watch":

![](https://i.imgur.com/W4hXXda.png)
