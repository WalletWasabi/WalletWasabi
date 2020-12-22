FROM amd64/debian:buster-slim

RUN apt-get update && apt-get install -y git \
    build-essential libtool autotools-dev automake pkg-config bsdmainutils python3 libevent-dev libboost-system-dev \
    libboost-filesystem-dev libboost-test-dev libboost-thread-dev
RUN git clone --branch 0.20.x-knots --single-branch --depth 1 https://github.com/bitcoinknots/bitcoin.git && \
    cd bitcoin && \
    ./autogen.sh && \
    ./configure \
    --without-gui \
    --disable-wallet \
    --without-miniupnpc && \
    make && make install && \
    cd .. && \
    rm -rf bitcoin

#VOLUME ["/home/bitcoin/.bitcoin"]

EXPOSE 8332 8333 18332 18333 18443 18444

ENTRYPOINT ["/entrypoint.sh"]

CMD ["bitcoind"]


