-- 033: research protocol — a researcher defines their own ordered list of steps.
--
-- PURELY ADDITIVE: one new column (default keeps every existing research as it is) plus new tables.
-- No existing column/constraint/row is changed, so a study already in progress is not affected.
-- Idempotent — safe to re-run. Apply to local Postgres first, then to Neon (with a backup).
--
-- Design notes (see CLAUDE.md "Research protocol"):
--   * Research.FlowMode: 'CLASSIC' (every research that exists today, the original BeyondAI flow
--     driven by the Uses*/TaskType switches) or 'PROTOCOL' (steps below). Chosen at creation only.
--   * Step links live in ProtocolStepLink, NOT in SurveyAccessToken, so that table's SurveyType
--     CHECK (used by the live study) stays untouched.
--   * Questionnaire answers are keyed by QuestionnaireItem.ItemKey (a stable uuid), never by a row
--     id that can change on save (the demographic-questions id bug, 2026-10-13).
--   * Participant-keyed tables use ParticipantGuid -> Participant.Guid (the platform convention).

ALTER TABLE "Research" ADD COLUMN IF NOT EXISTS "FlowMode" VARCHAR(20) NOT NULL DEFAULT 'CLASSIC';

DO $$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'Research_FlowMode_check') THEN
    ALTER TABLE "Research" ADD CONSTRAINT "Research_FlowMode_check" CHECK ("FlowMode" IN ('CLASSIC', 'PROTOCOL'));
  END IF;
END $$;

