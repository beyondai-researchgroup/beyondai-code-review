-- Phase C of platform-ification: per-research configurable Consent Form. A "ConsentSection"
-- table replaces the Consent app's fixed CONSENT.SECTION1..5 i18n text with per-research rows a
-- researcher can add/remove/reorder/edit through the Admin Dashboard's new "Konfiguracija
-- pristanka"/"Consent Form" page. "Research" also gains two nullable columns for the agreement
-- checkbox text — fixed at the end of the form structurally (never reorderable/removable, per
-- explicit confirmation), but its own text is still researcher-editable.
--
-- Idempotent — safe to re-run (CREATE TABLE IF NOT EXISTS, guarded column adds, ON CONFLICT
-- DO NOTHING seed). Run manually against Neon (psql "$DATABASE_URL" -f
-- Sql/017_consent_sections.sql, or paste into the Neon SQL console). Run after
-- 016_researcher_research_many_to_many.sql.

CREATE TABLE IF NOT EXISTS "ConsentSection" (
  "Id"         SERIAL PRIMARY KEY,
  "ResearchId" INTEGER NOT NULL REFERENCES "Research"("Id") ON DELETE CASCADE,
  "SortOrder"  INTEGER NOT NULL,
  "TitleSr"    TEXT, -- NULL = a continuation paragraph of the previous titled section, not
  "TitleEn"    TEXT, -- its own heading (e.g. today's fixed form has 3 untitled paragraphs
                      -- under "Dobrovoljnost učešća..." — split one-paragraph-per-section, the
                      -- 2nd/3rd keep no title of their own).
  "BodySr"     TEXT NOT NULL,
  "BodyEn"     TEXT NOT NULL,
  "CreatedAt"  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  "UpdatedAt"  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS "IX_ConsentSection_ResearchId" ON "ConsentSection" ("ResearchId", "SortOrder");

DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Research' AND column_name = 'ConsentCheckboxTextSr') THEN
    ALTER TABLE "Research" ADD COLUMN "ConsentCheckboxTextSr" TEXT;
  END IF;
  IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Research' AND column_name = 'ConsentCheckboxTextEn') THEN
    ALTER TABLE "Research" ADD COLUMN "ConsentCheckboxTextEn" TEXT;
  END IF;
END $$;

-- Seed the default 12 sections for the one research that exists today (Id=1) — text copied
-- verbatim from consent-andrejkatin's current fixed CONSENT.SECTION*/CHECKBOX_LABEL i18n keys.
-- Any research created after this migration gets the same defaults lazily, the first time its
-- Consent Form config page (or the Consent app itself) is loaded with zero ConsentSection rows —
-- see server/consent-sections/routes.mjs's seedDefaults() (admin-dashboard-andrejkatin) and the
-- matching duplicate in consent-andrejkatin/server.mjs.
DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM "ConsentSection" WHERE "ResearchId" = 1) THEN
    INSERT INTO "ConsentSection" ("ResearchId", "SortOrder", "TitleSr", "TitleEn", "BodySr", "BodyEn")
    VALUES (1, 0, 'Cilj istraživanja', 'Research Objective', 'Cilj ovog eksperimenta je da se ispita kako korišćenje alata podržanih veštačkom inteligencijom utiče na kognitivno opterećenje, stil razmišljanja i procenu profesionalnih zadataka kod programera. Vaše učešće nam pomaže u razumevanju interakcije između čoveka i AI sistema u domenu softverskog inženjerstva.', 'The aim of this experiment is to examine how the use of AI-assisted tools affects cognitive load, thinking style, and the evaluation of professional tasks among software developers. Your participation helps us understand the interaction between humans and AI systems in the domain of software engineering.');
    INSERT INTO "ConsentSection" ("ResearchId", "SortOrder", "TitleSr", "TitleEn", "BodySr", "BodyEn")
    VALUES (1, 1, 'Opis procedure', 'Procedure Description', 'Učešće u istraživanju podrazumeva jednu eksperimentalnu sesiju koja traje 45 do 60 minuta. Sesija će se odvijati u kontrolisanoj laboratoriji na Fakultetu tehničkih nauka u Novom Sadu.', 'Participation in the research involves a single experimental session lasting 45 to 60 minutes. The session will take place in a controlled laboratory at the Faculty of Technical Sciences in Novi Sad.');
    INSERT INTO "ConsentSection" ("ResearchId", "SortOrder", "TitleSr", "TitleEn", "BodySr", "BodyEn")
    VALUES (1, 2, 'Zadatak', 'Task', 'Biće Vam predstavljeni profesionalni zadaci u vezi sa procenom legacy koda (npr. provera ispravnosti Pull Request-ova), koje ćete rešavati uz korišćenje simuliranog AI asistenta.', 'You will be presented with professional tasks related to evaluating legacy code (e.g. reviewing Pull Requests), which you will solve using a simulated AI assistant.');
    INSERT INTO "ConsentSection" ("ResearchId", "SortOrder", "TitleSr", "TitleEn", "BodySr", "BodyEn")
    VALUES (1, 3, 'Merenja', 'Measurements', 'U toku rešavanja zadataka, biće korišćen EEG uređaj (Elektroencefalograf) za snimanje električne aktivnosti Vašeg mozga (moždanih talasa). Pored toga, bićete zamoljeni da popunite kratke upitnike za samoprocenu.', 'While completing the tasks, an EEG device (electroencephalograph) will be used to record the electrical activity of your brain (brainwaves). In addition, you will be asked to fill out short self-assessment questionnaires.');
    INSERT INTO "ConsentSection" ("ResearchId", "SortOrder", "TitleSr", "TitleEn", "BodySr", "BodyEn")
    VALUES (1, 4, 'Korišćenje EEG-a', 'Use of EEG', 'EEG je neinvazivan i potpuno bezbedan metod. Postavlja se na kožu glave i ne izaziva nikakvu bol.', 'EEG is a non-invasive and completely safe method. It is placed on the scalp and causes no pain.');
    INSERT INTO "ConsentSection" ("ResearchId", "SortOrder", "TitleSr", "TitleEn", "BodySr", "BodyEn")
    VALUES (1, 5, 'Dobrovoljnost učešća i pravo na odustajanje', 'Voluntary Participation and Right to Withdraw', 'Vaše učešće je potpuno dobrovoljno.', 'Your participation is entirely voluntary.');
    INSERT INTO "ConsentSection" ("ResearchId", "SortOrder", "TitleSr", "TitleEn", "BodySr", "BodyEn")
    VALUES (1, 6, NULL, NULL, 'Imate puno pravo da u svakom trenutku prekinete eksperiment, bez ikakvog objašnjenja, i bez ikakvih negativnih posledica po Vas.', 'You have the full right to stop the experiment at any time, without any explanation, and without any negative consequences for you.');
    INSERT INTO "ConsentSection" ("ResearchId", "SortOrder", "TitleSr", "TitleEn", "BodySr", "BodyEn")
    VALUES (1, 7, NULL, NULL, 'U slučaju odustajanja, svi podaci prikupljeni do tog trenutka biće izbrisani.', 'If you withdraw, all data collected up to that point will be deleted.');
    INSERT INTO "ConsentSection" ("ResearchId", "SortOrder", "TitleSr", "TitleEn", "BodySr", "BodyEn")
    VALUES (1, 8, 'Poverljivost i anonimnost podataka', 'Confidentiality and Anonymity of Data', 'Svi podaci prikupljeni tokom istraživanja biće tretirani kao strogo poverljivi i korišćeni isključivo u akademske i istraživačke svrhe.', 'All data collected during the research will be treated as strictly confidential and used exclusively for academic and research purposes.');
    INSERT INTO "ConsentSection" ("ResearchId", "SortOrder", "TitleSr", "TitleEn", "BodySr", "BodyEn")
    VALUES (1, 9, NULL, NULL, 'Vaše ime, prezime i kontakt informacije biće odvojene od eksperimentalnih podataka.', 'Your first name, last name, and contact information will be kept separate from the experimental data.');
    INSERT INTO "ConsentSection" ("ResearchId", "SortOrder", "TitleSr", "TitleEn", "BodySr", "BodyEn")
    VALUES (1, 10, NULL, NULL, 'Vaši individualni rezultati neće biti objavljivani; u publikacijama će biti korišćeni isključivo grupni i anonimizovani podaci.', 'Your individual results will not be published; only group-level, anonymized data will be used in publications.');
    INSERT INTO "ConsentSection" ("ResearchId", "SortOrder", "TitleSr", "TitleEn", "BodySr", "BodyEn")
    VALUES (1, 11, 'Kontakt', 'Contact', 'Za sva pitanja i nedoumice u vezi sa istraživanjem, možete nas kontaktirati putem mejl adrese: beyondai.researchgroup@gmail.com', 'For any questions or concerns regarding the research, you may contact us at: beyondai.researchgroup@gmail.com');
  END IF;

  UPDATE "Research" SET
    "ConsentCheckboxTextSr" = COALESCE("ConsentCheckboxTextSr", 'Pročitao/la sam i razumeo/la sam gore navedene informacije i dobrovoljno pristajem da učestvujem u ovom istraživanju.'),
    "ConsentCheckboxTextEn" = COALESCE("ConsentCheckboxTextEn", 'I have read and understood the information provided above, and I voluntarily agree to participate in this research.')
  WHERE "Id" = 1;
END $$;
