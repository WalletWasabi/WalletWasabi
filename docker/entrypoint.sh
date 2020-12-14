#!/bin/bash
set -e

#if [ "$1" = '' ]; then
ln -s /root/.walletwasabi /data
/app/WalletWasabi.Backend
#fi

#exec "$@"
