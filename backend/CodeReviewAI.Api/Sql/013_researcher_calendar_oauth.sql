-- Per-researcher Google Calendar connection (each researcher connects their OWN Google account
-- from the Admin Dashboard's Settings page — see admin-dashboard-andrejkatin/server/calendar/
-- routes.mjs). The refresh token is encrypted at rest (AES-256-GCM, key from
-- CALENDAR_TOKEN_ENCRYPTION_KEY — see server/calendar/tokenCrypto.mjs) since, unlike a bcrypt
-- password hash, it grants ongoing write access to that researcher's real calendar if ever
-- exposed.
ALTER TABLE "Researcher" ADD COLUMN IF NOT EXISTS "GoogleCalendarRefreshTokenEnc" TEXT;
ALTER TABLE "Researcher" ADD COLUMN IF NOT EXISTS "GoogleCalendarEmail" VARCHAR(255);
ALTER TABLE "Researcher" ADD COLUMN IF NOT EXISTS "GoogleCalendarConnectedAt" TIMESTAMPTZ;

-- Which researcher's calendar a given synced event lives in — needed because unassign/
-- reschedule/delete can be performed by a DIFFERENT researcher than the one who originally
-- assigned it, but only the owning researcher's OAuth token can patch/delete an event on their
-- own calendar. See 012_google_calendar_sync.sql for GoogleCalendarEventId itself.
ALTER TABLE "ParticipantSession" ADD COLUMN IF NOT EXISTS "GoogleCalendarResearcherId" INTEGER REFERENCES "Researcher"("Id");

-- Idempotent — safe to re-run. Run after 012_google_calendar_sync.sql.
