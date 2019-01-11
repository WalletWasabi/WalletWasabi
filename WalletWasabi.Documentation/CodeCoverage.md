Install `altcover` NuGet to the `WalletWasabi.Tests` project and Coverage Glutters extension to vscode.

```sh
dotnet build
dotnet C:\Users\user\.nuget\packages\altcover\5.0.663\tools\netcoreapp2.0\altcover.dll --save --inplace "-i=bin\Debug\netcoreapp2.2"
dotnet test
dotnet C:\Users\user\.nuget\packages\altcover\5.0.663\tools\netcoreapp2.0\altcover.dll runner --collect "-r=bin\Debug\netcoreapp2.2" "-l=lcov.info"
```

Run vscode and click on "Watch":

![](https://i.imgur.com/W4hXXda.png)
