#!/usr/bin/env bash

function config_extract() {
  jq -r "$1" ~/.walletwasabi/client/Config.json
}

CREDENTIALS=$(config_extract '.JsonRpcUser + ":" + .JsonRpcPassword')
ENDPOINT=$(config_extract '.JsonRpcServerPrefixes[0]')
BASIC_AUTH=$([ "$CREDENTIALS" == ":" ] && echo "" || echo "--user ${CREDENTIALS}")

WALLETNAME=""

if [ $# -ge 1 ]; then
    ARG="${1%=*}"
    if [[ "$ARG" == "-wallet" ]]; then
        WALLETNAME="${1#*=}/"
        shift
    fi
fi

METHOD=$1
shift

if [ $# -ge 1 ]; then
    if [[ "$1" ]]; then
        PARAMS="\"$1\""
        shift
    else
        PARAMS="\"\""
        shift
    fi

    while (( "$#" )); do
        if [[ "$1" ]]; then
            PARAMS="$PARAMS, $1"
        fi
        shift
    done
fi

REQUEST="{\"jsonrpc\":\"2.0\", \"id\":\"curltext\", \"method\":\"$METHOD\", \"params\":[$PARAMS]}"
RESULT=$(curl -s $BASIC_AUTH --data-binary "$REQUEST" -H -- "content-type: text/plain;" "$ENDPOINT$WALLETNAME")
CURL_ERRORCODE=$?
RESULT_ERROR=$(echo "$RESULT" | jq -r .error)
CURL_FAIL_TO_CONNECT_ERRORCODE=7

rawprint=(help)
if [ $CURL_ERRORCODE -eq $CURL_FAIL_TO_CONNECT_ERRORCODE ]; then
    echo "It was not possible to get a response. RPC server could be disabled."
elif [[ "$RESULT_ERROR" == "null" ]]; then
    if [[ " ${rawprint[*]} " =~ ${METHOD} || ${METHOD} == 'query' ]]; then
       echo "$RESULT" | jq -r .result
    else
        IS_NONEMPTY_ARRAY=$(echo "$RESULT" | jq -r '.result | if type=="array" and length > 0 then "true" else "false" end')
        if [[ "$IS_NONEMPTY_ARRAY" == "true" ]]; then
           echo "$RESULT" | jq -r '.result | [.[]| with_entries( .key |= ascii_downcase ) ]
                                         |    (.[0] |keys_unsorted | @tsv)
                                            , (.[]|.|map(.) |@tsv)' | column -t
        else
           echo "$RESULT" | jq -r .result
        fi
    fi
else
   echo "$RESULT_ERROR" | jq -r .message
fi
