-- 032: re-enable the free-text field on "Drugo / Other" demographic options.
--
-- Pilot feedback (2026-10-13): the demographic questionnaire offered "Drugo" without a field to
-- type what "other" means. In production, research 90's questions had been re-saved from the
-- admin page with the "Other, please specify" switch off on all three "Drugo" options, so the
-- participant app never showed the text field. Local Postgres was not affected.
--
-- Idempotent: only flips options that are still off. Run on production before the next deploy.

UPDATE "DemographicQuestionOption" o
SET "IsOtherSpecify" = TRUE
FROM "DemographicQuestion" q
WHERE q."Id" = o."QuestionId"
  AND o."IsOtherSpecify" = FALSE
  AND (o."LabelSr" ILIKE 'Drugo%' OR o."LabelEn" ILIKE 'Other%');

-- Check: every "Drugo" option should now report TRUE.
SELECT q."ResearchId", q."SortOrder" AS question, o."LabelSr", o."IsOtherSpecify"
FROM "DemographicQuestionOption" o
JOIN "DemographicQuestion" q ON q."Id" = o."QuestionId"
WHERE o."LabelSr" ILIKE 'Drugo%' OR o."LabelEn" ILIKE 'Other%'
ORDER BY 1, 2;
