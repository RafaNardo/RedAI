FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY . .
RUN dotnet publish src/RedAI.Api/RedAI.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
RUN apt-get update \
    && apt-get install -y --no-install-recommends wget ca-certificates gnupg \
    && wget -q https://packages.microsoft.com/config/ubuntu/24.04/packages-microsoft-prod.deb -O /tmp/packages-microsoft-prod.deb \
    && dpkg -i /tmp/packages-microsoft-prod.deb \
    && apt-get update \
    && apt-get install -y --no-install-recommends powershell \
    && pwsh ./playwright.ps1 install --with-deps chromium \
    && rm -rf /var/lib/apt/lists/* /tmp/packages-microsoft-prod.deb
ENTRYPOINT ["dotnet", "RedAI.Api.dll"]
