FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1
WORKDIR /src
COPY ["Directory.Build.props", "./"]
COPY ["Directory.Packages.props", "./"]
COPY ["BannedSymbols.txt", "./"]
COPY ["WalletWasabi.Backend/WalletWasabi.Backend.csproj", "WalletWasabi.Backend/"]
COPY ["WalletWasabi/WalletWasabi.csproj", "WalletWasabi/"]
RUN dotnet restore "WalletWasabi.Backend/WalletWasabi.Backend.csproj"
COPY . .
WORKDIR "/src/WalletWasabi.Backend"
RUN dotnet build "WalletWasabi.Backend.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "WalletWasabi.Backend.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "WalletWasabi.Backend.dll"]
