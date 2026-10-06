param(
    [Parameter(Mandatory = $true)][string]$Serial,
    [string]$BitcoindPath,
    [switch]$DownloadBitcoinCore,
    [ValidateSet('runtime','wallet','faults','coinjoin','tor','vault','fees','public-sync')][string[]]$Modes = @('runtime','wallet','faults','coinjoin','tor','vault'),
    [ValidateRange(1,20)][int]$CoinJoinRounds = 1,
    [ValidateSet('complete','stop-input','stop-confirmation','stop-output','stop-signing','stop-signed','interrupt-signing','dropout-confirmation','blame-signing','restart-output')][string]$CoinJoinScenario = 'complete',
    [ValidateSet('both','main','testnet')][string]$PublicNetwork = 'both',
    [switch]$ReleaseEngine,
    [ValidateSet(4096,16384)][int]$ExpectedPageSize = 4096,
    [string]$Repository = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
)
$ErrorActionPreference = 'Stop'
if ($Serial -notmatch '^emulator-[0-9]+$') { throw 'This fixture runs only on an explicitly selected Android emulator.' }
if ($CoinJoinScenario -ne 'complete' -and $CoinJoinRounds -ne 1) { throw 'Run each failure scenario in a separate fresh fixture.' }
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
$taskDisruptingParticipant = $null
$taskTest = $null
try {
  $taskSourceChanges = @(& git -C $Repository status --porcelain)
  $taskVerification = [ordered]@{
    recordedUtc=[DateTime]::UtcNow.ToString('o'); result='INCOMPLETE'; serial=$Serial
    sourceCommit=(& git -C $Repository rev-parse HEAD).Trim()
    workingTreeModified=($taskSourceChanges.Count -ne 0); workingTreeChanges=$taskSourceChanges
    apkSha256=(Get-FileHash -LiteralPath (Join-Path $Repository $taskApk) -Algorithm SHA256).Hash.ToLowerInvariant()
    package=$taskPackage; configuration=$(if ($ReleaseEngine) { 'Release' } else { 'Debug' })
    expectedPageSize=$ExpectedPageSize; coinJoinScenario=$CoinJoinScenario; publicNetwork=$PublicNetwork; modes=@()
  }
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
    if ($CoinJoinRounds -gt 1 -or $CoinJoinScenario -ne 'complete') { $taskCoordinatorArgs += 'qualification' }
    if ($CoinJoinScenario -in @('dropout-confirmation','blame-signing')) { $taskCoordinatorArgs[-1] = 'disruption' }
    if ($CoinJoinScenario -in @('dropout-confirmation','restart-output')) { $taskCoordinatorArgs[-1] = $CoinJoinScenario }
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
            if ($CoinJoinScenario -eq 'restart-output') { $taskParticipantArgs += 'restart-output' }
            $taskParticipantStart = @{ FilePath = 'dotnet'; ArgumentList = $taskParticipantArgs; RedirectStandardOutput = (Join-Path $taskRun "participant-$taskIteration.log"); RedirectStandardError = (Join-Path $taskRun "participant-$taskIteration-error.log"); PassThru = $true }
            if ($IsWindows) { $taskParticipantStart.WindowStyle = 'Hidden'; $taskParticipantStart.ArgumentList = $taskParticipantArgs | ForEach-Object { '"' + $_ + '"' } }
            $taskParticipant = Start-Process @taskParticipantStart
            if ($CoinJoinScenario -in @('dropout-confirmation','blame-signing')) {
                $taskDisruptionDirectory = Join-Path $taskRun "disrupted-participant-$taskIteration"
                $taskDisruptionArgs = @((Join-Path $PSScriptRoot 'RegtestParticipant/bin/Debug/net10.0/RegtestParticipant.dll'), $taskDisruptionDirectory, $CoinJoinScenario)
                $taskDisruptionStart = @{ FilePath = 'dotnet'; ArgumentList = $taskDisruptionArgs; RedirectStandardOutput = (Join-Path $taskRun "disrupted-participant-$taskIteration.log"); RedirectStandardError = (Join-Path $taskRun "disrupted-participant-$taskIteration-error.log"); PassThru = $true }
                if ($IsWindows) { $taskDisruptionStart.WindowStyle = 'Hidden'; $taskDisruptionStart.ArgumentList = $taskDisruptionArgs | ForEach-Object { '"' + $_ + '"' } }
                $taskDisruptingParticipant = Start-Process @taskDisruptionStart
            }
        }
        $taskTestArgs = @('-s',$Serial,'shell','am','instrument','-w','-r','-e','mode',$taskMode,'-e','coinjoin-scenario',$CoinJoinScenario,'-e','public-network',$PublicNetwork,"$taskPackage/io.wasabiwallet.android.WalletInstrumentation")
        $taskTestStart = @{ FilePath = (Get-Command adb).Source; ArgumentList = $taskTestArgs; RedirectStandardOutput = (Join-Path $taskRun "$taskEvidenceName.log"); RedirectStandardError = (Join-Path $taskRun "$taskEvidenceName-error.log"); PassThru = $true }
        if ($IsWindows) { $taskTestStart.WindowStyle = 'Hidden' }
        $taskTest = Start-Process @taskTestStart
        $taskTestDeadline = [DateTime]::UtcNow.AddMinutes(11)
        if ($taskMode -eq 'tor') { $taskTestDeadline = [DateTime]::UtcNow.AddMinutes(16) }
        if ($taskMode -eq 'public-sync') { $taskTestDeadline = [DateTime]::UtcNow.AddMinutes($(if ($PublicNetwork -eq 'both') { 85 } else { 45 })) }
        if ($taskMode -eq 'coinjoin' -and $CoinJoinScenario -eq 'restart-output') { $taskTestDeadline = [DateTime]::UtcNow.AddMinutes(20) }
        if ($taskMode -eq 'coinjoin') {
            # Cold Mono JIT and filter scanning can exceed two minutes on a
            # contended local emulator. This bounds readiness, not protocol
            # phases, privacy delays or the overall test deadline.
            $taskBarrierDeadline = [DateTime]::UtcNow.AddMinutes(5)
            do {
                $taskAndroidReady = if ($ReleaseEngine) { (Get-Content -LiteralPath (Join-Path $taskRun "$taskEvidenceName.log") -Raw) -match 'READY: synthetic Android participant' }
                    else { $null = & adb -s $Serial exec-out run-as $taskPackage cat files/coinjoin-ready 2>$null; $LASTEXITCODE -eq 0 }
                $taskBothReady = $taskAndroidReady -and (Test-Path -LiteralPath (Join-Path $taskParticipantDirectory 'ready.txt'))
                if ($taskDisruptingParticipant) { $taskBothReady = $taskBothReady -and (Test-Path -LiteralPath (Join-Path $taskDisruptionDirectory 'ready.txt')) -and !$taskDisruptingParticipant.HasExited }
                if (!$taskBothReady) { Start-Sleep -Milliseconds 500 }
            } while (!$taskBothReady -and !$taskTest.HasExited -and !$taskParticipant.HasExited -and [DateTime]::UtcNow -lt $taskBarrierDeadline)
            if (!$taskBothReady) { throw 'The synthetic participants did not synchronize before the round started.' }
            $taskCoordinatorArgs = @((Join-Path $PSScriptRoot 'RegtestCoordinator/bin/Debug/net10.0/RegtestCoordinator.dll'), (Join-Path $taskRun "coordinator-$taskIteration"))
            if ($CoinJoinRounds -gt 1 -or $CoinJoinScenario -ne 'complete') { $taskCoordinatorArgs += 'qualification' }
            if ($CoinJoinScenario -in @('dropout-confirmation','blame-signing')) { $taskCoordinatorArgs[-1] = 'disruption' }
            if ($CoinJoinScenario -in @('dropout-confirmation','restart-output')) { $taskCoordinatorArgs[-1] = $CoinJoinScenario }
            $taskCoordinatorStart.ArgumentList = $taskCoordinatorArgs
            $taskCoordinatorStart.RedirectStandardOutput = Join-Path $taskRun "coordinator-$taskIteration.log"
            $taskCoordinatorStart.RedirectStandardError = Join-Path $taskRun "coordinator-$taskIteration-error.log"
            if ($IsWindows) { $taskCoordinatorStart.ArgumentList = $taskCoordinatorArgs | ForEach-Object { '"' + $_ + '"' } }
            $taskCoordinator = Start-Process @taskCoordinatorStart
            'Synthetic participants may enter the fresh round.' | Set-Content -LiteralPath (Join-Path $taskParticipantDirectory 'go.txt')
            if ($taskDisruptingParticipant) { 'Synthetic disruption participant may enter the fresh round.' | Set-Content -LiteralPath (Join-Path $taskDisruptionDirectory 'go.txt') }
        }
        if ($taskMode -eq 'coinjoin' -and $taskDisruptingParticipant) {
            $taskDisruptionDeadline = [DateTime]::UtcNow.AddMinutes(5)
            while (!(Test-Path -LiteralPath (Join-Path $taskDisruptionDirectory 'disruption-ready.txt')) -and !$taskDisruptingParticipant.HasExited -and !$taskTest.HasExited -and [DateTime]::UtcNow -lt $taskDisruptionDeadline) { Start-Sleep -Milliseconds 250 }
            if (!(Test-Path -LiteralPath (Join-Path $taskDisruptionDirectory 'disruption-ready.txt'))) { throw 'The independently keyed dropout participant did not reach the selected phase.' }
            Stop-Process -Id $taskDisruptingParticipant.Id
            $taskDisruptingParticipant.WaitForExit(10000) | Out-Null
            if (!$taskDisruptingParticipant.HasExited) { throw 'The synthetic dropout process did not terminate.' }
            Write-Output "Interrupted synthetic participant at $CoinJoinScenario."
        }
        if ($taskMode -eq 'coinjoin' -and $CoinJoinScenario -eq 'restart-output') {
            $taskRestartDeadline = [DateTime]::UtcNow.AddMinutes(8)
            do {
                $taskRestartReady = (Get-Content -LiteralPath (Join-Path $taskRun "$taskEvidenceName.log") -Raw) -match 'DISRUPTION: restart synthetic coordinator'
                if (!$taskRestartReady) { Start-Sleep -Milliseconds 250 }
            } while (!$taskRestartReady -and !$taskTest.HasExited -and [DateTime]::UtcNow -lt $taskRestartDeadline)
            if (!$taskRestartReady) { throw 'The Android participant did not reach coordinator restart.' }
            Stop-Process -Id $taskCoordinator.Id
            $taskCoordinator.WaitForExit(10000) | Out-Null
            if (!$taskCoordinator.HasExited) { throw 'The fixture coordinator did not terminate.' }
            $taskCoordinatorStart.RedirectStandardOutput = Join-Path $taskRun "coordinator-$taskIteration-restarted.log"
            $taskCoordinatorStart.RedirectStandardError = Join-Path $taskRun "coordinator-$taskIteration-restarted-error.log"
            $taskCoordinator = Start-Process @taskCoordinatorStart
            Write-Output 'Restarted the same synthetic coordinator during output registration.'
        }
        if ($taskMode -eq 'coinjoin' -and $CoinJoinScenario -eq 'interrupt-signing') {
            $taskCheckpointDeadline = [DateTime]::UtcNow.AddMinutes(7)
            do {
                $taskCheckpoint = (Get-Content -LiteralPath (Join-Path $taskRun "$taskEvidenceName.log") -Raw) -match 'CHECKPOINT: synthetic CoinJoin signing'
                if (!$taskCheckpoint) { Start-Sleep -Milliseconds 250 }
            } while (!$taskCheckpoint -and !$taskTest.HasExited -and [DateTime]::UtcNow -lt $taskCheckpointDeadline)
            if (!$taskCheckpoint) { throw 'The durable signing checkpoint was not reached before interruption.' }
            & adb -s $Serial shell am force-stop $taskPackage
            if ($LASTEXITCODE -ne 0) { throw 'Fixture process termination failed.' }
            if (!$taskTest.WaitForExit(10000)) { Stop-Process -Id $taskTest.Id }
            $taskEvidenceName += '-resume'
            $taskTestStart.ArgumentList = @('-s',$Serial,'shell','am','instrument','-w','-r','-e','mode','coinjoin','-e','coinjoin-scenario','resume-interruption',"$taskPackage/io.wasabiwallet.android.WalletInstrumentation")
            $taskTestStart.RedirectStandardOutput = Join-Path $taskRun "$taskEvidenceName.log"
            $taskTestStart.RedirectStandardError = Join-Path $taskRun "$taskEvidenceName-error.log"
            $taskTest = Start-Process @taskTestStart
            $taskTestDeadline = [DateTime]::UtcNow.AddMinutes(5)
        }
        while (!$taskTest.HasExited -and [DateTime]::UtcNow -lt $taskTestDeadline) { Start-Sleep -Milliseconds 500 }
        if (!$taskTest.HasExited) {
            & adb -s $Serial shell am force-stop $taskPackage
            # Force-stop normally ends this ADB child itself. Preserve the timeout
            # diagnostic instead of racing that exit with Stop-Process by PID.
            if (!$taskTest.WaitForExit(10000)) { $taskTest.Kill() }
            & adb -s $Serial logcat -d -t 2000 | Set-Content -LiteralPath (Join-Path $taskRun 'android-timeout-logcat.log')
            throw 'Android instrumentation exceeded its bounded timeout.'
        }
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
            if ($taskDisruptingParticipant) {
                $taskFundedInput = Get-Content -LiteralPath (Join-Path $taskDisruptionDirectory 'funded-input.json') -Raw | ConvertFrom-Json
                $taskHonestInput = Get-Content -LiteralPath (Join-Path $taskParticipantDirectory 'funded-input.json') -Raw | ConvertFrom-Json
                if ($taskFundedInput.TransactionId -eq $taskHonestInput.TransactionId -and $taskFundedInput.Index -eq $taskHonestInput.Index) { throw 'The host participant inputs are not independent.' }
                if ((Get-Content -LiteralPath (Join-Path $taskDisruptionDirectory 'engine.log') -Raw) -notmatch 'Registered ') { throw 'The dropout participant never registered its input.' }
                $taskPreservedInput = Invoke-TestRpc 'gettxout' @($taskFundedInput.TransactionId, [int]$taskFundedInput.Index, $true)
                if ($null -eq $taskPreservedInput -or [decimal]$taskPreservedInput.value -ne 0.05) { throw 'The interrupted participant lost its original confirmed input.' }
                Write-Output 'Verified that the interrupted participant retains its funded input.'
                $taskDisruptingParticipant = $null
            }
            $taskParticipantDeadline = [DateTime]::UtcNow.AddSeconds(30)
            if ($CoinJoinScenario -in @('stop-input','interrupt-signing')) { $taskParticipantDeadline = [DateTime]::UtcNow }
            while (!(Test-Path -LiteralPath (Join-Path $taskParticipantDirectory 'completed.txt')) -and [DateTime]::UtcNow -lt $taskParticipantDeadline -and !$taskParticipant.HasExited) { Start-Sleep -Milliseconds 250 }
            if ($CoinJoinScenario -notin @('stop-input','interrupt-signing') -and !(Test-Path -LiteralPath (Join-Path $taskParticipantDirectory 'completed.txt'))) { throw 'The independent participant did not complete the same CoinJoin.' }
            if (!$taskParticipant.HasExited) { $taskParticipant.WaitForExit(10000) | Out-Null }
            if (!$taskParticipant.HasExited) { Stop-Process -Id $taskParticipant.Id }
            $taskParticipant = $null
            if ($taskCoordinator -and !$taskCoordinator.HasExited) { Stop-Process -Id $taskCoordinator.Id; $taskCoordinator.WaitForExit(10000) | Out-Null }
            $taskCoordinator = $null
        }
        Write-Output "Verified Android $taskEvidenceName integration."
        $taskEnvironment = [regex]::Match(($taskResult -join "`n"), 'environment=API ([0-9]+); page size ([0-9]+);')
        $taskVerification.modes += [ordered]@{
            mode=$taskMode; iteration=$taskIteration; result='PASS'; api=[int]$taskEnvironment.Groups[1].Value
            pageSize=[int]$taskEnvironment.Groups[2].Value; actualArchitecture='X64'; runtime=$taskToolchain.monoRuntime
            evidence="$taskEvidenceName.log"; sha256=(Get-FileHash -LiteralPath (Join-Path $taskRun "$taskEvidenceName.log") -Algorithm SHA256).Hash.ToLowerInvariant()
        }
        $taskVerification | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $taskRun 'verification.json')
      }
    }
    $taskVerification.result='PASS'
    $taskVerification | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $taskRun 'verification.json')
    Write-Output "Device evidence: $taskRun"
}
catch {
    if ($taskTest) {
        & adb -s $Serial logcat -d -t 2000 -s AndroidRuntime ActivityManager WasabiTests 2>&1 |
            Set-Content -LiteralPath (Join-Path $taskRun 'failure-logcat.log')
    }
    if ($null -ne $taskVerification) {
        $taskVerification.result='FAIL'
        $taskVerification.failure=$_.Exception.Message
        $taskVerification | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $taskRun 'verification.json')
    }
    throw
}
finally {
    if ($taskTest -and !$taskTest.HasExited) { & adb -s $Serial shell am force-stop $taskPackage; Stop-Process -Id $taskTest.Id -ErrorAction SilentlyContinue }
    # Shut down only the node started by this run; never touch another node if
    # the requested port was occupied and our process failed to start.
    if (!$taskNode.HasExited) { try { Invoke-TestRpc 'stop' | Out-Null } catch { Stop-Process -Id $taskNode.Id -ErrorAction SilentlyContinue } }
    if ($taskCoordinator -and !$taskCoordinator.HasExited) { Stop-Process -Id $taskCoordinator.Id }
    if ($taskParticipant -and !$taskParticipant.HasExited) { Stop-Process -Id $taskParticipant.Id }
    if ($taskDisruptingParticipant -and !$taskDisruptingParticipant.HasExited) { Stop-Process -Id $taskDisruptingParticipant.Id }
    foreach ($taskPort in @(18443,18444,18545)) { & adb -s $Serial reverse --remove "tcp:$taskPort" | Out-Null }
}
