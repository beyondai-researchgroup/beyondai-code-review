-- Section headings inside a researcher-authored questionnaire: a new item type 'HEADING' (a title, no answer).
-- Only widens the CHECK on QuestionnaireItem.ItemType; no row is touched. Idempotent.
DO $$
DECLARE c RECORD;
BEGIN
  FOR c IN
    SELECT conname FROM pg_constraint
    WHERE conrelid = '"QuestionnaireItem"'::regclass AND contype = 'c' AND pg_get_constraintdef(oid) LIKE '%ItemType%'
  LOOP
    EXECUTE format('ALTER TABLE "QuestionnaireItem" DROP CONSTRAINT %I', c.conname);
  END LOOP;
  ALTER TABLE "QuestionnaireItem" ADD CONSTRAINT "QuestionnaireItem_ItemType_check"
    CHECK ("ItemType" IN ('LIKERT', 'SINGLE_CHOICE', 'MULTI_CHOICE', 'TEXT', 'NUMBER', 'HEADING'));
END $$;
