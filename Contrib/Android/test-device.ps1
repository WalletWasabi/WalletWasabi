param(
    [Parameter(Mandatory = $true)][string]$Serial,
    [string]$BitcoindPath,
    [switch]$DownloadBitcoinCore,
    [ValidateSet('runtime','wallet','faults','coinjoin','tor','vault')][string[]]$Modes = @('runtime','wallet','faults','coinjoin','tor','vault'),
    [ValidateRange(1,20)][int]$CoinJoinRounds = 1,
    [switch]$ReleaseEngine,
    [ValidateSet(4096,16384)][int]$ExpectedPageSize = 4096,
    [string]$Repository = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
)
$ErrorActionPreference = 'Stop'
if ($Serial -notmatch '^emulator-[0-9]+$') { throw 'This fixture runs only on an explicitly selected Android emulator.' }
$taskPackage = if ($ReleaseEngine) { 'io.wasabiwallet.android.qualification' } else { 'io.wasabiwallet.android.dev' }
$taskApk = if ($ReleaseEngine) { 'Contrib/Android/ReleaseHarness/bin/Release/net10.0-android36.0/io.wasabiwallet.android.qualification-Signed.apk' } else { 'WalletWasabi.Android/bin/Debug/net10.0-android36.0/io.wasabiwallet.android.dev-Signed.apk' }
$taskArtifacts = Join-Path $Repository 'artifacts/android'
$taskToolchain = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'toolchain.json') -Raw | ConvertFrom-Json
New-Item -ItemType Directory -Force -Path $taskArtifacts | Out-Null
if ($DownloadBitcoinCore) {
    $taskVersion = '31.1'
    if ($IsWindows) { $taskArchiveName = "bitcoin-$taskVersion-win64.zip"; $taskHash = 'c99ef173471c58e6766d9eebd12e6c35349082eeed3939bc99eed58ef57db587' }
    elseif ($IsLinux) { $taskArchiveName = "bitcoin-$taskVersion-x86_64-linux-gnu.tar.gz"; $taskHash = 'b80d9c3e04da78fb6f0569685673418cf686fadba9042d926d13fb87ff503f9e' }
    else { throw 'Supply -BitcoindPath on this platform.' }
    $taskArchive = Join-Path $taskArtifacts $taskArchiveName
    if (!(Test-Path -LiteralPath $taskArchive)) { Invoke-WebRequest "https://bitcoincore.org/bin/bitcoin-core-$taskVersion/$taskArchiveName" -OutFile $taskArchive }
    if ((Get-FileHash -LiteralPath $taskArchive -Algorithm SHA256).Hash.ToLowerInvariant() -ne $taskHash) { throw 'Bitcoin Core failed its pinned SHA-256 verification.' }
    if ($IsWindows) { Expand-Archive -LiteralPath $taskArchive -DestinationPath $taskArtifacts -Force; $BitcoindPath = Join-Path $taskArtifacts "bitcoin-$taskVersion/bin/bitcoind.exe" }
    else { & tar -xzf $taskArchive -C $taskArtifacts; if ($LASTEXITCODE -ne 0) { throw 'Bitcoin Core extraction failed.' }; $BitcoindPath = Join-Path $taskArtifacts "bitcoin-$taskVersion/bin/bitcoind" }
}
if (!$BitcoindPath -or !(Test-Path -LiteralPath $BitcoindPath)) { throw 'Supply a Bitcoin Core executable or -DownloadBitcoinCore.' }
foreach ($taskPort in @(18543,18544,18545)) {
    $taskListener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, $taskPort)
    try { $taskListener.Start() } catch { throw "Port $taskPort is in use. Stop the existing fixture before running this isolated test." }
    finally { $taskListener.Stop() }
}
$taskRun = Join-Path $taskArtifacts ('device-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $taskRun | Out-Null
@'
regtest=1
server=1
blockfilterindex=1
txindex=1
fallbackfee=0.00002
rpcuser=wasabiandroid
rpcpassword=wasabi-android-regtest
[regtest]
rpcbind=127.0.0.1
rpcallowip=127.0.0.1
rpcport=18543
port=18544
bind=127.0.0.1
'@ | Set-Content -LiteralPath (Join-Path $taskRun 'bitcoin.conf')
$taskAuthorization = 'Basic ' + [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes('wasabiandroid:wasabi-android-regtest'))
function Invoke-TestRpc([string]$Method, [object[]]$Params = @()) {
    $taskBody = @{ jsonrpc = '2.0'; id = 1; method = $Method; params = $Params } | ConvertTo-Json -Compress -Depth 8
    $taskReply = Invoke-RestMethod -Uri 'http://127.0.0.1:18543/' -Method Post -Headers @{ Authorization = $taskAuthorization } -Body $taskBody -ContentType 'application/json'
    # Bitcoin Core can return JSON-RPC warmup errors with HTTP 200.
    if ($null -ne $taskReply.error) { throw "Fixture RPC $Method failed: $($taskReply.error.message)" }
    $taskReply.result
}
$taskStart = @{ FilePath = $BitcoindPath; ArgumentList = "-datadir=$taskRun"; RedirectStandardOutput = (Join-Path $taskRun 'node.log'); RedirectStandardError = (Join-Path $taskRun 'node-error.log'); PassThru = $true }
if ($IsWindows) { $taskStart.WindowStyle = 'Hidden'; $taskStart.ArgumentList = '"-datadir=' + $taskRun + '"' }
$taskNode = Start-Process @taskStart
$taskCoordinator = $null
$taskParticipant = $null
$taskTest = $null
try {
    $taskDeadline = [DateTime]::UtcNow.AddSeconds(45)
    do {
        try { $taskInfo = Invoke-TestRpc 'getblockchaininfo'; break } catch { Start-Sleep -Milliseconds 500 }
    } while ([DateTime]::UtcNow -lt $taskDeadline -and !$taskNode.HasExited)
    if (!$taskInfo -or $taskInfo.chain -ne 'regtest') { throw 'The isolated regtest node did not start. Check for existing users of ports 18543/18544.' }
    Invoke-TestRpc 'createwallet' @('android-tests') | Out-Null
    $taskMining = Invoke-TestRpc 'getnewaddress'
    Invoke-TestRpc 'generatetoaddress' @(101, $taskMining) | Out-Null
    & dotnet build (Join-Path $PSScriptRoot 'RegtestCoordinator/RegtestCoordinator.csproj') -p:WasabiSkipBundledApps=true --nologo -v:q
    if ($LASTEXITCODE -ne 0) { throw 'Regtest coordinator build failed.' }
    $taskCoordinatorArgs = @((Join-Path $PSScriptRoot 'RegtestCoordinator/bin/Debug/net10.0/RegtestCoordinator.dll'), (Join-Path $taskRun 'coordinator'))
    if ($CoinJoinRounds -gt 1) { $taskCoordinatorArgs += 'qualification' }
    $taskCoordinatorStart = @{ FilePath = 'dotnet'; ArgumentList = $taskCoordinatorArgs; RedirectStandardOutput = (Join-Path $taskRun 'coordinator.log'); RedirectStandardError = (Join-Path $taskRun 'coordinator-error.log'); PassThru = $true }
    if ($IsWindows) { $taskCoordinatorStart.WindowStyle = 'Hidden'; $taskCoordinatorStart.ArgumentList = $taskCoordinatorArgs | ForEach-Object { '"' + $_ + '"' } }
    foreach ($taskPair in @(@(18443,18543), @(18444,18544), @(18545,18545))) {
        & adb -s $Serial reverse "tcp:$($taskPair[0])" "tcp:$($taskPair[1])"
        if ($LASTEXITCODE -ne 0) { throw 'ADB reverse setup failed.' }
    }
    & adb -s $Serial install --no-incremental -r (Join-Path $Repository $taskApk)
    if ($LASTEXITCODE -ne 0) { throw 'APK installation failed.' }
    foreach ($taskMode in $Modes) {
      $taskIterations = if ($taskMode -eq 'coinjoin') { $CoinJoinRounds } else { 1 }
      for ($taskIteration = 1; $taskIteration -le $taskIterations; $taskIteration++) {
        $taskEvidenceName = if ($taskMode -eq 'coinjoin') { "coinjoin-$taskIteration" } else { $taskMode }
        if ($taskMode -eq 'coinjoin') {
            if (!$ReleaseEngine) { & adb -s $Serial shell run-as $taskPackage rm -f files/coinjoin-ready | Out-Null }
            & dotnet build (Join-Path $PSScriptRoot 'RegtestParticipant/RegtestParticipant.csproj') -p:WasabiSkipBundledApps=true --nologo -v:q
            if ($LASTEXITCODE -ne 0) { throw 'Independent participant build failed.' }
            $taskParticipantDirectory = Join-Path $taskRun "participant-$taskIteration"
            $taskParticipantArgs = @((Join-Path $PSScriptRoot 'RegtestParticipant/bin/Debug/net10.0/RegtestParticipant.dll'), $taskParticipantDirectory)
            $taskParticipantStart = @{ FilePath = 'dotnet'; ArgumentList = $taskParticipantArgs; RedirectStandardOutput = (Join-Path $taskRun "participant-$taskIteration.log"); RedirectStandardError = (Join-Path $taskRun "participant-$taskIteration-error.log"); PassThru = $true }
            if ($IsWindows) { $taskParticipantStart.WindowStyle = 'Hidden'; $taskParticipantStart.ArgumentList = $taskParticipantArgs | ForEach-Object { '"' + $_ + '"' } }
            $taskParticipant = Start-Process @taskParticipantStart
        }
        $taskTestArgs = @('-s',$Serial,'shell','am','instrument','-w','-r','-e','mode',$taskMode,"$taskPackage/io.wasabiwallet.android.WalletInstrumentation")
        $taskTestStart = @{ FilePath = (Get-Command adb).Source; ArgumentList = $taskTestArgs; RedirectStandardOutput = (Join-Path $taskRun "$taskEvidenceName.log"); RedirectStandardError = (Join-Path $taskRun "$taskEvidenceName-error.log"); PassThru = $true }
        if ($IsWindows) { $taskTestStart.WindowStyle = 'Hidden' }
        $taskTest = Start-Process @taskTestStart
        $taskTestDeadline = [DateTime]::UtcNow.AddMinutes(11)
        if ($taskMode -eq 'coinjoin') {
            $taskBarrierDeadline = [DateTime]::UtcNow.AddMinutes(2)
            do {
                $taskAndroidReady = if ($ReleaseEngine) { (Get-Content -LiteralPath (Join-Path $taskRun "$taskEvidenceName.log") -Raw) -match 'READY: synthetic Android participant' }
                    else { $null = & adb -s $Serial exec-out run-as $taskPackage cat files/coinjoin-ready 2>$null; $LASTEXITCODE -eq 0 }
                $taskBothReady = $taskAndroidReady -and (Test-Path -LiteralPath (Join-Path $taskParticipantDirectory 'ready.txt'))
                if (!$taskBothReady) { Start-Sleep -Milliseconds 500 }
            } while (!$taskBothReady -and !$taskTest.HasExited -and !$taskParticipant.HasExited -and [DateTime]::UtcNow -lt $taskBarrierDeadline)
            if (!$taskBothReady) { throw 'The synthetic participants did not synchronize before the round started.' }
            $taskCoordinatorArgs = @((Join-Path $PSScriptRoot 'RegtestCoordinator/bin/Debug/net10.0/RegtestCoordinator.dll'), (Join-Path $taskRun "coordinator-$taskIteration"))
            if ($CoinJoinRounds -gt 1) { $taskCoordinatorArgs += 'qualification' }
            $taskCoordinatorStart.ArgumentList = $taskCoordinatorArgs
            $taskCoordinatorStart.RedirectStandardOutput = Join-Path $taskRun "coordinator-$taskIteration.log"
            $taskCoordinatorStart.RedirectStandardError = Join-Path $taskRun "coordinator-$taskIteration-error.log"
            if ($IsWindows) { $taskCoordinatorStart.ArgumentList = $taskCoordinatorArgs | ForEach-Object { '"' + $_ + '"' } }
            $taskCoordinator = Start-Process @taskCoordinatorStart
            'Synthetic participants may enter the fresh round.' | Set-Content -LiteralPath (Join-Path $taskParticipantDirectory 'go.txt')
        }
        while (!$taskTest.HasExited -and [DateTime]::UtcNow -lt $taskTestDeadline) { Start-Sleep -Milliseconds 500 }
        if (!$taskTest.HasExited) { & adb -s $Serial shell am force-stop $taskPackage; Stop-Process -Id $taskTest.Id; throw 'Android instrumentation exceeded its bounded timeout.' }
        $taskResult = Get-Content -LiteralPath (Join-Path $taskRun "$taskEvidenceName.log")
        # The engine logs only to its private files. This supported Debug-only
        # read retrieves this fixture's log, without exporting wallet backups.
        if (!$ReleaseEngine) {
            $taskEngineLog = & adb -s $Serial exec-out run-as $taskPackage cat "files/instrumentation-$taskMode.log" 2>&1
            if ($LASTEXITCODE -eq 0) { $taskEngineLog | Set-Content -LiteralPath (Join-Path $taskRun "android-$taskEvidenceName-engine.log") }
        }
        if (($taskResult -join "`n") -notmatch "PASS: $taskMode Android integration") {
            $taskResult | Write-Output
            & adb -s $Serial logcat -d -t 2000 | Set-Content -LiteralPath (Join-Path $taskRun 'android-logcat.log')
            throw "Android $taskEvidenceName integration failed. See $taskRun/$taskEvidenceName.log"
        }
        if (($taskResult -join "`n") -notmatch "page size $ExpectedPageSize[; ]") { throw "The requested $ExpectedPageSize-byte page environment was not executed." }
        $taskExpectedRuntime = [regex]::Escape("runtime=.NET $($taskToolchain.monoRuntime); process X64")
        if (($taskResult -join "`n") -notmatch $taskExpectedRuntime) { throw 'The installed package did not execute the pinned Mono runtime on x64.' }
        if ($taskMode -eq 'coinjoin') {
            $taskParticipantDeadline = [DateTime]::UtcNow.AddSeconds(30)
            while (!(Test-Path -LiteralPath (Join-Path $taskParticipantDirectory 'completed.txt')) -and [DateTime]::UtcNow -lt $taskParticipantDeadline -and !$taskParticipant.HasExited) { Start-Sleep -Milliseconds 250 }
            if (!(Test-Path -LiteralPath (Join-Path $taskParticipantDirectory 'completed.txt'))) { throw 'The independent participant did not complete the same CoinJoin.' }
            if (!$taskParticipant.HasExited) { $taskParticipant.WaitForExit(10000) | Out-Null }
            if (!$taskParticipant.HasExited) { Stop-Process -Id $taskParticipant.Id }
            $taskParticipant = $null
            if ($taskCoordinator -and !$taskCoordinator.HasExited) { Stop-Process -Id $taskCoordinator.Id; $taskCoordinator.WaitForExit(10000) | Out-Null }
            $taskCoordinator = $null
        }
        Write-Output "Verified Android $taskEvidenceName integration."
      }
    }
    Write-Output "Device evidence: $taskRun"
}
finally {
    if ($taskTest -and !$taskTest.HasExited) { & adb -s $Serial shell am force-stop $taskPackage; Stop-Process -Id $taskTest.Id -ErrorAction SilentlyContinue }
    # Shut down only the node started by this run; never touch another node if
    # the requested port was occupied and our process failed to start.
    if (!$taskNode.HasExited) { try { Invoke-TestRpc 'stop' | Out-Null } catch { Stop-Process -Id $taskNode.Id -ErrorAction SilentlyContinue } }
    if ($taskCoordinator -and !$taskCoordinator.HasExited) { Stop-Process -Id $taskCoordinator.Id }
    if ($taskParticipant -and !$taskParticipant.HasExited) { Stop-Process -Id $taskParticipant.Id }
    foreach ($taskPort in @(18443,18444,18545)) { & adb -s $Serial reverse --remove "tcp:$taskPort" | Out-Null }
}
