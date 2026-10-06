param(
    [Parameter(Mandatory = $true)][string]$Serial,
    [Parameter(Mandatory = $true)][string]$BitcoindPath,
    [Parameter(Mandatory = $true)][string]$BaselineApk,
    [Parameter(Mandatory = $true)][string]$UpdateApk,
    [string]$ResumeFixture,
    [string]$Repository = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
)
$ErrorActionPreference = 'Stop'
if ($Serial -notmatch '^emulator-[0-9]+$') { throw 'Native qualification uses only an explicitly selected, disposable emulator.' }
$taskPackage = 'io.wasabiwallet.android.personal'
$taskAdb = Join-Path $env:LOCALAPPDATA 'Android/Sdk/platform-tools/adb.exe'
if (!(Test-Path -LiteralPath $taskAdb)) { $taskAdb = (Get-Command adb).Source }
foreach ($taskFile in @($BitcoindPath,$BaselineApk,$UpdateApk)) { if (!(Test-Path -LiteralPath $taskFile)) { throw "Required file missing: $taskFile" } }
$taskHardware = & $taskAdb -s $Serial shell getprop ro.hardware
if (($taskHardware -join '') -notmatch 'ranchu|goldfish') { throw 'Physical devices must never receive synthetic qualification data.' }
foreach ($taskPort in @(18553,18554)) {
    $taskListener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, $taskPort)
    try { $taskListener.Start() } catch { throw "Qualification port $taskPort is already in use." }
    finally { $taskListener.Stop() }
}
$taskRun = if ($ResumeFixture) { (Resolve-Path -LiteralPath $ResumeFixture).Path } else { Join-Path $Repository ('artifacts/android/native-ui-' + [guid]::NewGuid().ToString('N')) }
if ($ResumeFixture -and (!$taskRun.StartsWith((Join-Path $Repository 'artifacts/android/native-ui-'), [StringComparison]::OrdinalIgnoreCase) -or !(Test-Path -LiteralPath (Join-Path $taskRun 'bitcoin.conf')))) { throw 'Resume only an existing owned synthetic regtest fixture.' }
New-Item -ItemType Directory -Force -Path $taskRun | Out-Null
$taskVerificationPath = Join-Path $taskRun 'verification.json'
if ($ResumeFixture -and (Test-Path -LiteralPath $taskVerificationPath)) {
    $taskPrevious = Get-Content -LiteralPath $taskVerificationPath -Raw | ConvertFrom-Json
    if ($taskPrevious.result -eq 'PASS') {
        foreach ($taskLog in $taskPrevious.logs) {
            if ([IO.Path]::GetFileName($taskLog.path) -ne $taskLog.path -or [IO.Path]::GetExtension($taskLog.path) -ne '.log') { throw 'Previous verification has an invalid log reference.' }
            $taskLogPath = Join-Path $taskRun $taskLog.path
            if (!(Test-Path -LiteralPath $taskLogPath) -or (Get-FileHash -LiteralPath $taskLogPath -Algorithm SHA256).Hash.ToLowerInvariant() -ne $taskLog.sha256) { throw 'Previous verification logs changed. Preserve and investigate the evidence before resuming.' }
        }
    }
    $taskArchive = Join-Path $taskRun ('evidence-before-' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfff') + '-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $taskArchive | Out-Null
    Copy-Item -LiteralPath $taskVerificationPath -Destination (Join-Path $taskArchive 'verification.json')
    Get-ChildItem -LiteralPath $taskRun -File -Filter '*.log' | Copy-Item -Destination $taskArchive
}
function Get-NativeApkIdentity([string]$Apk, [bool]$RequireChecks = $true) {
    $taskHash = (Get-FileHash -LiteralPath $Apk -Algorithm SHA256).Hash.ToLowerInvariant()
    $taskManifestPath = Join-Path (Split-Path -Parent $Apk) 'package-manifest.json'
    if (!(Test-Path -LiteralPath $taskManifestPath)) { throw "Inspect the signed package before UI qualification: $Apk" }
    $taskManifest = Get-Content -LiteralPath $taskManifestPath -Raw | ConvertFrom-Json
    if ($taskManifest.apkSha256 -ne $taskHash -or ($RequireChecks -and (!$taskManifest.packageChecksPassed -or $taskManifest.failedChecks.Count -gt 0))) { throw 'Package inspection does not qualify these APK bytes.' }
    @{ sha256=$taskHash; sourceCommit=$taskManifest.sourceCommit; versionCode=$taskManifest.versionCode; certificateSha256=$taskManifest.certificateSha256; packageChecksPassed=$taskManifest.packageChecksPassed; failedChecks=$taskManifest.failedChecks; screenshotsAllowed=[bool]$taskManifest.screenshotsAllowed; personalNodeSupported=$(if($null -eq $taskManifest.personalNodeSupported){$taskManifest.versionCode -lt 12}else{[bool]$taskManifest.personalNodeSupported}) }
}
$taskVerification = [ordered]@{
    recordedUtc=[DateTime]::UtcNow.ToString('O'); result='INCOMPLETE'; serial=$Serial
    driverSourceCommit=(& git -C $Repository rev-parse HEAD).Trim()
    driverWorkingTreeModified=[bool](& git -C $Repository status --porcelain)
}
$taskVerification | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $taskVerificationPath
try {
    # Upgrades may start from the retained older candidate with known findings.
    # Record those findings; only the final updated APK must pass inspection.
    $taskVerification.baseline=Get-NativeApkIdentity $BaselineApk $false
    $taskVerification.update=Get-NativeApkIdentity $UpdateApk
    if ($taskVerification.baseline.certificateSha256 -ne $taskVerification.update.certificateSha256) { throw 'The update must retain the baseline signing identity.' }
    $taskVerification.api=[int]((& $taskAdb -s $Serial shell getprop ro.build.version.sdk).Trim())
    $taskVerification.architecture=((& $taskAdb -s $Serial shell getprop ro.product.cpu.abi).Trim())
    $taskVerification.pageSize=[int]((& $taskAdb -s $Serial shell getconf PAGE_SIZE).Trim())
    $taskVerification | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $taskVerificationPath
}
catch {
    $taskVerification.result='FAIL'; $taskVerification.failure=$_.Exception.Message
    $taskVerification | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $taskVerificationPath
    throw
}
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
rpcport=18553
port=18554
bind=127.0.0.1
'@ | Set-Content -LiteralPath (Join-Path $taskRun 'bitcoin.conf')
$taskAuthorization = 'Basic ' + [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes('wasabiandroid:wasabi-android-regtest'))
function Invoke-NativeRpc([string]$Method, [object[]]$Params = @()) {
    $taskBody = @{ jsonrpc='2.0'; id=1; method=$Method; params=$Params } | ConvertTo-Json -Compress -Depth 8
    $taskReply = Invoke-RestMethod -Uri 'http://127.0.0.1:18553/' -Method Post -Headers @{ Authorization=$taskAuthorization } -ContentType 'application/json' -Body $taskBody
    if ($null -ne $taskReply.error) { throw "RPC $Method failed: $($taskReply.error.message)" }
    $taskReply.result
}
function Invoke-NativeMode([string]$Mode, [string[]]$Extra = @()) {
    $taskLog = Join-Path $taskRun "$Mode.log"
    $taskModeStart = @{ FilePath=$taskAdb; ArgumentList=@('-s',$Serial,'shell','am','instrument','-w','-r','-e','mode',$Mode,'-e','screenshot-policy',$taskScreenshotPolicy) + $Extra + @('io.wasabiwallet.android.uiqualification/io.wasabiwallet.android.tests.ReleaseUiInstrumentation'); RedirectStandardOutput=$taskLog; RedirectStandardError=(Join-Path $taskRun "$Mode-error.log"); PassThru=$true }
    if ($IsWindows) { $taskModeStart.WindowStyle='Hidden' }
    $taskModeProcess = Start-Process @taskModeStart
    try {
        $taskModeDeadline = [DateTime]::UtcNow.AddMinutes(3)
        while (!$taskModeProcess.HasExited -and [DateTime]::UtcNow -lt $taskModeDeadline) { Start-Sleep -Milliseconds 250 }
        if (!$taskModeProcess.HasExited) {
            & $taskAdb -s $Serial shell am force-stop $taskPackage
            if (!$taskModeProcess.WaitForExit(10000)) { $taskModeProcess.Kill() }
            throw "Actual APK $Mode instrumentation exceeded its bounded timeout. See $taskLog"
        }
        Get-Content -LiteralPath $taskLog
        if ($taskModeProcess.ExitCode -ne 0 -or (Get-Content -LiteralPath $taskLog -Raw) -notmatch "PASS: actual Release $Mode UI") { throw "Actual APK $Mode qualification failed. See $taskLog" }
    }
    finally { $taskModeProcess.Dispose() }
}
function Install-NativeApk([string]$Apk, [string]$Label) {
    $taskInstallLog = Join-Path $taskRun "$Label-install.log"
    $taskInstallStart = @{ FilePath=$taskAdb; ArgumentList=@('-s',$Serial,'install','--no-incremental','-r',('"' + (Resolve-Path -LiteralPath $Apk).Path + '"')); RedirectStandardOutput=$taskInstallLog; RedirectStandardError=(Join-Path $taskRun "$Label-install-error.log"); PassThru=$true }
    if ($IsWindows) { $taskInstallStart.WindowStyle='Hidden' }
    $taskInstallProcess = Start-Process @taskInstallStart
    try {
        $taskInstallDeadline = [DateTime]::UtcNow.AddMinutes(3)
        while (!$taskInstallProcess.HasExited -and [DateTime]::UtcNow -lt $taskInstallDeadline) { Start-Sleep -Milliseconds 250 }
        if (!$taskInstallProcess.HasExited) {
            $taskInstallProcess.Kill()
            $taskInstallProcess.WaitForExit(10000) | Out-Null
            throw "$Label installation exceeded its bounded timeout; existing wallet data is preserved. See $taskInstallLog"
        }
        Get-Content -LiteralPath $taskInstallLog
        if ($taskInstallProcess.ExitCode -ne 0 -or (Get-Content -LiteralPath $taskInstallLog -Raw) -notmatch '(?m)^Success\s*$') { throw "$Label installation failed; existing wallet data is preserved. See $taskInstallLog" }
    }
    finally { $taskInstallProcess.Dispose() }
}
$taskNodeStart = @{ FilePath=$BitcoindPath; ArgumentList=@("-datadir=$taskRun"); RedirectStandardOutput=(Join-Path $taskRun 'node.log'); RedirectStandardError=(Join-Path $taskRun 'node-error.log'); PassThru=$true }
if ($IsWindows) { $taskNodeStart.WindowStyle='Hidden'; $taskNodeStart.ArgumentList='"-datadir=' + $taskRun + '"' }
$taskNode = Start-Process @taskNodeStart
$taskInstrumentation = $null
try {
    $taskDeadline = [DateTime]::UtcNow.AddSeconds(45)
    do { try { $taskInfo=Invoke-NativeRpc getblockchaininfo; break } catch { Start-Sleep -Milliseconds 500 } } while ([DateTime]::UtcNow -lt $taskDeadline -and !$taskNode.HasExited)
    if (!$taskInfo -or $taskInfo.chain -ne 'regtest') { throw 'Owned regtest node did not start.' }
    if ($ResumeFixture) { Invoke-NativeRpc loadwallet @('native-ui') | Out-Null }
    else { Invoke-NativeRpc createwallet @('native-ui') | Out-Null }
    $taskMining = Invoke-NativeRpc getnewaddress
    if (!$ResumeFixture) { Invoke-NativeRpc generatetoaddress @(101,$taskMining) | Out-Null }
    $taskDestination = Invoke-NativeRpc getnewaddress
    & $taskAdb -s $Serial root | Out-Null
    & $taskAdb -s $Serial wait-for-device
    # Restarting adbd drops reverse listeners. Configure them after root.
    foreach ($taskPair in @(@(18443,18553),@(18444,18554))) { & $taskAdb -s $Serial reverse "tcp:$($taskPair[0])" "tcp:$($taskPair[1])"; if ($LASTEXITCODE -ne 0) { throw 'ADB reverse failed.' } }
    $taskPrivate = "/data/user/0/$taskPackage/files/Wasabi"
    # Refuse to overwrite an existing wallet or repair a signing mismatch by
    # deleting data. Use another fresh, disposable emulator in that situation.
    $taskExisting = & $taskAdb -s $Serial shell "find $taskPrivate/Wallets -type f 2>/dev/null"
    if ($taskExisting -and (!$ResumeFixture -or @($taskExisting | Where-Object { $_ -notmatch '/Native qualification\.json(\.old)?$' }).Count -gt 0)) { throw 'The selected emulator contains an unrelated wallet. Qualification preserves it; select a fresh emulator.' }
    Install-NativeApk $BaselineApk 'baseline'
    $taskScreenshotPolicy=if($taskVerification.baseline.screenshotsAllowed){'allow'}else{'blocked'}
    $taskApi = [int]((& $taskAdb -s $Serial shell getprop ro.build.version.sdk).Trim())
    if ($taskApi -ge 33) {
        & $taskAdb -s $Serial shell pm grant $taskPackage android.permission.POST_NOTIFICATIONS
        if ($LASTEXITCODE -ne 0) { throw 'Notification permission setup failed.' }
    }
    Install-NativeApk (Join-Path $PSScriptRoot 'NativeUiHarness/bin/io.wasabiwallet.android.uiqualification.apk') 'harness'
    & $taskAdb -s $Serial shell am force-stop $taskPackage
    $taskSettings = Join-Path $taskRun 'mobile-settings.json'
    @{ Network='regtest'; Coordinator=''; CoordinatorIdentifier='CoinJoinCoordinatorIdentifier'; BitcoinRpcUri='http://127.0.0.1:18443/' } | ConvertTo-Json | Set-Content -LiteralPath $taskSettings -Encoding utf8NoBOM
    $taskRemote = '/data/local/tmp/wasabi-ui-' + [guid]::NewGuid().ToString('N') + '.json'
    & $taskAdb -s $Serial push $taskSettings $taskRemote | Out-Null
    $taskUid = (& $taskAdb -s $Serial shell "stat -c %u /data/user/0/$taskPackage").Trim()
    if ($taskUid -notmatch '^\d+$') { throw 'Application UID could not be verified.' }
    # Emulator qualification injects its localhost node separately from mobile
    # settings. The personal runtime ignores this file on public networks and
    # rejects regtest on hardware phones; it cannot configure a personal node.
    & $taskAdb -s $Serial shell "mkdir -p $taskPrivate"
    & $taskAdb -s $Serial shell "cp $taskRemote $taskPrivate/mobile-settings.json"
    & $taskAdb -s $Serial shell "chown ${taskUid}:${taskUid} $taskPrivate"
    & $taskAdb -s $Serial shell "chown ${taskUid}:${taskUid} $taskPrivate/mobile-settings.json"
    & $taskAdb -s $Serial shell "chmod 600 $taskPrivate/mobile-settings.json"
    & $taskAdb -s $Serial shell "rm $taskRemote"
    & $taskAdb -s $Serial shell "restorecon -R $taskPrivate"
    $taskRegtestNode = Join-Path $taskRun 'regtest-node.json'
    @{ Uri='http://127.0.0.1:18443/'; Credentials='wasabiandroid:wasabi-android-regtest' } | ConvertTo-Json | Set-Content -LiteralPath $taskRegtestNode -Encoding utf8NoBOM
    & $taskAdb -s $Serial push $taskRegtestNode $taskRemote | Out-Null
    & $taskAdb -s $Serial shell "cp $taskRemote $taskPrivate/regtest-node.json"
    & $taskAdb -s $Serial shell "chown ${taskUid}:${taskUid} $taskPrivate/regtest-node.json"
    & $taskAdb -s $Serial shell "chmod 600 $taskPrivate/regtest-node.json"
    & $taskAdb -s $Serial shell "restorecon $taskPrivate/regtest-node.json"
    & $taskAdb -s $Serial shell "rm $taskRemote"
    $taskSetupMode = if($taskVerification.baseline.personalNodeSupported){'setup-rpc'}else{'settings'}
    Invoke-NativeMode $taskSetupMode
    & $taskAdb -s $Serial shell am force-stop $taskPackage
    # Legacy baselines save mainnet while enrolling their old RPC credential.
    # Restore only this owned fixture's regtest settings before wallet testing.
    & $taskAdb -s $Serial push $taskSettings $taskRemote | Out-Null
    & $taskAdb -s $Serial shell "cp $taskRemote $taskPrivate/mobile-settings.json"
    & $taskAdb -s $Serial shell "chown ${taskUid}:${taskUid} $taskPrivate/mobile-settings.json"
    & $taskAdb -s $Serial shell "chmod 600 $taskPrivate/mobile-settings.json"
    & $taskAdb -s $Serial shell "restorecon $taskPrivate/mobile-settings.json"
    & $taskAdb -s $Serial shell "rm $taskRemote"
    $taskLog = Join-Path $taskRun 'wallet.log'
    $taskArgs = @('-s',$Serial,'shell','am','instrument','-w','-r','-e','mode','wallet','-e','destination',$taskDestination,'-e','inactivity','true','-e','screenshot-policy',$taskScreenshotPolicy,'io.wasabiwallet.android.uiqualification/io.wasabiwallet.android.tests.ReleaseUiInstrumentation')
    $taskStart = @{ FilePath=$taskAdb; ArgumentList=$taskArgs; RedirectStandardOutput=$taskLog; RedirectStandardError=(Join-Path $taskRun 'wallet-error.log'); PassThru=$true }
    if ($IsWindows) { $taskStart.WindowStyle='Hidden' }
    $taskInstrumentation = Start-Process @taskStart
    $taskDeadline = [DateTime]::UtcNow.AddMinutes(7)
    $taskFunded=$false; $taskTransaction=$null
    while (!$taskInstrumentation.HasExited -and [DateTime]::UtcNow -lt $taskDeadline) {
        $taskText = Get-Content -LiteralPath $taskLog -Raw
        if (!$taskFunded -and $taskText -match 'RECEIVE=(bcrt1[a-z0-9]+)') {
            $taskReceive=$Matches[1]
            Invoke-NativeRpc sendtoaddress @($taskReceive,1.0) | Out-Null
            Invoke-NativeRpc generatetoaddress @(1,$taskMining) | Out-Null
            $taskFunded=$true
        }
        if ($taskText -match 'TRANSACTION=([a-f0-9]{64})') { $taskTransaction=$Matches[1] }
        Start-Sleep -Milliseconds 500
    }
    if (!$taskInstrumentation.HasExited) { throw 'Native UI qualification exceeded its bounded timeout.' }
    $taskResult = Get-Content -LiteralPath $taskLog -Raw
    if ($taskResult -notmatch 'PASS: actual Release wallet UI' -or !$taskTransaction -or $taskResult -notmatch 'AUTH_REJECTED') { throw "Native payment/authorization/locking qualification failed. See $taskLog" }
    if ($taskResult -notmatch 'INSTRUMENTATION_RESULT: versionCode=(\d+)') { throw 'Actual baseline version was not reported.' }
    $taskVerification.baseline.versionCode=[int]$Matches[1]
    $taskRaw = Invoke-NativeRpc getrawtransaction @($taskTransaction,$true)
    $taskRecipientOutputs = @($taskRaw.vout | Where-Object { $_.scriptPubKey.address -eq $taskDestination })
    if ($taskRecipientOutputs.Count -ne 1 -or [decimal]$taskRecipientOutputs[0].value -ne 0.1) { throw 'Actual approved recipient output mismatch.' }
    if (@(Invoke-NativeRpc getrawmempool).Count -ne 1) { throw 'Duplicate tap produced an unexpected additional payment.' }
    # Snapshot hashes, not private contents, before process death and update.
    & $taskAdb -s $Serial shell am force-stop $taskPackage
    $taskJournal = & $taskAdb -s $Serial shell "find $taskPrivate -maxdepth 1 -name 'submissions-*.json'"
    if (@($taskJournal).Count -ne 1) { throw 'Actual durable journal missing.' }
    $taskBefore = (& $taskAdb -s $Serial shell "sha256sum $taskJournal").Split(' ')[0]
    Invoke-NativeMode resume @('-e','transaction',$taskTransaction)
    Copy-Item -LiteralPath (Join-Path $taskRun 'resume.log') -Destination (Join-Path $taskRun 'resume-before-update.log')
    & $taskAdb -s $Serial shell am force-stop $taskPackage
    Install-NativeApk $UpdateApk 'update'
    $taskScreenshotPolicy=if($taskVerification.update.screenshotsAllowed){'allow'}else{'blocked'}
    Invoke-NativeMode resume @('-e','transaction',$taskTransaction)
    $taskUpdatedResult=Get-Content -LiteralPath (Join-Path $taskRun 'resume.log') -Raw
    if ($taskUpdatedResult -notmatch 'INSTRUMENTATION_RESULT: versionCode=(\d+)' -or [int]$Matches[1] -ne $taskVerification.update.versionCode) { throw 'Actual updated version does not match the inspected package.' }
    $taskAfter = (& $taskAdb -s $Serial shell "sha256sum $taskJournal").Split(' ')[0]
    if ($taskBefore -ne $taskAfter) { throw 'Update changed pending transaction journal bytes.' }
    if ($taskVerification.update.versionCode -ge 13) {
        & $taskAdb -s $Serial shell am force-stop $taskPackage
        Invoke-NativeMode lost-device-key
        if ((Get-Content -LiteralPath (Join-Path $taskRun 'lost-device-key.log') -Raw) -notmatch 'DEVICE_KEY_LOSS_PASSWORD_RECOVERY') { throw 'Actual device-key loss did not preserve password recovery.' }
    }
    $taskBlock = @(Invoke-NativeRpc generatetoaddress @(1,$taskMining))[0]
    if ((Invoke-NativeRpc getrawtransaction @($taskTransaction,$true)).confirmations -lt 1) { throw 'Actual payment did not confirm.' }
    $taskVerification.destination=$taskDestination
    $taskVerification.transactionId=$taskTransaction
    $taskVerification.journalSha256=$taskAfter
    $taskVerification.baselineSha256=$taskVerification.baseline.sha256
    $taskVerification.updateSha256=$taskVerification.update.sha256
    $taskVerification.confirmationBlock=$taskBlock
    $taskVerification.checkedUtc=[DateTime]::UtcNow.ToString('O')
    $taskVerification.result='PASS'
    $taskEvidenceLogs=@("$taskSetupMode.log",'wallet.log','resume-before-update.log','resume.log')
    if ($taskVerification.update.versionCode -ge 13) { $taskEvidenceLogs += 'lost-device-key.log' }
    $taskVerification.logs=$taskEvidenceLogs | ForEach-Object { @{ path=$_; sha256=(Get-FileHash -LiteralPath (Join-Path $taskRun $_) -Algorithm SHA256).Hash.ToLowerInvariant() } }
    $taskVerification | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $taskVerificationPath
    Write-Output "Actual Release APK native UI evidence: $taskRun"
}
catch {
    $taskVerification.result='FAIL'
    $taskVerification.failure=$_.Exception.Message
    $taskVerification.checkedUtc=[DateTime]::UtcNow.ToString('O')
    $taskVerification | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $taskVerificationPath
    throw
}
finally {
    if ($taskInstrumentation -and !$taskInstrumentation.HasExited) {
        & $taskAdb -s $Serial shell am force-stop $taskPackage
        if (!$taskInstrumentation.WaitForExit(10000)) { $taskInstrumentation.Kill() }
    }
    if (!$taskNode.HasExited) { try { Invoke-NativeRpc stop | Out-Null } catch { Stop-Process -Id $taskNode.Id -ErrorAction SilentlyContinue } }
    foreach ($taskPort in @(18443,18444)) { & $taskAdb -s $Serial reverse --remove "tcp:$taskPort" | Out-Null }
}
