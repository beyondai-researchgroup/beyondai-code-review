-- Documents the study-flow tables that already exist live on the shared Neon database but have
-- never had committed DDL anywhere in either repo (they were created ad-hoc via psql/the Neon
-- console during earlier work). Every statement is idempotent (CREATE TABLE IF NOT EXISTS /
-- ON CONFLICT DO NOTHING), so running this against the real, already-populated database is a
-- no-op — it only matters for provisioning a fresh environment from scratch.
--
-- Run manually against Neon (psql "$DATABASE_URL" -f Sql/001_document_existing_study_schema.sql,
-- or paste into the Neon SQL console). There is no migration runner in this codebase — run
-- 001 -> 002 -> 003 in order, once.
--
-- Assumes "Participant" already exists (defined in the NASA-TLX repo's src/db/schema.sql):
--   CREATE TABLE "Participant" ("Id" SERIAL PRIMARY KEY, "FirstName" VARCHAR(100),
--     "LastName" VARCHAR(100), "ParticipantId" VARCHAR(50) UNIQUE NOT NULL);

CREATE TABLE IF NOT EXISTS "Sessions" (
  "Id"   INTEGER PRIMARY KEY,
  "Name" TEXT NOT NULL
);

INSERT INTO "Sessions" ("Id", "Name") VALUES
  (1, 'Intro'), (2, 'AI'), (3, 'Report')
ON CONFLICT ("Id") DO NOTHING;

CREATE TABLE IF NOT EXISTS "ParticipantSession" (
  "ParticipantId" VARCHAR(50) NOT NULL REFERENCES "Participant"("ParticipantId"),
  "SessionId"     INTEGER NOT NULL REFERENCES "Sessions"("Id"),
  "IsFinished"    BOOLEAN NOT NULL DEFAULT FALSE,
  CONSTRAINT "ParticipantSession_Participant_Session_UQ" UNIQUE ("ParticipantId", "SessionId")
);

CREATE TABLE IF NOT EXISTS "ReviewDecision" (
  "ParticipantId" VARCHAR(50) NOT NULL REFERENCES "Participant"("ParticipantId"),
  "SessionId"     INTEGER NOT NULL REFERENCES "Sessions"("Id"),
  "ReviewMode"    TEXT NOT NULL,
  "Decision"      TEXT NOT NULL,
  "Comment"       TEXT,
  "DecidedAt"     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  CONSTRAINT "ReviewDecision_Participant_Session_UQ" UNIQUE ("ParticipantId", "SessionId")
);

CREATE TABLE IF NOT EXISTS "ChatMessage" (
  "Id"            SERIAL PRIMARY KEY,
  "ParticipantId" VARCHAR(50) NOT NULL REFERENCES "Participant"("ParticipantId"),
  "SessionId"     INTEGER NOT NULL REFERENCES "Sessions"("Id"),
  "Role"          TEXT NOT NULL,
  "Content"       TEXT NOT NULL,
  "CreatedAt"     TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS "IX_ChatMessage_Participant_Session"
  ON "ChatMessage" ("ParticipantId", "SessionId");
