-- Predefined + custom "hashtags" a researcher can attach to any individual participant-session
-- (shown next to its Notes on the Experimental Session detail page) — e.g. flagging that
-- something noteworthy happened during it. See PREDEFINED_TAGS in
-- admin-dashboard-andrejkatin/server/experimental-sessions/routes.mjs for the validated
-- vocabulary; custom free-text tags are also allowed.
--
-- Idempotent — safe to re-run. Run after 010_participant_session_scheduled_time.sql.

ALTER TABLE "ParticipantSession" ADD COLUMN IF NOT EXISTS "Tags" JSONB NOT NULL DEFAULT '[]'::jsonb;
