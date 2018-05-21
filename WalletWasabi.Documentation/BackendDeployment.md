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

# 5. Install, Configure and Synchronize bitcoind

https://bitcoin.org/en/download

```
sudo add-apt-repository ppa:bitcoin/bitcoin
sudo apt-get update
sudo apt-get install bitcoind
mkdir ~/.bitcoin
pico ~/.bitcoin/bitcoin.conf
```

```
maxuploadtarget=144
listen=0

txindex=1

daemon=1
server=1
rpcuser=bitcoinuser
rpcpassword=password

testnet=[0/1]
```

https://medium.com/@loopring/how-to-run-lighting-btc-node-and-start-mining-b55c4bab8ad  
https://github.com/MrChrisJ/fullnode/issues/18

```
sudo ufw allow ssh
sudo ufw allow [18333/8333]
bitcoind
bitcoin-cli getblockcount
bitcoin-cli stop
bitcoind
```


# 6. Install, Configure and Run Nginx

## Nginx as Reverse Proxy

https://www.digitalocean.com/community/tutorials/how-to-install-nginx-on-ubuntu-18-04  
https://docs.microsoft.com/en-us/aspnet/core/host-and-deploy/linux-nginx?view=aspnetcore-2.0&tabs=aspnetcore2x  

> Use apt-get to install Nginx. The installer creates a System V init script that runs Nginx as daemon on system startup. Since Nginx was installed for the first time, explicitly start it by running:
```
sudo apt install nginx
sudo service nginx start
sudo pico /etc/nginx/sites-available/default
```

Comment out the current server configuration, then use this:

```
server {
    listen   80 default_server;
    # listen [::]:80 default_server deferred;
    return   444;
}

server {
    listen        127.0.0.1:8080;
    server_name   [ToDo: generate onion for testnet and mainnet and set it to be this];
    location / {
        proxy_pass         http://localhost:37127;
        proxy_http_version 1.1;
        proxy_set_header   Upgrade $http_upgrade;
        proxy_set_header   Connection keep-alive;
        proxy_set_header   Host $http_host;
        proxy_cache_bypass $http_upgrade;
    }
}
```

```
sudo nginx -t
sudo nginx -s reload
```

# Monitor the App



# 7. Publish, Configure and Run WalletWasabi.Backend

```
git clone https://github.com/zkSNACKs/WalletWasabi.git
cd WalletWasabi
dotnet restore
dotnet build
dotnet publish WalletWasabi.Backend --configuration Release --self-contained false
dotnet WalletWasabi.Backend/bin/Release/netcoreapp2.0/publish/WalletWasabi.Backend.dll
cd ..
cat .walletwasabi/backend/Logs.txt
pico .walletwasabi/backend/Config.json
pico .walletwasabi/backend/CcjRoundConfig.json
dotnet WalletWasabi/WalletWasabi.Backend/bin/Release/netcoreapp2.0/publish/WalletWasabi.Backend.dll
cat .walletwasabi/backend/Logs.txt
```
