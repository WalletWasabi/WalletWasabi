|          | Website                                                                |
|----------|------------------------------------------------------------------------|
| Clearnet | https://wasabiwallet.io/                                               |
| Tor      | http://wasabiukrxmkdgve5kynjztuovbg43uxcbcxn6y2okcrsg7gb6jdmbad.onion/ |

# Windows

Check out this [video guide](https://www.youtube.com/watch?v=tkaaC8yET1o) or take a look at the instructions below:

![](https://imgur.com/K2J1WWG.png)

Download the Windows installer (.msi) and follow the instructions.
Wasabi will be installed to your `C:\Program Files\WasabiWallet\` folder. You will also have an icon in your Start Menu and on your Desktop.  

After first run, a working directory will be created: `%appdata%\WalletWasabi\`. Among others, here is where your wallet files and your logs reside.

# Debian/Ubuntu Based Linux

Check out [Max's video tutorial](https://www.youtube.com/watch?v=DUc9A76rwX4) or follow the instructions:

After downloading the `.deb` package install it by running double clicking on it or running `sudo dpkg -i Wasabi-1.1.1.deb`.

After first run, a working directory will be created: `~/.walletwasabi/`. Among others, here is where your wallet files and your logs reside.

# Linux

Check out this short, to-the-point [video guide](https://www.youtube.com/watch?v=qFbv_b-bju4), or [this other funnier one](https://www.youtube.com/watch?time_continue=4&v=zPKpC9cRcZo) with more explanation, or take a look at the instructions below. Note that, the first video was created on OSX, but the steps are the same for Linux.

![](https://imgur.com/wsJ66Qt.png)

Download the Linux archive and extract it, while keeping the file permissions: `tar -pxzf WasabiLinux-1.1.1.tar.gz`.
You can run Wasabi by executing `./wassabee`.

After first run, a working directory will be created: `~/.walletwasabi/`. Among others, here is where your wallet files and your logs reside.

# OSX

Check out this [video guide](https://www.youtube.com/watch?v=_Zmc54XYzBA) or take a look at the instructions below:

![](https://imgur.com/k0cEYjz.png)

Download and open the `.dmg` file, then install Wasabi by dragging it into your `Applications` folder.

![](https://i.imgur.com/7UEZ8wI.png)

After opening Wasabi, you may encounter a security popup. You can bypass it in multiple ways. One way would be to keep the control key down while opening Wasabi.

![](https://imgur.com/dy1zfJG.png)

# GPG Verification

[Get GnuPG](https://www.gnupg.org/download/index.html), then save [nopara73's PGP](https://github.com/zkSNACKs/WalletWasabi/blob/master/PGP.txt).

Import the key you downloaded to GnuPG. Open the terminal/command line:

```sh
gpg --import PGP.txt
```

With this public key, from now on, you will be able to make sure the Wasabi software you download was not tampered by checking against the corresponding `.asc` file:

```sh
gpg --verify {path to downloaded signature}.asc {path to binary}
```

Example: `gpg --verify WasabiInstaller.msi.asc WasabiInstaller.msi`.
 
If the message returned says Good signature and that it was signed by `Ficsór Ádám` with a Primary key fingerprint: `21D7 CA45 565D BCCE BE45  115D B4B7 2266 C47E 075E`, then the software wasn't tampered with since the developer signed it.
 
Remember to check again the PGP signature every time you make a new download.

If you trust nopara73's key and you are faimiliar with the [Web Of Trust](https://security.stackexchange.com/questions/147447/gpg-why-is-my-trusted-key-not-certified-with-a-trusted-signature), please consider also [validating it](https://www.gnupg.org/gph/en/manual/x334.html).
