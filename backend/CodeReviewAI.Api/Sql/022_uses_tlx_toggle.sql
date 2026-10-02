-- Per-research "uses NASA-TLX" toggle, requested alongside making the admin dashboard's Results
-- menu configurable (2026-08-19). NASA-TLX itself keeps running unconditionally for every
-- participant in the fixed Intro->AI->Report flow (that's the separate NASA-TLX app's own
-- behavior, untouched by this column) -- this only controls whether the "NASA-TLX" item appears
-- in the admin dashboard's Results navigation for a research that doesn't care about TLX data.
-- Same shape as UsesPsychTests (018_psych_tests_toggle.sql): default TRUE, every existing
-- research keeps today's behavior unchanged.
--
-- Idempotent -- safe to re-run. Run manually against Neon (psql "$DATABASE_URL" -f
-- Sql/022_uses_tlx_toggle.sql, or paste into the Neon SQL console). Run after 021_r_analysis.sql.

DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Research' AND column_name = 'UsesTlx') THEN
    ALTER TABLE "Research" ADD COLUMN "UsesTlx" BOOLEAN NOT NULL DEFAULT TRUE;
  END IF;
END $$;
