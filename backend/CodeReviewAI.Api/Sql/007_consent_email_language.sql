-- Phase A of the consent-gate + emailed-magic-links feature (see project plan/CLAUDE.md).
-- Additive only: new nullable/defaulted columns + a new table. Nothing reads these yet — later
-- phases (Consent app, magic-link resolution, BeyondAI consent gate, Admin study-config page)
-- wire up the actual behavior.

-- Set once at the participant's very first login (the new Consent app) and never changed again;
-- propagates the language choice to every other app instead of each app picking its own.
ALTER TABLE "Participant" ADD COLUMN IF NOT EXISTS "Email" VARCHAR(255);
ALTER TABLE "Participant" ADD COLUMN IF NOT EXISTS "Language" VARCHAR(2);
ALTER TABLE "Participant" ADD COLUMN IF NOT EXISTS "ConsentGivenAt" TIMESTAMPTZ;

-- One row per (ParticipantId, SurveyType); regenerating a link overwrites Token/ExpiresAt in
-- place rather than inserting a new row, so the old link value stops resolving immediately.
-- A token is valid while NOW() < ExpiresAt AND no matching Rei40Result/BigFiveResult row exists
-- yet for that participant (those tables' own upsert-on-ParticipantId behavior is the completion
-- signal — no separate "consumed" flag needed here).
CREATE TABLE IF NOT EXISTS "SurveyAccessToken" (
  "Id"            SERIAL PRIMARY KEY,
  "ParticipantId" VARCHAR(50) NOT NULL REFERENCES "Participant"("ParticipantId"),
  "SurveyType"    VARCHAR(20) NOT NULL CHECK ("SurveyType" IN ('REI40','BIGFIVE')),
  "Token"         VARCHAR(64) NOT NULL UNIQUE,
  "ExpiresAt"     TIMESTAMPTZ NOT NULL,
  "CreatedAt"     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  CONSTRAINT "SurveyAccessToken_Participant_Survey_UQ" UNIQUE ("ParticipantId","SurveyType")
);
CREATE INDEX IF NOT EXISTS "IX_SurveyAccessToken_Token" ON "SurveyAccessToken" ("Token");

-- Per-replication instrument configuration, consolidated later into Admin Dashboard's new
-- Study Configuration page (moved off NASA-TLX's own manual /login checkboxes).
ALTER TABLE "Replication" ADD COLUMN IF NOT EXISTS "TlxCalculateScores" BOOLEAN NOT NULL DEFAULT TRUE;
ALTER TABLE "Replication" ADD COLUMN IF NOT EXISTS "TlxIncludeWeightings" BOOLEAN NOT NULL DEFAULT TRUE;
ALTER TABLE "Replication" ADD COLUMN IF NOT EXISTS "Rei40Variant" VARCHAR(20) NOT NULL DEFAULT 'v1';
