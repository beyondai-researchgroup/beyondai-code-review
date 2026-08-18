-- Lets a specific (Participant, Session) pair be pinned to a specific PR config instead of
-- always falling back to whichever "ReplicationPrConfig" row is currently marked IsActive for the
-- whole replication. This is what makes "several PRs per replication, different participants (or
-- sessions) reviewing different PRs concurrently" actually usable — previously a replication could
-- store several PR configs, but only the active one was ever resolved for any participant.
--
-- NULL (the default, and the value on every pre-existing row) means "no explicit override — use
-- the replication's active config", exactly today's behavior. Fully backward compatible.
--
-- Idempotent — safe to re-run. Run after 005_eeg_support.sql.

ALTER TABLE "ParticipantSession" ADD COLUMN IF NOT EXISTS "PrConfigId" INTEGER REFERENCES "ReplicationPrConfig"("Id");

CREATE INDEX IF NOT EXISTS "IX_ParticipantSession_PrConfigId" ON "ParticipantSession" ("PrConfigId");
