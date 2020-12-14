FROM amd64/debian:buster-slim

RUN apt-get update && apt-get install -y git libevent-dev gnupg gcc make automake ca-certificates autoconf musl-dev coreutils libffi-dev zlib1g-dev
RUN apt-get install libssl-dev
RUN mkdir -p /usr/local/src/ /var/lib/tor/ && \
    git clone --branch tor-0.4.4.6 --single-branch --depth 1 https://git.torproject.org/tor.git /usr/local/src/tor && \
    cd /usr/local/src/tor && \
    ./autogen.sh && \
    ./configure \
    --disable-asciidoc \
    --sysconfdir=/etc \
    --disable-unittests && \
    make && make install && \
    cd .. && \
    rm -rf tor

ENTRYPOINT ["tor"]


