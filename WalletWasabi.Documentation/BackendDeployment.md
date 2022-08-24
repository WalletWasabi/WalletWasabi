# Update

Consider updating the versions in `WalletWasabi.Helpers.Constants`. If the versions are updated, make sure the Client Release is already available before updating the backend.

```sh
sudo apt-get update && cd ~/WalletWasabi && git pull && cd ~/WalletWasabi/WalletWasabi.Backend && dotnet restore && cd ~

# Check if rounds are not in critical phase

https://wasabiwallet.io/api/v4/btc/ChaumianCoinJoin/states

# Stop
sudo service nginx stop
sudo systemctl stop walletwasabi.service
sudo killall tor
bitcoin-cli stop

# Status checks
echo -n 'Tor: '; systemctl is-active tor; echo -n 'Wasabi: '; systemctl is-active walletwasabi; echo -n 'Bitcoind: '; ps -C bitcoind >/dev/null && echo "active" || echo "incative";

# Upgrade and reboot
sudo apt-get upgrade -y && sudo apt-get autoremove -y
sudo reboot
set DOTNET_CLI_TELEMETRY_OPTOUT=1

# Start Bitcoind
bitcoind
bitcoin-cli getblockchaininfo

# Start Tor
tor

# Start Nginx
sudo service nginx start

# Start Wasabi
rm -rf WalletWasabi/WalletWasabi.Backend/bin && dotnet publish ~/WalletWasabi/WalletWasabi.Backend --configuration Release --self-contained false
sudo systemctl start walletwasabi.service
echo -n 'Tor: '; systemctl is-active tor; echo -n 'Wasabi: '; systemctl is-active walletwasabi; echo -n 'Bitcoind: '; ps -C bitcoind >/dev/null && echo "active" || echo "incative";
tail -200 /home/user/.walletwasabi/backend/Logs.txt

# Advanced status checks
systemctl status nginx
systemctl status walletwasabi
systemctl status tor
pgrep -ilfa bitcoin

```

# 1. Create Remote Server

## Name
WalletWasabi.Backend.[TestNet/Main]

## Image
Ubuntu 20.04 x64

## Region
Mostly anywhere is fine, except the US or China.

## Size

https://bitcoin.org/en/full-node#minimum-requirements

[4GB Standard/32GB Standard]

# 2. Setup Server

https://www.digitalocean.com/community/tutorials/initial-server-setup-with-ubuntu-20-04

## SSH in as Root

Putty (Copypaste with Ctrl+Insert and Shift+Insert)
https://www.digitalocean.com/community/tutorials/how-to-use-ssh-keys-with-putty-on-digitalocean-droplets-windows-users

Make sure the new user's SSH pubkey added to ~/.ssh/authorized_keys on the server as well.

### Create a New User and Grant Administrative Privileges

```sh
adduser user
usermod -aG sudo user
```

### Increase the number of files limit

By default a process can keep open up to 4096 files. Increase that limit for the `user` user as follows:

```sh
pico /etc/security/limits.conf
```

```
# Wasabi backend
# Wasabi runs with the user called user
user    soft nofile 16384
user    hard nofile 16384
# End of Wasabi backend
```

# Setup Firewall

https://www.digitalocean.com/community/tutorials/how-to-set-up-a-firewall-with-ufw-on-ubuntu-14-04

```sh
ufw allow OpenSSH
ufw enable
```

> As the firewall is currently blocking all connections except for SSH, if you install and configure additional services, you will need to adjust the firewall settings to allow acceptable traffic in. You can learn some common UFW operations in this guide.
> https://www.digitalocean.com/community/tutorials/ufw-essentials-common-firewall-rules-and-commands

## Enable External Access for User

```sh
rsync --archive --chown=user:user ~/.ssh /home/user
```

## Update Ubuntu

```sh
sudo apt-get update && sudo apt-get dist-upgrade -y
```

# 3. Install .NET SDK

https://docs.microsoft.com/en-us/dotnet/core/install/linux-ubuntu

Opt out of the telemetry:

```sh
export DOTNET_CLI_TELEMETRY_OPTOUT=1
```

# 4. Install Tor

```sh
sudo apt-get install tor
pgrep -ilfa tor
sudo killall tor
```

### Update Tor

```sh
sudo apt update
apt list --upgradable | grep tor
sudo apt install --only-upgrade tor
```


