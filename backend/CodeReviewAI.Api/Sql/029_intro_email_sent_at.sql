-- Supports the new cross-app email chain: Consent magic-link -> REI-40/Big Five -> automatic
-- "Intro" email (a CODE_REVIEW magic link into this app) once BOTH REI-40 and Big Five are
-- complete for a participant -> a session-aware "done" message in NASA-TLX once the Intro
-- session's own TLX scale is filled in.
--
-- "IntroEmailSentAt" is the atomic single-send guard: rei40-andrejkatin's and bigfive-andrejkatin's
-- own POST /api/result handlers each independently check "is the sibling instrument also done?"
-- after a successful submission, and since both could plausibly finish within moments of each
-- other, a plain `UPDATE ... WHERE "IntroEmailSentAt" IS NULL RETURNING ...` claim (only the
-- caller that actually flips NULL -> NOW() proceeds to send) is what prevents a participant from
-- ever receiving the Intro email twice.
--
-- LOCAL DEV ONLY for now, same policy as every other migration in this project currently in
-- progress (025/026/028) — do not run against production Neon until this whole feature has been
-- verified live.
--
-- Idempotent — safe to re-run. Run after 028_cloud_storage_keys.sql.

ALTER TABLE "Participant" ADD COLUMN IF NOT EXISTS "IntroEmailSentAt" TIMESTAMPTZ;