-- ── Researcher-authored questionnaires ──────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS "Questionnaire" (
  "Id"          SERIAL PRIMARY KEY,
  "ResearchId"  INTEGER NOT NULL REFERENCES "Research"("Id") ON DELETE CASCADE,
  "TitleSr"     VARCHAR(200) NOT NULL,
  "TitleEn"     VARCHAR(200) NOT NULL,
  "CreatedAt"   TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX IF NOT EXISTS "IX_Questionnaire_ResearchId" ON "Questionnaire" ("ResearchId");

CREATE TABLE IF NOT EXISTS "QuestionnaireItem" (
  "Id"              SERIAL PRIMARY KEY,
  "QuestionnaireId" INTEGER NOT NULL REFERENCES "Questionnaire"("Id") ON DELETE CASCADE,
  "ItemKey"         UUID NOT NULL DEFAULT gen_random_uuid(),
  "SortOrder"       INTEGER NOT NULL,
  "ItemType"        VARCHAR(20) NOT NULL,
  "PromptSr"        VARCHAR(500) NOT NULL,
  "PromptEn"        VARCHAR(500) NOT NULL,
  -- SINGLE_CHOICE / MULTI_CHOICE: [{"key":"<uuid>","labelSr":"..","labelEn":"..","isOther":false}, ...]
  "Options"         JSONB NOT NULL DEFAULT '[]'::jsonb,
  -- LIKERT: number of points (2..10) and optional end labels {"minSr","minEn","maxSr","maxEn"}
  "ScalePoints"     INTEGER,
  "ScaleLabels"     JSONB,
  "Subscale"        VARCHAR(100),
  "ReverseScored"   BOOLEAN NOT NULL DEFAULT FALSE,
  "Required"        BOOLEAN NOT NULL DEFAULT TRUE,
  CONSTRAINT "QuestionnaireItem_ItemType_check"
    CHECK ("ItemType" IN ('LIKERT', 'SINGLE_CHOICE', 'MULTI_CHOICE', 'TEXT', 'NUMBER')),
  CONSTRAINT "QuestionnaireItem_ScalePoints_check"
    CHECK ("ScalePoints" IS NULL OR ("ScalePoints" BETWEEN 2 AND 10))
);
CREATE UNIQUE INDEX IF NOT EXISTS "UX_QuestionnaireItem_ItemKey" ON "QuestionnaireItem" ("ItemKey");
CREATE INDEX IF NOT EXISTS "IX_QuestionnaireItem_Questionnaire" ON "QuestionnaireItem" ("QuestionnaireId", "SortOrder");

-- ── The protocol itself: an ordered list of steps per research ──────────────────────────────────
CREATE TABLE IF NOT EXISTS "ResearchProtocolStep" (
  "Id"              SERIAL PRIMARY KEY,
  "ResearchId"      INTEGER NOT NULL REFERENCES "Research"("Id") ON DELETE CASCADE,
  "SortOrder"       INTEGER NOT NULL,
  "StepType"        VARCHAR(20) NOT NULL,
  -- Internal name, shown to the researcher only (a participant never sees the protocol).
  "TitleSr"         VARCHAR(200) NOT NULL,
  "TitleEn"         VARCHAR(200) NOT NULL,
  "QuestionnaireId" INTEGER REFERENCES "Questionnaire"("Id") ON DELETE RESTRICT,
  -- EXTERNAL_LINK: {"url": "..."}   MESSAGE/MANUAL: {"bodySr": "..", "bodyEn": ".."}
  "Config"          JSONB NOT NULL DEFAULT '{}'::jsonb,
  -- AUTO: send as soon as the previous step is completed; AFTER_DELAY: DelayDays after that;
  -- MANUAL: the researcher triggers it (also the only mode for the MANUAL step type).
  "Delivery"        VARCHAR(20) NOT NULL DEFAULT 'AUTO',
  "DelayDays"       INTEGER,
  "EmailMessageSr"  VARCHAR(2000),
  "EmailMessageEn"  VARCHAR(2000),
  CONSTRAINT "ResearchProtocolStep_StepType_check"
    CHECK ("StepType" IN ('CONSENT', 'DEMOGRAPHIC', 'QUESTIONNAIRE', 'REI40', 'BIGFIVE', 'NASA_TLX',
                          'EXTERNAL_LINK', 'MESSAGE', 'MANUAL')),
  CONSTRAINT "ResearchProtocolStep_Delivery_check"
    CHECK ("Delivery" IN ('AUTO', 'AFTER_DELAY', 'MANUAL')),
  CONSTRAINT "ResearchProtocolStep_DelayDays_check"
    CHECK ("DelayDays" IS NULL OR ("DelayDays" BETWEEN 0 AND 365)),
  CONSTRAINT "ResearchProtocolStep_Questionnaire_check"
    CHECK (("StepType" = 'QUESTIONNAIRE') = ("QuestionnaireId" IS NOT NULL))
);
CREATE INDEX IF NOT EXISTS "IX_ResearchProtocolStep_Research" ON "ResearchProtocolStep" ("ResearchId", "SortOrder");

-- ── Per-participant progress through the protocol ───────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS "ParticipantProtocolStep" (
  "Id"              SERIAL PRIMARY KEY,
  "ParticipantGuid" UUID NOT NULL REFERENCES "Participant"("Guid") ON DELETE CASCADE,
  "StepId"          INTEGER NOT NULL REFERENCES "ResearchProtocolStep"("Id") ON DELETE CASCADE,
  "Status"          VARCHAR(20) NOT NULL DEFAULT 'PENDING',
  "ScheduledFor"    TIMESTAMPTZ,
  "SentAt"          TIMESTAMPTZ,
  "CompletedAt"     TIMESTAMPTZ,
  -- 'AUTO' (detected from the module's own result) or the id of the researcher who marked it done.
  "CompletedBy"     VARCHAR(40),
  CONSTRAINT "ParticipantProtocolStep_Status_check"
    CHECK ("Status" IN ('PENDING', 'SCHEDULED', 'SENT', 'COMPLETED', 'SKIPPED')),
  CONSTRAINT "UX_ParticipantProtocolStep" UNIQUE ("ParticipantGuid", "StepId")
);
CREATE INDEX IF NOT EXISTS "IX_ParticipantProtocolStep_Due"
  ON "ParticipantProtocolStep" ("ScheduledFor") WHERE "Status" = 'SCHEDULED';

-- ── Magic links for the steps the platform renders itself (questionnaire, consent, external link) ─
CREATE TABLE IF NOT EXISTS "ProtocolStepLink" (
  "Id"              SERIAL PRIMARY KEY,
  "Token"           VARCHAR(64) NOT NULL,
  "ParticipantGuid" UUID NOT NULL REFERENCES "Participant"("Guid") ON DELETE CASCADE,
  "StepId"          INTEGER NOT NULL REFERENCES "ResearchProtocolStep"("Id") ON DELETE CASCADE,
  "ExpiresAt"       TIMESTAMPTZ NOT NULL,
  "CreatedAt"       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  CONSTRAINT "UX_ProtocolStepLink_Token" UNIQUE ("Token"),
  CONSTRAINT "UX_ProtocolStepLink_Participant_Step" UNIQUE ("ParticipantGuid", "StepId")
);

-- ── Answers to researcher-authored questionnaires ───────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS "QuestionnaireResponse" (
  "Id"              SERIAL PRIMARY KEY,
  "ParticipantGuid" UUID NOT NULL REFERENCES "Participant"("Guid") ON DELETE CASCADE,
  "StepId"          INTEGER NOT NULL REFERENCES "ResearchProtocolStep"("Id") ON DELETE CASCADE,
  "QuestionnaireId" INTEGER NOT NULL REFERENCES "Questionnaire"("Id") ON DELETE RESTRICT,
  "Language"        VARCHAR(2) NOT NULL,
  -- { "<ItemKey>": { "value": ..., "otherText": ".." }, ... }
  "Answers"         JSONB NOT NULL,
  "SubmittedAt"     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  CONSTRAINT "UX_QuestionnaireResponse_Participant_Step" UNIQUE ("ParticipantGuid", "StepId")
);
CREATE INDEX IF NOT EXISTS "IX_QuestionnaireResponse_Questionnaire" ON "QuestionnaireResponse" ("QuestionnaireId");
