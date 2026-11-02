-- Availability window of a research's tables (2026-10-30): the participant's link opens the table only between
-- these two moments. Both are optional (NULL = no limit on that side), so every existing research is unchanged.
-- Stored in UTC; shown to people in Europe/Belgrade.
ALTER TABLE "Research" ADD COLUMN IF NOT EXISTS "SheetsOpensAt"  TIMESTAMPTZ;
ALTER TABLE "Research" ADD COLUMN IF NOT EXISTS "SheetsClosesAt" TIMESTAMPTZ;

DO $$ BEGIN
    ALTER TABLE "Research" ADD CONSTRAINT "Research_SheetsWindow_check"
        CHECK ("SheetsOpensAt" IS NULL OR "SheetsClosesAt" IS NULL OR "SheetsOpensAt" < "SheetsClosesAt");
EXCEPTION WHEN duplicate_object THEN NULL; END $$;
