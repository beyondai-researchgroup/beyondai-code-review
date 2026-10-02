-- Phase D of platform-ification: per-research "uses psychological tests" toggle. When off, the
-- Consent app skips REI-40/Big Five token issuance entirely and sends a fixed-template
-- thank-you email instead (only the study's display name is configurable in that email, not
-- free-form body text — per explicit confirmation).
--
-- Idempotent — safe to re-run (guarded column adds). Run manually against Neon (psql
-- "$DATABASE_URL" -f Sql/018_psych_tests_toggle.sql, or paste into the Neon SQL console). Run
-- after 017_consent_sections.sql.

DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Research' AND column_name = 'UsesPsychTests') THEN
    ALTER TABLE "Research" ADD COLUMN "UsesPsychTests" BOOLEAN NOT NULL DEFAULT TRUE;
  END IF;
  IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Research' AND column_name = 'StudyDisplayName') THEN
    ALTER TABLE "Research" ADD COLUMN "StudyDisplayName" VARCHAR(200);
  END IF;
END $$;

-- Give the one existing seeded research a sensible display name so the thank-you email has
-- something real to show if it's ever switched off — falls back to "Research"."Name" in code
-- when NULL, this is just a friendlier default for the one research that exists today.
UPDATE "Research" SET "StudyDisplayName" = COALESCE("StudyDisplayName", "Name") WHERE "Id" = 1;
