# Computes SHA256 hashes for "<platform>/Tor" folders by concating all files in a single blob of data that is then hashed.

[CmdletBinding()]
param()

Set-StrictMode -Version 3
$ErrorActionPreference = "Stop"

$supportedPlatforms = @(
  "win64",
  "osx64",
  "lin64"
)

$hashes = [Ordered]@{}

foreach ($platform in $supportedPlatforms) {
  Write-Debug "# Processing '$platform' folder."
  $files = Get-ChildItem -Path "$platform/Tor/*" -File -Recurse -Exclude "LICENSE" | Sort-Object FullName
  $files | Write-Debug  # List files for inspection.
  $dataToHash = Get-Content -Raw $files
  $hash = $(Get-FileHash -Algorithm SHA256 -InputStream ([System.IO.MemoryStream]::New([System.Text.Encoding]::ASCII.GetBytes($dataToHash)))).Hash
  $hashes.Add($platform, $hash)
}

$hashes | format-table
