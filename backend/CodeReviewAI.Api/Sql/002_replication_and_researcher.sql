-- Adds the multi-university "Replication" concept and the Admin Dashboard's "Researcher" login
-- table. Idempotent — safe to re-run. Run after 001_document_existing_study_schema.sql.
--
-- Researcher rows are NOT seeded here — there is deliberately no self-registration. Generate a
-- bcrypt hash with:
--   cd admin-dashboard-andrejkatin && node scripts/hash-password.mjs <password>
-- then insert manually, e.g.:
--   INSERT INTO "Researcher" ("Username", "PasswordHash", "IsSuperAdmin", "ReplicationId")
--   VALUES ('andrej', '<paste-hash-here>', TRUE, NULL);

CREATE TABLE IF NOT EXISTS "Replication" (
  "Id"             SERIAL PRIMARY KEY,
  "Name"           VARCHAR(150) NOT NULL,
  "Description"    TEXT,
  "University"     VARCHAR(200),
  "City"           VARCHAR(100),
  "Country"        VARCHAR(100),
  -- Per-replication demo PR config; replaces the old global Study:Pr:*/GitHub:PersonalAccessToken.
  "GitHubOwner"    VARCHAR(200),
  "GitHubRepo"     VARCHAR(200),
  "GitHubPrNumber" INTEGER,
  "GitHubToken"    TEXT,
  "CreatedAt"      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Seed exactly one test replication (per explicit instruction: no creation UI in v1, further
-- replications are inserted by hand the same way, at least until a future "manage replications"
-- admin feature is built).
INSERT INTO "Replication" ("Id", "Name", "Description", "University", "City", "Country")
VALUES (1, 'Test Replication', 'Seeded replication for local development/testing.',
        'University of Novi Sad', 'Novi Sad', 'Serbia')
ON CONFLICT ("Id") DO NOTHING;

-- Keep the SERIAL sequence consistent with the explicit Id=1 insert above.
SELECT setval(pg_get_serial_sequence('"Replication"', 'Id'), (SELECT MAX("Id") FROM "Replication"));

CREATE TABLE IF NOT EXISTS "Researcher" (
  "Id"            SERIAL PRIMARY KEY,
  "Username"      VARCHAR(100) UNIQUE NOT NULL,
  "PasswordHash"  TEXT NOT NULL,
  "IsSuperAdmin"  BOOLEAN NOT NULL DEFAULT FALSE,
  "ReplicationId" INTEGER REFERENCES "Replication"("Id"),
  "CreatedAt"     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  -- Enforces the role model at the DB level: a superadmin has no single replication (sees all);
  -- a regular researcher must be scoped to exactly one.
  CONSTRAINT "Researcher_Scope_CHK" CHECK (
    ("IsSuperAdmin" = TRUE  AND "ReplicationId" IS NULL) OR
    ("IsSuperAdmin" = FALSE AND "ReplicationId" IS NOT NULL)
  )
);
