-- Experimental sessions: an optional group, and a checklist per session (a template + one copy per termin).
-- Additive only: no existing row is touched. Idempotent.
ALTER TABLE "ExperimentalSession"
  ADD COLUMN IF NOT EXISTS "GroupId" INTEGER REFERENCES "ResearchGroup"("Id") ON DELETE SET NULL;

-- The template of a session's checklist: the items every termin of the session starts with.
CREATE TABLE IF NOT EXISTS "ExperimentalSessionChecklistItem" (
  "Id"                    SERIAL PRIMARY KEY,
  "ExperimentalSessionId" INTEGER NOT NULL REFERENCES "ExperimentalSession"("Id") ON DELETE CASCADE,
  "SortOrder"             INTEGER NOT NULL DEFAULT 0,
  "Text"                  VARCHAR(300) NOT NULL,
  "CreatedAt"             TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX IF NOT EXISTS "IX_ESChecklistItem_Session" ON "ExperimentalSessionChecklistItem" ("ExperimentalSessionId", "SortOrder");

-- One checklist per termin (an "individual session": a time slot for a participant, a group or everyone); made when the termin is first listed.
CREATE TABLE IF NOT EXISTS "SessionSlotChecklist" (
  "Id"        SERIAL PRIMARY KEY,
  "SlotId"    INTEGER NOT NULL REFERENCES "SessionSlot"("Id") ON DELETE CASCADE,
  "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  CONSTRAINT "UX_SessionSlotChecklist" UNIQUE ("SlotId")
);

CREATE TABLE IF NOT EXISTS "SessionSlotChecklistItem" (
  "Id"                 SERIAL PRIMARY KEY,
  "ChecklistId"        INTEGER NOT NULL REFERENCES "SessionSlotChecklist"("Id") ON DELETE CASCADE,
  "TemplateItemId"     INTEGER REFERENCES "ExperimentalSessionChecklistItem"("Id") ON DELETE SET NULL,
  "SortOrder"          INTEGER NOT NULL DEFAULT 0,
  "Text"               VARCHAR(300) NOT NULL,
  "IsDone"             BOOLEAN NOT NULL DEFAULT FALSE,
  "DoneAt"             TIMESTAMPTZ,
  "DoneByResearcherId" INTEGER REFERENCES "Researcher"("Id") ON DELETE SET NULL
);
CREATE INDEX IF NOT EXISTS "IX_SessionSlotChecklistItem_List" ON "SessionSlotChecklistItem" ("ChecklistId", "SortOrder");
