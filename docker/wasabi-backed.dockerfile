FROM mcr.microsoft.com/dotnet/sdk:5.0-buster-slim AS builder
RUN apt-get update && apt-get install git

ENV DOTNET_CLI_TELEMETRY_OPTOUT=1
WORKDIR /source

RUN git clone --depth 1 https://github.com/zkSNACKs/WalletWasabi.git ./ && \
    cd WalletWasabi.Backend && \
    dotnet restore --disable-parallel && \
    dotnet publish --no-restore --output /wasabi-build/ --configuration Release

FROM mcr.microsoft.com/dotnet/aspnet:5.0-buster-slim AS base
RUN apt-get update && rm -rf /var/lib/apt/lists/* 

EXPOSE 37127/tcp 

ENV DOTNET_CLI_TELEMETRY_OPTOUT=1
VOLUME [ "/data" ]

WORKDIR /app
COPY --from=builder "/wasabi-build" .
COPY entrypoint.sh entrypoint.sh
#ENTRYPOINT ["/app/entrypoint.sh"]
ENTRYPOINT ["bash"]
