-- 036_experiment_step_and_rules.sql
-- Protocol step type EXPERIMENT ("experimental session": the researcher describes what participants do and
-- picks a task kind) and the rules screen shown before a sheets table (2026-10-28).
--
-- Purely additive and idempotent:
--   * ResearchProtocolStep may now have StepType 'EXPERIMENT' (the task kind lives in the step's Config JSON).
--   * SheetWorkbook gains the researcher-written rules text (SR/EN) shown to the participant first.
--   * SheetAccessLink records when the participant confirmed the rules, and a hash of the exact text they saw.
--     It is on the link, not on SheetEntry, so reading the rules does not lock the template (the template
--     locks at the first entry, as before).

ALTER TABLE "ResearchProtocolStep" DROP CONSTRAINT IF EXISTS "ResearchProtocolStep_StepType_check";
ALTER TABLE "ResearchProtocolStep" ADD CONSTRAINT "ResearchProtocolStep_StepType_check"
  CHECK ("StepType" IN ('CONSENT', 'DEMOGRAPHIC', 'QUESTIONNAIRE', 'REI40', 'BIGFIVE', 'NASA_TLX',
                        'EXTERNAL_LINK', 'MESSAGE', 'MANUAL', 'EXPERIMENT'));

ALTER TABLE "SheetWorkbook" ADD COLUMN IF NOT EXISTS "InstructionsSr" TEXT;
ALTER TABLE "SheetWorkbook" ADD COLUMN IF NOT EXISTS "InstructionsEn" TEXT;

ALTER TABLE "SheetAccessLink" ADD COLUMN IF NOT EXISTS "RulesAcknowledgedAt" TIMESTAMPTZ;
ALTER TABLE "SheetAccessLink" ADD COLUMN IF NOT EXISTS "RulesAckHash" VARCHAR(64);
