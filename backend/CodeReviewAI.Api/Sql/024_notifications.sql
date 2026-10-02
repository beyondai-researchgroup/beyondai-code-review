-- In-app notification system (2026-08-20, admin-dashboard-andrejkatin follow-up project) — bell
-- icon + toast popups + dropdown list, triggered by participant imports, Experimental Session
-- creation, and a day-before-session reminder. See admin_dashboard_followup_2026_08_20 memory /
-- code-review-ai/CLAUDE.md for the full design.
--
-- "Message" stores pre-rendered display text (not an i18n key + params) — simplest given the
-- recipient's Language is now known at insert time (Researcher.Language, added in
-- 023_researcher_profiles.sql) but there's no client-side renderer for notification bodies.
--
-- Idempotent -- safe to re-run. Run manually against Neon (psql "$DATABASE_URL" -f
-- Sql/024_notifications.sql, or paste into the Neon SQL console). Run after
-- 023_researcher_profiles.sql.

CREATE TABLE IF NOT EXISTS "Notification" (
  "Id" SERIAL PRIMARY KEY,
  "ResearcherId" INTEGER NOT NULL REFERENCES "Researcher"("Id") ON DELETE CASCADE,
  "ResearchId" INTEGER REFERENCES "Research"("Id") ON DELETE CASCADE,
  "Type" VARCHAR(30) NOT NULL,
  "Message" TEXT NOT NULL,
  "IsRead" BOOLEAN NOT NULL DEFAULT FALSE,
  "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS "IX_Notification_ResearcherId_CreatedAt" ON "Notification" ("ResearcherId", "CreatedAt" DESC);

-- Used by the day-before-session reminder job's dedup guard (one SESSION_REMINDER per research
-- per day, even though the in-process setInterval ticks many times a day).
CREATE INDEX IF NOT EXISTS "IX_Notification_ResearchId_Type_CreatedAt" ON "Notification" ("ResearchId", "Type", "CreatedAt");
