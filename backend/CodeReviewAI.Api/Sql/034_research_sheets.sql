-- Excel-like tables for a research ("sheets" app, 2026-10-27). Additive and idempotent: nothing existing
-- changes, and every research keeps behaving as before until UsesSheets is switched on for it.
--
--   Research.UsesSheets   opt-in switch (admin Configuration); default FALSE
--   SheetWorkbook         ONE template per research: Univer layout snapshot (merges, widths, header text)
--                         + Rules (per column options / required, per cell exceptions) — Rules is the
--                         single source of truth the server validates against
--   SheetEntry            one filled copy per participant: sparse values + DRAFT/SUBMITTED
--   SheetAccessLink       magic link per participant (own table, SurveyAccessToken's CHECK untouched)

ALTER TABLE "Research" ADD COLUMN IF NOT EXISTS "UsesSheets" BOOLEAN NOT NULL DEFAULT FALSE;

CREATE TABLE IF NOT EXISTS "SheetWorkbook" (
  "Id"          SERIAL PRIMARY KEY,
  "ResearchId"  INTEGER NOT NULL UNIQUE REFERENCES "Research"("Id") ON DELETE CASCADE,
  "Title"       VARCHAR(200) NOT NULL DEFAULT '',
  "Snapshot"    JSONB NOT NULL,
  "Rules"       JSONB NOT NULL,
  "Version"     INTEGER NOT NULL DEFAULT 1,
  "UpdatedBy"   INTEGER REFERENCES "Researcher"("Id") ON DELETE SET NULL,
  "UpdatedAt"   TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS "SheetEntry" (
  "Id"              SERIAL PRIMARY KEY,
  "WorkbookId"      INTEGER NOT NULL REFERENCES "SheetWorkbook"("Id") ON DELETE CASCADE,
  "ParticipantGuid" UUID NOT NULL REFERENCES "Participant"("Guid") ON DELETE CASCADE,
  "Rows"            JSONB NOT NULL DEFAULT '{}'::jsonb,
  "Status"          VARCHAR(12) NOT NULL DEFAULT 'DRAFT' CHECK ("Status" IN ('DRAFT', 'SUBMITTED')),
  "UpdatedAt"       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  "SubmittedAt"     TIMESTAMPTZ,
  UNIQUE ("WorkbookId", "ParticipantGuid")
);

CREATE TABLE IF NOT EXISTS "SheetAccessLink" (
  "Id"              SERIAL PRIMARY KEY,
  "Token"           VARCHAR(64) NOT NULL UNIQUE,
  "WorkbookId"      INTEGER NOT NULL REFERENCES "SheetWorkbook"("Id") ON DELETE CASCADE,
  "ParticipantGuid" UUID NOT NULL REFERENCES "Participant"("Guid") ON DELETE CASCADE,
  "ExpiresAt"       TIMESTAMPTZ NOT NULL,
  "CreatedAt"       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  UNIQUE ("WorkbookId", "ParticipantGuid")
);
