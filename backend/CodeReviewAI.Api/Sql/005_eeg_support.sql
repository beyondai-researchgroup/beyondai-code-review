-- Adds optional EEG-device support: a per-replication config flag (does this site run an EEG
-- headset, and which one) plus a place to attach the resulting raw CSV recording to a
-- participant after the fact.
--
-- The device records once per participant, continuously across all three study sessions in a
-- single file (with manual pauses between sessions) — this is NOT session-scoped, unlike
-- "TlxResult". Same cadence class as "Rei40Result"/"BigFiveResult" (once, not per-session), just
-- raw sensor data instead of a questionnaire. Raw CSV content is stored directly in a Postgres
-- TEXT column (simplest option, no new infra); fine for typical single-participant recordings,
-- not intended for very large files.
--
-- Idempotent — safe to re-run. Run after 004_multiple_pr_configs.sql.

ALTER TABLE "Replication" ADD COLUMN IF NOT EXISTS "UsesEeg" BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE "Replication" ADD COLUMN IF NOT EXISTS "EegDeviceType" VARCHAR(200);

CREATE TABLE IF NOT EXISTS "EegRecording" (
  "Id"               SERIAL PRIMARY KEY,
  "ParticipantId"    VARCHAR(50) NOT NULL REFERENCES "Participant"("ParticipantId"),
  "OriginalFilename" VARCHAR(255) NOT NULL,
  "DeviceType"       VARCHAR(200),
  "RawCsv"           TEXT NOT NULL,
  "RowCount"         INTEGER NOT NULL,
  "Columns"          TEXT NOT NULL,  -- JSON array of CSV header column names
  "UploadedAt"       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  CONSTRAINT "EegRecording_Participant_UQ" UNIQUE ("ParticipantId")
);

CREATE INDEX IF NOT EXISTS "IX_EegRecording_ParticipantId" ON "EegRecording" ("ParticipantId");
