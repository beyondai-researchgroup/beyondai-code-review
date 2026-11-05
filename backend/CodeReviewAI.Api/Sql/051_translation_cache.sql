-- Translations of research data for downloads (Serbian -> English), remembered per research so a repeated download
-- or a retry only translates what is still missing. Holds the hash of the source text and its translation (not the source).
-- Removed together with the research. Additive; idempotent.
CREATE TABLE IF NOT EXISTS "TranslationCache" (
  "ResearchId"  INTEGER NOT NULL REFERENCES "Research"("Id") ON DELETE CASCADE,
  "Lang"        VARCHAR(5) NOT NULL,
  "Hash"        CHAR(64) NOT NULL,
  "Translation" TEXT NOT NULL,
  "CreatedAt"   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  PRIMARY KEY ("ResearchId", "Lang", "Hash")
);
