-- Admin Dashboard kao kontrola istraživanja: PR zadaci sa labelom + Intro flag (umesto
-- "jedan aktivan"), test-učesnici na nivou baze (umesto Study:TestParticipantIds konfiguracije),
-- FinishedAt na ParticipantSession, novi SurveyAccessToken tipovi za Code Review lične linkove
-- i NASA-TLX handoff. Idempotentno — sigurno za ponovno pokretanje.

-- ── ResearchPrConfig: label + Intro flag umesto "jedan aktivan" ────────────────────────────
ALTER TABLE "ResearchPrConfig" ADD COLUMN IF NOT EXISTS "Label" VARCHAR(40);
ALTER TABLE "ResearchPrConfig" ADD COLUMN IF NOT EXISTS "IsIntro" BOOLEAN NOT NULL DEFAULT FALSE;

DROP INDEX IF EXISTS "UX_ResearchPrConfig_ActivePerResearch";

DO $$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE indexname = 'UX_ResearchPrConfig_IntroPerResearch') THEN
    CREATE UNIQUE INDEX "UX_ResearchPrConfig_IntroPerResearch" ON "ResearchPrConfig" ("ResearchId") WHERE "IsIntro";
  END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE indexname = 'UX_ResearchPrConfig_LabelPerResearch') THEN
    CREATE UNIQUE INDEX "UX_ResearchPrConfig_LabelPerResearch" ON "ResearchPrConfig" ("ResearchId", "Label");
  END IF;
END $$;
-- "IsActive" column is left in place (untouched) — the Neon production code still reads it and
-- this migration only ever runs against the local dev DB.

-- ── Participant: test-participant flags ─────────────────────────────────────────────────────
ALTER TABLE "Participant" ADD COLUMN IF NOT EXISTS "IsTestParticipant" BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE "Participant" ADD COLUMN IF NOT EXISTS "TestFixedSessionId" INTEGER NULL REFERENCES "Sessions"("Id");
ALTER TABLE "Participant" ADD COLUMN IF NOT EXISTS "BaselineDoneAt" TIMESTAMPTZ NULL;

-- ── ParticipantSession: real completion timestamp (IsFinished had none before) ─────────────
ALTER TABLE "ParticipantSession" ADD COLUMN IF NOT EXISTS "FinishedAt" TIMESTAMPTZ NULL;

-- ── SurveyAccessToken: per-participant Code Review link + NASA-TLX handoff token ───────────
ALTER TABLE "SurveyAccessToken" ADD COLUMN IF NOT EXISTS "StudySessionId" INTEGER NULL;

ALTER TABLE "SurveyAccessToken" DROP CONSTRAINT IF EXISTS "SurveyAccessToken_SurveyType_check";
ALTER TABLE "SurveyAccessToken" ADD CONSTRAINT "SurveyAccessToken_SurveyType_check"
  CHECK ("SurveyType" IN ('REI40', 'BIGFIVE', 'NASA_TLX', 'CONSENT_ENTRY', 'GENERIC_TASK', 'CODE_REVIEW', 'TLX_HANDOFF'));
