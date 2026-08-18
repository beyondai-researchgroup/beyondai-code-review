-- Drops the NOT NULL constraint on Rei40Result's 4 facet-score columns.
--
-- 008_rei40_answers_and_variant.sql already declared these NUMERIC(4,2) with no NOT NULL — but
-- the live Neon table (created ad-hoc before that migration was written, same "documented after
-- the fact" situation that migration's own comment describes) had NOT NULL on all 4 anyway,
-- undetected until now because every real Rei40Result row to date used the 'v1' variant, which
-- always supplies all 6 scores. The new 'short' variant (rei40-andrejkatin's
-- data/rei40-short-items.ts) only ever reports Rationality/Experientiality — 2-3 items per facet
-- isn't a meaningful facet score — so it needs to store NULL there, not a fabricated value.
--
-- Idempotent: ALTER COLUMN ... DROP NOT NULL on an already-nullable column is a harmless no-op.
-- Run manually against Neon (psql "$DATABASE_URL" -f Sql/014_rei40_nullable_facet_scores.sql, or
-- paste into the Neon SQL console). Run after 013_researcher_calendar_oauth.sql.

ALTER TABLE "Rei40Result" ALTER COLUMN "RationalAbility" DROP NOT NULL;
ALTER TABLE "Rei40Result" ALTER COLUMN "RationalEngagement" DROP NOT NULL;
ALTER TABLE "Rei40Result" ALTER COLUMN "ExperientialAbility" DROP NOT NULL;
ALTER TABLE "Rei40Result" ALTER COLUMN "ExperientialEngagement" DROP NOT NULL;
