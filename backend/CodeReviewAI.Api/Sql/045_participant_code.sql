-- A random four-digit code per participant (unique inside a research), used instead of the real ParticipantId in analysis and papers.
-- Additive and idempotent; existing participants keep NULL until the researcher runs "assign codes".
ALTER TABLE "Participant" ADD COLUMN IF NOT EXISTS "ParticipantCode" VARCHAR(8) NULL;
CREATE UNIQUE INDEX IF NOT EXISTS "UX_Participant_ResearchCode" ON "Participant" ("ResearchId", "ParticipantCode") WHERE "ParticipantCode" IS NOT NULL;