Create torrc:

```sh
mkdir ~/.walletwasabi
sudo pico /etc/tor/torrc
```

```sh
Log notice file /home/user/.walletwasabi/notices.log

HiddenServiceDir /home/user/.hidden_service_v3
HiddenServiceVersion 3
HiddenServicePort 80 127.0.0.1:37127

RunAsDaemon 1

# ---MAKE TOR FASTER---

# Need to disable for HiddenServiceNonAnonymousMode
SOCKSPort 0
# Need to enable for HiddenServiceSingleHopMode
HiddenServiceNonAnonymousMode 1
# This option makes every hidden service instance hosted by a tor
# instance a Single Onion Service. One-hop circuits make Single Onion
# servers easily locatable, but clients remain location-anonymous.
HiddenServiceSingleHopMode 1
```

Enable firewall:
```sh
sudo ufw allow 80
```

Start Tor and verify it is properly running:
```sh
tor
pgrep -ilfa tor
```

**Backup the generated private key!**

# 5. Install, Configure and Synchronize bitcoind (Bitcoin Knots)

https://bitcoinknots.org/

```sh
sudo add-apt-repository ppa:luke-jr/bitcoinknots
sudo apt-get update
sudo apt-get install bitcoind
mkdir ~/.bitcoin
pico ~/.bitcoin/bitcoin.conf
```

```sh
testnet=[0/1]

[main/test].rpcworkqueue=256
[main/test].rpcthreads=8

[main/test].txindex=1

[main/test].daemon=1
[main/test].server=1
[main/test].rpcuser=bitcoinuser
[main/test].rpcpassword=password
[main/test].whitebind=127.0.0.1:[8333/18333]
[main/test].mempoolreplacement=fee,optin # Only valid for Bitcoin Knots - https://github.com/MetacoSA/NBitcoin/pull/884#issuecomment-663620290
#[main/test].debug=rpc     # in some cases it could be good to uncomment this line.
```
https://bitcoincore.org/en/releases/0.17.0/
https://medium.com/@loopring/how-to-run-lighting-btc-node-and-start-mining-b55c4bab8ad
https://github.com/MrChrisJ/fullnode/issues/18

```sh
sudo ufw allow ssh
sudo ufw allow [8333/18333]
bitcoind
bitcoin-cli getblockcount
bitcoin-cli stop
bitcoind
```

### Upgrade Knots

```sh
bitcoin-cli stop
sudo apt-get update && sudo apt install --only-upgrade bitcoind
bitcoind --version
bitcoin-cli getblockchaininfo
```

# 6. Publish, Configure and Run WalletWasabi.Backend

```sh
git clone https://github.com/zkSNACKs/WalletWasabi.git
cd WalletWasabi
dotnet restore
dotnet build
dotnet publish WalletWasabi.Backend --configuration Release --self-contained false
cd ..
```

https://docs.microsoft.com/en-us/aspnet/core/host-and-deploy/linux-nginx?view=aspnetcore-2.0&tabs=aspnetcore2x

```sh
sudo pico /etc/systemd/system/walletwasabi.service
```

```sh
[Unit]
Description=WalletWasabi Backend API

[Service]
WorkingDirectory=/home/user/WalletWasabi/WalletWasabi.Backend/bin/Release/net6.0/publish
ExecStart=/usr/bin/dotnet /home/user/WalletWasabi/WalletWasabi.Backend/bin/Release/net6.0/publish/WalletWasabi.Backend.dll
Restart=always
RestartSec=10  # Restart service after 10 seconds if dotnet service crashes
SyslogIdentifier=walletwasabi-backend
User=user
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false

[Install]
WantedBy=multi-user.target
```

```sh
sudo systemctl enable walletwasabi.service
sudo systemctl start walletwasabi.service
systemctl status walletwasabi.service
tail -10000 .walletwasabi/backend/Logs.txt

pico .walletwasabi/backend/Config.json
pico .walletwasabi/backend/CcjRoundConfig.json
sudo systemctl start walletwasabi.service
tail -10000 .walletwasabi/backend/Logs.txt
```

## Tor

```sh
tor
pgrep -ilfa tor
```

Review the tor activity using the logs stored in the linux journal:

```sh
sudo journalctl -u tor@default
```

## Load balance and server performance

