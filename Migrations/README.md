# Migrations

No `.cs` migration files are checked in - generate them yourself against your actual PostgreSQL
connection, either from Visual Studio's Package Manager Console or the `dotnet-ef` CLI.

## Option A: Visual Studio Package Manager Console

Tools -> NuGet Package Manager -> Package Manager Console, then:

```powershell
Add-Migration InitialCreate
Update-Database
```

## Option B: dotnet-ef CLI

From `src/CvManagementSystem`:

```bash
dotnet tool install --global dotnet-ef   # if you don't have it yet
dotnet ef migrations add InitialCreate
dotnet ef database update
```

## Then: full-text search indexes

```bash
psql -h localhost -U postgres -d cvmanagement -f Migrations/AddFullTextIndexes.sql
```

Or paste the file's contents into pgAdmin's Query Tool against the `cvmanagement` database.

## Local install vs Render

For local development, install PostgreSQL (with pgAdmin) and point `ConnectionStrings:Default`
in `appsettings.json` at it. For deployment, create a free PostgreSQL instance on Render and
point the *deployed* app's connection string (an environment variable / Render's own
`appsettings` override) at Render's connection string instead - you do not need to migrate your
local database to Render manually; just run `Update-Database` once against the Render
connection string too (or let Render's environment run the migration on first deploy).
