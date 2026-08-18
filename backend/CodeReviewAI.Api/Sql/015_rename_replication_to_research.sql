-- Full rename: "Replication" → "Research" (table, dependent table, FK columns, indexes/constraint
-- names) — the researcher-facing term "replikacija" is being replaced everywhere with
-- "istraživanje"/"research" as this project turns into a general multi-experiment platform. This
-- is a live-schema rename (Postgres RENAME preserves data and existing FK relationships — FKs are
-- tracked by OID internally, not by name), applied against the already-populated Neon DB.
--
-- Historical migrations 002-009 are left untouched on purpose — they're a record of what was
-- actually run at the time, not a live description of today's schema. This file is the live
-- description going forward.
--
-- Idempotent — safe to re-run (every step is guarded by an existence check, since RENAME itself
-- isn't naturally idempotent like the ADD COLUMN IF NOT EXISTS pattern used elsewhere).
--
-- Run manually against Neon (psql "$DATABASE_URL" -f Sql/015_rename_replication_to_research.sql,
-- or paste into the Neon SQL console). Run after 014_rei40_nullable_facet_scores.sql.
--
-- DEPLOYMENT COORDINATION: CodeReviewAI.Api (Render) and the NASA-TLX Vercel function both query
-- these identifiers live, for an active study — run this migration and redeploy both of them in
-- the same short window, not as separate steps hours apart.

-- 1. Table renames.
ALTER TABLE IF EXISTS "Replication" RENAME TO "Research";
ALTER TABLE IF EXISTS "ReplicationPrConfig" RENAME TO "ResearchPrConfig";

-- 2. Column renames (each guarded — ALTER TABLE ... RENAME COLUMN has no IF EXISTS form).
DO $$
BEGIN
  IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Researcher' AND column_name = 'ReplicationId') THEN
    ALTER TABLE "Researcher" RENAME COLUMN "ReplicationId" TO "ResearchId";
  END IF;
  IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Participant' AND column_name = 'ReplicationId') THEN
    ALTER TABLE "Participant" RENAME COLUMN "ReplicationId" TO "ResearchId";
  END IF;
  IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ParticipantSession' AND column_name = 'ReplicationId') THEN
    ALTER TABLE "ParticipantSession" RENAME COLUMN "ReplicationId" TO "ResearchId";
  END IF;
  IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ResearchPrConfig' AND column_name = 'ReplicationId') THEN
    ALTER TABLE "ResearchPrConfig" RENAME COLUMN "ReplicationId" TO "ResearchId";
  END IF;
  IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ExperimentalSession' AND column_name = 'ReplicationId') THEN
    ALTER TABLE "ExperimentalSession" RENAME COLUMN "ReplicationId" TO "ResearchId";
  END IF;
END $$;

-- 3. Known explicitly-named indexes (from the original migrations) — cosmetic, but matches the
-- full-rename request rather than leaving stray "Replication*" names behind.
ALTER INDEX IF EXISTS "IX_Participant_ReplicationId" RENAME TO "IX_Participant_ResearchId";
ALTER INDEX IF EXISTS "IX_ParticipantSession_ReplicationId" RENAME TO "IX_ParticipantSession_ResearchId";
ALTER INDEX IF EXISTS "UX_ReplicationPrConfig_ActivePerReplication" RENAME TO "UX_ResearchPrConfig_ActivePerResearch";
ALTER INDEX IF EXISTS "IX_ReplicationPrConfig_ReplicationId" RENAME TO "IX_ResearchPrConfig_ResearchId";
ALTER INDEX IF EXISTS "IX_ExperimentalSession_ReplicationId" RENAME TO "IX_ExperimentalSession_ResearchId";

-- 4. Auto-generated constraint names (FK, PK, and Postgres's own named NOT NULL constraints —
-- never explicitly named in the original migrations, so their exact live names depend on
-- creation order/history rather than anything we can hardcode reliably) — found dynamically via
-- pg_constraint and renamed to follow the same "s/Replication/Research/" pattern, purely
-- cosmetic (constraint behavior itself doesn't depend on the name). Excludes Postgres's own
-- system replication-origin catalog objects (pg_replication_origin*), which are unrelated.
DO $$
DECLARE
  rec RECORD;
BEGIN
  FOR rec IN
    SELECT conname, conrelid::regclass::text AS table_name
    FROM pg_constraint
    WHERE conname ILIKE '%replication%' AND conrelid::regclass::text NOT LIKE '%pg_replication%'
  LOOP
    -- rec.table_name comes from conrelid::regclass::text, which is already correctly quoted
    -- when needed (mixed-case identifiers) — %I would double-quote it, so use %s here and
    -- reserve %I for the plain, unquoted constraint names from pg_constraint.conname.
    EXECUTE format(
      'ALTER TABLE %s RENAME CONSTRAINT %I TO %I',
      rec.table_name,
      rec.conname,
      replace(replace(rec.conname, 'Replication', 'Research'), 'replication', 'research')
    );
  END LOOP;
END $$;
