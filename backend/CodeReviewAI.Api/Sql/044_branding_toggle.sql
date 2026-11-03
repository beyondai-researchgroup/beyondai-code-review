-- Per-research switch for the BeyondAI branding that participants see (logo + "BeyondAI Research Group" in the apps' header, the
-- brand line in participant emails). Additive and idempotent. Every existing research keeps its branding (default TRUE).
ALTER TABLE "Research" ADD COLUMN IF NOT EXISTS "ShowBeyondAiBranding" BOOLEAN NOT NULL DEFAULT TRUE;
