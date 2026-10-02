-- Persists each review session's activity-log CSV into the shared study database, so the
-- researcher can read it in the Admin Dashboard instead of digging through files on whichever
-- machine the backend happened to run on. LOCAL DEV ONLY for now — do not run against
-- production Neon until the surrounding feature is deployed there (same policy as
-- 025_hybrid_mode.sql and the Generic Task/timer work).
--
-- Storage shape follows "EegRecording" verbatim: the raw CSV goes into a Postgres TEXT column
-- untouched, exactly as ActivityLogService wrote it to disk, and is parsed on read for display.
-- That keeps the original artifact intact (and downloadable) instead of shredding it into rows
-- that would then have to be reassembled to get the file back.
--
-- Unlike "EegRecording" (one continuous recording per participant across every session), this is
-- SESSION-scoped: ActivityLogService creates one file per review session, so the same participant
-- legitimately has one row per session they ran. Same cadence class as "TlxResult".
--
-- Idempotent — safe to re-run. Run after 025_hybrid_mode.sql.

CREATE TABLE IF NOT EXISTS "ActivityLog" (
  "Id"               SERIAL PRIMARY KEY,
  "ParticipantId"    TEXT NOT NULL,
  "ParticipantGuid"  UUID NOT NULL REFERENCES "Participant"("Guid"),
  "SessionId"        INTEGER NOT NULL REFERENCES "Sessions"("Id"),
  "ReviewMode"       TEXT NOT NULL,
  "OriginalFilename" VARCHAR(255) NOT NULL,
  "RawCsv"           TEXT NOT NULL,
  "RowCount"         INTEGER NOT NULL,  -- data rows, header excluded
  "SavedAt"          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  -- One row per participant per session. The log is written twice by design (once when the
  -- decision is submitted, once more when the session is torn down, to also capture abandoned
  -- and timed-out sessions), so the second write must overwrite rather than duplicate.
  CONSTRAINT "ActivityLog_Participant_Session_UQ" UNIQUE ("ParticipantGuid", "SessionId")
);

CREATE INDEX IF NOT EXISTS "IX_ActivityLog_Participant_Session"
  ON "ActivityLog" ("ParticipantId", "SessionId");

-- Same auto-populate-ParticipantGuid-from-ParticipantId trigger already installed on every other
-- participant-linked table (ChatMessage, ReviewDecision, HybridSectionEngagement, ...). It fires
-- BEFORE INSERT, so the ON CONFLICT ("ParticipantGuid", "SessionId") target above resolves
-- correctly without the application ever computing a Guid itself.
DROP TRIGGER IF EXISTS trg_set_participant_guid ON "ActivityLog";
CREATE TRIGGER trg_set_participant_guid
  BEFORE INSERT ON "ActivityLog"
  FOR EACH ROW EXECUTE FUNCTION set_participant_guid();
