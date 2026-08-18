-- Replaces the single GitHubOwner/GitHubRepo/GitHubPrNumber/GitHubToken columns on
-- "Replication" with a proper one-to-many "ReplicationPrConfig" table: a replication can now
-- have several configured PRs (added over time via the Admin Dashboard), with exactly one
-- marked active — the one BeyondAI's StudyService actually resolves for participants. This is
-- also what makes "no remembered PAT" possible: adding a new PR config is always a fresh insert
-- with its own token, never an in-place edit that could pre-fill/leak a previous token.
--
-- Idempotent — safe to re-run. Run after 003_replication_scoping.sql.

CREATE TABLE IF NOT EXISTS "ReplicationPrConfig" (
  "Id"             SERIAL PRIMARY KEY,
  "ReplicationId"  INTEGER NOT NULL REFERENCES "Replication"("Id"),
  "GitHubOwner"    VARCHAR(200) NOT NULL,
  "GitHubRepo"     VARCHAR(200) NOT NULL,
  "GitHubPrNumber" INTEGER NOT NULL,
  "GitHubToken"    TEXT NOT NULL,
  "IsActive"       BOOLEAN NOT NULL DEFAULT FALSE,
  "CreatedAt"      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- At most one active config per replication, enforced at the DB level (partial unique index).
CREATE UNIQUE INDEX IF NOT EXISTS "UX_ReplicationPrConfig_ActivePerReplication"
  ON "ReplicationPrConfig" ("ReplicationId")
  WHERE "IsActive";

CREATE INDEX IF NOT EXISTS "IX_ReplicationPrConfig_ReplicationId" ON "ReplicationPrConfig" ("ReplicationId");

-- One-time data migration: carry over each replication's existing single PR config (if it had
-- one) as its initial active row. Guarded by NOT EXISTS so re-running this script is a no-op
-- once migrated.
INSERT INTO "ReplicationPrConfig" ("ReplicationId", "GitHubOwner", "GitHubRepo", "GitHubPrNumber", "GitHubToken", "IsActive")
SELECT r."Id", r."GitHubOwner", r."GitHubRepo", r."GitHubPrNumber", r."GitHubToken", TRUE
FROM "Replication" r
WHERE r."GitHubOwner" IS NOT NULL AND r."GitHubRepo" IS NOT NULL
  AND r."GitHubPrNumber" IS NOT NULL AND r."GitHubToken" IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM "ReplicationPrConfig" prc WHERE prc."ReplicationId" = r."Id");

-- The old single-config columns are now redundant — drop them so there's exactly one source
-- of truth (StudyService and the Admin Dashboard both read/write "ReplicationPrConfig" only).
ALTER TABLE "Replication" DROP COLUMN IF EXISTS "GitHubOwner";
ALTER TABLE "Replication" DROP COLUMN IF EXISTS "GitHubRepo";
ALTER TABLE "Replication" DROP COLUMN IF EXISTS "GitHubPrNumber";
ALTER TABLE "Replication" DROP COLUMN IF EXISTS "GitHubToken";
