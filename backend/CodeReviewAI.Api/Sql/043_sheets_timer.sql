-- Sheets: an optional countdown for filling in the table, and a record of whether an entry was handed in because the time ran out.
-- Additive and idempotent. The time starts when the participant presses "Start task" (SheetAccessLink.RulesAcknowledgedAt), so no
-- separate start column is needed.
ALTER TABLE "Research" ADD COLUMN IF NOT EXISTS "SheetsTimerMinutes" INT NULL;
DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'Research_SheetsTimerMinutes_chk') THEN
    ALTER TABLE "Research" ADD CONSTRAINT "Research_SheetsTimerMinutes_chk" CHECK ("SheetsTimerMinutes" IS NULL OR "SheetsTimerMinutes" BETWEEN 1 AND 480);
  END IF;
END $$;
ALTER TABLE "SheetEntry" ADD COLUMN IF NOT EXISTS "IsTimedOut" BOOLEAN NOT NULL DEFAULT FALSE;
