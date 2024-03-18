# Code coverage (How to)

Wasabi Wallet is built using dotnet core. Currently dotnet XUnit and MSTest templates come with support for [Coverlet](https://github.com/coverlet-coverage/coverlet).

You can read more about this here https://docs.microsoft.com/en-us/dotnet/core/testing/unit-testing-code-coverage and also here https://codeburst.io/code-coverage-in-net-core-projects-c3d6536fd7d7.

```sh
dotnet test --filter "FullyQualifiedName~WalletWasabi.Tests.UnitTests.Crypto" --collect:"XPlat Code Coverage"
```

Running the above command we get a `coverage.cobertura.info` file containing the covered lines.

In order to see what lines are covered we need to install a `vscode extension` called [Coverage Gutters](https://github.com/ryanluker/vscode-coverage-gutters).
Run vscode and click on "Watch":

![](https://i.imgur.com/W4hXXda.png)

## Important

Coverlet cannot instrument the binaries correctly because of the `PathMap` entries in the project files (*.csproj) and for this reason the easiest way to fix it is by removing the entries.