Check load avarages
```sh
uptime
```
Check the number of CPU-s
```sh
nproc
```

Load average numbers are in order according to the average time-window in the last - 1, 5, 15 minutes. Zero means no load, 1 means 100% load - however, average loads are added up among the number of CPUs. So as far as the load average is not bigger than the number of CPUs, there shouldn't be any performance issues.

For interactive monitoring you can use:
```sh
htop
```

# 8. Setup Nginx

https://docs.microsoft.com/en-us/aspnet/core/host-and-deploy/linux-nginx?view=aspnetcore-2.0&tabs=aspnetcore2x#install-nginx
Only setup Nginx if you want to expose the autogenerated website to the clearnet.

Enable firewall:
```sh
sudo ufw allow http
sudo ufw allow https
```

```sh
sudo apt-get install nginx -y
sudo service nginx start
```
Verify the browser displays the default landing page for Nginx.
The landing page is reachable at `http://<server_IP_address>/index.nginx-debian.html`

```sh
sudo pico /etc/nginx/sites-available/default
```

Fill out the first server's name with the server's IP and domain, and remove the unneeded domains and the second server. (Note that I use `wasabiwallet.co` for testnet.)

```
server {
    listen        80;
    listen        [::]:80;
    listen        443 ssl;
    listen        [::]:443 ssl;
    server_name   [InsertServerIPHere] wasabiwallet.net www.wasabiwallet.net wasabiwallet.org www.wasabiwallet.org wasabiwallet.info www.wasabiwallet.info wasabiwallet.co www.wasabiwallet.co zerolink.info www.zerolink.info hiddenwallet.org www.hiddenwallet.org;
    location / {
        sub_filter '<head>'  '<head><meta name="robots" content="noindex, nofollow" />';
        sub_filter_once on;
        proxy_pass         http://localhost:37127;
    }
}

server {
    listen        80;
    listen        [::]:80;
    listen        443 ssl;
    listen        [::]:443 ssl;
    server_name   wasabiwallet.io www.wasabiwallet.io;
    location / {
        proxy_pass         http://localhost:37127;
    }
}
```

```sh
sudo nginx -t
sudo nginx -s reload
```

Setup https, redirect to https when asks. This will modify the above config file, but oh well.

```sh
sudo certbot -d wasabiwallet.io -d www.wasabiwallet.io -d wasabiwallet.net -d www.wasabiwallet.net -d wasabiwallet.org -d www.wasabiwallet.org -d wasabiwallet.info -d www.wasabiwallet.info -d wasabiwallet.co -d www.wasabiwallet.co -d zerolink.info -d www.zerolink.info -d hiddenwallet.org -d www.hiddenwallet.org
```

certbot will not properly redirect www, so it must be setup by hand, one by one.
Duplicate all entries like this by adding a `www.`:
```
server {
    if ($host = wasabiwallet.co) {
        return 301 https://$host$request_uri;
    }
}
```

Add `add_header Strict-Transport-Security "max-age=31536000; includeSubDomains; preload" always;` and `server_tokens off;` to every HTTPS `server` block.

```sh
sudo nginx -t
sudo nginx -s reload
```

After accessing the website finalize preload in https://hstspreload.org/

# Check If Everything Works

TestNet: http://testwnp3fugjln6vh5vpj7mvq3lkqqwjj3c2aafyu7laxz42kgwh2rad.onion/swagger/
Main: http://wasabiukrxmkdgve5kynjztuovbg43uxcbcxn6y2okcrsg7gb6jdmbad.onion/swagger/
GET fees

http://www.wasabiwallet.io/

# Check Statuses

```sh
tail -f ~/.bitcoin/debug.log
tail -10000 .walletwasabi/backend/Logs.txt
du -bsh .walletwasabi/backend/IndexBuilderService/*
```
# Specify Your ExtPubKey

Take your ExtPubKey from Wasabi. Never receive money to that wallet's external keypath.

```sh
pico ~/.walletwasabi/backend/CcjRoundConfig.json
```

Add your extpub to the `CoordinatorExtPubKey`.

# Additional (optional) Settings

## Rolling Bitcoin Knots node debug logs

The following command line adds a configuration file to let logrotate service know
how to rotate the bitcoin debug logs.

