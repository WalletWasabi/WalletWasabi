param(
    [Parameter(Mandatory = $true)][string]$Serial,
    [string]$BitcoindPath,
    [switch]$DownloadBitcoinCore,
    [ValidateSet(4096,16384)][int]$ExpectedPageSize = 4096
)
$ErrorActionPreference = 'Stop'
foreach ($taskScenario in @('stop-input','stop-confirmation','stop-output','stop-signing','stop-signed','interrupt-signing','dropout-confirmation','blame-signing','restart-output')) {
    $taskArguments = @{ Serial=$Serial; ReleaseEngine=$true; Modes=@('coinjoin'); CoinJoinScenario=$taskScenario; ExpectedPageSize=$ExpectedPageSize }
    if ($DownloadBitcoinCore) { $taskArguments.DownloadBitcoinCore=$true }
    else { $taskArguments.BitcoindPath=$BitcoindPath }
    & (Join-Path $PSScriptRoot 'test-device.ps1') @taskArguments
    if ($LASTEXITCODE -ne 0) { throw "CoinJoin failure qualification failed at $taskScenario." }
    Write-Output "PASS: deterministic CoinJoin scenario $taskScenario."
}
