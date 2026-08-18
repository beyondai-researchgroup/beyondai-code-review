-- Tracks the Google Calendar event backing a scheduled ParticipantSession, so a later
-- reschedule/unassign/delete can target the correct event instead of creating duplicates. See
-- admin-dashboard-andrejkatin/server/calendar/googleCalendar.mjs for the sync logic and
-- server/experimental-sessions/routes.mjs for where it's called from (assign/unassign/PUT/:id
-- reschedule/DELETE /:id). Nullable/additive — existing rows are simply unsynced until their
-- next assign or a parent-session reschedule.
--
-- Idempotent — safe to re-run. Run after 011_participant_session_tags.sql.

ALTER TABLE "ParticipantSession" ADD COLUMN IF NOT EXISTS "GoogleCalendarEventId" VARCHAR(255);
