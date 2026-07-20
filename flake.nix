{
  description = "Wasabi Wallet flake";

  inputs.nixpkgs.url = "github:NixOS/nixpkgs/nixpkgs-unstable";
  outputs = { self, nixpkgs }:
    let
        pkgs = import nixpkgs { system = "x86_64-linux"; config.permittedInsecurePackages = ["python3.13-ecdsa-0.19.1"]; };
        pkgsUnfree = import nixpkgs { system = "x86_64-linux"; config.permittedInsecurePackages = ["python3.13-ecdsa-0.19.1"]; config.allowUnfree = true; };
        deployScript = pkgs.writeScriptBin "deploy" (builtins.readFile ./Contrib/deploy.sh);
        gitRev = if (builtins.hasAttr "rev" self) then self.rev else "dirty";
        buildWasabiModule = pkgs.buildDotnetModule {
          pname = "wasabi";
          version = "2.0.0-${builtins.substring 0 8 (self.lastModifiedDate or self.lastModified or "19700101")}-${gitRev}";
          nugetDeps = ./deps.json; # nix build .#packages.x86_64-linux.all.passthru.fetch-deps
          dotnetFlags = [ "-p:CommitHash=${gitRev}"];
          dotnet-sdk = pkgs.dotnetCorePackages.sdk_10_0;
          dotnet-runtime = pkgs.dotnetCorePackages.aspnetcore_10_0;
          projectFile = [
            "WalletWasabi.Coordinator/WalletWasabi.Coordinator.csproj"
            "WalletWasabi.Fluent.Desktop/WalletWasabi.Fluent.Desktop.csproj"
            "WalletWasabi.Tests/WalletWasabi.Tests.csproj"
            "WalletWasabi.IntegrationTests/WalletWasabi.IntegrationTests.csproj"
          ];

          src = ./.;
        };

        # Common build settings for all configurations
        commonBuildAttrs = rec {
          pname = "WalletWasabi";
          projectFile = [
             "WalletWasabi.Coordinator/WalletWasabi.Coordinator.csproj"
             "WalletWasabi.Fluent.Desktop/WalletWasabi.Fluent.Desktop.csproj"];
          dotnetProjectFiles = projectFile;
          executables = [
            "WalletWasabi.Coordinator"
            "WalletWasabi.Fluent.Desktop" ];
          runtimeDeps = with pkgs; [
             pkgs.openssl pkgs.zlib
             # for client
             tor hwi bitcoind
             xorg.libX11 xorg.libXrandr xorg.libX11.dev xorg.libICE xorg.libSM fontconfig.lib ];

          # Disable parallel builds to avoid Avalonia resource file locking issues
          enableParallelBuilding = false;

          # wrap manually, because we want not so ugly executable names
          dontDotnetFixup = true;

          preFixup = ''
            wrapDotnetProgram $out/lib/${pname}/WalletWasabi.Fluent.Desktop $out/bin/wasabi
            wrapDotnetProgram $out/lib/${pname}/WalletWasabi.Coordinator $out/bin/wasabi-coordinator
          '';

          binaries = "BundledApps/Binaries/linux-x64";
          bundledApps = "./WalletWasabi/${binaries}";
          bundledAppsIntegrationTest = "./WalletWasabi.IntegrationTests/${binaries}";
          preBuild = ''
            cp -r ${pkgs.tor}/bin/tor ${bundledApps}/Tor/tor
            cp ${pkgs.hwi}/bin/hwi ${bundledApps}/hwi
            cp ${pkgs.bitcoind}/bin/bitcoind ${bundledAppsIntegrationTest}/bitcoind
          '';
        };

        # Build everything and run unit tests (default CI target)
        buildWithUnitTests = buildWasabiModule.overrideAttrs (oldAttrs: commonBuildAttrs // rec {
          projectFile = [
             "WalletWasabi.Coordinator/WalletWasabi.Coordinator.csproj"
             "WalletWasabi.Fluent.Desktop/WalletWasabi.Fluent.Desktop.csproj"
             "WalletWasabi.Tests/WalletWasabi.Tests.csproj"
          ];
          dotnetProjectFiles = projectFile;
          doCheck = true;
          checkPhase = ''
            runHook preCheck
            dotnet test WalletWasabi.Tests/WalletWasabi.Tests.csproj \
              --filter "FullyQualifiedName~UnitTests" \
              --no-build \
              --configuration Release \
              --logger "console;verbosity=detailed"
            runHook postCheck
          '';
        });

        # Build everything and run integration tests
        buildWithIntegrationTests = buildWasabiModule.overrideAttrs (oldAttrs: commonBuildAttrs // rec {
          projectFile = [
             "WalletWasabi.Coordinator/WalletWasabi.Coordinator.csproj"
             "WalletWasabi.Fluent.Desktop/WalletWasabi.Fluent.Desktop.csproj"
             "WalletWasabi.IntegrationTests/WalletWasabi.IntegrationTests.csproj"
          ];
          dotnetProjectFiles = projectFile;
          doCheck = true;
          checkPhase = ''
            runHook preCheck
            dotnet test WalletWasabi.IntegrationTests/WalletWasabi.IntegrationTests.csproj \
              --no-build \
              --configuration Release \
              --logger "console;verbosity=detailed"
            runHook postCheck
          '';
        });

        # Build everything and run all tests (unit + integration)
        buildWithAllTests = buildWasabiModule.overrideAttrs (oldAttrs: commonBuildAttrs // rec {
          projectFile = [
             "WalletWasabi.Coordinator/WalletWasabi.Coordinator.csproj"
             "WalletWasabi.Fluent.Desktop/WalletWasabi.Fluent.Desktop.csproj"
             "WalletWasabi.Tests/WalletWasabi.Tests.csproj"
             "WalletWasabi.IntegrationTests/WalletWasabi.IntegrationTests.csproj"
          ];
          dotnetProjectFiles = projectFile;
          doCheck = true;
          checkPhase = ''
            runHook preCheck
            dotnet test WalletWasabi.Tests/WalletWasabi.Tests.csproj \
              --filter "FullyQualifiedName~UnitTests" \
              --no-build \
              --configuration Release \
              --logger "console;verbosity=detailed"
            dotnet test WalletWasabi.IntegrationTests/WalletWasabi.IntegrationTests.csproj \
              --no-build \
              --configuration Release \
              --logger "console;verbosity=detailed"
            runHook postCheck
          '';
        });

        # dotnet trace
        dotnet-trace = pkgs.buildDotnetGlobalTool {
          pname = "dotnet-trace";
          nugetName = "dotnet-trace";
          version = "8.0.510501";
          nugetSha256 = "sha256-Kt5x8n5Q0T+BaTVufhsyjXbi/BlGKidb97DWSbI6Iq8=";
          dotnet-sdk = pkgs.dotnetCorePackages.sdk_10_0;
        };
        # dotnet dump
        dotnet-dump = pkgs.buildDotnetGlobalTool {
          pname = "dotnet-dump";
          nugetName = "dotnet-dump";
          version = "8.0.510501";
          nugetSha256 = "sha256-H7Z4EA/9G3DvVuXbnQJF7IJMEB2SkzRjTAL3eZMqCpI=";
          dotnet-sdk = pkgs.dotnetCorePackages.sdk_10_0;
        };
        # dotnet counters
        dotnet-counters = pkgs.buildDotnetGlobalTool {
          pname = "dotnet-counters";
          nugetName = "dotnet-counters";
          version = "8.0.510501";
          nugetSha256 = "sha256-gAexbRzKP/8VPhFy2OqnUCp6ze3CkcWLYR1nUqG71PI=";
          dotnet-sdk = pkgs.dotnetCorePackages.sdk_10_0;
        };
        # dotnet gcdump
        dotnet-gcdump = pkgs.buildDotnetGlobalTool {
          pname = "dotnet-gcdump";
          nugetName = "dotnet-gcdump";
          version = "8.0.510501";
          nugetSha256 = "sha256-y10InQA1sAvFYrRe+7I2+txKOvu1qQ1ii/7DnXvipxM=";
          dotnet-sdk = pkgs.dotnetCorePackages.sdk_10_0;
        };

        wasabi-shell =
          with {
            libs = with pkgs; [
              xorg.libX11
              xorg.libXrandr
              xorg.libX11.dev
              xorg.libICE
              xorg.libSM
              pkgs.zlib
              fontconfig.lib
            ];
            skiaSharp=toString ./. + "/WalletWasabi.Fluent.Desktop/bin/Debug/net10.0/runtimes/linux-x64/native";
          };
          pkgs.mkShell {
            name = "wasabi-shell";
            buildInputs = libs;
            packages = [
              pkgs.dotnetCorePackages.sdk_10_0

              # tools
              dotnet-trace
              dotnet-dump
              dotnet-gcdump
              dotnet-counters

              # dependencies
              pkgs.bitcoind
              pkgs.tor
              pkgs.hwi

              # IDE
              pkgsUnfree.jetbrains.rider

              # Claude code
              pkgsUnfree.claude-code
              pkgs.python314 # claude loves python
           ];

            DOTNET_CLI_TELEMETRY_OPTOUT = 1;
            AVALONIA_TELEMETRY_OPTOUT=1;
            DOTNET_NOLOGO = 1;
            DOTNET_ROOT = "${pkgs.dotnetCorePackages.sdk_10_0}";
            DOTNET_GLOBAL_TOOLS_PATH = "${builtins.getEnv "HOME"}/.dotnet/tools";
            #DOTNET_ROLL_FORWARD = "latestPatch";
            LD_LIBRARY_PATH = "${skiaSharp};${pkgs.lib.makeLibraryPath libs}";
            BUNDLED_APPS_BINARIES_PATH = "WalletWasabi/BundledApps/Binaries/linux-x64";
            BUNDLED_APPS_INTEGRATION_TEST_BINARIES_PATH = "WalletWasabi.IntegrationTests/BundledApps/Binaries/linux-x64";

            shellHook = ''
              export PATH="$PATH:$DOTNET_GLOBAL_TOOLS_PATH"
              cp $(which tor) "$BUNDLED_APPS_BINARIES_PATH/Tor/"
              cp $(which hwi) "$BUNDLED_APPS_BINARIES_PATH/hwi"
              cp $(which bitcoind) "$BUNDLED_APPS_INTEGRATION_TEST_BINARIES_PATH/"

              export PS1='\n\[\033[1;34m\][Wasabi:\w]\$\[\033[0m\] '
            '';
        };
    in
    {
      packages.x86_64-linux = {
        default = buildWithUnitTests;
        unit-tests = buildWithUnitTests;
        integration-tests = buildWithIntegrationTests;
        all = buildWithAllTests;
      };
      devShells.x86_64-linux.default = wasabi-shell;
    };
}
