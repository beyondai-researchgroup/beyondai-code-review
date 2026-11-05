-- When a participant opened their Generic Task (the link opened in the task app = the moment the countdown starts).
-- One row per participant, written once (first open). Additive; no existing row is touched.
CREATE TABLE IF NOT EXISTS "GenericTaskStart" (
  "ParticipantGuid" UUID PRIMARY KEY REFERENCES "Participant"("Guid") ON DELETE CASCADE,
  "StartedAt"       TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
