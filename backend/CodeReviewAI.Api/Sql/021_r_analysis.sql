-- Task Configuration Phase 3: sandboxed R script execution for the Google Forms task type's
-- opt-in "R analysis" step. A researcher uploads one .R script per research (most-recent-wins,
-- like EegRecording's overwrite pattern); each "Run" creates an AnalysisRun row and is executed
-- in a sandboxed Docker container by server/analysis/runner.mjs (--network none, resource/time
-- limits) — see docker/r-runner/ and docs/task-r-analysis-setup.md.
--
-- Idempotent — safe to re-run (CREATE TABLE IF NOT EXISTS). Run manually against Neon (psql
-- "$DATABASE_URL" -f Sql/021_r_analysis.sql, or paste into the Neon SQL console). Run after
-- 020_task_files_and_google_forms.sql.

CREATE TABLE IF NOT EXISTS "AnalysisScript" (
  "Id"               SERIAL PRIMARY KEY,
  "ResearchId"       INTEGER NOT NULL REFERENCES "Research"("Id") ON DELETE CASCADE,
  "OriginalFilename" VARCHAR(255) NOT NULL,
  "ScriptContent"    TEXT NOT NULL,
  "UploadedAt"       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  CONSTRAINT "AnalysisScript_Research_UQ" UNIQUE ("ResearchId")
);

CREATE TABLE IF NOT EXISTS "AnalysisRun" (
  "Id"         SERIAL PRIMARY KEY,
  "ResearchId" INTEGER NOT NULL REFERENCES "Research"("Id") ON DELETE CASCADE,
  "ScriptId"   INTEGER NOT NULL REFERENCES "AnalysisScript"("Id") ON DELETE CASCADE,
  "Status"     VARCHAR(20) NOT NULL DEFAULT 'PENDING', -- PENDING/RUNNING/SUCCESS/FAILED/TIMEOUT
  "StartedAt"  TIMESTAMPTZ,
  "FinishedAt" TIMESTAMPTZ,
  "StdOut"     TEXT,
  "StdErr"     TEXT,
  "ResultsText" TEXT,
  "CreatedAt"  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS "IX_AnalysisRun_ResearchId" ON "AnalysisRun" ("ResearchId", "CreatedAt" DESC);

CREATE TABLE IF NOT EXISTS "AnalysisRunPlot" (
  "Id"          SERIAL PRIMARY KEY,
  "RunId"       INTEGER NOT NULL REFERENCES "AnalysisRun"("Id") ON DELETE CASCADE,
  "Filename"    VARCHAR(255) NOT NULL,
  "ImageData"   BYTEA NOT NULL,
  "SortOrder"   INTEGER NOT NULL DEFAULT 0
);

CREATE INDEX IF NOT EXISTS "IX_AnalysisRunPlot_RunId" ON "AnalysisRunPlot" ("RunId", "SortOrder");
