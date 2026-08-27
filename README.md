# Rema Butiksværktøjer

Web-app med værktøjer til den daglige drift i en Rema 1000-butik. Hver butik har
sin egen konto (multi-tenant); data er adskilt pr. butik.

## Status

| Funktion | Tilstand |
|----------|----------|
| Login / opret butik / roller (Købmand, Souschef, Medarbejder) | ✅ |
| Avancekalkulator (DB / dækningsgrad, moms, pant, gem beregninger) | ✅ |
| Gulvplan (flere planer pr. butik, træk/skalér placeringer, ugens vare pr. boks, print) | ✅ |
| AI Facebook-opslag | planlagt |
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

   En `postgres://…`-URL (som Neon/Fly udleverer) virker også direkte.

3. **Kør.** Migrationer køres automatisk ved opstart.

   ```bash
   dotnet run --project src/Rema.App
   ```

   Åbn den viste URL, vælg **Opret butik**, og du er logget ind som købmand.

## Migrationer

```bash
dotnet ef migrations add <Navn> --project src/Rema.App.Data --startup-project src/Rema.App
dotnet ef database update            --project src/Rema.App.Data --startup-project src/Rema.App
```

## Test

```bash
dotnet test
```

## Deployment (skitse)

- **Database:** Neon free tier, region Frankfurt.
- **Web:** Fly.io (`fly launch`, region `fra`) eller Azure App Service (West Europe).
  Sæt miljøvariablen `ConnectionStrings__DefaultConnection` (eller `DATABASE_URL`).
- Alt data holdes i EU (GDPR – løsningen indeholder medarbejderoplysninger).
- Erstat `src/Rema.App/wwwroot/img/rema-logo.svg` med det officielle Rema 1000-logo.
