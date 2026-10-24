FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore first for layer caching.
COPY global.json ./
COPY src/MapleRemit.Api/MapleRemit.Api.csproj src/MapleRemit.Api/
RUN dotnet restore src/MapleRemit.Api/MapleRemit.Api.csproj

COPY src/ src/
RUN dotnet publish src/MapleRemit.Api/MapleRemit.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime
WORKDIR /app
COPY --from=build /app .

# Non-root user from the base image.
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "MapleRemit.Api.dll"]
