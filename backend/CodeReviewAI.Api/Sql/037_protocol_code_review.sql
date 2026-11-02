-- 037_protocol_code_review.sql
-- Code Review AI as a task kind of the protocol's EXPERIMENT step (2026-10-28).
--
-- Additive and idempotent. The step's mode / intro settings live in ResearchProtocolStep.Config (JSON); the only new
-- column is the pull request a participant reviews, chosen per participant (Excel import or the participants page).
-- NULL for every participant that exists today, so nothing changes for the classic study (whose PR comes from
-- ParticipantSession.PrConfigId).

ALTER TABLE "Participant" ADD COLUMN IF NOT EXISTS "CodeReviewPrConfigId" INT;

DO $$ BEGIN
    ALTER TABLE "Participant" ADD CONSTRAINT "Participant_CodeReviewPrConfigId_fkey"
        FOREIGN KEY ("CodeReviewPrConfigId") REFERENCES "ResearchPrConfig"("Id") ON DELETE SET NULL;
EXCEPTION WHEN duplicate_object THEN NULL; END $$;
