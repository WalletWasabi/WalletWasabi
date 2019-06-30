# Developers' Guide for debugging Wasabi

This guide is for giving detailed instructions about how to debug Wasabi wallet components to those developers who want to contribute with the project. We will focus on how to achieve this with `vscode` first because that's the cross-platform IDE used by some of the developer team members.

## Before to start
We assume the reader has already read the project [README](https://github.com/zkSNACKs/WalletWasabi/blob/master/README.md) file  and has installed the .NET Core SDK, knows how to clone the repository and build the Wasabi solution.


## Install VS Code and extensions
### 1: Get Visual Studio Code
Install Visual Studio Code (VSC). Pick the latest VSC version from here: https://code.visualstudio.com/download


### 2: Install C# Extension
Open the command palette in VS Code (press <kbd>F1</kbd>) and type `ext install C#` to trigger the installation of the extension. VS Code will show a message that the extension has been installed and it will restart. Installing this extension can take a while.


### 3: Open the WalletWasabi directory in VS Code
Go to `File->Open Folder` and open the WalletWasabi directory in Visual Studio Code. The Wasabi vscode settings forces to download the latest omnisharp versions every time the folder is opened and this can take a while.

After this step we are ready to start configuring the the build tasks and the launches.

## Understanding the Wasabi components

Wasabi Wallet is a client/server system where the client part is the Wasabi Wallet that users download and install in their machines and the server part is the Wasabi backend that runs on the zkSNACKs servers.  Additionally the solution contains other components like the Packager used to prepare Wasabi client to be distributed, integration tests project and others.

Here we are going to focus on how to debug the client component first and the backend component after.

### Wasabi Client

Lets start creating the launcher (it goes into the `.vscode/launch.json` file). This file is the one that contains the list of projects that can be launched, how to do it, what tasks have to be executed previously to the launching, etc.

```json
{
   "version": "0.2.0",
   "configurations": [{
      "name": "Wasabi GUI .NET Core",
      "type": "coreclr",
      "request": "launch",
      "preLaunchTask": "build-client",
      "program": "${workspaceFolder}/WalletWasabi.Gui/bin/Debug/netcoreapp2.2/WalletWasabi.Gui.dll",
      "args": [],
      "cwd": "${workspaceFolder}/WalletWasabi.Gui",
      "stopAtEntry": false,
      "internalConsoleOptions": "openOnSessionStart",
   }]
}
```

It is important to see that this launcher requires the execution of a `build-client` task. that is defined in the tasks file (it goes into `.vscode/tasks.json` file).

```json
{
    "version": "2.0.0",
    "tasks": [{
        "label": "build-client",
        "command": "dotnet",
        "type": "process",
        "args": [
           "build",
           "${workspaceFolder}/WalletWasabi.Gui/WalletWasabi.Gui.csproj"
        ],
        "problemMatcher": "$msCompile"
    }]
}
```

After these two files are created press (CTRL+SHIFT+D) to go to the debugger, set a couple of breakpoints, select the `Wasabi GUI .NET Core` launcher (it should be the only one available) and press the play button. That's all.

### Wasabi Backend

In the same way we did for the client part, we need to create a launcher and a task for running and debugging the server-side component. Lets start with the lancher. Add the following launcher to the array of `configurations` in the `.vscode/launch.json` 

```json
{
   "name": "Wasabi Backend .NET Core",
   "type": "coreclr",
   "request": "launch",
   "preLaunchTask": "build",
   "program": "${workspaceFolder}sabi.Backend/bin/Debug/netcoreapp2.2/WalletWasabi.dll",
   "args": [],
   "cwd": "${workspaceFolder}/WalletWasabi.Backend",
   "stopAtEntry": false,
   "internalConsoleOptions": "openOnSessionStart",
   "launchBrowser": {
       "enabled": true,
       "args": "${auto-detect-url}",
       "windows": {
           "command": "cmd.exe",
           "args": "/C start ${auto-detect-url}"
       },
       "osx": {
           "command": "open"
       },
       "linux": {
           "command": "xdg-open"
       }
   },
   "env": {
       "ASPNETCORE_ENVIRONMENT": "Development"
   },
   "sourceFileMap": {
       "/Views": "${workspaceFolder}/Views"
   }
}
```
As before, we need to create a task for compiling the backend project before executing the code, and this is done again in the `.vscode/tasks.jon` file. Add the following task to the array of tasks:

```json
{
   "label": "build-backend",
   "command": "dotnet",
   "type": "process",
   "args": [
      "build",
      "${workspaceFolder}asabi.Backend/WalletWasabi.Backend.csproj"
   ],
   "problemMatcher": "$msCompile"
}
```

And that's all. Once this has been done a developer can press (CTRL+SHIFT+D) to go to the debugger, set a couple of breakpoints, select the `Wasabi Backend .NET Core` launcher and press the play button to start debugging.

