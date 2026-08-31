# syntax=docker/dockerfile:1

# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore separat for at udnytte Docker-lag-cache.
COPY src/Rema.App/Rema.App.csproj                 src/Rema.App/
COPY src/Rema.App.Core/Rema.App.Core.csproj       src/Rema.App.Core/
COPY src/Rema.App.Data/Rema.App.Data.csproj       src/Rema.App.Data/
RUN dotnet restore src/Rema.App/Rema.App.csproj

COPY src/ src/
RUN dotnet publish src/Rema.App/Rema.App.csproj -c Release -o /app --no-restore /p:UseAppHost=false

# ---- runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# APP_UID (1654) og bruger "app" er indbygget i .NET-imaget.
ENV ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    ASPNETCORE_URLS=http://0.0.0.0:8080

COPY --from=build --chown=$APP_UID:$APP_UID /app ./
USER $APP_UID

# Railway sætter selv PORT; 8080 er kun standard hvis den mangler.
EXPOSE 8080

ENTRYPOINT ["dotnet", "Rema.App.dll"]
