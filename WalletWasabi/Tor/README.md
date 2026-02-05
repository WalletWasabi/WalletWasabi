This document describes how to build and run [Arti](https://arti.torproject.org/), a new implementation of [C Tor](https://gitlab.torproject.org/tpo/core/tor).

## Arti

Arti is a complete rewrite of C Tor written in Rust. As of 2025, it is not possible to dowload compiled binaries with RPC support, so one must [compile](https://arti.torproject.org/guides/compiling-arti) it oneself.

### How to Build and Run Arti from Source

Install Rust compiler by following instructions from https://rustup.rs/.

#### GNU/Linux

```bash
cd ~/projects
git clone https://gitlab.torproject.org/tpo/core/arti.git     # Check out tag 1.8.0
cd arti

cargo build --package arti --features="rpc,static"            # Build for the DEBUG configuration. Note that the RPC feature is OFF by default, so we ask for it.
./target/debug/arti proxy                                     # Run Arti compiled for the DEBUG configuration; CTRL+C to terminate the app.

cargo build --package arti --release --features="rpc,static"  # Compile for the RELEASE configuration
./target/release/arti proxy                                   # Run Arti compiled for the RELEASE configuration; CTRL+C to terminate the app.
```

An alternative that does not require the `static` feature (i.e. `--features="rpc,static"` -> `--features="rpc"`) is:

```bash
sudo apt update
sudo apt upgrade
sudo apt install build-essential libssl-dev pkg-config libsqlite3-dev
cargo build --package arti --release --features=rpc
./target/release/arti proxy
```


#### Windows

```powershell
cd X:\projects
git clone https://gitlab.torproject.org/tpo/core/arti.git     # Check out tag 1.8.0
cd arti

cargo build --package arti --features="rpc,static"            # Build for the DEBUG configuration. Note that the RPC feature is OFF by default, so we ask for it.
./target/debug/arti proxy                                     # Run Arti compiled for the DEBUG configuration; CTRL+C to terminate the app.

cargo build --package arti --release --features="rpc,static"  # Build for the RELEASE configuration.
./target/release/arti proxy                                   # Run Arti compiled for the RELEASE configuration; CTRL+C to terminate the app.
```

An [alternative](https://tor.stackexchange.com/questions/22950/how-to-compile-arti-on-windows/22952#22952) that does not require the `static` feature is:

1. Download source from [source](https://www.sqlite.org/download.html) (https://www.sqlite.org/download.html)
   For example: [source](https://www.sqlite.org/2020/sqlite-amalgamation-3310100.zip) `https://www.sqlite.org/2020/sqlite-amalgamation-3310100.zip`
2. Download binary from [binary](https://www.sqlite.org/download.html)
   For example: [binary](https://www.sqlite.org/2020/sqlite-dll-win64-x64-3310100.zip) `https://www.sqlite.org/2020/sqlite-dll-win64-x64-3310100.zip`
3. Extract both archives to the same directory
4. Open **Developer Command Prompt for VS 2017** by typing _Developer Command_ in Windows Search
5. Go to directory where you've extracted **source code** and **binary** files (via opened cmd)
6. Run `lib /DEF:sqlite3.def /OUT:sqlite3.lib /MACHINE:x64`


### Debug Arti in Visual Studio Code

1. Install [`rust-analyzer extension`](https://rust-analyzer.github.io/book/vs_code.html) and [`CodeLLDB`](https://marketplace.visualstudio.com/items?itemName=vadimcn.vscode-lldb) extensions in your Visual Studio Code
2. Modify `launch.json` as follows:
   ```json
   {
      // Use IntelliSense to learn about possible attributes.
      // Hover to view descriptions of existing attributes.
      // For more information, visit: https://go.microsoft.com/fwlink/?linkid=830387
      "version": "0.2.0",
      "configurations": [
         {
         "type": "lldb",
         "request": "launch",
         "name": "Debug",
         "cargo": { "args": ["build", "--bin=arti", "--features=rpc"] },
         "args": [
            "proxy", 
            "-o", "rpc.enable=true",
            "-o", "application.permit_debugging=true"
         ],
         "cwd": "${workspaceFolder}",
         "sourceLanguages": ["rust"]
         }
      ]
   }
   ```
3. In Visual Studio Code, open the Command Palette in VS Code (Ctrl+Shift+P or Cmd+Shift+P on macOS) -> Select `Preferences: Open Workspace Settings (JSON)` and set the contents of the file to be:
   ```json
   {
      "rust-analyzer.cargo.features": ["rpc"]
   }
   ```
   (This enables intellisense Arti's RPC code in VS Code; otherwise it would not work.)
