-- Adds a required-at-assignment-time scheduling field: when a ParticipantSession row is tied to
-- an ExperimentalSession (see 009), the researcher also records the planned time of day for that
-- specific participant's session — one ExperimentalSession/day can cover several participants at
-- different times. Nullable/additive here (existing rows unaffected); the admin-dashboard's
-- assign endpoint is what actually requires it going forward (see
-- server/experimental-sessions/routes.mjs).
--
-- Idempotent — safe to re-run. Run after 009_experimental_sessions.sql.

ALTER TABLE "ParticipantSession" ADD COLUMN IF NOT EXISTS "ScheduledTime" TIME;
