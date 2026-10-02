-- Cloudflare R2 cloud storage for the Activity Log (a real research instrument — must keep
-- working once the app is hosted, not just in local dev) and, in admin-dashboard-andrejkatin's
-- own uncommitted migration (same convention as its other DB changes — applied directly, not
-- checked in here), the same StorageKey pattern for TaskFile, EegRecording,
-- GenericTaskSubmissionFile, AnalysisRunPlot, and Researcher.AvatarStorageKey.
--
-- LOCAL DEV ONLY for now, same policy as 025_hybrid_mode.sql/026_activity_log.sql — do not run
-- against production Neon until real R2 credentials exist and the surrounding feature has been
-- verified live against them.
--
-- "StorageKey" NULL means "the bytes live in the existing in-DB column, exactly as before" (old
-- rows, or any environment with R2 unconfigured — e.g. a developer machine with no R2
-- credentials set). Non-NULL means "the real bytes are in R2 at this object key; the in-DB
-- column is left empty for that row." Both code paths stay live indefinitely — nothing forces a
-- migration of old rows, and an unconfigured environment is a complete, correct fallback, not a
-- degraded mode. "RawCsv" is relaxed to nullable for exactly this reason: a StorageKey-backed row
-- legitimately has no CSV text in the database at all.
--
-- Idempotent — safe to re-run. Run after 027_research_control.sql.

ALTER TABLE "ActivityLog" ADD COLUMN IF NOT EXISTS "StorageKey" VARCHAR(500);
ALTER TABLE "ActivityLog" ALTER COLUMN "RawCsv" DROP NOT NULL;
