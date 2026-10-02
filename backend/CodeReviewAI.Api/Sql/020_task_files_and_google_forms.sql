-- Task Configuration Phase 2: fills in the Google Forms (link + mandatory multi-file survey-
-- results upload) and Generic (instructions + the same multi-file upload) layouts.
--
-- Idempotent — safe to re-run (guarded column adds, CREATE TABLE IF NOT EXISTS). Run manually
-- against Neon (psql "$DATABASE_URL" -f Sql/020_task_files_and_google_forms.sql, or paste into
-- the Neon SQL console). Run after 019_task_type.sql.

DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Research' AND column_name = 'GoogleFormsUrl') THEN
    ALTER TABLE "Research" ADD COLUMN "GoogleFormsUrl" TEXT;
  END IF;
  IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Research' AND column_name = 'TaskInstructions') THEN
    ALTER TABLE "Research" ADD COLUMN "TaskInstructions" TEXT;
  END IF;
END $$;

-- Multiple files per research, no uniqueness constraint ("dodaje više fajlova"). BYTEA (not
-- TEXT like EegRecording's RawCsv) since survey exports/attachments can be binary (XLSX, PDF).
CREATE TABLE IF NOT EXISTS "TaskFile" (
  "Id"               SERIAL PRIMARY KEY,
  "ResearchId"       INTEGER NOT NULL REFERENCES "Research"("Id") ON DELETE CASCADE,
  "OriginalFilename" VARCHAR(255) NOT NULL,
  "ContentType"      VARCHAR(100),
  "FileContent"      BYTEA NOT NULL,
  "FileSizeBytes"    INTEGER NOT NULL,
  "UploadedAt"       TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS "IX_TaskFile_ResearchId" ON "TaskFile" ("ResearchId");
