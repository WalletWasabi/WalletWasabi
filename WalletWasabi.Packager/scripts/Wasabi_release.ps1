# If you have permission problems runnig this script run this in PS: Set-ExecutionPolicy RemoteSigned

$host.UI.RawUI.ForegroundColor = "Green"
$host.UI.RawUI.BackgroundColor = "Black"
Read-Host -Prompt 'Releasing Wasabi Wallet - Insert pendrive for macOS notarization candidate files [Press ENTER]'

cd $env:userprofile\desktop/WalletWasabi/WalletWasabi.Packager
dotnet run -- publish

$host.UI.RawUI.ForegroundColor = "Green"
$host.UI.RawUI.BackgroundColor = "Black"
Read-Host -Prompt 'Remove and Plug the penrive to macOS and run the packager to notarize the files. Starting MSI build [Press ENTER]'

$arguments = $env:userprofile + '\Desktop\WalletWasabi\WalletWasabi.WindowsInstaller\WalletWasabi.WindowsInstaller.wixproj /Build "Release|x64"'
Start-Process -FilePath 'C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\Common7\IDE\devenv.com' -ArgumentList $arguments -NoNewWindow -Wait 

$host.UI.RawUI.ForegroundColor = "Green"
$host.UI.RawUI.BackgroundColor = "Black"
Read-Host -Prompt 'Wait until macOS notarization is done and insert the pendrive to this PC [Press ENTER]'
dotnet run -- sign

Read-Host -Prompt 'Release finished [Press ENTER]'