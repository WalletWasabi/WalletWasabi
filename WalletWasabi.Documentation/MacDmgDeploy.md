# Creating dmg file for mac users

1. Get a Mac
2. Install create-dmg https://github.com/andreyvit/create-dmg 
3. Create a folder called somewhere called wasabidmg
4. Download the current release for Mac https://github.com/zkSNACKs/WalletWasabi/releases
5. Extract the content of WasabiOsx-x.y.z.tar.gz under wasabidmg/wasabi
6. Copy the following two files under wasabidmg https://github.com/zkSNACKs/WalletWasabi/blob/master/WalletWasabi.Gui/Assets/Logo_with_text_small.png and https://github.com/zkSNACKs/WalletWasabi/blob/master/WalletWasabi.Gui/Assets/Logo_without_text.icns
7. Open terminal at wasabidmg
8. Set the version number in the following command and run it:  ```create-dmg --volname "WasabiWallet 1.0.2 Installer" --volicon "Logo_without_text.icns" --background "Logo_with_text_small.png" --window-pos 200 120 --window-size 600 440 --icon "wassabee" 110 150 --app-drop-link 500 150 --hdiutil-verbose "Wasabi_1_0_2-Installer.dmg" "wasabi/"```
9. Dmg file is created under wasabidmg
10. Upload the file to the web