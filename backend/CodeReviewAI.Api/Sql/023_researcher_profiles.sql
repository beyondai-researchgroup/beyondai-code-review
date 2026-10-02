-- Researcher profile system (2026-08-20, admin-dashboard-andrejkatin follow-up project). Adds
-- full profile fields to "Researcher" and switches login from Username to Email — see
-- admin_dashboard_followup_2026_08_20 memory / code-review-ai/CLAUDE.md for the full design.
--
-- "Username"/"PasswordHash" columns are kept (not dropped) — cheapest safe path, they simply go
-- unused by the new login flow going forward rather than requiring a destructive migration of
-- every historical row. "Language" (VARCHAR 2, default 'sr') is included here too, even though
-- it's primarily needed by the separate Notifications phase, since it's a natural profile field
-- and avoids a second migration just for it.
--
-- Idempotent -- safe to re-run. Run manually against Neon (psql "$DATABASE_URL" -f
-- Sql/023_researcher_profiles.sql, or paste into the Neon SQL console). Run after
-- 022_uses_tlx_toggle.sql.

DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Researcher' AND column_name = 'Email') THEN
    ALTER TABLE "Researcher" ADD COLUMN "Email" VARCHAR(255);
  END IF;
  IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Researcher' AND column_name = 'FirstName') THEN
    ALTER TABLE "Researcher" ADD COLUMN "FirstName" VARCHAR(100);
  END IF;
  IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Researcher' AND column_name = 'LastName') THEN
    ALTER TABLE "Researcher" ADD COLUMN "LastName" VARCHAR(100);
  END IF;
  IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Researcher' AND column_name = 'DateOfBirth') THEN
    ALTER TABLE "Researcher" ADD COLUMN "DateOfBirth" DATE;
  END IF;
  IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Researcher' AND column_name = 'AcademicStatus') THEN
    ALTER TABLE "Researcher" ADD COLUMN "AcademicStatus" VARCHAR(30);
  END IF;
  IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Researcher' AND column_name = 'Country') THEN
    ALTER TABLE "Researcher" ADD COLUMN "Country" VARCHAR(100);
  END IF;
  IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Researcher' AND column_name = 'AvatarImage') THEN
    ALTER TABLE "Researcher" ADD COLUMN "AvatarImage" BYTEA;
  END IF;
  IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Researcher' AND column_name = 'AvatarContentType') THEN
    ALTER TABLE "Researcher" ADD COLUMN "AvatarContentType" VARCHAR(100);
  END IF;
  IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Researcher' AND column_name = 'MustChangePassword') THEN
    ALTER TABLE "Researcher" ADD COLUMN "MustChangePassword" BOOLEAN NOT NULL DEFAULT FALSE;
  END IF;
  IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Researcher' AND column_name = 'Language') THEN
    ALTER TABLE "Researcher" ADD COLUMN "Language" VARCHAR(2) NOT NULL DEFAULT 'sr';
  END IF;
END $$;

-- CHECK constraint on AcademicStatus, allowlist-in-code-and-DB pattern (mirrors TaskType/
-- Rei40Variant elsewhere in this project). NULL is allowed (an account may not have this filled
-- in yet).
DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM pg_constraint WHERE conname = 'Researcher_AcademicStatus_CHK'
  ) THEN
    ALTER TABLE "Researcher" ADD CONSTRAINT "Researcher_AcademicStatus_CHK"
      CHECK ("AcademicStatus" IS NULL OR "AcademicStatus" IN ('PHD_STUDENT', 'MASTER', 'DOCTOR'));
  END IF;
END $$;

-- Backfill the real superadmin account's login email so it can log in immediately once the
-- login flow switches to Email — confirmed explicit choice, avoids a lockout. Only touches a row
-- that has no Email yet, so re-running this migration (or running it after someone has already
-- set a different email by hand) is safe/idempotent.
UPDATE "Researcher" SET "Email" = 'katin.andrej96@gmail.com'
WHERE "IsSuperAdmin" = TRUE AND "Email" IS NULL;

-- Unique constraint on Email, added only once every existing row has one (or is NULL — a NULL
-- Email is allowed and excluded from uniqueness by Postgres's default NULLS DISTINCT behavior,
-- so this is safe to add even though most legacy accounts won't have an Email yet).
DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM pg_constraint WHERE conname = 'Researcher_Email_UQ'
  ) THEN
    ALTER TABLE "Researcher" ADD CONSTRAINT "Researcher_Email_UQ" UNIQUE ("Email");
  END IF;
END $$;

-- One-time account-setup token (mirrors SurveyAccessToken's exact shape from
-- consent-andrejkatin) — issued when a superadmin creates a new researcher account, emailed as a
-- magic link, resolved by AcceptInviteComponent. No separate "used" flag: completion is inferred
-- from Researcher.MustChangePassword flipping to FALSE, same "infer completion from a downstream
-- state change" pattern SurveyAccessToken already established.
CREATE TABLE IF NOT EXISTS "ResearcherInviteToken" (
  "Id" SERIAL PRIMARY KEY,
  "ResearcherId" INTEGER NOT NULL REFERENCES "Researcher"("Id") ON DELETE CASCADE,
  "Token" VARCHAR(64) NOT NULL UNIQUE,
  "ExpiresAt" TIMESTAMPTZ NOT NULL,
  "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS "IX_ResearcherInviteToken_ResearcherId" ON "ResearcherInviteToken" ("ResearcherId");
