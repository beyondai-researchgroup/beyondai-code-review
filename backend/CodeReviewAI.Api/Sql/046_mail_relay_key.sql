-- Per-research mail relay (2026-11-03): a research may send its participant mails through its own Apps Script relay
-- (another Google account), chosen by a short key. The relay URL and secret stay in environment variables
-- (MAIL_RELAY_URL_<KEY>, MAIL_RELAY_SECRET_<KEY>); the database holds only the key. NULL = the default relay.
ALTER TABLE "Research" ADD COLUMN IF NOT EXISTS "MailRelayKey" VARCHAR(30);
ALTER TABLE "EmailOutbox" ADD COLUMN IF NOT EXISTS "RelayKey" VARCHAR(30);
