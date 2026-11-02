-- Queue for the e-mails of the research protocol and the sheets links (2026-10-30). The free mail relay allows only about 100
-- recipients a day and a serverless function lives 60 s, so a bulk send cannot simply loop over sendMail: messages are
-- written here first and a drain job sends them within the daily limit, retrying later what the relay refused.
CREATE TABLE IF NOT EXISTS "EmailOutbox" (
    "Id"              BIGSERIAL PRIMARY KEY,
    "ResearchId"      INT REFERENCES "Research"("Id") ON DELETE CASCADE,
    "ParticipantGuid" UUID,
    "StepId"          INT,
    "ToEmail"         VARCHAR(255) NOT NULL,
    "Subject"         VARCHAR(300) NOT NULL,
    "Html"            TEXT NOT NULL,
    "FromName"        VARCHAR(150),
    "Status"          VARCHAR(10) NOT NULL DEFAULT 'PENDING',      -- PENDING | SENDING | SENT | FAILED
    "Attempts"        INT NOT NULL DEFAULT 0,
    "NextAttemptAt"   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "ClaimedAt"       TIMESTAMPTZ,                                  -- when a drain took it (a stuck SENDING is retried)
    "LastError"       TEXT,
    "CreatedAt"       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "SentAt"          TIMESTAMPTZ,
    CHECK ("Status" IN ('PENDING','SENDING','SENT','FAILED'))
);

CREATE INDEX IF NOT EXISTS "IX_EmailOutbox_Due" ON "EmailOutbox" ("Status", "NextAttemptAt");
CREATE INDEX IF NOT EXISTS "IX_EmailOutbox_Research" ON "EmailOutbox" ("ResearchId", "Status");
CREATE INDEX IF NOT EXISTS "IX_EmailOutbox_Sent" ON "EmailOutbox" ("SentAt") WHERE "Status" = 'SENT';
