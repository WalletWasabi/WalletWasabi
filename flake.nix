{
  description = "Wasabi wallet coordinator";

  inputs.nixpkgs.url = "github:NixOS/nixpkgs/nixpkgs-unstable";
  outputs = { self, nixpkgs }:
    let
        pkgs = import nixpkgs { system = "x86_64-linux"; }; #nixpkgs.legacyPackages.x86_64-linux;
        deployScript = pkgs.writeScriptBin "deploy" (builtins.readFile ./Contrib/deploy.sh);
        gitRev = if (builtins.hasAttr "rev" self) then self.rev else "dirty";
        backend-build = pkgs.buildDotnetModule rec {
          pname = "WalletWasabi.Backend";
          version = "2.0.0-${builtins.substring 0 8 (self.lastModifiedDate or self.lastModified or "19700101")}-${gitRev}";
          nugetDeps = ./deps.nix; # nix build .#packages.x86_64-linux.default.passthru.fetch-deps
          dotnetFlags = [ "-p:CommitHash=${gitRev}" ];
          runtimeDeps = [ pkgs.openssl pkgs.zlib ];
          dotnet-sdk = pkgs.dotnetCorePackages.sdk_8_0;
          selfContainedBuild = true;

          src = ./.;
          projectFile = "WalletWasabi.Backend/WalletWasabi.Backend.csproj";
          executables = [ "WalletWasabi.Backend" ];
          postInstall = ''
            ln -s ${deployScript}/bin/deploy $out
          '';
        };

        # dotnet trace
        dotnet-trace = pkgs.buildDotnetGlobalTool {
          pname = "dotnet-trace";
          nugetName = "dotnet-trace";
          version = "7.0.442301";
          nugetSha256 = "sha256-yKmpygSNpNWNhLt9vS/1mterTU1l0gmpo3Ef2HvLsLw=";
          dotnet-sdk = pkgs.dotnetCorePackages.sdk_8_0;
        };
        # dotnet dump
        dotnet-dump = pkgs.buildDotnetGlobalTool {
          pname = "dotnet-dump";
          nugetName = "dotnet-dump";
          version = "7.0.442301";
          nugetSha256 = "sha256-UZE1UJfOWYw+ONOemAtuhtfXE/9a2WbnOQFXXuE7p80=";
          dotnet-sdk = pkgs.dotnetCorePackages.sdk_8_0;
        };
        wasabi-shell = pkgs.mkShell {
           name = "wasabi-shell";
           packages = [
             pkgs.dotnetCorePackages.sdk_8_0
             dotnet-trace
             dotnet-dump
             ];

           shellHook = ''
             export DOTNET_CLI_TELEMETRY_OPTOUT=1
             export DOTNET_NOLOGO=1
             export DOTNET_ROOT=${pkgs.dotnetCorePackages.sdk_8_0}
             export PS1='\n\[\033[1;34m\][Wasabi:\w]\$\[\033[0m\] '
           '';
        };
    in
    {
      packages.x86_64-linux.default = backend-build;
      devShells.x86_64-linux.default = wasabi-shell;
    };
}
