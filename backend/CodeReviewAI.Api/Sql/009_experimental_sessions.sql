-- Adds the "Experimental Session" concept: a physical day/timeslot within a Replication where
-- one or more participants are run through one or more of their Intro/AI/Report sessions (e.g.
-- "3 participants ran their Intro session on Aug 20"). Notes/metadata exist at two levels: the
-- whole event (ExperimentalSession.Notes) and the individual participant-session instance
-- (ParticipantSession.Notes) — see the Admin Dashboard's Experimental Sessions master-detail page.
--
-- Reuses ParticipantSession as the join target (it already represents "this participant's one
-- Intro/AI/Report session instance") rather than inventing a parallel session concept. Both new
-- ParticipantSession columns are nullable/additive — every existing row stays unassigned
-- (ExperimentalSessionId IS NULL) and keeps working exactly as before; this is forward-looking
-- only, no retroactive backfill.
--
-- Idempotent — safe to re-run. Run after 008_rei40_answers_and_variant.sql.

CREATE TABLE IF NOT EXISTS "ExperimentalSession" (
  "Id"                    SERIAL PRIMARY KEY,
  "ReplicationId"         INTEGER NOT NULL REFERENCES "Replication"("Id"),
  "SessionDate"           DATE NOT NULL,
  "Label"                 VARCHAR(150),
  "Notes"                 TEXT,
  "CreatedByResearcherId" INTEGER REFERENCES "Researcher"("Id"),
  "CreatedAt"             TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX IF NOT EXISTS "IX_ExperimentalSession_ReplicationId" ON "ExperimentalSession" ("ReplicationId");

ALTER TABLE "ParticipantSession" ADD COLUMN IF NOT EXISTS "ExperimentalSessionId" INTEGER REFERENCES "ExperimentalSession"("Id");
ALTER TABLE "ParticipantSession" ADD COLUMN IF NOT EXISTS "Notes" TEXT;
CREATE INDEX IF NOT EXISTS "IX_ParticipantSession_ExperimentalSessionId" ON "ParticipantSession" ("ExperimentalSessionId");
