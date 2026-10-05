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
    & $taskAdb -s $Serial shell am instrument -w -r -e mode $Mode @Extra io.wasabiwallet.android.uiqualification/io.wasabiwallet.android.tests.ReleaseUiInstrumentation | Tee-Object $taskLog
    if ($LASTEXITCODE -ne 0 -or (Get-Content -LiteralPath $taskLog -Raw) -notmatch "PASS: actual Release $Mode UI") { throw "Actual APK $Mode qualification failed. See $taskLog" }
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
    & $taskAdb -s $Serial install --no-incremental -r $BaselineApk
    if ($LASTEXITCODE -ne 0) { throw 'Baseline installation failed; existing data is preserved.' }
    $taskApi = [int]((& $taskAdb -s $Serial shell getprop ro.build.version.sdk).Trim())
    if ($taskApi -ge 33) {
        & $taskAdb -s $Serial shell pm grant $taskPackage android.permission.POST_NOTIFICATIONS
        if ($LASTEXITCODE -ne 0) { throw 'Notification permission setup failed.' }
    }
    & $taskAdb -s $Serial install --no-incremental -r (Join-Path $PSScriptRoot 'NativeUiHarness/bin/io.wasabiwallet.android.uiqualification.apk')
    if ($LASTEXITCODE -ne 0) { throw 'Native harness installation failed.' }
    & $taskAdb -s $Serial shell am force-stop $taskPackage
    $taskSettings = Join-Path $taskRun 'mobile-settings.json'
    @{ Network='regtest'; Coordinator=''; CoordinatorIdentifier='CoinJoinCoordinatorIdentifier'; BitcoinRpcUri='http://127.0.0.1:18443/' } | ConvertTo-Json | Set-Content -LiteralPath $taskSettings -Encoding utf8NoBOM
    $taskRemote = '/data/local/tmp/wasabi-ui-' + [guid]::NewGuid().ToString('N') + '.json'
    & $taskAdb -s $Serial push $taskSettings $taskRemote | Out-Null
    $taskUid = (& $taskAdb -s $Serial shell "stat -c %u /data/user/0/$taskPackage").Trim()
    if ($taskUid -notmatch '^\d+$') { throw 'Application UID could not be verified.' }
    # Seed only public regtest settings before the first app launch. Otherwise
    # unrelated public Tor bootstrap becomes a prerequisite of this funded UI
    # fixture. Credentials are still entered and vaulted through the real UI.
    & $taskAdb -s $Serial shell "mkdir -p $taskPrivate"
    & $taskAdb -s $Serial shell "cp $taskRemote $taskPrivate/mobile-settings.json"
    & $taskAdb -s $Serial shell "chown ${taskUid}:${taskUid} $taskPrivate"
    & $taskAdb -s $Serial shell "chown ${taskUid}:${taskUid} $taskPrivate/mobile-settings.json"
    & $taskAdb -s $Serial shell "chmod 600 $taskPrivate/mobile-settings.json"
    & $taskAdb -s $Serial shell "rm $taskRemote"
    & $taskAdb -s $Serial shell "restorecon -R $taskPrivate"
    Invoke-NativeMode setup-rpc
    & $taskAdb -s $Serial shell am force-stop $taskPackage
    # Regtest is intentionally absent from the production network selector.
    # Its real settings UI saved mainnet while enrolling the RPC credential;
    # restore the emulator's public fixture network before starting the wallet.
    & $taskAdb -s $Serial push $taskSettings $taskRemote | Out-Null
    & $taskAdb -s $Serial shell "cp $taskRemote $taskPrivate/mobile-settings.json"
    & $taskAdb -s $Serial shell "chown ${taskUid}:${taskUid} $taskPrivate/mobile-settings.json"
    & $taskAdb -s $Serial shell "chmod 600 $taskPrivate/mobile-settings.json"
    & $taskAdb -s $Serial shell "restorecon $taskPrivate/mobile-settings.json"
    & $taskAdb -s $Serial shell "rm $taskRemote"
    $taskLog = Join-Path $taskRun 'wallet.log'
    $taskArgs = @('-s',$Serial,'shell','am','instrument','-w','-r','-e','mode','wallet','-e','destination',$taskDestination,'-e','inactivity','true','io.wasabiwallet.android.uiqualification/io.wasabiwallet.android.tests.ReleaseUiInstrumentation')
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
    & $taskAdb -s $Serial shell am force-stop $taskPackage
    & $taskAdb -s $Serial install --no-incremental -r $UpdateApk
    if ($LASTEXITCODE -ne 0) { throw 'Signed update failed; wallet data is preserved.' }
    Invoke-NativeMode resume @('-e','transaction',$taskTransaction)
    $taskAfter = (& $taskAdb -s $Serial shell "sha256sum $taskJournal").Split(' ')[0]
    if ($taskBefore -ne $taskAfter) { throw 'Update changed pending transaction journal bytes.' }
    $taskBlock = @(Invoke-NativeRpc generatetoaddress @(1,$taskMining))[0]
    if ((Invoke-NativeRpc getrawtransaction @($taskTransaction,$true)).confirmations -lt 1) { throw 'Actual payment did not confirm.' }
    @{ serial=$Serial; destination=$taskDestination; transactionId=$taskTransaction; journalSha256=$taskAfter; baselineSha256=(Get-FileHash $BaselineApk -Algorithm SHA256).Hash; updateSha256=(Get-FileHash $UpdateApk -Algorithm SHA256).Hash; confirmationBlock=$taskBlock; checkedUtc=[DateTime]::UtcNow.ToString('O'); result='PASS' } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $taskRun 'verification.json')
    Write-Output "Actual Release APK native UI evidence: $taskRun"
}
finally {
    if ($taskInstrumentation -and !$taskInstrumentation.HasExited) { & $taskAdb -s $Serial shell am force-stop $taskPackage; Stop-Process -Id $taskInstrumentation.Id -ErrorAction SilentlyContinue }
    if (!$taskNode.HasExited) { try { Invoke-NativeRpc stop | Out-Null } catch { Stop-Process -Id $taskNode.Id -ErrorAction SilentlyContinue } }
    foreach ($taskPort in @(18443,18444)) { & $taskAdb -s $Serial reverse --remove "tcp:$taskPort" | Out-Null }
}
