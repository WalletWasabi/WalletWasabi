# If you are not allowed to run this script, run the following command in your PowerShell console: 
# Set-ExecutionPolicy RemoteSigned

$host.UI.RawUI.ForegroundColor = "Green"
$host.UI.RawUI.BackgroundColor = "Black"
Read-Host -Prompt 'Releasing Wasabi Wallet - Insert a pendrive to store macOS notarization candidate files [Press ENTER]'

cd $env:userprofile\desktop/WalletWasabi/WalletWasabi.Packager
dotnet run -- publish

$host.UI.RawUI.ForegroundColor = "Green"
$host.UI.RawUI.BackgroundColor = "Black"
Read-Host -Prompt 'Remove and plug the pendrive to macOS and run the packager to notarize the files. Starting MSI build [Press ENTER]'

$arguments = $env:userprofile + '\Desktop\WalletWasabi\WalletWasabi.WindowsInstaller\WalletWasabi.WindowsInstaller.wixproj /Build "Release|x64"'
Start-Process -FilePath 'C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\Common7\IDE\devenv.com' -ArgumentList $arguments # If -Wait -NoNewWindow added devenv will hang forever at the end of the build.

$host.UI.RawUI.ForegroundColor = "Green"
$host.UI.RawUI.BackgroundColor = "Black"
Read-Host -Prompt 'Wait until WiX building the MSI installer, then [Press ENTER]'
Read-Host -Prompt 'Wait until macOS notarization is done and insert the pendrive to this PC [Press ENTER]'
dotnet run -- sign

Read-Host -Prompt 'Release finished [Press ENTER]'