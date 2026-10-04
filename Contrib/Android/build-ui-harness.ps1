param(
    [string]$SigningDirectory = (Join-Path $env:USERPROFILE '.codex/secrets/wasabi-android-personal'),
    [string]$AndroidSdk = (Join-Path $env:LOCALAPPDATA 'Android/Sdk'),
    [string]$JavaSdk = 'C:/Program Files/Android/openjdk/jdk-21.0.8'
)
$ErrorActionPreference = 'Stop'
$taskDirectory = Join-Path $PSScriptRoot 'NativeUiHarness'
$taskClasses = Join-Path $taskDirectory 'obj/classes'
$taskDex = Join-Path $taskDirectory 'obj/dex'
$taskOutput = Join-Path $taskDirectory 'bin'
New-Item -ItemType Directory -Force $taskClasses,$taskDex,$taskOutput | Out-Null
$taskJar = Join-Path $AndroidSdk 'platforms/android-36/android.jar'
$taskTools = Join-Path $AndroidSdk 'build-tools/36.0.0'
& (Join-Path $JavaSdk 'bin/javac.exe') -encoding UTF-8 --release 8 -classpath $taskJar -d $taskClasses (Join-Path $taskDirectory 'ReleaseUiInstrumentation.java')
if ($LASTEXITCODE -ne 0) { throw 'Native UI harness compilation failed.' }
$taskClassFiles = @(Get-ChildItem -LiteralPath $taskClasses -Filter '*.class' -File -Recurse | ForEach-Object FullName)
$taskOldJava = $env:JAVA_HOME
$env:JAVA_HOME = $JavaSdk
try {
    & (Join-Path $taskTools 'd8.bat') --lib $taskJar --min-api 24 --output $taskDex @taskClassFiles
    if ($LASTEXITCODE -ne 0) { throw 'Native UI harness DEX build failed.' }
    $taskBase = Join-Path $taskOutput 'unsigned.apk'
    & (Join-Path $taskTools 'aapt2.exe') link -o $taskBase --manifest (Join-Path $taskDirectory 'AndroidManifest.xml') -I $taskJar
    if ($LASTEXITCODE -ne 0) { throw 'Native UI harness packaging failed.' }
    $taskZip = [IO.Compression.ZipFile]::Open($taskBase, [IO.Compression.ZipArchiveMode]::Update)
    try { [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($taskZip, (Join-Path $taskDex 'classes.dex'), 'classes.dex') | Out-Null }
    finally { $taskZip.Dispose() }
    $taskAligned = Join-Path $taskOutput 'aligned.apk'
    & (Join-Path $taskTools 'zipalign.exe') -P 16 -f 4 $taskBase $taskAligned
    if ($LASTEXITCODE -ne 0) { throw 'Native UI harness alignment failed.' }
    $taskSecure = (Get-Content -LiteralPath (Join-Path $SigningDirectory 'password.dpapi') -Raw).Trim() | ConvertTo-SecureString
    $taskPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($taskSecure)
    try {
        $env:WASABI_UI_SIGN_PASS = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($taskPointer)
        & (Join-Path $taskTools 'apksigner.bat') sign --ks (Join-Path $SigningDirectory 'personal.keystore') --ks-key-alias wasabi-personal --ks-pass env:WASABI_UI_SIGN_PASS --key-pass env:WASABI_UI_SIGN_PASS --out (Join-Path $taskOutput 'io.wasabiwallet.android.uiqualification.apk') $taskAligned
        if ($LASTEXITCODE -ne 0) { throw 'Native UI harness signing failed.' }
    }
    finally { Remove-Item Env:WASABI_UI_SIGN_PASS -ErrorAction SilentlyContinue; [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($taskPointer); $taskSecure.Dispose() }
} finally { $env:JAVA_HOME = $taskOldJava }
Write-Output 'The native emulator UI harness was built separately; do not distribute it with the personal wallet.'
