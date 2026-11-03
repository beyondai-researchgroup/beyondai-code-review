-- Sheets: the earliest the participant may hand the table in by hand, in minutes from pressing Start (NULL = any time).
-- The countdown running out always hands the table in, regardless of this.
ALTER TABLE "Research" ADD COLUMN IF NOT EXISTS "SheetsMinSubmitMinutes" INTEGER;
