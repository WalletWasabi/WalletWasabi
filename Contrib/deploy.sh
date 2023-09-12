set -e

SERVICE="walletwasabi.service"

# Restarting WalletWasabi service....
sudo systemctl restart $SERVICE
echo "[OK] WalletWasabi service was restarted"

# Checking deployment...
sleep 1
systemctl status $SERVICE --no-pager
WASABI_SERVICE_STATUS="$(systemctl is-active $SERVICE)"
if [ "${WASABI_SERVICE_STATUS}" = "active" ]; then
   echo "$SERVICE is running"
else
   echo "$SERVICE is NOT running"
fi
