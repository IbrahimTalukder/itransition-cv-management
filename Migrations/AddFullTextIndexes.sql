-- Run this once after the initial `Update-Database` (or `dotnet ef database update`).
-- EF Core (via Npgsql) has no fluent API for PostgreSQL generated tsvector columns, so they're
-- added here as raw SQL rather than as EF-scanned application code - keeps search queries
-- native (no "SELECT *" + LINQ .Contains() full scans, no per-row app-level filtering).
--
-- Each column is STORED and GENERATED ALWAYS, so Postgres keeps it in sync automatically on
-- every INSERT/UPDATE - no application code has to remember to update it.

ALTER TABLE "Positions"
  ADD COLUMN "SearchVector" tsvector
  GENERATED ALWAYS AS (
    to_tsvector('english', coalesce("Title", '') || ' ' || coalesce("ShortDescription", ''))
  ) STORED;

CREATE INDEX IF NOT EXISTS "IX_Positions_SearchVector" ON "Positions" USING GIN ("SearchVector");

-- Candidate name search backing the Recruiter/Admin "search CVs" flow.
ALTER TABLE "AspNetUsers"
  ADD COLUMN "SearchVector" tsvector
  GENERATED ALWAYS AS (
    to_tsvector('english', coalesce("FirstName", '') || ' ' || coalesce("LastName", '') || ' ' || coalesce("Location", ''))
  ) STORED;

CREATE INDEX IF NOT EXISTS "IX_AspNetUsers_SearchVector" ON "AspNetUsers" USING GIN ("SearchVector");

-- Free-text attribute values (String/Text types) so filled-in CV content is searchable too.
ALTER TABLE "UserAttributeValues"
  ADD COLUMN "SearchVector" tsvector
  GENERATED ALWAYS AS (
    to_tsvector('english', coalesce("StringValue", '') || ' ' || coalesce("TextValue", ''))
  ) STORED;

CREATE INDEX IF NOT EXISTS "IX_UserAttributeValues_SearchVector" ON "UserAttributeValues" USING GIN ("SearchVector");
