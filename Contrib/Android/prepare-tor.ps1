param([string]$Repository = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path)
$ErrorActionPreference = 'Stop'
$taskNative = Join-Path $Repository 'WalletWasabi.Android/Native'
$taskLock = Get-Content -Raw -LiteralPath (Join-Path $taskNative 'tor.lock.json') | ConvertFrom-Json
$taskArtifacts = Join-Path $Repository 'artifacts/android'
New-Item -ItemType Directory -Force -Path $taskArtifacts | Out-Null
$taskArchive = Join-Path $taskArtifacts "tor-android-$($taskLock.version).aar"
if (!(Test-Path -LiteralPath $taskArchive) -or (Get-FileHash -Algorithm SHA256 -LiteralPath $taskArchive).Hash.ToLowerInvariant() -ne $taskLock.sha256) {
    Invoke-WebRequest -Uri $taskLock.url -OutFile $taskArchive
}
if ((Get-FileHash -Algorithm SHA256 -LiteralPath $taskArchive).Hash.ToLowerInvariant() -ne $taskLock.sha256) {
    throw 'The Tor artifact failed its pinned SHA-256 verification.'
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$taskZip = [System.IO.Compression.ZipFile]::OpenRead($taskArchive)
try {
    foreach ($taskAbi in @('arm64-v8a', 'x86_64')) {
        $taskEntry = $taskZip.GetEntry("jni/$taskAbi/libtor.so")
        if ($null -eq $taskEntry -or $taskEntry.Length -lt 1000000) { throw "Missing Tor executable for $taskAbi" }
        $taskDirectory = Join-Path $taskNative $taskAbi
        New-Item -ItemType Directory -Force -Path $taskDirectory | Out-Null
        [System.IO.Compression.ZipFileExtensions]::ExtractToFile($taskEntry, (Join-Path $taskDirectory 'libtor.so'), $true)
        $taskBytes = [System.IO.File]::ReadAllBytes((Join-Path $taskDirectory 'libtor.so'))
        if ($taskBytes[0] -ne 0x7f -or $taskBytes[1] -ne 0x45 -or $taskBytes[2] -ne 0x4c -or $taskBytes[3] -ne 0x46 -or [BitConverter]::ToUInt64($taskBytes, 24) -eq 0) {
            throw "The Tor library for $taskAbi is not an executable ELF."
        }
        Write-Output "Verified Tor $($taskLock.version): $taskAbi ($($taskEntry.Length) bytes)"
    }
}
finally { $taskZip.Dispose() }
