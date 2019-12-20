#!/bin/sh

export LANGUAGE=en_US.UTF-8
export LC_ALL=en_US.UTF-8
export LANG=en_US.UTF-8
export LC_CTYPE=en_US.UTF-8

set -e

rsa_key_file="temp.key"
csr_file_name="request.csr"
openssl req -new -key "$rsa_key_file" -out "$csr_file_name" -subj "/emailAddress=$email, CN=$common_name, C=$country_code"