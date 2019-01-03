|          | Website                                                                |
|----------|------------------------------------------------------------------------|
| Clearnet | https://wasabiwallet.io/                                               |
| Tor      | http://wasabiukrxmkdgve5kynjztuovbg43uxcbcxn6y2okcrsg7gb6jdmbad.onion/ |

# Windows

Check out this [video guide](https://www.youtube.com/watch?v=tkaaC8yET1o) or take a look at the instructions below:

![](https://imgur.com/K2J1WWG.png)

Download the Windows installer (.msi) and follow the instructions.
Wasabi will be installed to your `C:\Program Files\WasabiWallet\` folder. You will also have an icon in your Start Menu and on your Desktop.  

After first run, a working directory will be created: `%appdata%\WalletWasabi\`. Amongst others, here is where your wallet files and your logs reside.

# Linux

Check out this short, to-the-point [video guide](https://www.youtube.com/watch?v=qFbv_b-bju4), or [this other funnier one](https://www.youtube.com/watch?time_continue=4&v=zPKpC9cRcZo) with more explanation, or take a look at the instructions below. Note that, the first video was created on OSX, but the steps are the same for Linux.

![](https://imgur.com/wsJ66Qt.png)

Download the Linux archive and extract it, while keeping the file permissions: `tar -pxzf WasabiLinux-1.0.5.tar.gz`.
You can run Wasabi by executing `./wassabee`.

After first run, a working directory will be created: `~/.walletwasabi/`. Amongst others, here is where your wallet files and your logs reside.

# OSX

Check out this [video guide](https://www.youtube.com/watch?v=_Zmc54XYzBA) or take a look at the instructions below:

![](https://imgur.com/k0cEYjz.png)

Download and open the `.dmg` file, then install Wasabi by dragging it into your `Applications` folder.

![](https://i.imgur.com/7UEZ8wI.png)

After opening Wasabi, you may encounter a security popup. You can bypass it in multiple ways. One way would be to keep the control key down while opening Wasabi.

![](https://imgur.com/dy1zfJG.png)

# GPG Verification

First, you need nopara73 fingerprint. 

Open your terminal and type:

gpg --recv-keys 21D7CA45565DBCCEBE45115DB4B72266C47E075E
 
Or save from: https://github.com/zkSNACKs/WalletWasabi/blob/master/PGP.txt as nopara73.asc

Go back to Wasabi website (https://www.wasabiwallet.io/) and download WasabiLinux-X.X.X.tar.gz and its signature WasabiLinux-X.X.X.tar.gz.asc
 
Copy all the 3 files to the same folder, open the terminal and use command 'cd' to navigate to that folder or right click on the folder and select "Open in Terminal" and run these commands.
 
gpg --import nopara73.asc
  
gpg --verify WasabiLinux-X.X.X.tar.gz.asc WasabiLinux-X.X.X.tar.gz
 
If the message returned says Good signature and that it was signed by Ficsór Ádám with a Primary key fingerprint: 21D7 CA45 565D BCCE BE45  115D B4B7 2266 C47E 075E, then the software is authentic.
 
Remember to check again the pgp signature every time you make a new download.