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

# libgssapi-krb5-2: Npgsql prøver at loade GSSAPI ved forbindelse; uden den
#   spammes loggen med "Cannot load library libgssapi_krb5.so.2".
# tzdata: så "Europe/Copenhagen" kan slås op (påmindelser + "i dag" i dansk tid).
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 tzdata \
    && rm -rf /var/lib/apt/lists/*

ENV TZ=Europe/Copenhagen

ENV ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true

# APP_UID (1654) og bruger "app" er indbygget i .NET-imaget.
COPY --from=build --chown=$APP_UID:$APP_UID /app ./
USER $APP_UID

# Program.cs binder til $PORT (ellers 8080). EXPOSE er kun dokumentation.
EXPOSE 8080

ENTRYPOINT ["dotnet", "Rema.App.dll"]
