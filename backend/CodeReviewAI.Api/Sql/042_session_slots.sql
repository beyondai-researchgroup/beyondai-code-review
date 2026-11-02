-- Termini (konkretne sesije) unutar eksperimentalne sesije, za PROTOCOL istraživanja.
-- Termin ima jedno vreme, tip (online / uživo), link ili lokaciju i publiku (jedan učesnik, grupa, svi).
-- Aditivno i idempotentno. Stari tok (ParticipantSession.ExperimentalSessionId) se ne dira.

CREATE TABLE IF NOT EXISTS "SessionSlot" (
  "Id"                    SERIAL PRIMARY KEY,
  "ExperimentalSessionId" INTEGER NOT NULL REFERENCES "ExperimentalSession"("Id") ON DELETE CASCADE,
  "ResearchId"            INTEGER NOT NULL REFERENCES "Research"("Id") ON DELETE CASCADE,
  "StartTime"             TIME NOT NULL,
  "DurationMinutes"       INTEGER NOT NULL DEFAULT 45,
  "Kind"                  VARCHAR(12) NOT NULL,
  "MeetingUrl"            VARCHAR(500),
  "Location"              VARCHAR(300),
  "AudienceType"          VARCHAR(12) NOT NULL,
  "GroupId"               INTEGER REFERENCES "ResearchGroup"("Id") ON DELETE RESTRICT,
  "ProtocolStepId"        INTEGER REFERENCES "ResearchProtocolStep"("Id") ON DELETE SET NULL,
  "Notes"                 TEXT,
  "Tags"                  JSONB NOT NULL DEFAULT '[]'::jsonb,
  "GoogleCalendarEventId"        VARCHAR(255),
  "GoogleCalendarEventUrl"       TEXT,
  "GoogleCalendarResearcherId"   INTEGER REFERENCES "Researcher"("Id"),
  "CreatedByResearcherId" INTEGER REFERENCES "Researcher"("Id"),
  "CreatedAt"             TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  CONSTRAINT "SessionSlot_Kind_check" CHECK ("Kind" IN ('ONLINE', 'IN_PERSON')),
  CONSTRAINT "SessionSlot_Audience_check" CHECK ("AudienceType" IN ('PARTICIPANT', 'GROUP', 'ALL')),
  CONSTRAINT "SessionSlot_Duration_check" CHECK ("DurationMinutes" BETWEEN 5 AND 480),
  CONSTRAINT "SessionSlot_Group_check" CHECK (("AudienceType" = 'GROUP') = ("GroupId" IS NOT NULL))
);
CREATE INDEX IF NOT EXISTS "IX_SessionSlot_Session" ON "SessionSlot" ("ExperimentalSessionId", "StartTime");
CREATE INDEX IF NOT EXISTS "IX_SessionSlot_Research" ON "SessionSlot" ("ResearchId");
CREATE INDEX IF NOT EXISTS "IX_SessionSlot_Group" ON "SessionSlot" ("GroupId");

CREATE TABLE IF NOT EXISTS "SessionSlotAttendee" (
  "SlotId"          INTEGER NOT NULL REFERENCES "SessionSlot"("Id") ON DELETE CASCADE,
  "ParticipantGuid" UUID NOT NULL REFERENCES "Participant"("Guid") ON DELETE CASCADE,
  "AddedAt"         TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  PRIMARY KEY ("SlotId", "ParticipantGuid")
);
CREATE INDEX IF NOT EXISTS "IX_SessionSlotAttendee_Participant" ON "SessionSlotAttendee" ("ParticipantGuid");

-- The code already reads/writes this column on ParticipantSession; no earlier migration declared it.
ALTER TABLE "ParticipantSession" ADD COLUMN IF NOT EXISTS "GoogleCalendarEventUrl" TEXT;
