-- Attributes every participant and every session instance to a Replication. Idempotent — safe
-- to re-run. Run after 002_replication_and_researcher.sql (needs the "Replication" table and its
-- seeded row 1 to backfill against).
--
-- ReplicationId lives on BOTH "Participant" (source of truth, set at import time) AND
-- "ParticipantSession" (denormalized copy) rather than only on "Participant": the user explicitly
-- asked that every *session* — not just every participant — record its own replication, since a
-- future scenario could see a replication's session setup change independently of a participant's
-- own assignment. Denormalizing also means every dashboard query that scopes by replication can
-- filter "ParticipantSession" directly without an extra join.

ALTER TABLE "Participant" ADD COLUMN IF NOT EXISTS "ReplicationId" INTEGER REFERENCES "Replication"("Id");
UPDATE "Participant" SET "ReplicationId" = 1 WHERE "ReplicationId" IS NULL;
ALTER TABLE "Participant" ALTER COLUMN "ReplicationId" SET NOT NULL;

ALTER TABLE "ParticipantSession" ADD COLUMN IF NOT EXISTS "ReplicationId" INTEGER REFERENCES "Replication"("Id");
UPDATE "ParticipantSession" ps
  SET "ReplicationId" = p."ReplicationId"
  FROM "Participant" p
  WHERE p."ParticipantId" = ps."ParticipantId" AND ps."ReplicationId" IS NULL;
ALTER TABLE "ParticipantSession" ALTER COLUMN "ReplicationId" SET NOT NULL;

-- Per-participant session ordering, so a batch import can swap AI(2)/Report(3) for counterbalancing
-- while Intro(1) always stays first. NULL (pre-existing rows) falls back to SessionId order via
-- StudyService's `ORDER BY COALESCE("SequenceOrder", "SessionId")`, so this backfill is really just
-- making that fallback explicit/queryable rather than strictly required for correctness.
ALTER TABLE "ParticipantSession" ADD COLUMN IF NOT EXISTS "SequenceOrder" SMALLINT;
UPDATE "ParticipantSession" SET "SequenceOrder" = "SessionId" WHERE "SequenceOrder" IS NULL;

CREATE INDEX IF NOT EXISTS "IX_Participant_ReplicationId" ON "Participant" ("ReplicationId");
CREATE INDEX IF NOT EXISTS "IX_ParticipantSession_ReplicationId" ON "ParticipantSession" ("ReplicationId");
