FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1
WORKDIR /src
COPY ["WalletWasabi.Indexer/WalletWasabi.Indexer.csproj", "WalletWasabi.Indexer/"]
COPY ["WalletWasabi/WalletWasabi.csproj", "WalletWasabi/"]
RUN dotnet restore "WalletWasabi.Indexer/WalletWasabi.Indexer.csproj"
COPY . .
WORKDIR "/src/WalletWasabi.Indexer"
RUN dotnet build "WalletWasabi.Indexer.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "WalletWasabi.Indexer.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "WalletWasabi.Indexer.dll"]
