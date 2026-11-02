-- 041: a PDF per research task (2026-10-30). The researcher attaches the written task as a PDF to a task on the research design
-- page; the participant reads it in the sheets app (on the introduction page and again from the table page) before and while
-- working. Additive and idempotent. Stored in the row like the Generic Task PDF (BYTEA, up to 25 MB); the filename is kept only
-- for the researcher and is never sent to a participant.
ALTER TABLE "ResearchTask" ADD COLUMN IF NOT EXISTS "PdfFilename"  VARCHAR(255);
ALTER TABLE "ResearchTask" ADD COLUMN IF NOT EXISTS "PdfData"      BYTEA;
ALTER TABLE "ResearchTask" ADD COLUMN IF NOT EXISTS "PdfSize"      INTEGER;
ALTER TABLE "ResearchTask" ADD COLUMN IF NOT EXISTS "PdfUpdatedAt" TIMESTAMPTZ;
