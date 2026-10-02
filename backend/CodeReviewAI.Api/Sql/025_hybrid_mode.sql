-- Hybrid review mode (experimental, participant "004" only). LOCAL DEV ONLY — do not run
-- against production Neon for this feature.
--
-- Adds a 4th row to the shared "Sessions" table (also read by the NASA-TLX app — its own repo
-- needs matching updates, see code-review-ai/CLAUDE.md) plus a table recording which
-- documentation accordion sections a Hybrid-mode participant expanded/collapsed and how long
-- each stayed open. Only ever written to for Hybrid sessions — Ai/Report sessions never touch
-- this table.

INSERT INTO "Sessions" ("Id", "Name") VALUES (4, 'Hybrid')
ON CONFLICT ("Id") DO NOTHING;

CREATE TABLE IF NOT EXISTS "HybridSectionEngagement" (
  "Id"              SERIAL PRIMARY KEY,
  "ParticipantId"   TEXT NOT NULL,
  "ParticipantGuid" UUID NOT NULL REFERENCES "Participant"("Guid"),
  "SessionId"       INTEGER NOT NULL REFERENCES "Sessions"("Id"),
  "SectionId"       TEXT NOT NULL,
  "SectionTitle"    TEXT NOT NULL,
  "Action"          TEXT NOT NULL CHECK ("Action" IN ('Expand', 'Collapse')),
  "DurationSeconds" INTEGER,
  "CreatedAt"       TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS "IX_HybridSectionEngagement_Participant_Session"
  ON "HybridSectionEngagement" ("ParticipantId", "SessionId");

-- Same auto-populate-ParticipantGuid-from-ParticipantId trigger already installed on every other
-- participant-linked table (ChatMessage, ReviewDecision, ParticipantSession, ...).
DROP TRIGGER IF EXISTS trg_set_participant_guid ON "HybridSectionEngagement";
CREATE TRIGGER trg_set_participant_guid
  BEFORE INSERT ON "HybridSectionEngagement"
  FOR EACH ROW EXECUTE FUNCTION set_participant_guid();
