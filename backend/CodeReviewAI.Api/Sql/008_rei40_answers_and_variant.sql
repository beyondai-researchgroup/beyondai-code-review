-- Formally documents "Rei40Result"/"BigFiveResult", which have existed ad-hoc on the live Neon DB
-- (created by rei40-andrejkatin/server.mjs's and bigfive-andrejkatin/server.mjs's INSERT
-- statements) with no committed DDL anywhere — same "documented after the fact" gap that
-- 001_document_existing_study_schema.sql closed for Sessions/ParticipantSession/etc. Every
-- statement is idempotent (CREATE TABLE IF NOT EXISTS / ADD COLUMN IF NOT EXISTS), so running
-- this against the real, already-populated database is a no-op for the CREATE TABLE blocks.
--
-- Also adds Rei40Result.Variant, the last piece of the REI item-set variant selector: which
-- variant produced a given result. Only 'v1' (the existing REI-40) is a real, implemented item
-- set today — see Replication.Rei40Variant (007) and the Study Configuration page — this column
-- just lets a result record which variant it was answered under, and is what
-- server/study-config/routes.mjs's lock check reads (a replication can't change Rei40Variant once
-- any of its participants has a Rei40Result row).
--
-- Run manually against Neon (psql "$DATABASE_URL" -f Sql/008_rei40_answers_and_variant.sql, or
-- paste into the Neon SQL console). Run after 007_consent_email_language.sql.

CREATE TABLE IF NOT EXISTS "Rei40Result" (
  "ParticipantId"          VARCHAR(50) PRIMARY KEY REFERENCES "Participant"("ParticipantId"),
  "Language"               VARCHAR(5),
  "Answers"                JSONB NOT NULL,
  "RationalAbility"        NUMERIC(4,2),
  "RationalEngagement"     NUMERIC(4,2),
  "ExperientialAbility"    NUMERIC(4,2),
  "ExperientialEngagement" NUMERIC(4,2),
  "Rationality"            NUMERIC(4,2),
  "Experientiality"        NUMERIC(4,2),
  "CompletedAt"            TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS "BigFiveResult" (
  "ParticipantId"     VARCHAR(50) PRIMARY KEY REFERENCES "Participant"("ParticipantId"),
  "Language"          VARCHAR(5),
  "Answers"           JSONB NOT NULL,
  "Openness"          NUMERIC(4,2),
  "Conscientiousness" NUMERIC(4,2),
  "Extraversion"      NUMERIC(4,2),
  "Agreeableness"     NUMERIC(4,2),
  "Neuroticism"        NUMERIC(4,2),
  "CompletedAt"       TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

ALTER TABLE "Rei40Result" ADD COLUMN IF NOT EXISTS "Variant" VARCHAR(20) NOT NULL DEFAULT 'v1';
