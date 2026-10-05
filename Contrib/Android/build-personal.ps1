param(
    [string]$SigningDirectory = (Join-Path $env:USERPROFILE '.codex/secrets/wasabi-android-personal'),
    [Parameter(Mandatory = $true)][string]$CoordinatorBootstrap,
    [switch]$QualificationHarness,
    [switch]$Rebuild,
    [string]$NativeBuildDirectory,
    [string]$Repository = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
)
$ErrorActionPreference = 'Stop'
$taskBaseline = Get-Content (Join-Path $PSScriptRoot 'toolchain.json') -Raw | ConvertFrom-Json
if ((& dotnet --version).Trim() -ne $taskBaseline.dotnetSdk) { throw 'The pinned .NET SDK is required.' }
$taskWorkloads = & dotnet workload --info
if (($taskWorkloads -join "`n") -notmatch [regex]::Escape($taskBaseline.androidWorkloadManifest)) { throw 'The pinned Android workload manifest is required.' }
$taskKeyPath = Join-Path $SigningDirectory 'personal.keystore'
$taskPasswordPath = Join-Path $SigningDirectory 'password.dpapi'
if (!(Test-Path -LiteralPath $taskKeyPath) -or !(Test-Path -LiteralPath $taskPasswordPath)) { throw 'The external signing key and its Windows DPAPI password are required. Never replace a key to fix an update error.' }
$taskBootstrapPath = (Resolve-Path -LiteralPath $CoordinatorBootstrap).Path
$taskBootstrap = Get-Content -LiteralPath $taskBootstrapPath -Raw | ConvertFrom-Json
if (@($taskBootstrap.PSObject.Properties.Name).Count -ne 2 -or @($taskBootstrap.PSObject.Properties.Name | Where-Object { $_ -notin @('Coordinator','CoordinatorIdentifier') }).Count -ne 0) { throw 'The bootstrap must contain only Coordinator and CoordinatorIdentifier.' }
$taskUri = [Uri]$taskBootstrap.Coordinator
if ($taskUri.Scheme -ne 'https' -and !($taskUri.Scheme -eq 'http' -and $taskUri.Host.EndsWith('.onion'))) { throw 'The coordinator requires HTTPS or an onion address.' }
if ($taskUri.UserInfo -or !$taskBootstrap.CoordinatorIdentifier) { throw 'Invalid coordinator bootstrap.' }
$taskSecurePassword = (Get-Content -LiteralPath $taskPasswordPath -Raw).Trim() | ConvertTo-SecureString
$taskPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($taskSecurePassword)
try {
    $env:WASABI_PERSONAL_STORE_PASS = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($taskPointer)
    $taskProject = if ($QualificationHarness) { 'Contrib/Android/ReleaseHarness/ReleaseHarness.csproj' } else { 'WalletWasabi.Android/WalletWasabi.Android.csproj' }
    $taskTarget = if ($Rebuild) { '-t:Rebuild' } else { '-t:Build' }
    $taskNativeArguments = @()
    if ($NativeBuildDirectory) { $taskNativeArguments = @("-p:WasabiNativeBuildDirectory=$((Resolve-Path -LiteralPath $NativeBuildDirectory).Path)") }
    & dotnet build (Join-Path $Repository $taskProject) $taskTarget -m:1 -c Release -p:WasabiSkipBundledApps=true -p:WasabiPackageChannel=Personal -p:AndroidKeyStore=true "-p:AndroidSigningKeyStore=$taskKeyPath" -p:AndroidSigningKeyAlias=wasabi-personal -p:AndroidSigningStorePass=env:WASABI_PERSONAL_STORE_PASS -p:AndroidSigningKeyPass=env:WASABI_PERSONAL_STORE_PASS "-p:WasabiCoordinatorBootstrapFile=$taskBootstrapPath" @taskNativeArguments --nologo -v:q
    if ($LASTEXITCODE -ne 0) { throw 'Personal APK build failed.' }
} finally {
    Remove-Item Env:WASABI_PERSONAL_STORE_PASS -ErrorAction SilentlyContinue
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($taskPointer)
    $taskSecurePassword.Dispose()
}
if ($QualificationHarness) { Write-Output 'The emulator-only qualification APK was built separately. Do not install this test harness on a phone with real funds.'; return }
$taskOutput = Join-Path $Repository 'WalletWasabi.Android/bin/Release/net10.0-android36.0'
$taskApk = Join-Path $taskOutput 'io.wasabiwallet.android.personal-Signed.apk'
if (!(Test-Path -LiteralPath $taskApk)) { throw 'The signed APK is missing.' }
$taskHash = (Get-FileHash -LiteralPath $taskApk -Algorithm SHA256).Hash.ToLowerInvariant()
"$taskHash  $([IO.Path]::GetFileName($taskApk))" | Set-Content -LiteralPath "$taskApk.sha256"
Write-Output "Personal APK: $taskApk"
Write-Output "SHA256: $taskHash"
