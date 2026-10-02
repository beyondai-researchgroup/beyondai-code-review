-- Demographic Questionnaire (2026-10-01) — a new standalone, magic-link-only sibling app
-- (demographics-andrejkatin, ports 4305 ng / 4315 API) delivering a one-time, per-participant,
-- admin-configurable demographic form (gender/employment/age/education/field/role/experience/
-- language/AI-tool usage+attitude+free-text). Sent in the same email as REI-40/Big Five, labeled
-- "Demografski upitnik"/"Demographic questionnaire" — outside the numbered "Upitnik N" sequence,
-- same treatment as GENERIC_TASK's "Zadatak" card.
--
-- "UsesDemographics" is an explicit, researcher-toggled, per-research boolean (same shape as
-- UsesPsychTests/UsesTlx) read once in admin-dashboard's regenerate-links/study-config and
-- consent-andrejkatin's /api/consent/submit — not a derived EXISTS(...) check (would silently
-- flip link-issuance mid-authoring) and not a per-participant signal like GenericTaskId.
--
-- DemographicQuestion/DemographicQuestionOption are per-research, admin-authored, bilingual,
-- reorderable — same shape as ConsentSection (017). DemographicResponse is one-shot per
-- participant (ParticipantGuid UNIQUE), same shape as Rei40Result/BigFiveResult/
-- GenericTaskSubmission. "IsOtherSpecify" lives on the OPTION (not the question) — an admin just
-- flags a normal option row, no question-level special-casing needed.
--
-- Research 1 (the live PR_REVIEW study) gets UsesDemographics = TRUE and all 12 default
-- questions + their options seeded directly below, same precedent as 017_consent_sections.sql's
-- 12-section research-1 seed. Any other research that turns the toggle on with zero authored
-- questions gets the same 12 lazily seeded by admin-dashboard-andrejkatin's
-- server/demographic-questions/defaults.mjs (server/demographic-questions/routes.mjs's
-- seedDefaultsIfEmpty) — dual approach, same as the Consent Form feature.
--
-- LOCAL DEV ONLY — do not run against production Neon until this feature has been verified live
-- (same policy as every other in-progress migration, 025/026/028/029).
--
-- Idempotent — safe to re-run. Run after 029_intro_email_sent_at.sql.

-- ── Research.UsesDemographics ───────────────────────────────────────────────────────────────
DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Research' AND column_name = 'UsesDemographics') THEN
    ALTER TABLE "Research" ADD COLUMN "UsesDemographics" BOOLEAN NOT NULL DEFAULT FALSE;
  END IF;
END $$;

