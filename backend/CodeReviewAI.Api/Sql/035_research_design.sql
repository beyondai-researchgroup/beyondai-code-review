-- 035_research_design.sql
-- Research design (type of study), variables and participant groups for PROTOCOL researches (2026-10-28).
--
-- Purely additive and idempotent. Nothing here changes behaviour for an existing research:
--   * Research.SetupComplete defaults to TRUE, so every research that exists today counts as fully set up;
--     only a research created through the study wizard starts with FALSE until the wizard is finished.
--   * The design / variable / group tables are only ever read or written by routes that refuse a CLASSIC
--     research (NOT_PROTOCOL), exactly like the protocol routes.
--   * Participant.GroupId is NULL for every participant that exists today.
--
-- Design values are VARCHAR + CHECK (like FlowMode / StepType), every field is optional (NULL = "not specified").

ALTER TABLE "Research" ADD COLUMN IF NOT EXISTS "SetupComplete" BOOLEAN NOT NULL DEFAULT TRUE;

CREATE TABLE IF NOT EXISTS "ResearchDesign" (
    "ResearchId"       INT PRIMARY KEY REFERENCES "Research"("Id") ON DELETE CASCADE,
    "Approach"         VARCHAR(30),   -- EXPERIMENTAL | QUASI_EXPERIMENTAL | CORRELATIONAL | DESCRIPTIVE
    "SubjectDesign"    VARCHAR(30),   -- BETWEEN | WITHIN | MIXED | SINGLE_GROUP
    "TimeDimension"    VARCHAR(30),   -- CROSS_SECTIONAL | PRE_POST | REPEATED | CROSSOVER
    "FactorStructure"  VARCHAR(30),   -- SINGLE_FACTOR | FACTORIAL
    "ControlCondition" VARCHAR(30),   -- NONE | NO_TREATMENT | PLACEBO_ACTIVE | WAITLIST | TREATMENT_AS_USUAL
    "Blinding"         VARCHAR(30),   -- NONE | SINGLE | DOUBLE
    "AssignmentMode"   VARCHAR(20) NOT NULL DEFAULT 'MANUAL',
    "Hypotheses"       TEXT,
    "Notes"            TEXT,
    "UpdatedAt"        TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CHECK ("Approach"         IS NULL OR "Approach"         IN ('EXPERIMENTAL','QUASI_EXPERIMENTAL','CORRELATIONAL','DESCRIPTIVE')),
    CHECK ("SubjectDesign"    IS NULL OR "SubjectDesign"    IN ('BETWEEN','WITHIN','MIXED','SINGLE_GROUP')),
    CHECK ("TimeDimension"    IS NULL OR "TimeDimension"    IN ('CROSS_SECTIONAL','PRE_POST','REPEATED','CROSSOVER')),
    CHECK ("FactorStructure"  IS NULL OR "FactorStructure"  IN ('SINGLE_FACTOR','FACTORIAL')),
    CHECK ("ControlCondition" IS NULL OR "ControlCondition" IN ('NONE','NO_TREATMENT','PLACEBO_ACTIVE','WAITLIST','TREATMENT_AS_USUAL')),
    CHECK ("Blinding"         IS NULL OR "Blinding"         IN ('NONE','SINGLE','DOUBLE')),
    CHECK ("AssignmentMode" IN ('MANUAL','RANDOM'))
);

CREATE TABLE IF NOT EXISTS "ResearchVariable" (
    "Id"          SERIAL PRIMARY KEY,
    "ResearchId"  INT NOT NULL REFERENCES "Research"("Id") ON DELETE CASCADE,
    "SortOrder"   INT NOT NULL DEFAULT 0,
    "Role"        VARCHAR(20) NOT NULL,
    "Name"        VARCHAR(200) NOT NULL,
    "Description" VARCHAR(2000),
    "Levels"      JSONB NOT NULL DEFAULT '[]'::jsonb,   -- independent variables: e.g. ["control","treatment"]
    "Measurement" VARCHAR(1000),                         -- how it is manipulated / measured
    CHECK ("Role" IN ('INDEPENDENT','DEPENDENT','CONTROL','MODERATOR','MEDIATOR','COVARIATE'))
);
CREATE INDEX IF NOT EXISTS "IX_ResearchVariable_Research" ON "ResearchVariable" ("ResearchId", "SortOrder");

CREATE TABLE IF NOT EXISTS "ResearchGroup" (
    "Id"          SERIAL PRIMARY KEY,
    "ResearchId"  INT NOT NULL REFERENCES "Research"("Id") ON DELETE CASCADE,
    "SortOrder"   INT NOT NULL DEFAULT 0,
    "Name"        VARCHAR(100) NOT NULL,
    "Description" VARCHAR(1000),
    "Weight"      INT NOT NULL DEFAULT 1,     -- relative size for balanced random assignment (1:1, 2:1, ...)
    "IsControl"   BOOLEAN NOT NULL DEFAULT FALSE,
    CHECK ("Weight" BETWEEN 1 AND 100)
);
CREATE UNIQUE INDEX IF NOT EXISTS "UX_ResearchGroup_NamePerResearch" ON "ResearchGroup" ("ResearchId", LOWER("Name"));

-- One row per random assignment run, so the exact allocation can be reproduced from the seed.
CREATE TABLE IF NOT EXISTS "GroupAssignmentRun" (
    "Id"          SERIAL PRIMARY KEY,
    "ResearchId"  INT NOT NULL REFERENCES "Research"("Id") ON DELETE CASCADE,
    "Method"      VARCHAR(30) NOT NULL DEFAULT 'BALANCED',
    "Seed"        VARCHAR(64) NOT NULL,
    "Weights"     JSONB NOT NULL,
    "Count"       INT NOT NULL,
    "CreatedBy"   INT,
    "CreatedAt"   TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

ALTER TABLE "Participant" ADD COLUMN IF NOT EXISTS "GroupId" INT;
ALTER TABLE "Participant" ADD COLUMN IF NOT EXISTS "GroupAssignedBy" VARCHAR(20);
ALTER TABLE "Participant" ADD COLUMN IF NOT EXISTS "GroupAssignedAt" TIMESTAMPTZ;
ALTER TABLE "Participant" ADD COLUMN IF NOT EXISTS "GroupAssignmentRunId" INT;

DO $$ BEGIN
    ALTER TABLE "Participant" ADD CONSTRAINT "Participant_GroupId_fkey"
        FOREIGN KEY ("GroupId") REFERENCES "ResearchGroup"("Id") ON DELETE RESTRICT;
EXCEPTION WHEN duplicate_object THEN NULL; END $$;

DO $$ BEGIN
    ALTER TABLE "Participant" ADD CONSTRAINT "Participant_GroupAssignmentRunId_fkey"
        FOREIGN KEY ("GroupAssignmentRunId") REFERENCES "GroupAssignmentRun"("Id") ON DELETE SET NULL;
EXCEPTION WHEN duplicate_object THEN NULL; END $$;

DO $$ BEGIN
    ALTER TABLE "Participant" ADD CONSTRAINT "Participant_GroupAssignedBy_check"
        CHECK ("GroupAssignedBy" IS NULL OR "GroupAssignedBy" IN ('RESEARCHER','RANDOM'));
EXCEPTION WHEN duplicate_object THEN NULL; END $$;

CREATE INDEX IF NOT EXISTS "IX_Participant_Group" ON "Participant" ("GroupId") WHERE "GroupId" IS NOT NULL;
