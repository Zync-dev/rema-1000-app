# Rema Butiksværktøjer

Web-app med værktøjer til den daglige drift i en Rema 1000-butik. Hver butik har
sin egen konto (multi-tenant); data er adskilt pr. butik.

## Status

| Funktion | Tilstand |
|----------|----------|
| Login / opret butik / roller (Købmand, Souschef, Medarbejder) | ✅ |
| Avancekalkulator (DB / dækningsgrad, moms, pant, gem beregninger) | ✅ |
| Gulvplan (flere planer pr. butik, træk/skalér placeringer, ugens vare pr. boks, print) | ✅ |
| AI Facebook-opslag (stilprofil pr. butik, gratis Gemini-nøgle, tilbud/konkurrence/event) | ✅ |
| AI vagtplan | senere |

## Teknologi

- ASP.NET Core Razor Pages (.NET 10)
- PostgreSQL via EF Core (Npgsql)
- ASP.NET Core Identity (cookie-login, kontolåsning, Data Protection-nøgler i databasen)
- Multi-tenancy: globalt EF-query-filter på `StoreId`, sat ud fra brugerens claims

## Projektstruktur

```
src/Rema.App          Web (Razor Pages, sider, wwwroot)
src/Rema.App.Core      Domænelogik uden afhængigheder (DB/DG-beregning)
src/Rema.App.Data      EF Core: DbContext, entiteter, migrationer, tenancy
tests/Rema.App.Tests   xUnit (beregning + tenant-isolation)
```

## Kør lokalt

1. **Database.** Enten via Docker:

   ```bash
   docker compose up -d
   ```

   …eller en gratis Neon-database (https://neon.tech, vælg region Frankfurt).

2. **Forbindelsesstreng** i user-secrets (aldrig i git):

   ```bash
   cd src/Rema.App
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=rema_app;Username=rema;Password=rema"
   ```

   En `postgres://…`-URL (som Neon og Railway udleverer) virker også direkte.

3. **Kør.** Migrationer køres automatisk ved opstart.

   ```bash
   dotnet run --project src/Rema.App
   ```

   Åbn den viste URL, vælg **Opret butik**, og du er logget ind som købmand.

## AI Facebook-opslag

Hver butik indtaster sin **egen Google Gemini API-nøgle** under `/facebook/indstillinger`
(kun købmand/souschef). Nøglen oprettes gratis på aistudio.google.com/apikey (kun en
Google-konto, intet betalingskort) og gemmes krypteret med Data Protection – aldrig i
klartekst, aldrig vist igen. Stilprofilen (tone, emoji, afslutning, hashtags, adresse,
åbningstider, op til 3 eksempel-opslag) låser tonen, så alle opslag ligner hinanden.
Konkurrence-opslag får automatisk Facebook-disclaimer + de betingelser butikken har angivet.

Model vælges pr. butik som **fritekst** (standard `gemini-3.7-flash`, forslag i en
datalist) – så et nyt modelnavn kan skrives ind uden kodeændring når Google skifter
dem ud. Kun mønsteret `gemini-…` valideres. Kaldet går mod Geminis
`v1beta/models/{model}:generateContent` via `HttpClient`, nøgle i
`x-goog-api-key`-header (aldrig i URL). `generateContent` er fortsat fuldt understøttet;
Googles nyere "Interactions API" er ikke taget i brug endnu. Provideren sidder bag
`IFacebookPostGenerator` – en anden model/udbyder kan sættes ind uden at røre resten.

## Migrationer

```bash
dotnet ef migrations add <Navn> --project src/Rema.App.Data --startup-project src/Rema.App
dotnet ef database update            --project src/Rema.App.Data --startup-project src/Rema.App
```

## Test

```bash
dotnet test
```

## Deploy på Railway

Repoet er klar til Railway: `Dockerfile` (multi-stage, non-root) + `railway.json`
(Dockerfile-builder, healthcheck på `/healthz`, 1 replica).

1. **Push repoet til GitHub.**
2. Railway → **New Project → Deploy from GitHub repo** → vælg repoet.
   Railway læser `railway.json` og bygger via `Dockerfile`.
3. **Database** – vælg én:
   - **Behold Neon:** i app-servicens **Variables**, tilføj
     `DATABASE_URL` = din Neon-forbindelsesstreng (`postgresql://…`).
   - **Railway Postgres:** tilføj en **PostgreSQL**-service i projektet, og sæt så
     på app-servicen `DATABASE_URL` = `${{Postgres.DATABASE_URL}}` (reference-variabel).
   Appen accepterer også `ConnectionStrings__DefaultConnection` i stedet.
4. **Domæne:** app-servicen → **Settings → Networking → Generate Domain**.
5. Første deploy kører migrationerne automatisk ved opstart. Færdig.

Ingen andre variabler er nødvendige – `PORT` sættes af Railway, `ASPNETCORE_ENVIRONMENT=Production`
sættes i `Dockerfile`, og appen kører bag Railways TLS-proxy (`UseForwardedHeaders`,
`Secure`-cookies). Data Protection-nøglerne ligger i databasen, så et redeploy
logger ikke nogen ud.

**Behold 1 replica** (migrationer kører ved opstart og bør ikke race). Skal der
skaleres, så flyt migrationer til et separat trin.

### GDPR / drift

- Vælg en **EU-region** for både app og database (løsningen indeholder medarbejderoplysninger).
- Erstat `src/Rema.App/wwwroot/img/rema-logo.svg` med det officielle Rema 1000-logo.

## Kør containeren lokalt (valgfrit)

```bash
docker build -t rema-app .
docker run -p 8080:8080 -e DATABASE_URL="postgresql://…" rema-app
```
