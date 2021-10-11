#!/usr/bin/env bash

key_chain="login.keychain"
key_chain_pass=""
security unlock-keychain -p "$key_chain_pass" "$key_chain"
security import macdevsign.p12 -k "$key_chain" -P "alma" -A
security set-key-partition-list -S apple-tool:,apple: -s -k "$key_chain_pass" "$key_chain"
