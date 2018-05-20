# 1. Create Remote Server

## Name
WalletWasabi.Backend.[TestNet/Main]

## Image
Ubuntu 18.04 x64

## Region
Mostly anywhere is fine, except the US or China.

## Size

https://bitcoin.org/en/full-node#minimum-requirements

[4GB Standard/32GB Standard]

# 2. Setup Server

https://www.digitalocean.com/community/tutorials/initial-server-setup-with-ubuntu-18-04

## SSH in as Root

Putty (Note copypaste with Ctrl+Insert and Shift+Insert.)  
https://www.digitalocean.com/community/tutorials/how-to-use-ssh-keys-with-putty-on-digitalocean-droplets-windows-users

### Create a New User and Grant Administrative Privileges

```
adduser user
usermod -aG sudo user
```

# Setup Firewall

https://www.digitalocean.com/community/tutorials/how-to-set-up-a-firewall-with-ufw-on-ubuntu-14-04

```
ufw allow OpenSSH
ufw enable
```

> As the firewall is currently blocking all connections except for SSH, if you install and configure additional services, you will need to adjust the firewall settings to allow acceptable traffic in. You can learn some common UFW operations in this guide.
> https://www.digitalocean.com/community/tutorials/ufw-essentials-common-firewall-rules-and-commands

## Enabling External Access for User

```
rsync --archive --chown=user:user ~/.ssh /home/user
```

## Update Ubuntu

```
sudo apt-get update && sudo apt-get dist-upgrade -y
```

# 3. Install .NET SDK

https://www.microsoft.com/net/learn/get-started/linux/ubuntu18-04

# 4. Install Tor

```
sudo apt-get install tor
```

Check if Tor is already running in the background:

```
sudo netstat -plnt | fgrep 9050
```

If yes, kill it:

```
sudo killall tor
```

Verify Tor is properly running:
```
tor
```

# 5. Publish

## [Remote Machine]

```
git clone https://github.com/zkSNACKs/WalletWasabi.git
cd WalletWasabi
dotnet restore
dotnet build
dotnet publish WalletWasabi.Backend --configuration Release --self-contained false
```
