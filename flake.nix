{
  description = "Wasabi Wallet flake";

  inputs.nixpkgs.url = "github:NixOS/nixpkgs/nixpkgs-unstable";
  outputs = { self, nixpkgs }:
    let
        pkgs = import nixpkgs { system = "x86_64-linux"; config.permittedInsecurePackages = ["python3.13-ecdsa-0.19.1"]; };
        deployScript = pkgs.writeScriptBin "deploy" (builtins.readFile ./Contrib/deploy.sh);
        gitRev = if (builtins.hasAttr "rev" self) then self.rev else "dirty";
        buildWasabiModule = pkgs.buildDotnetModule {
          pname = "wasabi";
          version = "2.0.0-${builtins.substring 0 8 (self.lastModifiedDate or self.lastModified or "19700101")}-${gitRev}";
          nugetDeps = ./deps.json; # nix build .#packages.x86_64-linux.all.passthru.fetch-deps
          dotnetFlags = [ "-p:CommitHash=${gitRev}"];
          dotnet-sdk = pkgs.dotnetCorePackages.sdk_10_0;
          dotnet-runtime = pkgs.dotnetCorePackages.aspnetcore_10_0;

          src = ./.;
        };

        # Build Wasabi Backend
        buildBackend = buildWasabiModule.overrideAttrs (oldAttrs: rec {
          pname = "WalletWasabi.Backend";
          projectFile = "WalletWasabi.Backend/WalletWasabi.Backend.csproj";
          executables = [ "WalletWasabi.Backend" ];
          runtimeDeps = [ pkgs.openssl pkgs.zlib ];
          postInstall = ''
            ln -s ${deployScript}/bin/deploy $out
          '';
        });

        # Build all components and run tests (CI)
        buildEverything = buildWasabiModule.overrideAttrs (oldAttrs: rec {
          pname = "WalletWasabi";
          projectFile = [
             "WalletWasabi.Backend/WalletWasabi.Backend.csproj"
             "WalletWasabi.Backend/WalletWasabi.Coordinator.csproj"
             "WalletWasabi.Fluent.Desktop/WalletWasabi.Fluent.Desktop.csproj"];
          executables = [
            "WalletWasabi.Backend"
            "WalletWasabi.Coordinator"
            "WalletWasabi.Fluent.Desktop" ];
          runtimeDeps = with pkgs; [
             pkgs.openssl pkgs.zlib
             # for client
             tor hwi bitcoind
             xorg.libX11 xorg.libXrandr xorg.libX11.dev xorg.libICE xorg.libSM fontconfig.lib ];
          # Testing
          doCheck = true;
          testProjectFile = "WalletWasabi.Tests/WalletWasabi.Tests.csproj";
          dotnetTestFlags = ["--filter \"FullyQualifiedName~UnitTests\"" "--logger \"console\""];

          # wrap manually, because we want not so ugly executable names
          dontDotnetFixup = true;

          preFixup = ''
            wrapDotnetProgram $out/lib/${pname}/WalletWasabi.Fluent.Desktop $out/bin/wasabi
            wrapDotnetProgram $out/lib/${pname}/WalletWasabi.Backend $out/bin/wbend
          '';

          binaries = "Microservices/Binaries/lin64";
          microservices = "./WalletWasabi/${binaries}";
          microservicesTest = "./WalletWasabi.Tests/${binaries}";
          preBuild = ''
            cp -r ${pkgs.tor}/bin/tor ${microservices}/Tor/tor
            cp ${pkgs.hwi}/bin/hwi ${microservices}/hwi
            cp ${pkgs.bitcoind}/bin/bitcoind ${microservicesTest}/bitcoind
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
        wasabi-shell = pkgs.mkShell {
           name = "wasabi-shell";
           packages = [
             pkgs.dotnetCorePackages.sdk_10_0
             dotnet-trace
             dotnet-dump
             dotnet-counters
             ];

           shellHook = ''
             export DOTNET_CLI_TELEMETRY_OPTOUT=1
             export DOTNET_NOLOGO=1
             export DOTNET_ROOT=${pkgs.dotnetCorePackages.sdk_10_0}
             export PS1='\n\[\033[1;34m\][Wasabi:\w]\$\[\033[0m\] '
           '';
        };
        migrateBackendFilters = {
           type = "app";
           program = "${(pkgs.writeShellScript "migrateBackendFilters" ''
              ${pkgs.dotnetCorePackages.sdk_10_0}/bin/dotnet fsi ${./.}/Contrib/Migration/migrateBackendFilters.fsx;
              '')}";
        };
    in
    {
      packages.x86_64-linux.default = buildBackend;
      packages.x86_64-linux.all = buildEverything;
      devShells.x86_64-linux.default = wasabi-shell;

      apps.x86_64-linux.migrateFilters = migrateBackendFilters;
    };
}
