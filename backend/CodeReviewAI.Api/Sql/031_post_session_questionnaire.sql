-- Post-session upitnik (2026-10-01) — kratak upitnik (5 pitanja: 3 fiksna Likert 1-5 + 1 Likert
-- koji varira po tipu sesije + 1 slobodan tekst, opciono) koji se popunjava ODMAH posle NASA-TLX
-- skale, PRE nego što se sesija stvarno smatra završenom (ParticipantSession.IsFinished se sada
-- flip-uje tek po predaji ovog upitnika — videti NASA-TLX's PostSessionComponent). Pitanja su
-- fiksna u kodu (NE admin-konfigurabilna, za razliku od Demografskog upitnika iz 030) — duplirana
-- u Nasa-TLX-FullImplementation-AndrejKatin (forma) i admin-dashboard-andrejkatin (modal za
-- čitanje), isti "duplirano po app-u" obrazac kao svuda u ovom projektu.
--
-- "Answers" oblik: {"q1": 1-5, "q2": 1-5, "q3": 1-5, "q4": 1-5, "q5": "<tekst>" | null}.
-- Jedan odgovor po učesniku po sesiji (UNIQUE ParticipantGuid+SessionId) — isti oblik kao
-- TlxResult, re-submit prepisuje (ON CONFLICT ... DO UPDATE), ne duplira red.
--
-- Koristi se isti set_participant_guid() trigger koji je već instaliran u lokalnoj bazi i
-- korišćen za HybridSectionEngagement (025_hybrid_mode.sql) — ovde se samo kači na novu tabelu,
-- funkcija sama se ne redefiniše.
--
-- LOCAL DEV ONLY — isto kao svaka druga migracija trenutno u razvoju (025/026/028/029/030) — ne
-- primenjivati na produkcioni Neon dok feature ne bude uživo verifikovan.
--
-- Idempotentno — bezbedno za ponovno pokretanje. Pokrenuti posle 030_demographic_questionnaire.sql.

CREATE TABLE IF NOT EXISTS "PostSessionResponse" (
  "Id"              SERIAL PRIMARY KEY,
  "ParticipantId"   TEXT NOT NULL,
  "ParticipantGuid" UUID NOT NULL REFERENCES "Participant"("Guid"),
  "SessionId"       INTEGER NOT NULL REFERENCES "Sessions"("Id"),
  "Language"        VARCHAR(2) NOT NULL,
  "Answers"         JSONB NOT NULL,
  "CompletedAt"     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  UNIQUE ("ParticipantGuid", "SessionId")
);

CREATE INDEX IF NOT EXISTS "IX_PostSessionResponse_Participant_Session"
  ON "PostSessionResponse" ("ParticipantId", "SessionId");

DROP TRIGGER IF EXISTS trg_set_participant_guid ON "PostSessionResponse";
CREATE TRIGGER trg_set_participant_guid
  BEFORE INSERT ON "PostSessionResponse"
  FOR EACH ROW EXECUTE FUNCTION set_participant_guid();
