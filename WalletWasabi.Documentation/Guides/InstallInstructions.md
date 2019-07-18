|          | Website                                                                |
|----------|------------------------------------------------------------------------|
| Clearnet | https://wasabiwallet.io/                                               |
| Tor      | http://wasabiukrxmkdgve5kynjztuovbg43uxcbcxn6y2okcrsg7gb6jdmbad.onion/ |

# Windows

## Video Guides

Check out [this](https://www.youtube.com/watch?v=tkaaC8yET1o) or [this](https://www.youtube.com/watch?v=D8U53PFEsVk) video guides or follow the instructions below:

![](https://imgur.com/K2J1WWG.png)

Download the Windows installer (.msi), double click on it and follow the instructions.
Wasabi will be installed to your `C:\Program Files\WasabiWallet\` folder. You will also have a shortcut in your Start Menu and on your Desktop.

After the first run, a working directory will be created: `%appdata%\WalletWasabi\`. Among others, here is where your wallet files and your logs reside.

# Debian/Ubuntu Based Linux

Check out [Max's video tutorial](https://www.youtube.com/watch?v=DUc9A76rwX4) or follow the instructions below:

After downloading the `.deb` package, install it by double clicking on it or running `sudo dpkg -i Wasabi-1.1.6.deb`.

After the first run, a working directory will be created: `~/.walletwasabi/`. Among others, here is where your wallet files and your logs reside.

# Linux

Check out this short, to-the-point [video guide](https://www.youtube.com/watch?v=qFbv_b-bju4), or [this other funnier one](https://www.youtube.com/watch?time_continue=4&v=zPKpC9cRcZo) with more explanations, or follow the instructions below. Note that the first video was created on OSX, but the steps are the same for Linux.

![](https://imgur.com/wsJ66Qt.png)

Download the Linux archive and extract it, while keeping the file permissions: `tar -pxzf WasabiLinux-1.1.6.tar.gz`.
You can run Wasabi by executing `./wassabee`.

After the first run, a working directory will be created: `~/.walletwasabi/`. Among others, here is where your wallet files and your logs reside.

# OSX

Check out this [video guide](https://www.youtube.com/watch?v=_Zmc54XYzBA) or follow the instructions below:

![](https://imgur.com/k0cEYjz.png)

Download and open the `.dmg` file, then install Wasabi by dragging it into your `Applications` folder.

![](https://i.imgur.com/7UEZ8wI.png)

After opening Wasabi, you may encounter a security popup. You can bypass it in multiple ways:
One way would be to keep the control key down while opening Wasabi.

![](https://imgur.com/dy1zfJG.png)

Another way is to go to System Preferences / Security & Privacy, where you should find a message `"Wasabi Wallet" was blocked from opening because it is not from an identified developer` and an `open anyway` button. Click the button and confirm by entering your Mac user password.

# GPG Verification

Get [GnuPG](https://www.gnupg.org/download/index.html), then save [nopara73's PGP](https://github.com/zkSNACKs/WalletWasabi/blob/master/PGP.txt).

Import the key you downloaded to GnuPG. Open the terminal/command line and navigate to the folder in which you saved the `PGP.txt` file and run:

```sh
gpg --import PGP.txt
```

From now on, with this public key you will be able to make sure the Wasabi software you download was not tampered with by checking against the corresponding `.asc` file:

```sh
gpg --verify {file name}.asc {file name}.msi
```

Example: `gpg --verify WasabiInstaller.msi.asc WasabiInstaller.msi`.
 
If the message returned says `Good signature` and that it was signed by `Ficsór Ádám` with Primary key fingerprint: `21D7 CA45 565D BCCE BE45  115D B4B7 2266 C47E 075E`, then the software was not tampered with since the developer signed it.
 
Remember to check again the PGP signature every time you make a new download.

If you trust nopara73's key and you are familiar with the [Web Of Trust](https://security.stackexchange.com/questions/147447/gpg-why-is-my-trusted-key-not-certified-with-a-trusted-signature), please consider also [validating it](https://www.gnupg.org/gph/en/manual/x334.html).

## GPG Verification on OSX

Get [GnuPG](https://www.gnupg.org/download/index.html), then save [nopara73's PGP](https://github.com/zkSNACKs/WalletWasabi/blob/master/PGP.txt). You can do so by copy-pasting it into a new TextEdit document and saving it as `PGP.txt`. Before saving, you need to go to Format / Make Plain Text (otherwise TextEdit will not be able to save it as a .txt file).

Open the Terminal and navigate to the folder in which you saved the `PGP.txt` file. In this case, it is on the desktop (substitute `desktop` with the actual folder): 

```sh
cd desktop
``` 

Import nopara73's PGP key (you might be asked to enter your Mac user password to confirm the command): 

```sh
sudo gpg2 --import PGP.txt
``` 


This should return the output: 

`key B4B72266C47E075E: public key "nopara73 (GitHub key) <nopara73@github.com>" imported` 

From now on, with this public key you will be able to make sure the Wasabi software you download (the `.dmg` file) was not tampered with by checking against the corresponding `.asc` file.

Navigate to the location where the `.dmg` and the `.asc` files are saved. In this case, it is in the Downloads folder (substitute accordingly, if necessary): 

```sh
cd downloads
```

Check the signature (you might be asked for the Mac user password again):

```sh
sudo gpg2 --verify {file name}.asc {file name}.dmg
```

Example: `gpg2 --verify Wasabi-1.1.6.dmg.asc Wasabi-1.1.6.dmg`.

If the message returned says `Good signature from nopara73 aka Ficsór Ádám` and that it was signed with Primary key fingerprint: `21D7 CA45 565D BCCE BE45  115D B4B7 2266 C47E 075E`, then the software was not tampered with since the developer signed it.
 
Remember to check again the PGP signature every time you make a new download.

If you trust nopara73's key and you are familiar with the [Web Of Trust](https://security.stackexchange.com/questions/147447/gpg-why-is-my-trusted-key-not-certified-with-a-trusted-signature), please consider also [validating it](https://www.gnupg.org/gph/en/manual/x334.html).

## GPG Verification with GUI (Windows)

If you prefer the Graphical User Interface, this guide is yours. There is also a nice video guide [here](https://youtu.be/D8U53PFEsVk?t=45). 

1. Download Gpg4win from https://www.gnupg.org/download/index.html.
2. Install Gpg4win.

![Install Gpg4win](https://i.imgur.com/YKDdw1k.png)

3. Download Wasabi's latest __release__ and the corresponding __.asc__ file.

4. Double click on .asc file or right click it and navigate to More GpgEX options and click Verify. If the context menu is missing restart the computer.

![](https://i.imgur.com/fJME8Yh.png)

5. Press Search.

![](https://i.imgur.com/cj00rev.png)

6. Wait until Ficsór Ádám's certificate (adam.ficsor73@gmail.com) appears. Who is this guy? The owner of WasabiWallet AKA [nopara73](https://github.com/nopara73).

![](https://i.imgur.com/B3WZn1n.png)

7. Add Ádám's certificate. (Next time you want to verify a new release you can skip the previous steps because the certificate will be there).

![](https://i.imgur.com/9zGpuI6.png)

8. Select all and verify the fingerprint: `21D7 CA45 565D BCCE BE45  115D B4B7 2266 C47E 075E`.

![](https://i.imgur.com/PfdbegY.png)

9. Press next, next, next. If there is an error, you can try to import the key manually, navigate to section: [Import key manually](https://github.com/molnard/WalletWasabi/blob/patch-3/WalletWasabi.Documentation/Guides/InstallInstructions.md#import-key-manually).

10. Successful validation. The file was signed by the developer.

![Imgur](https://i.imgur.com/7e0O9dQ.png)

11. You can install Wasabi with the msi.

### Import key manually

1. Go to [this link](https://github.com/zkSNACKs/WalletWasabi), you will find the developer's (nopara73) [public key](https://github.com/zkSNACKs/WalletWasabi/blob/master/PGP.txt) there. Press Copy.

![Imgur](https://i.imgur.com/zLVqhOu.png)

2. Create a TXT file and name it pgp.

![Create txt file](https://i.imgur.com/F8LMu6W.png), 

3. Open it and right click and paste. It looks like this:

![Imgur](https://i.imgur.com/82XiHce.png)

4. Save the file and close.

5. Right click on pgp.txt file. In the context menu navigate to More GpgEx options and click Import keys. If the context menu is missing restart the computer.

![Imgur](https://i.imgur.com/qmuF3Hx.png)

6. Kleopatra pops up and nopara73's key is imported. Press OK and close Kleopatra.

![Imgur](https://i.imgur.com/EICwNWq.png)

