-- Phase B of platform-ification: researchers become many-to-many with researches — a
-- persistent Researcher can now be assigned to several researches (previously
-- Researcher.ResearchId was a single nullable FK, enforced via CHECK to be non-null for a
-- non-superadmin and NULL for a superadmin). Replaces that with a join table.
--
-- Idempotent — safe to re-run (CREATE TABLE IF NOT EXISTS, guarded column/constraint drops).
-- Run manually against Neon (psql "$DATABASE_URL" -f Sql/016_researcher_research_many_to_many.sql,
-- or paste into the Neon SQL console). Run after 015_rename_replication_to_research.sql.

-- 1. New join table. One row per (researcher, research) assignment.
CREATE TABLE IF NOT EXISTS "ResearcherResearch" (
  "ResearcherId" INTEGER NOT NULL REFERENCES "Researcher"("Id") ON DELETE CASCADE,
  "ResearchId"   INTEGER NOT NULL REFERENCES "Research"("Id") ON DELETE CASCADE,
  "CreatedAt"    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  CONSTRAINT "ResearcherResearch_PK" PRIMARY KEY ("ResearcherId", "ResearchId")
);

CREATE INDEX IF NOT EXISTS "IX_ResearcherResearch_ResearchId" ON "ResearcherResearch" ("ResearchId");

-- 2. Backfill: every existing scoped researcher's single ResearchId becomes one join-table row.
-- Safe to re-run — ON CONFLICT DO NOTHING means a second run after the column is already gone
-- (see step 3) is simply a no-op, not an error, since the SELECT will then return zero rows.
DO $$
BEGIN
  IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Researcher' AND column_name = 'ResearchId') THEN
    INSERT INTO "ResearcherResearch" ("ResearcherId", "ResearchId")
    SELECT "Id", "ResearchId" FROM "Researcher" WHERE "ResearchId" IS NOT NULL
    ON CONFLICT ("ResearcherId", "ResearchId") DO NOTHING;
  END IF;
END $$;

-- 3. Drop the old single-FK column and its scope CHECK constraint — the join table is now the
-- only source of truth for which researches a researcher can see. IsSuperAdmin stays a plain
-- column (still means "sees every research"); there's no DB-level constraint tying it to the
-- join table's contents (a superadmin with stray join rows is harmless — the app-level scope
-- resolution always treats isSuperAdmin as seeing everything, regardless of join rows), enforced
-- instead at the application layer in server/researchers/routes.mjs.
DO $$
BEGIN
  IF EXISTS (
    SELECT 1 FROM pg_constraint WHERE conname = 'Researcher_Scope_CHK'
  ) THEN
    ALTER TABLE "Researcher" DROP CONSTRAINT "Researcher_Scope_CHK";
  END IF;
  IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Researcher' AND column_name = 'ResearchId') THEN
    ALTER TABLE "Researcher" DROP COLUMN "ResearchId";
  END IF;
END $$;
