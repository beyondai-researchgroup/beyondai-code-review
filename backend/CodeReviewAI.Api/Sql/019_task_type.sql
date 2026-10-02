-- Task Configuration Phase 1: a research picks a "Task Type" — what kind of task its
-- participants' materials/results center on. Starts with just this one column; Phase 2 adds
-- GoogleFormsUrl/TaskInstructions + a TaskFile table, Phase 3 adds sandboxed R analysis.
--
-- Idempotent — safe to re-run (guarded column add). Run manually against Neon (psql
-- "$DATABASE_URL" -f Sql/019_task_type.sql, or paste into the Neon SQL console). Run after
-- 018_psych_tests_toggle.sql.

DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Research' AND column_name = 'TaskType') THEN
    ALTER TABLE "Research" ADD COLUMN "TaskType" VARCHAR(30) NOT NULL DEFAULT 'PR_REVIEW';
  END IF;
END $$;
