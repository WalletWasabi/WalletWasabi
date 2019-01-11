# Code coverage (How to)

Wasabi wallet is build using dotnet core. Given there is no a cross-platform Profiling API like the one available on Windows, we 
use [AltCover](https://github.com/SteveGilham/altcover) package for instrumenting the assemblies and recording the execution
coverage.

So, first of all we need to install the package in the `WalletWasabi.Tests` project as follow:

```sh
dotnet add WalletWasabi.Tests/WalletWasabi.Tests.csproj package AltCover
```

Next, we must build the solution as usual. After that, it is necessary to instrument the assemblies, run the tests and collect coverage:

```sh
dotnet build

altcover=$(locate AltCover.dll | tail -1)
dotnet $altcover --save --inplace "-i=./WalletWasabi.Tests/bin/Debug/netcoreapp2.2"
dotnet test
dotnet $altcover runner --collect "-r=./WalletWasabi.Tests/bin/Debug/netcoreapp2.2" "-l=lcov.info"
```

All the previous steps can be simplified running just one line:

```sh
dotnet test /p:AltCover=true /p:AltCoverLcovReport=lcov.info
```

Whatever our approach is, the final result is the sameone: a `lcov.info` file containing the covered lines. In order to be able to see what lines
are cevered we need to install a `vscode extension` called  `Coverage Glutters`.  

Run vscode and click on "Watch":

![](https://i.imgur.com/W4hXXda.png)
