{
  description = "Wasabi wallet coordinator";

  inputs.nixpkgs.url = "github:NixOS/nixpkgs/nixpkgs-unstable";
  outputs = { self, nixpkgs }:
    let
        pkgs = import nixpkgs { system = "x86_64-linux"; }; #nixpkgs.legacyPackages.x86_64-linux;
        deployScript = pkgs.writeScriptBin "deploy" (builtins.readFile ./Contrib/deploy.sh);

        backend-build = pkgs.buildDotnetModule rec {
          pname = "WalletWasabi.Backend";
          version = "2.0.0-${builtins.substring 0 8 (self.lastModifiedDate or self.lastModified or "19700101")}-${self.shortRev or "dirty"}";
          nugetDeps = ./deps.nix; # nix build .#packages.x86_64-linux.default.passthru.fetch-deps
          runtimeDeps = [ pkgs.openssl pkgs.zlib ];
          dotnet-sdk = pkgs.dotnetCorePackages.sdk_7_0;
          selfContainedBuild = true;

          src = ./.;
          projectFile = "WalletWasabi.Backend/WalletWasabi.Backend.csproj";
          executables = [ "WalletWasabi.Backend" ];
          postInstall = ''
            ln -s ${deployScript}/bin/deploy $out
          '';
        };
    in
    {
      packages.x86_64-linux.default = backend-build;
    };
}
