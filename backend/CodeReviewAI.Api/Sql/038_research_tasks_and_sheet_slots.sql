-- Second assignment factor ("task", e.g. A / B) and several sheet templates per research (2026-10-30).
-- Additive and idempotent. Only PROTOCOL researches use any of it; nothing existing changes meaning:
-- a research with one workbook (slot NULL/NULL) works exactly as before.

-- The tasks of a research. A participant gets ONE of them (balanced inside the group), the way he gets one group.
CREATE TABLE IF NOT EXISTS "ResearchTask" (
    "Id"          SERIAL PRIMARY KEY,
    "ResearchId"  INT NOT NULL REFERENCES "Research"("Id") ON DELETE CASCADE,
    "SortOrder"   INT NOT NULL DEFAULT 0,
    "Name"        VARCHAR(100) NOT NULL,
    "Weight"      INT NOT NULL DEFAULT 1,      -- relative size for balanced random assignment inside a group
    "CreatedAt"   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CHECK ("Weight" BETWEEN 1 AND 100)
);
CREATE UNIQUE INDEX IF NOT EXISTS "UX_ResearchTask_NamePerResearch" ON "ResearchTask" ("ResearchId", LOWER("Name"));

ALTER TABLE "Participant" ADD COLUMN IF NOT EXISTS "ResearchTaskId" INT;
ALTER TABLE "Participant" ADD COLUMN IF NOT EXISTS "TaskAssignedBy" VARCHAR(20);
ALTER TABLE "Participant" ADD COLUMN IF NOT EXISTS "TaskAssignedAt" TIMESTAMPTZ;

DO $$ BEGIN
    ALTER TABLE "Participant" ADD CONSTRAINT "Participant_ResearchTaskId_fkey"
        FOREIGN KEY ("ResearchTaskId") REFERENCES "ResearchTask"("Id") ON DELETE RESTRICT;
EXCEPTION WHEN duplicate_object THEN NULL; END $$;

DO $$ BEGIN
    ALTER TABLE "Participant" ADD CONSTRAINT "Participant_TaskAssignedBy_check"
        CHECK ("TaskAssignedBy" IS NULL OR "TaskAssignedBy" IN ('RESEARCHER','RANDOM'));
EXCEPTION WHEN duplicate_object THEN NULL; END $$;

CREATE INDEX IF NOT EXISTS "IX_Participant_Task" ON "Participant" ("ResearchTaskId") WHERE "ResearchTaskId" IS NOT NULL;

-- A random run records everything it was computed from, so it can be reproduced exactly.
ALTER TABLE "GroupAssignmentRun" ADD COLUMN IF NOT EXISTS "Params" JSONB;

-- Several templates per research. A template is a "slot": a (group, task) pair; NULL means "any".
ALTER TABLE "SheetWorkbook" DROP CONSTRAINT IF EXISTS "SheetWorkbook_ResearchId_key";
ALTER TABLE "SheetWorkbook" ADD COLUMN IF NOT EXISTS "Label" VARCHAR(100);
ALTER TABLE "SheetWorkbook" ADD COLUMN IF NOT EXISTS "GroupId" INT;
ALTER TABLE "SheetWorkbook" ADD COLUMN IF NOT EXISTS "ResearchTaskId" INT;
ALTER TABLE "SheetWorkbook" ADD COLUMN IF NOT EXISTS "SortOrder" INT NOT NULL DEFAULT 0;

DO $$ BEGIN
    ALTER TABLE "SheetWorkbook" ADD CONSTRAINT "SheetWorkbook_GroupId_fkey"
        FOREIGN KEY ("GroupId") REFERENCES "ResearchGroup"("Id") ON DELETE RESTRICT;
EXCEPTION WHEN duplicate_object THEN NULL; END $$;

DO $$ BEGIN
    ALTER TABLE "SheetWorkbook" ADD CONSTRAINT "SheetWorkbook_ResearchTaskId_fkey"
        FOREIGN KEY ("ResearchTaskId") REFERENCES "ResearchTask"("Id") ON DELETE RESTRICT;
EXCEPTION WHEN duplicate_object THEN NULL; END $$;

CREATE INDEX IF NOT EXISTS "IX_SheetWorkbook_Research" ON "SheetWorkbook" ("ResearchId", "SortOrder", "Id");
-- One template per (group, task) inside a research.
CREATE UNIQUE INDEX IF NOT EXISTS "UX_SheetWorkbook_Slot"
    ON "SheetWorkbook" ("ResearchId", COALESCE("GroupId", 0), COALESCE("ResearchTaskId", 0));