-- ── DemographicQuestion / DemographicQuestionOption ─────────────────────────────────────────
CREATE TABLE IF NOT EXISTS "DemographicQuestion" (
  "Id"           SERIAL PRIMARY KEY,
  "ResearchId"   INTEGER NOT NULL REFERENCES "Research"("Id") ON DELETE CASCADE,
  "SortOrder"    INTEGER NOT NULL,
  "QuestionType" VARCHAR(20) NOT NULL CHECK ("QuestionType" IN ('TEXT', 'SINGLE_CHOICE')),
  "PromptSr"     TEXT NOT NULL,
  "PromptEn"     TEXT NOT NULL,
  "CreatedAt"    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  "UpdatedAt"    TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX IF NOT EXISTS "IX_DemographicQuestion_ResearchId" ON "DemographicQuestion" ("ResearchId", "SortOrder");

CREATE TABLE IF NOT EXISTS "DemographicQuestionOption" (
  "Id"             SERIAL PRIMARY KEY,
  "QuestionId"     INTEGER NOT NULL REFERENCES "DemographicQuestion"("Id") ON DELETE CASCADE,
  "SortOrder"      INTEGER NOT NULL,
  "LabelSr"        TEXT NOT NULL,
  "LabelEn"        TEXT NOT NULL,
  "IsOtherSpecify" BOOLEAN NOT NULL DEFAULT FALSE
);
CREATE INDEX IF NOT EXISTS "IX_DemographicQuestionOption_QuestionId" ON "DemographicQuestionOption" ("QuestionId", "SortOrder");

-- ── DemographicResponse: one-shot per participant ───────────────────────────────────────────
CREATE TABLE IF NOT EXISTS "DemographicResponse" (
  "Id"              SERIAL PRIMARY KEY,
  "ParticipantGuid" UUID NOT NULL UNIQUE REFERENCES "Participant"("Guid"),
  "Language"        VARCHAR(2) NOT NULL,
  "Answers"         JSONB NOT NULL,
  "CompletedAt"     TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ── SurveyAccessToken: widen SurveyType CHECK to add 'DEMOGRAPHIC' ──────────────────────────
ALTER TABLE "SurveyAccessToken" DROP CONSTRAINT IF EXISTS "SurveyAccessToken_SurveyType_check";
ALTER TABLE "SurveyAccessToken" ADD CONSTRAINT "SurveyAccessToken_SurveyType_check"
  CHECK ("SurveyType" IN ('REI40', 'BIGFIVE', 'NASA_TLX', 'CONSENT_ENTRY', 'GENERIC_TASK', 'CODE_REVIEW', 'TLX_HANDOFF', 'DEMOGRAPHIC'));

-- ── Research 1: turn the toggle on + seed the 12 default questions ─────────────────────────
DO $$
DECLARE
  qid INTEGER;
BEGIN
  UPDATE "Research" SET "UsesDemographics" = TRUE WHERE "Id" = 1;

  IF NOT EXISTS (SELECT 1 FROM "DemographicQuestion" WHERE "ResearchId" = 1) THEN

    -- Q1: Gender
    INSERT INTO "DemographicQuestion" ("ResearchId","SortOrder","QuestionType","PromptSr","PromptEn")
      VALUES (1, 0, 'SINGLE_CHOICE', 'Pol', 'Gender') RETURNING "Id" INTO qid;
    INSERT INTO "DemographicQuestionOption" ("QuestionId","SortOrder","LabelSr","LabelEn","IsOtherSpecify") VALUES
      (qid, 0, 'Muški', 'Male', FALSE),
      (qid, 1, 'Ženski', 'Female', FALSE),
      (qid, 2, 'Ne želim da se izjasnim', 'Prefer not to say', FALSE);

    -- Q2: Employment status
    INSERT INTO "DemographicQuestion" ("ResearchId","SortOrder","QuestionType","PromptSr","PromptEn")
      VALUES (1, 1, 'SINGLE_CHOICE', 'Trenutni radni angažman', 'Current employment status') RETURNING "Id" INTO qid;
    INSERT INTO "DemographicQuestionOption" ("QuestionId","SortOrder","LabelSr","LabelEn","IsOtherSpecify") VALUES
      (qid, 0, 'Zaposlen', 'Employed', FALSE),
      (qid, 1, 'Student', 'Student', FALSE),
      (qid, 2, 'Nezaposlen', 'Unemployed', FALSE);

    -- Q3: Age bracket
    INSERT INTO "DemographicQuestion" ("ResearchId","SortOrder","QuestionType","PromptSr","PromptEn")
      VALUES (1, 2, 'SINGLE_CHOICE', 'Starosna kategorija', 'Age bracket') RETURNING "Id" INTO qid;
    INSERT INTO "DemographicQuestionOption" ("QuestionId","SortOrder","LabelSr","LabelEn","IsOtherSpecify") VALUES
      (qid, 0, '18-24', '18-24', FALSE),
      (qid, 1, '25-34', '25-34', FALSE),
      (qid, 2, '35-44', '35-44', FALSE),
      (qid, 3, 'Više od 44', 'Over 44', FALSE);

    -- Q4: Education
    INSERT INTO "DemographicQuestion" ("ResearchId","SortOrder","QuestionType","PromptSr","PromptEn")
      VALUES (1, 3, 'SINGLE_CHOICE', 'Najviši završeni stepen obrazovanja', 'Highest completed level of education') RETURNING "Id" INTO qid;
    INSERT INTO "DemographicQuestionOption" ("QuestionId","SortOrder","LabelSr","LabelEn","IsOtherSpecify") VALUES
      (qid, 0, 'Srednja škola', 'High school', FALSE),
      (qid, 1, 'Akademske studije', 'Bachelor''s studies', FALSE),
      (qid, 2, 'Master studije', 'Master''s studies', FALSE),
      (qid, 3, 'Doktorske studije', 'Doctoral studies', FALSE);

    -- Q5: Field of study/work
    INSERT INTO "DemographicQuestion" ("ResearchId","SortOrder","QuestionType","PromptSr","PromptEn")
      VALUES (1, 4, 'SINGLE_CHOICE', 'Primarna oblast studija/rada', 'Primary field of study or work') RETURNING "Id" INTO qid;
    INSERT INTO "DemographicQuestionOption" ("QuestionId","SortOrder","LabelSr","LabelEn","IsOtherSpecify") VALUES
      (qid, 0, 'Računarstvo', 'Computer Science', FALSE),
      (qid, 1, 'Informacioni sistemi', 'Information Systems', FALSE),
      (qid, 2, 'Elektrotehnika', 'Electrical Engineering', FALSE),
      (qid, 3, 'Matematika', 'Mathematics', FALSE),
      (qid, 4, 'Drugo (navedite)', 'Other (please specify)', TRUE);

    -- Q6: Current role
    INSERT INTO "DemographicQuestion" ("ResearchId","SortOrder","QuestionType","PromptSr","PromptEn")
      VALUES (1, 5, 'SINGLE_CHOICE', 'Trenutna primarna uloga (ukoliko ste zaposleni)', 'Current primary role (if employed)') RETURNING "Id" INTO qid;
    INSERT INTO "DemographicQuestionOption" ("QuestionId","SortOrder","LabelSr","LabelEn","IsOtherSpecify") VALUES
      (qid, 0, 'Backend Developer', 'Backend Developer', FALSE),
      (qid, 1, 'Frontend Developer', 'Frontend Developer', FALSE),
      (qid, 2, 'DevOps', 'DevOps', FALSE),
      (qid, 3, 'QA Engineer', 'QA Engineer', FALSE),
      (qid, 4, 'Software Architect', 'Software Architect', FALSE),
      (qid, 5, 'Data Scientist', 'Data Scientist', FALSE),
      (qid, 6, 'Business Analyst', 'Business Analyst', FALSE),
      (qid, 7, 'Project Manager', 'Project Manager', FALSE),
      (qid, 8, 'Drugo', 'Other', TRUE);

    -- Q7: Years of programming experience
    INSERT INTO "DemographicQuestion" ("ResearchId","SortOrder","QuestionType","PromptSr","PromptEn")
      VALUES (1, 6, 'SINGLE_CHOICE', 'Godine iskustva u programiranju (uključujući studije)', 'Years of programming experience (including studies)') RETURNING "Id" INTO qid;
    INSERT INTO "DemographicQuestionOption" ("QuestionId","SortOrder","LabelSr","LabelEn","IsOtherSpecify") VALUES
      (qid, 0, 'Manje od 3 godine', 'Less than 3 years', FALSE),
      (qid, 1, '3-5 godina', '3-5 years', FALSE),
      (qid, 2, '6-8 godina', '6-8 years', FALSE),
      (qid, 3, '9-11 godina', '9-11 years', FALSE),
      (qid, 4, 'Više od 11 godina', 'More than 11 years', FALSE);

    -- Q8: Years of professional experience
    INSERT INTO "DemographicQuestion" ("ResearchId","SortOrder","QuestionType","PromptSr","PromptEn")
      VALUES (1, 7, 'SINGLE_CHOICE', 'Godine profesionalnog (industrijskog) iskustva', 'Years of professional (industry) experience') RETURNING "Id" INTO qid;
    INSERT INTO "DemographicQuestionOption" ("QuestionId","SortOrder","LabelSr","LabelEn","IsOtherSpecify") VALUES
      (qid, 0, 'Manje od 3 godine', 'Less than 3 years', FALSE),
      (qid, 1, '3-5 godina', '3-5 years', FALSE),
      (qid, 2, '6-9 godina', '6-9 years', FALSE),
      (qid, 3, 'Više od 9 godina', 'More than 9 years', FALSE);

    -- Q9: Dominant programming language
    INSERT INTO "DemographicQuestion" ("ResearchId","SortOrder","QuestionType","PromptSr","PromptEn")
      VALUES (1, 8, 'SINGLE_CHOICE', 'Dominantan programski jezik', 'Dominant programming language') RETURNING "Id" INTO qid;
    INSERT INTO "DemographicQuestionOption" ("QuestionId","SortOrder","LabelSr","LabelEn","IsOtherSpecify") VALUES
      (qid, 0, 'C#', 'C#', FALSE),
      (qid, 1, 'Java', 'Java', FALSE),
      (qid, 2, 'Python', 'Python', FALSE),
      (qid, 3, 'JavaScript', 'JavaScript', FALSE),
      (qid, 4, 'TypeScript', 'TypeScript', FALSE),
      (qid, 5, 'C++', 'C++', FALSE),
      (qid, 6, 'Drugo', 'Other', TRUE);

    -- Q10: AI tool usage frequency
    INSERT INTO "DemographicQuestion" ("ResearchId","SortOrder","QuestionType","PromptSr","PromptEn")
      VALUES (1, 9, 'SINGLE_CHOICE', 'Učestalost korišćenja AI alata u radu', 'Frequency of using AI tools at work') RETURNING "Id" INTO qid;
    INSERT INTO "DemographicQuestionOption" ("QuestionId","SortOrder","LabelSr","LabelEn","IsOtherSpecify") VALUES
      (qid, 0, 'Nikada', 'Never', FALSE),
      (qid, 1, 'Retko (par puta mesečno)', 'Rarely (a few times a month)', FALSE),
      (qid, 2, 'Povremeno (par puta nedeljno)', 'Occasionally (a few times a week)', FALSE),
      (qid, 3, 'Svakodnevno', 'Daily', FALSE);

    -- Q11: Attitude toward AI tools
    INSERT INTO "DemographicQuestion" ("ResearchId","SortOrder","QuestionType","PromptSr","PromptEn")
      VALUES (1, 10, 'SINGLE_CHOICE', 'Stav prema upotrebi AI alata u programiranju', 'Attitude toward using AI tools in programming') RETURNING "Id" INTO qid;
    INSERT INTO "DemographicQuestionOption" ("QuestionId","SortOrder","LabelSr","LabelEn","IsOtherSpecify") VALUES
      (qid, 0, 'AI alati su odlična podrška programerima.', 'AI tools are an excellent support for programmers.', FALSE),
      (qid, 1, 'AI alati mogu biti od pomoći programerima, ali uz obaveznu kontrolu.', 'AI tools can help programmers, but require mandatory oversight.', FALSE),
      (qid, 2, 'Koliko pomažu, toliko i otežavaju programiranje.', 'They help as much as they hinder programming.', FALSE),
      (qid, 3, 'AI alati retko kada pomažu programerima.', 'AI tools rarely help programmers.', FALSE),
      (qid, 4, 'AI alati ne pružaju nikakvu podršku i olakšanje programerima.', 'AI tools provide no support or relief for programmers.', FALSE);

    -- Q12: Free-text explanation (TEXT, no options)
    INSERT INTO "DemographicQuestion" ("ResearchId","SortOrder","QuestionType","PromptSr","PromptEn")
      VALUES (1, 11, 'TEXT', 'Ukratko obrazložite Vaš stav o upotrebi AI alata u programiranju.', 'Briefly explain your attitude toward the use of AI tools in programming.');

  END IF;
END $$;