```sh
sudo tee -a /etc/logrotate.d/bitcoin <<EOS
/home/user/.bitcoin/debug.log
{
        su user user
        rotate 5
        copytruncate
        daily
        missingok
        notifempty
        compress
        delaycompress
        sharedscripts
}
EOS
```

**Note:** In test server replace the first line by the following one `/home/user/.bitcoin/testnet3/debug.log`

## Welcome Banner

The following command line adds a welcome banner indicating the ssh logged user that he is in the production server.

```sh
sudo pico /etc/motd
```

```
****************************************************************************
***            Attention! Wasabi PRODUCTION server                       ***
****************************************************************************
```

## Prompt

Additionally to the welcome banner it could be good to know in which server we are all the time and to see clearly which branch is checked out, in this case update the prompt as follow:

```sh
pico ~/.bashrc
```

Replace the line:

```sh
PS1='${debian_chroot:+($debian_chroot)}\[\033[01;32m\]\u@\h\[\033[00m\]:\[\033[01;34m\]\w\[\033[00m\]\$ '
```

by this one:

```sh
PS1='${debian_chroot:+($debian_chroot)}\[\033[01;32m\]\u@\h\[\033[00m\]:(PROD):\[\033[01;34m\]\w\[\033[01;31m\]$(parse_git_branch)\[\033[00m\]\$ '
```

Additionally add the following function before:

```sh
parse_git_branch() {
 git branch 2> /dev/null | sed -e '/^[^*]/d' -e 's/* \(.*\)/(\1)/'
}
```

**Note:** In the test server replace the word **PROD** by **TEST**

## Let's Encrypt

[Let’s Encrypt](https://letsencrypt.org/about/) is a free, automated, and open certificate authority (CA), run for the public’s benefit.
It is renewed automatically by certbot which is an agent software installed on both backends. A newly created or renewed certificates are valid for 90 days and the renewal process should start automatically (`cronjob`) if the certificate will expire in less than 30 days.

You can list the certificates with:

`sudo certbot certificates`

Check all of the certificates that you’ve obtained and tries to renew any that will expire in less than 30 days (this should be automatic):

`sudo certbot renew`

!Be aware that after 5 failures you will be suspended for an hour - for debugging use `certbot renew --dry-run`.

Detailed instuctions about configuration [here](https://certbot.eff.org/lets-encrypt/ubuntubionic-nginx).

## Accessing Software Logs

You can read the log file with the `tail -1000 ~/.walletwasabi/backend/Logs.txt`. However these logs aren't kept around forever. In order to access a longer timeframe use `sudo tail -1000 /var/log/syslog | grep "walletwasabi-backend"` and `sudo tail -1000 /var/log/syslog.1 | grep "walletwasabi-backend"`.

## Setup Cloudflare Anti-DDoS for clearnet website

Properly implemented DDoS mitigation is what keeps websites online during an attack.
We use Cloudflare to mitigate a DDoS attack by detecting, responding, routing and adapting to it.

### Create a Cloudflare account

1. Visit https://dash.cloudflare.com/sign-up
2. Enter Email address and Password
3. Click `Create Account`

### Add a domain to Cloudflare

1. Log in to your Cloudflare account
2. Click on `Add site` from the top navigation bar
3. Enter your website’s root domain and then click `Add Site`.
For example, if your website is `https://www.wasabiwallet.co`, type `wasabiwallet.co`
4. Cloudflare will automatically identify your DNS records
5. Click `Next`
6. Select a plan level (the `Free` package is enough, as it contains DDoS attack mitigation and Global Content Delivery Network services)
7. Click `Confirm` in the Confirm Plan window that appears
8. Review whether all DNS records were identified in the DNS query results window
9. Click `Continue`
10. Copy the 2 Cloudflare nameservers displayed and click `Continue`

### Replace default nameservers with Cloudflare ones

1. Log in to your registrar (eg. Godaddy)
2. Make sure your registrar has disabled DNSSEC for your domain
3. Replace the current/default nameserver records in your registrar account with the information you copied from Cloudflare
4. Wait some hours (max 24) while your registrar updates your nameservers and the DNS propagates
You will receive an email when your site is active on Cloudflare

## DEBUG

- Find PID `ps aufx | grep 'WalletWasabi.Backend`
- Creating a core dump: `dotnet dump collect -p 21600`
- Cound threads for dotnet process `pstree -tpl 21600 | wc -l` usually around 109

