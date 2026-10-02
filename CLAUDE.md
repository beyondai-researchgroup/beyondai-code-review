# Code Review AI Assistant — Project Context

## What this project is
An AI-powered Code Review Assistant web application. It is an educational tool that helps developers understand Pull Requests by explaining what was done and whether it aligns with good programming practices. It deliberately never says "approve" or "reject" — the developer always makes that call.

## Tech stack
- Backend: ASP.NET Core 8 (C#), minimal API style preferred
- Frontend: Angular 17+ with standalone components and Signals
- AI: Anthropic Claude API (claude-sonnet-4-6), streaming via SSE
- GitHub integration: Octokit.net (read-only, PAT-based)
- Markdown rendering: ngx-markdown + Prism.js

## Project structure
- /backend — ASP.NET Core solution
- /frontend — Angular application
- /docs — architecture notes and API contracts

## Coding conventions
- C#: use record types for DTOs, async/await throughout, XML doc comments on public methods
- Angular: standalone components, inject() function (not constructor injection), reactive forms
- Never hardcode API keys — use environment variables or appsettings
- Write one unit test per service method (xUnit for backend, Jasmine for frontend)

## Key constraint
The AI assistant must never recommend approving or rejecting a PR. All prompts and UI must reinforce this. Flag any code that contradicts this principle.

---

## Current status — MVP functional as of 2026-06-15

### What works (verified end-to-end 2026-06-15)
- All 23 backend unit tests pass (xUnit + Moq)
- Backend starts on `http://localhost:5000`, `/health` returns 200
- Frontend `ng build` and `ng serve` succeed with zero TypeScript errors
- Angular proxy (`src/proxy.conf.json`) routes `/api/*` from port 4202 to backend on 5000
- Full session lifecycle: POST /api/session → 200, GET /api/session/{id}/pr-summary (empty) → 404, DELETE → 204
- Error handling: bad session ID → 404, invalid GitHub token → 400
- CORS configured to allow any localhost origin (any dev port)
- **AI SSE streaming confirmed live** — Claude responds in Serbian, token-by-token, via real Anthropic API
- Conversation history working — follow-up questions reference prior context correctly
- Null diff handled gracefully — AI explains patch is unavailable rather than hallucinating
- No "approve"/"reject" language in any AI response — constraint holds ✅

### How to run locally
```
# Terminal 1 — backend
cd backend/CodeReviewAI.Api
dotnet run --launch-profile http

# Terminal 2 — frontend
cd frontend
ng serve
# Default port is 4202 (set in angular.json). Port 4201 is intentionally left free —
# it's reserved for the separate NASA-TLX workload-assessment app (see below).
```

Set the Anthropic API key via user-secrets (never commit it):
```
dotnet user-secrets set "Anthropic:ApiKey" "sk-ant-..." --project backend/CodeReviewAI.Api
dotnet user-secrets set "GitHub:PersonalAccessToken" "ghp_..." --project backend/CodeReviewAI.Api
```

**Critical**: after running `dotnet user-secrets init` (first-time setup), always do a full `dotnet build` before `dotnet run`. The `UserSecretsId` is baked into the assembly at compile time — running with `--no-build` on a stale binary means user secrets are silently ignored, causing 401 errors from the Anthropic API.

### Known limitations (discovered during integration)
1. **Port collision**: `ng serve` defaults to 4200, but this project pins it to 4202 in `angular.json` (`projects.frontend.architect.serve.options.port`). Port 4200 has historically been taken by another local app, and 4201 is reserved for the NASA-TLX app (see below) — don't reuse either without checking first.
2. **Anthropic key not in user-secrets by default**: The project `UserSecretsId` was initialised on 2026-06-15. Each developer must run `dotnet user-secrets set "Anthropic:ApiKey" "..."` once.
3. **GitHub token is per-request only**: The PAT is entered in the form and sent in the request body. There is no server-side token caching. Long-lived sessions that go idle still require the original token at load time.
4. **UserSecretsId must be compiled in**: After `dotnet user-secrets init`, always `dotnet build` before `dotnet run`. Using `--no-build` on a stale binary silently ignores user-secrets and causes 401 from Anthropic API.
5. **No HTTPS in dev**: The backend runs HTTP-only locally. TLS termination is expected at the reverse proxy layer in production.
6. **Session timeout is in-memory only**: Restarting the backend clears all sessions. Refreshing the Angular page with an active session will lose the session state.

### Layout restructuring (2026-06-18)

**Left panel split (FileListComponent):**
- Top half: scrollable file list (unchanged behavior)
- Bottom half: "Sažetak" section — shows first 300 chars of the PR description (no AI call, plain truncation). "Prikaži ceo opis →" text link opens the full PR description in the middle panel (same mechanism as before).
- Bottom: full-width "Donesi odluku" button (accent green) that opens the finish-review-modal.

**Decision button relocated:**
- Removed from the top navigation bar.
- Now lives at the bottom of the left panel (`FileListComponent` emits `finishClicked` output; `AppComponent` calls `openFinishModal()`). Works identically in both AI Mode and Report Mode.

**Drag-to-resize column boundaries:**
- A 5px `.resize-handle` flex element sits between each adjacent panel pair.
- `mousedown` on a handle starts tracking `document:mousemove`/`mouseup` via `@HostListener`.
- Left panel: 14–46%, middle panel: min 20%, right/chat panel: min 20%.
- Panel collapse toggle buttons (‹/›) are independent — they collapse to 28px via CSS `!important`; when uncollapsed the panel returns to its drag-set width.

### Report Mode static content + decision → NASA-TLX handoff (2026-06-24)

**Report Mode serves static content (test/demo override):**
- `GenerateReport` checks `Session:UseStaticReport` in config; when `true`, it streams a fixed SR/EN technical-documentation Markdown (`Services/StaticReportContent.cs`) instead of calling Claude. The dynamic AI generation path is untouched — the flag is `true` in `appsettings.Development.json` (local dev) and `false` in base `appsettings.json` (since 2026-07-17).
- Expanded 2026-07-22 with more depth (same no-thesis-mention constraint): non-functional requirements, a step-by-step transaction execution flow, a dedicated security-considerations section, an enumerations table, 4 more code excerpts (TokenFactory, GetMerklePath, Redis wallet locking, YARP gateway routing), a full API endpoint overview per microservice, a testing-strategy section, and a glossary — 18 `##` sections total (up from 12), ~23–24k rendered characters, 8 code blocks per language.
- `ReportViewComponent` now re-fetches on language change (`effect()` tracking `i18n.lang()`), not just on first mount, so switching SR/EN while the panel is open updates the document language too.

**Decision → NASA-TLX handoff:**
- Submitting a decision in the finish-review modal (`AppComponent.onDecisionSubmitted`) deletes the session, then does a full `window.location.href` redirect to `environment.nasaTlxUrl` (`http://localhost:4201/login`) — a separate NASA-TLX workload-assessment app. This is why the dev port moved to 4202: 4201 must stay free for that app.

### Robustness & polish pass (2026-07-17)

Fixes from a systematic code audit (functionality unchanged):

**Backend:**
- `ChatStream` rate limit now counts only `user` messages (was counting assistant replies too, halving the effective limit); the limit check + history mutation run atomically under a per-session lock (`ReviewSession.Sync`) so concurrent requests can't corrupt `History`.
- On a completely failed stream the dangling user turn is retracted from history (in `finally`), preventing duplicate consecutive user messages and phantom rate-limit hits.
- Input length caps: chat message ≤ 8000 chars, decision comment ≤ 2000 chars (400 with detail otherwise).
- Short-summary and suggestions Claude calls now pass the language-matched system prompt (previously always fell back to the SR chat prompt, even in EN mode).
- `GlobalExceptionMiddleware` no longer forwards `ex.Message` to clients; `ClaudeApiException` maps to a sanitized 502. Full details stay in the log.
- Files whose patch exceeds the 15k cap now get full-content fallback from the head branch (`OctokitApiAdapter` treats oversized patches like missing ones), so the AI no longer sees "content unavailable" for large diffs.
- CORS uses `uri.IsLoopback` (covers `127.0.0.1`/`::1`, not just `localhost`).
- `ChatRequest.SessionId` removed (session comes from the URL path).
- `Session:UseStaticReport` moved: `false` in base appsettings, `true` in `appsettings.Development.json`.

**Frontend:**
- `PrLoaderComponent.extractError` handles `HttpErrorResponse` (it doesn't extend `Error`), so specific backend messages ("PR not found", "token invalid"…) finally surface in the error banner instead of the generic fallback. Verified live.
- Decision submit redirects to NASA-TLX only after the session DELETE settles (previously the redirect aborted the in-flight request, leaving the PAT-holding session alive until cleanup).
- Diff parser skips `\ No newline at end of file` markers (they shifted all later line numbers by one, breaking quote-to-chat references).
- Quote-to-chat ranges prefer added/context (new-file) numbering; removed-only selections fall back to old numbering.
- Quote popup closes on diff scroll (it's fixed-positioned and would float over the wrong line).
- Character counters with near-limit highlight on the chat textarea (8000) and decision-comment textarea (2000); send is blocked when a programmatic insert (quote-to-chat) exceeds the limit.
- GitHub token field uses `autocomplete="off"` (was `current-password`, which invited password managers to store the PAT).

### Study flow: participant login + session orchestration (2026-07-18)

The app now runs as part of a three-session user study, orchestrated with the NASA-TLX app
through a shared Neon Postgres database (the same one NASA-TLX already used).

**Database (Neon, shared with NASA-TLX):**
- Existing: `Participant("ParticipantId")`, `TlxResult`.
- New: `Sessions` (fixed 3 rows: 1=Intro, 2=AI, 3=Report) and `ParticipantSession`
  (`ParticipantId`, `SessionId`, `IsFinished`, unique pair) — pre-filled per participant;
  participant `001` is seeded with all three.

**BeyondAI backend:**
- Npgsql added. `StudyService` reads `Study:DatabaseUrl` (user-secrets; postgres:// URI is
  converted to a keyword connection string).
- `POST /api/study/login { participantId }` → validates the participant and returns the
  first unfinished session in Intro → AI → Report order, or `{ allFinished: true }`.
- **Test participants** (`Study:TestParticipantIds`, currently `["001"]`): login always
  returns Intro for these ids regardless of their actual `IsFinished` flags — lets the
  testing phase repeatedly exercise the AI/Report mode choice. NASA-TLX still updates the
  flags and still records `TlxResult` normally for them; the override only affects what
  BeyondAI's login reads back. Remove an id from the list to switch it to real progression.
- `POST /api/study/start-review { participantId, reviewMode, lang }` → creates a review
  session preloaded with the **preconfigured demo PR** (`Study:Pr:Owner/Repo/Number` +
  `GitHub:PersonalAccessToken`); participants never see or enter GitHub data.

**BeyondAI frontend:**
- New `StudyLoginComponent` (identical visual design to the old loader card): Participant ID
  + SR/EN language picker (language locked after login; nav-bar toggle commented out).
  Intro session shows the AI/Report mode cards; AI/Report sessions open their mode directly.
  All-finished participants see a "done" banner and cannot enter.
- The original `PrLoaderComponent` (repo/PR/token form) is kept in the codebase but unused.
- `StudyStateService` persists `{participantId, sessionId, sessionName, lang}` in sessionStorage.
- Decision submit now redirects to `http://localhost:4201/start?participantId=…&sessionId=…&lang=…`
  (`environment.nasaTlxStartUrl`).

**NASA-TLX app** (`C:\Users\Andrej\Documents\Nasa-TLX-FullImplementation-AndrejKatin`):
- New `/start` route (AutoStartComponent): auto-login from the BeyondAI handoff — maps
  sessionId 1/2/3 → 'Uvodna sesija'/'Sesija 1'/'Sesija 2' (TlxResult naming unchanged),
  sets + locks the language (header toggle commented out), config = full TLX (scores +
  weightings), jumps straight to /instructions. The manual /login stays but is bypassed.
- `POST /api/db/session-finished { participantId, sessionId }` (server.ts) flips
  `ParticipantSession.IsFinished`; called by the results page right after the TLX result
  saves, then a "Sesija je završena" popup appears whose OK returns to the BeyondAI login.
- Dev note: `/api` on 4201 is proxied to the built SSR server on port 4000
  (`npm run serve:db` after `ng build`) — server.ts changes need a rebuild + restart of that.

**To finish setup**: set the demo PR via user-secrets/appsettings once provided:
`Study:Pr:Owner`, `Study:Pr:Repo`, `Study:Pr:Number`, and refresh `GitHub:PersonalAccessToken`.
Done as of 2026-07-19 — demo PR is `beyondai-researchgroup/TokenPaymentSystemSolution#1`.

### Test participants + decision/chat persistence (2026-07-22)

**Test participants** (`Study:TestParticipantIds`, currently `["001"]`): `StudyService.GetLoginStateAsync`
always returns Intro for these ids regardless of their actual `ParticipantSession.IsFinished` flags,
so the testing phase can repeatedly exercise the AI/Report mode choice. NASA-TLX still updates the
flags and still records `TlxResult` normally — the override only affects what BeyondAI's login reads
back. Remove an id from the list to switch it to real Intro → AI → Report progression.

**Review decisions and chat transcripts now persist to Neon** (previously only lived in the
in-memory `ReviewSession` and were lost on decision-submit/session-delete or backend restart):
- New tables: `ReviewDecision` (`ParticipantId`, `SessionId`, `ReviewMode`, `Decision`, `Comment`,
  `DecidedAt`; unique on `(ParticipantId, SessionId)` — re-submitting overwrites, same pattern as
  `TlxResult`) and `ChatMessage` (`ParticipantId`, `SessionId`, `Role`, `Content`, `CreatedAt`;
  append-only log, indexed on `(ParticipantId, SessionId)`).
- `ReviewSession` gained `ParticipantId`/`StudySessionId` (nullable — only set by
  `StudyEndpoints.StartReview`; the unused classic loader flow leaves them null).
- `IStudyService.SaveDecisionAsync`/`SaveChatMessageAsync` are best-effort: DB failures are logged
  (`ILogger<StudyService>`) and never thrown, so a Neon hiccup can't block a participant mid-study.
  Called from `SubmitDecision` (after the decision is recorded) and from `ChatStream` (once per
  user turn and once per non-empty assistant reply) — Report mode has no chat, so `ChatMessage`
  naturally stays empty for those sessions.
- Verified live: chat Q&A and a decision for participant `001` produced matching rows in Neon;
  resubmitting a decision for the same participant+session updated the existing row instead of
  duplicating it.

### Public deployment (2026-07-22)

Both apps are publicly hosted under a dedicated `beyondai-researchgroup` GitHub account (kept
separate from the developer's personal accounts — nothing in the public deployment should point
back to a personal identity). Source of truth is now these two repos, pushed from the local
working copies:
- `https://github.com/beyondai-researchgroup/beyondai-code-review` (this repo — backend + frontend)
- `https://github.com/beyondai-researchgroup/beyondai-nasa-tlx`

**Live URLs:**
- BeyondAI frontend (Vercel): `https://beyondai-code-review.vercel.app`
- BeyondAI backend (Render, Docker): `https://beyondai-backend.onrender.com`
- NASA-TLX (Vercel): `https://beyondai-nasa-tlx.vercel.app`

**Backend on Render:**
- `Dockerfile` + `.dockerignore` at the repo root (build context = root, so paths inside are
  `backend/CodeReviewAI.Api/...`); multi-stage build on `mcr.microsoft.com/dotnet/sdk:10.0` /
  `aspnet:10.0`.
- Secrets (`GitHub__PersonalAccessToken`, `Anthropic__ApiKey`, `Study__DatabaseUrl`,
  `Study__Pr__Owner/Repo/Number`, `Study__TestParticipantIds__0`, `Cors__AllowedOrigins__0`) are
  Render environment variables — **not** committed anywhere (`appsettings.json` keeps empty
  defaults, exactly as in local dev via user-secrets).
- `Program.cs` reads `$PORT` (Render's injected listen port) via `builder.WebHost.UseUrls(...)`
  when present; local dev is unaffected since launchSettings.json profiles never set `$PORT`.
- CORS is config-driven: `Cors:AllowedOrigins` (plus the always-allowed loopback origins for local
  dev) — add a Vercel URL there (as a Render env var, `Cors__AllowedOrigins__N`) whenever a new
  frontend origin needs to call this API.
- **Known container gotcha, already fixed**: the default ASP.NET host wires up a
  `FileSystemWatcher` (inotify) per `appsettings*.json` file for hot-reload. Render's free-tier
  containers have a very low inotify-instance limit, so the app crashed on startup with
  `IOException: The configured user limit (128) on the number of inotify instances has been
  reached`. `WebApplicationBuilder`'s `ConfigurationManager` builds JSON sources — and starts
  their watcher — **synchronously inside `CreateBuilder(args)`**, before any code in `Program.cs`
  runs, so disabling `ReloadOnChange` in code is too late. The fix had to be a bootstrap-time
  setting: the Dockerfile `ENTRYPOINT` passes `--hostBuilder:reloadConfigOnChange=false` as a CLI
  arg (Render's env-var UI rejects keys containing `:`, so this can't be set as a normal env var).
- Render doesn't auto-trigger a build on `git push` reliably even with `autoDeploy: "yes"` — a
  manual `POST /v1/services/{id}/deploys` (Render API) may be needed after pushing.

**BeyondAI frontend on Vercel:**
- Deployed via `vercel --prod` CLI directly from `frontend/` (no GitHub integration needed —
  Vercel builds `ng build` zero-config from the Angular "application" builder's
  `dist/frontend/browser` output).
- `environment.prod.ts` hardcodes the production URLs (`apiUrl` → Render backend,
  `nasaTlxStartUrl` → NASA-TLX Vercel URL) since Angular bakes environment files in at build time,
  not runtime — there is no dashboard env var for these.

**NASA-TLX on Vercel — the `/api/db/*` Express routes needed a rework:**
- Vercel's zero-config Angular deployment **prerenders every route** (`angular.json`
  `ssr.prerender: true`) and, since every route was statically prerenderable, Vercel decided no
  Node server function was needed at all — it deployed a **fully static site**, silently dropping
  every custom Express route defined inside `server.ts`'s `app()` (`/api/db/participant/:id`,
  `/api/db/result`, `/api/db/session-finished`, `/api/db/export`). Requests to those paths just
  hit the SPA fallback (200 with `index.html`, or 405 for POST). This was **not** a build error —
  it took a `vercel build` (local Build Output API v3 inspection) to see `config.json` had zero
  entries under `functions/`.
- Fix: moved the DB endpoints to **standalone Vercel serverless functions** under `/api/db/*.ts`
  (Vercel's first-class, always-deployed convention — independent of whatever the Angular
  framework preset decides about SSR/prerendering). Shared validation/DB logic lives in
  `api/_lib/db.ts`; `server.ts` (still used for local dev via `npm run serve:db`) now imports the
  same module instead of duplicating it.
- Two more container/runtime gotchas hit along the way, both fixed:
  1. `api/package.json` with `{"type": "module"}` — needed because the repo root `package.json`
     has no `"type"` field (defaults to CommonJS), but Vercel compiles `/api/*.ts` to ESM `.js`;
     Node's nearest-`package.json` rule means this file only affects `/api`, not the Angular build.
  2. All relative imports under `/api` need an explicit `.js` extension
     (`from '../../_lib/db.js'`, not `'../../_lib/db'`) — Node's strict ESM resolver (unlike
     CommonJS `require`) doesn't infer extensions, and Vercel's per-function build doesn't rewrite
     bare specifiers.
- `DATABASE_URL` is a Vercel project environment variable (production + preview), set via the
  Vercel API — a redeploy is needed after changing it for a Node function to pick it up.
- Verified live end-to-end after the fix: `/api/db/participant/001` → `{"exists":true}`,
  `/api/db/session-finished` → correct 400/404/200 per case, full BeyondAI → NASA-TLX handoff
  through a real browser run with zero console errors.

### Informed consent + emailed magic-links for REI-40/Big Five (in progress, phased — 2026-08-13)

Team decision: the whole study family becomes one connected flow. A new front door — a
to-be-built **Consent app** (`consent-andrejkatin`, ports 4303/4313) — is where a participant
logs in with their Participant ID for the very first time, picks SR/EN (locked from then on,
propagated to every other app via `Participant.Language` instead of each app's own picker),
reads/accepts an informed-consent form, and only then gets emailed dynamically-generated,
24h-expiring links to REI-40 and Big Five (`beyondai-researchgroup@gmail.com`, Gmail SMTP +
Nodemailer). Consent also becomes a hard prerequisite for the existing BeyondAI/NASA-TLX
Intro→AI→Report flow. REI-40/Big Five's own participant-ID login pages stop being the real entry
point for participants once this ships (kept for dev/testing only) — magic link only. Three
pieces of per-replication instrument config (EEG usage, NASA-TLX structure, REI-40 item-set
variant) move into a new Admin Dashboard "Study Configuration" page.

Being built in explicit phases, verified one at a time (not one big pass) — full design lives in
the plan file from the planning session that scoped this
(`create-a-claude-md-file-cozy-hinton.md` as of 2026-08-13; a fresh plan file may exist by the
time you read this if the next phase went through its own planning pass).

**Phase A — done (2026-08-13): shared schema + email-sending capability.**
- New `Sql/007_consent_email_language.sql`, applied to Neon and verified (idempotent re-run,
  columns/types/defaults confirmed, `SurveyAccessToken`'s `SurveyType` CHECK constraint
  confirmed rejecting bogus values):
  - `Participant` gains `Email` (VARCHAR 255), `Language` (VARCHAR 2), `ConsentGivenAt`
    (TIMESTAMPTZ) — all nullable, additive, no existing reads broken (confirmed via
    `dotnet build` + `dotnet test`, 39/39 still passing, no C# code touched yet).
  - New `SurveyAccessToken` table — one row per `(ParticipantId, SurveyType)`, unique constraint;
    regenerating a link is an `UPDATE` in place (old token value stops resolving instantly), not
    a new row. Valid while `NOW() < ExpiresAt` **and** no matching `Rei40Result`/`BigFiveResult`
    row exists yet for that participant — no separate "consumed" flag, reuses those tables'
    existing upsert-on-`ParticipantId` behavior as the completion signal.
  - `Replication` gains `TlxCalculateScores`/`TlxIncludeWeightings` (BOOLEAN, default `TRUE`) and
    `Rei40Variant` (VARCHAR 20, default `'v1'`) — landing spots for Phase E's config page.
- `admin-dashboard-andrejkatin/server/email/mailer.mjs` — new Nodemailer/Gmail-SMTP helper
  (`sendMail({to, subject, html})`), reads `GMAIL_USER`/`GMAIL_APP_PASSWORD` from `.env` (never
  hardcoded, same convention as `DATABASE_URL`/`JWT_SECRET`). **Not yet wired to anything** —
  nothing calls it yet; it's the template Phase B's Consent app copies verbatim. `nodemailer`
  added as a dependency there.
- **Phase A fully closed out (2026-08-13)**: Gmail App Password set as `GMAIL_USER`/
  `GMAIL_APP_PASSWORD` in `admin-dashboard-andrejkatin/.env`. A throwaway `sendMail(...)` call
  was run against the real Gmail SMTP and delivered successfully.
  **Note**: the live sending account is `beyondai.researchgroup@gmail.com` (dot before
  "researchgroup") — the original request named it `beyondai-researchgroup@gmail.com` (hyphen).
  Gmail ignores dots in usernames but hyphens are significant, so these could in principle be two
  different accounts; using exactly what's in `.env` since that's the account that actually holds
  the App Password and successfully sent the test mail. Flag to the user if this turns out to be
  a typo rather than the intended address.

**Phase B — done (2026-08-13): the Consent app itself.**
New repo `C:\Users\Andrej\Documents\consent-andrejkatin` (ports 4303 ng / 4313 API), cloned from
REI-40's shell (same `package.json`/`angular.json`/`styles.scss`/`global-header`/`ThemeService`
pattern, storage keys prefixed `consent-*`). Routes: `/login` (Participant ID + the SR/EN picker
— this login *is* the very first login for the whole family, the choice gets written to
`Participant.Language` on submit, not just localStorage) → `/consent` (full bilingual informed-
consent text as `CONSENT.*` i18n keys, a required checkbox, Submit; guarded by `sessionGuard`) →
`/done` ("check your email" — same message whether consent was just given or had already been
given before, since the right next action is identical either way).

Backend (`server.mjs`): `GET /api/participant/:id` → `{exists, alreadyConsented, language}`;
`POST /api/consent/submit` → validates, sets `Language`/`ConsentGivenAt` via
`COALESCE(...)` (never overwrites an already-locked language or an already-recorded consent
timestamp), upserts both `SurveyAccessToken` rows (`crypto.randomBytes(24).toString('base64url')`,
`NOW() + 24h`, `ON CONFLICT (ParticipantId, SurveyType) DO UPDATE` — regenerating overwrites in
place), sends one branded bilingual HTML email (`server/email/consentEmail.mjs`, table-based
layout) via `server/email/mailer.mjs` (copied verbatim from Admin Dashboard's Phase A version).
Link URLs are built from `REI40_APP_URL`/`BIGFIVE_APP_URL` env vars (default
`http://localhost:4300`/`4301`) + `/link/:token` — **that route doesn't exist yet in REI-40/Big
Five** (it's Phase C), so today's emailed links 404 until Phase C ships; Phase B only proves
generation + email delivery, not the receiving end.

Verified live end-to-end via a temp `consenttest01`/`consenttest02` participant (deleted after):
full submit → DB state (`Language`/`ConsentGivenAt`/two `SurveyAccessToken` rows with correct
24h `ExpiresAt`) confirmed correct; `NO_EMAIL` error path (participant with no `Email` on file)
and unknown-participant path both confirmed; a real branded email was sent and delivered via
Gmail SMTP. `ng build --configuration development` clean.

**Phase C — done (2026-08-13): magic-link resolution in REI-40 and Big Five.**
Both apps gain `GET /api/link/:token` (server.mjs) — resolves against `SurveyAccessToken`
(`SurveyType='REI40'`/`'BIGFIVE'` respectively) joined to `Participant` for the locked
`Language`; `404 NOT_FOUND` if the token doesn't match, `410 EXPIRED` if past `ExpiresAt`,
`409 ALREADY_COMPLETED` if a `Rei40Result`/`BigFiveResult` row already exists for that
participant (the completion-signal design from Phase A — no separate consumed flag). Frontend
gains `LinkAccessComponent` (`/link/:token`) — on success sets `StateService` directly (no login
form, no language choice, both already resolved) and routes to `/test`; on
error/already-completed shows a translated message instead. **The app's default route changed**:
root now shows a new `AccessInfoComponent` ("access via the emailed personal link only") instead
of redirecting to the login form; `/login` still exists (dev/testing only) but is no longer
linked from anywhere or the default landing page. New i18n namespaces `ACCESS_INFO`/`LINK` added
to both apps' `sr.json`/`en.json`.

Verified live end-to-end via a temp `phasectest` participant (deleted after): real consent
submit → real tokens → both `/api/link/:token` routes resolve correctly; `ALREADY_COMPLETED`
(simulated via an inserted `Rei40Result` row) and `EXPIRED` (simulated via a backdated
`ExpiresAt`) both return the correct status/error code; frontend `/`, `/link/:token` routes
serve 200. `ng build --configuration development` clean on both apps.

**Phase D — done (2026-08-13): BeyondAI consent-gate + dropped its own language picker.**
`StudyLoginState` (`IStudyService.cs`) gained `ConsentRequired`/`Language` — `GetLoginStateAsync`
now fetches `Participant.Language`/`ConsentGivenAt` up front (replacing the old bare
`SELECT 1` existence check), returns `ConsentRequired: true` immediately when
`ConsentGivenAt IS NULL`, and threads the resolved `Language` through every return path. Test
participants (`Study:TestParticipantIds`) bypass the consent check entirely — same rationale as
their existing session-progression bypass, and pre-existing test rows predate the column anyway
(confirmed: `001` still logs in fine post-change, `language: null` since it was never set).
`StudyEndpoints.Login` returns `{ allFinished: false, consentRequired: true }` before the
`AllFinished` check; on success now also returns `language`. `StartReview` gained the same
`ConsentRequired` guard for defense in depth (a client could otherwise call it directly,
bypassing `Login`).

Frontend: `StudyLoginComponent`'s lang-cards UI is gone entirely — `login()` now applies
`res.language` via `i18n.set(...)` automatically instead of ever showing a picker; a new
`consentRequired` signal swaps the form out for a message + link to
`environment.consentAppUrl` (`http://localhost:4303` locally; **`environment.prod.ts` has a
placeholder TODO** since the Consent app isn't deployed publicly yet). Removed the now-dead
`studyLangLabel`/`setLang()`, added `studyConsentRequiredTitle/Text`/`studyGoToConsent` to
`translations.ts` (both languages) and `.consent-required-banner` styles (replacing the removed
`.lang-cards`/`.lang-card` rules) in `study-login.component.scss`.

Verified live end-to-end via `dotnet test` (39/39 green) plus direct `/api/study/login`/
`/api/study/start-review` calls against temp participants (cleaned up after): an unconsented
participant gets `consentRequired: true` with no session data leaked; a consented participant
with `Language='en'` gets the correct next session **and** `language: "en"` back; test
participant `001` still bypasses the gate unchanged; `start-review` correctly 400s for an
unconsented participant even when called directly. `ng build --configuration development` clean.

**Phase E — done (2026-08-13): Admin Dashboard Study Configuration page + regenerate-links.**
New `admin-dashboard-andrejkatin/server/study-config/routes.mjs`
(`GET`/`PUT /api/admin/study-config/:replicationId`) — a narrower, separate PUT from
`replications/routes.mjs`'s own: only ever touches `UsesEeg`/`EegDeviceType`/
`TlxCalculateScores`/`TlxIncludeWeightings`/`Rei40Variant`, never `Name`/`Description`/etc., so
it can't clobber edits made on the Replications page. **Pragmatic deviation from the original
phase design**: the EEG toggle was *not* removed from the Replications page (too much
churn/risk for one form that's already exercised elsewhere) — it's now editable from *both*
pages, safely, since they write the same columns via non-overlapping field sets and neither
does a full-object overwrite of the other's fields.

New `/study-config` route + nav entry (`StudyConfigComponent`, mirrors `PrConfigComponent`'s
`scope.selectedReplicationId` reactive-load pattern): three sections — EEG
toggle+device (checkbox/text, same pattern as the Replications page), NASA-TLX structure
(`tlxCalculateScores`/`tlxIncludeWeightings` checkboxes — NASA-TLX itself doesn't read these yet,
that's a follow-up not in this plan's scope, this phase only builds the config surface), and
REI-40 variant (`app-select` dropdown, one option — `'v1'`/"Varijanta 1" — today;
`REI40_VARIANTS` array in the backend route is the single place a `'v2'` gets added later).

**Regenerate-links**: new `POST /api/admin/participants/:id/regenerate-links` — re-runs the same
upsert-token-and-email logic as the Consent app's `/consent/submit` (own duplicated
`server/email/regenerateLinksEmail.mjs`, adjusted "here are your new links" copy vs. the
original "thanks for consenting" copy), gated by `resolveReplicationScope` (participant's own
`ReplicationId`, not the caller's) and by `ConsentGivenAt IS NOT NULL` (`NOT_CONSENTED` 400 if
not — this endpoint only ever re-sends replacement links, never a participant's very first
ones) and `Email IS NOT NULL` (`NO_EMAIL` 400 otherwise). Button + result banner added to
Participant Detail (`regenerateLinks()`/`regenerateResult` signal, 5 outcome states). Needs
`REI40_APP_URL`/`BIGFIVE_APP_URL` env vars (added to `.env`, same defaults as the Consent app's).

Verified live end-to-end via a temp superadmin researcher + 3 temp participants (all deleted
after): `GET`/`PUT /study-config/1` round-trips correctly (restored to its original values after
the test write); `regenerate-links` correctly returns `NOT_FOUND`/`NOT_CONSENTED`/`NO_EMAIL`/
success for each of the 4 cases, and the success case's `SurveyAccessToken` rows were confirmed
in the DB with the correct 24h `ExpiresAt`. `ng build --configuration development` clean.

**Phase E follow-up — done (2026-08-13): closed both gaps flagged above, on request.**
1. **EEG toggle de-duplicated**: removed from the Replications page entirely (form, `startEdit`/
   `cancelEdit`/`submit`, and the `usesEeg`/`eegDeviceType` fields dropped from
   `ReplicationInput`) — Study Configuration is now the only place that writes those two
   columns. `server/replications/routes.mjs`'s `POST`/`PUT` no longer touch `UsesEeg`/
   `EegDeviceType` at all (a new replication starts EEG-disabled via the column's own
   `DEFAULT FALSE`, configured afterward on Study Configuration); `GET`/list still return them
   for the Replications page's now-read-only EEG badge column. i18n: removed
   `REPLICATIONS.USES_EEG_LABEL`/`EEG_DEVICE_LABEL`/`EEG_DEVICE_PLACEHOLDER` (STUDY_CONFIG has
   its own copies), added `REPLICATIONS.EEG_MOVED_HINT` pointing at the new page.
2. **NASA-TLX now reads `TlxCalculateScores`/`TlxIncludeWeightings` from the DB.** New shared
   `getTlxConfigForParticipant(sql, participantId)` in `api/_lib/db.ts` (joins
   `Participant.ReplicationId` → `Replication`), wired into both entry points:
   - `GET /api/db/tlx-config/:participantId` — added to `server.ts` (local dev) **and** as its
     own standalone Vercel function `api/db/tlx-config/[participantId].ts` (mirrors the existing
     `api/db/participant/[id].ts` pattern exactly — same reason as always: Vercel's Angular
     preset only deploys standalone `/api/*.ts` functions, not custom Express routes buried in
     `server.ts`). 404s for an unresolvable participant.
   - `DatabaseService.getTlxConfig(participantId)` (frontend) — **falls back to
     `{calculateScores: true, includeWeightings: true}` on any failure** (network error, 404,
     timeout), never blocks getting into the test.
   - `AutoStartComponent` (`/start`, the BeyondAI handoff): `ngOnInit` is now `async`, awaits
     `getTlxConfig(participantId)` instead of hardcoding `{true, true}`.
   - `LoginComponent` (`/login`, manual entry): the `calculateScores`/`includeWeightings`
     checkboxes are gone from the form entirely; `submit()` awaits the same DB lookup after the
     participant-exists check instead. Removed now-dead `.advanced-section`/`.advanced-title`
     styles and `LOGIN.ADVANCED_TITLE`/`CALC_SCORES`/`INCLUDE_WEIGHTINGS` i18n keys; added a
     one-line `LOGIN.STRUCTURE_FROM_CONFIG_HINT` explaining where the structure now comes from.

Verified live: Admin Dashboard `ng build` clean after the EEG removal; NASA-TLX
`ng build --configuration development` clean (browser **and** SSR server bundle — confirms
`server.ts`'s new route compiles); restarted NASA-TLX's `serve:db`, hit
`GET /api/db/tlx-config/:id` directly against a temp participant with replication 1's config
deliberately set to a non-default `{true, false}` — got back exactly that (not the old
hardcoded default), confirming the DB round-trip actually works and isn't silently falling back;
unknown participant correctly 404s. Replication 1's config and all temp data restored/deleted
after.

**Phase F — done (2026-08-13), redefined by the user from "Excel import gains an Email column"
to a simpler direct seed**: `Participant.Email` set to `beyondai.researchgroup@gmail.com` for
test participants `001`/`002`/`003` directly via SQL, so the real end-to-end flow (Consent app
→ real email → real magic link → REI-40/Big Five) can be exercised live through the browser
without building the Excel-import UI capability first. The Excel-import `Email` column (the
original Phase F design) was **not** built — revisit if/when real participant emails need to be
bulk-imported rather than typed one at a time via direct SQL.

**Anti-priming fix (2026-08-13)**: per a colleague's suggestion (relayed by the user), participant-
facing copy must never name which specific instruments are coming — a participant who knows
"REI-40" and "Big Five" by name in advance could look them up and prepare/skew their answers
before actually seeing the items. The consent form text itself (`CONSENT.*` i18n keys, both
languages) already only ever said "short self-assessment questionnaires" generically, so it
needed no change. What did name them and got genericized to "Psihološki test 1" / "Psychological
Test 1" and "...test 2" / "...Test 2": the Consent app's `DONE.TEXT` (both `sr.json`/`en.json`),
and both participant-facing emails — `consent-andrejkatin/server/email/consentEmail.mjs` and
`admin-dashboard-andrejkatin/server/email/regenerateLinksEmail.mjs` (subject, preheader, intro,
per-link label, all instances). Internal code (`SurveyType` DB values `'REI40'`/`'BIGFIVE'`,
`server.mjs` variable/comment names, Admin Dashboard's researcher-facing Study Configuration/
Participant Detail pages) intentionally still uses the real names — only what a participant
actually sees before reaching the test itself was scrubbed. Out of scope for now (not requested):
renaming the REI-40/Big Five apps' own browser tab titles (`<title>REI-40</title>` in
`rei40-andrejkatin/src/index.html`) or in-app headers — those are visible only after the
participant has already followed the magic link into that app, arguably past the "before they
reach the questionnaire" boundary the request was about, but flagged here in case it should be
addressed later too.

**Debugging note for future reference**: a "link invalid" report from a real inbox click was
traced to the participant/token having already been deleted by this session's own
verification-cleanup step (Phase B/C/E testing all sent real emails to
`beyondai.researchgroup@gmail.com`, then deleted the underlying temp participant afterward,
same as always) — not a pipeline bug. Confirmed via direct DB query
(`SurveyAccessToken` was empty) and by re-confirming all 4 app servers (REI-40 ng+API, Consent
app ng+API) were still live and unchanged. If this happens again: check whether the clicked
link's participant still exists in `Participant`/`SurveyAccessToken` before assuming a code
regression — a stale test email in the same inbox as real ones is an easy mix-up.

### REI variant selector (lockable) + raw per-item answer drill-down (2026-08-13)

Phase A of a 2-phase plan (Phase B = Experimental Sessions, below). Full design in the
now-superseded plan file — see memory for the durable record.

**REI-40/BigFive formally documented**: `backend/CodeReviewAI.Api/Sql/008_rei40_answers_and_variant.sql`
adds `CREATE TABLE IF NOT EXISTS` DDL for `Rei40Result`/`BigFiveResult` (previously existed
ad-hoc on Neon with zero committed schema anywhere — same gap migration 001 closed for
`Sessions`/etc.) plus `Rei40Result.Variant VARCHAR(20) DEFAULT 'v1'`. Applied and verified live.

**REI-40 variant lock**: `admin-dashboard-andrejkatin/server/study-config/routes.mjs` — lock is
derived, not stored (`EXISTS (SELECT 1 FROM Rei40Result r JOIN Participant p ... WHERE
p.ReplicationId = :id)`), returned as `rei40VariantLocked` from `GET`; `PUT` re-checks
server-side before applying a genuine variant change (409 `REI40_VARIANT_LOCKED`; same-value
resubmit always allowed). `study-config.component.ts/.html` disables the dropdown + shows a
hint when locked; `select.component.ts` gained a `disabled` input for this (reusable elsewhere).
Verified live: replication 1 (participant `001` already completed REI-40) correctly shows
`rei40VariantLocked: true`.

**Wired end-to-end, not just an inert column**: `rei40-andrejkatin/server.mjs`'s
`/api/participant/:id` (dev-login path) and `/api/link/:token` (real magic-link path) both now
resolve and return `rei40Variant` (via `Participant.ReplicationId` → `Replication.Rei40Variant`);
`/api/result` accepts and stores it. Frontend (`state.service.ts`, `database.service.ts`,
`login.component.ts`, `link-access.component.ts`, `test.component.ts`) threads it through.
**Only `'v1'` (the current 40-item set) is real** — a second variant was explicitly descoped this
phase (see below) — but the plumbing is now live and provably correct instead of dead, verified
via a temp participant through the real magic-link flow end-to-end (cleaned up after).

**Second variant explicitly not built**: REI-10 (Need for Cognition/Faith in Intuition, 2-subscale)
was considered, but its full 10-item text isn't freely published — only 2 of 10 confirmed via
public sources, the rest live in an unpublished Norris/Pacini/Epstein manual. Fabricating
psychometric instrument content was rejected; user chose "infra-only for now" when told this.

**Raw per-item answer drill-down**: `server/results/routes.mjs`'s raw-mode `/rei40`/`/bigfive`
now select `"Answers"` (was already stored, never returned). New duplicated metadata files —
`src/app/data/rei40-items-meta.ts` (40 items, id/subscale/reverse/sr+en text) and
`bigfive-items-meta.ts` (29 items) — power a new `AnswerDetailModalComponent`
(`src/app/results/answer-detail-modal/`), reachable via a "Prikaži odgovore" action column
(`RawTableComponent` gained an optional `rowActionLabel`/`rowAction` output — inert/no-op on the
TLX page, which has no per-item view) on `results-rei40`/`results-bigfive`/
`participant-detail`. Shows every item's text + the participant's 1-5 answer grouped by
subscale/factor; falls back to an "item text not available" message for any future variant with
no matching metadata entry yet. Verified live against participant `001`'s real REI-40 answers —
manually recomputed the Rational Ability subscale from the raw per-item answers/reverse-flags and
confirmed it matches the stored `RationalAbility` score exactly (3.20).

**Process gotcha hit twice during verification**: killing/restarting `admin-dashboard-andrejkatin`'s
and `rei40-andrejkatin`'s local dev servers via a backgrounded `(node ... &)` silently attached to
an already-running stale process from earlier in the session (old code) rather than actually
restarting — response payloads looked plausible but were missing the new fields. Always check
`netstat -ano | grep ':<port>' | grep LISTENING` for the actual owning PID and `taskkill //PID <pid>
//F` it before relaunching when a route's behavior doesn't match the code on disk.

**Also hit a self-inflicted near-miss**: an early curl `PUT .../study-config/1` test call included
`usesEeg:false` (copy-pasted from a template body) and briefly overwrote replication 1's real EEG
config (`true`/"Emotiv Insight 5" → `false`/null) before being caught and restored in the next
call. Worth remembering when hand-crafting PUT bodies against a real replication: read the current
GET response first and reuse its values for any field not under test, don't assume defaults.

### Experimental Session granularity (Phase B — done, 2026-08-13)

New `ExperimentalSession` entity: a physical day/timeslot within a Replication (e.g. "3
participants ran their Intro session on Aug 20"), with general notes/metadata at the event level
plus per-participant-per-session notes. `Sql/009_experimental_sessions.sql` adds the table
(`ReplicationId`, `SessionDate`, `Label`, `Notes`, `CreatedByResearcherId`, `CreatedAt`) plus two
nullable/additive columns on `ParticipantSession` (`ExperimentalSessionId`, `Notes`) — reuses
`ParticipantSession` as the join target rather than a parallel session concept; every existing row
stays unassigned, forward-looking only, no retroactive backfill. Applied and verified live.

**Backend**: new `admin-dashboard-andrejkatin/server/experimental-sessions/routes.mjs` — `GET /`
(list + assigned-count per replication scope), `POST /` (create), `GET /:id` (detail incl. every
assigned `ParticipantSession` row), `PUT /:id` (general Label/SessionDate/Notes), `POST /:id/assign`
and `/:id/unassign` (link/unlink a specific `(participantId, sessionId)` pair, validated same-
replication), `PUT /participant-session-notes` (per-row note, independent of assignment state).
All follow the established `resolveReplicationScope`/`scopeGuard` pattern from `server/scope.mjs`.
Mounted at `/api/admin/experimental-sessions` in `server/index.mjs`.

**Frontend**: new `/experimental-sessions` (list + create form) and `/experimental-sessions/:id`
(master-detail: general-info form on top, assigned-participant-sessions table below with an
inline-editable note + Unassign per row, plus an Assign control) — `experimental-sessions-list
.component.ts/.html` and `experimental-session-detail.component.ts/.html`, new nav entry in
`dashboard-shell.component.html`. `select.component.ts`'s `disabled` input (added in Phase A) is
reused here too where relevant.

**Bugs caught and fixed during live verification** (both would have shipped broken):
1. `GET /` and `GET /:id`'s SQL results were returned with raw PascalCase column names
   (`Id`/`ReplicationId`/...) instead of being mapped to the camelCase shape the frontend
   TypeScript interfaces expect — every field except the already-mapped ones would have rendered
   as `undefined`. Fixed by explicitly mapping both responses.
2. `router.put('/participant-session-notes', ...)` was registered *after* `router.put('/:id',
   ...)` — Express matches PUT routes in registration order, so "participant-session-notes" was
   being captured as the `:id` param and failing its `Number.isInteger` check every time
   (`{"error":"Invalid id"}`). Fixed by moving the literal-path route before the parameterized one.

Verified live end-to-end (temp experimental session + real participants `001`/`002`, cleaned up
after): create → list/detail (camelCase confirmed) → assign both → per-row note update (confirmed
round-tripped) → unassign → cross-replication assign/GET correctly rejected with 403 via a scoped
JWT for a different replication id. `ng build` clean throughout.

### Experimental Sessions refinement — scheduled time, list/detail UX, soft enforcement (2026-08-14)

Follow-up to Phase B above, requested after live use. **Enforcement stays Admin-Dashboard-only**
this round (explicit user choice) — confirmed via code inspection that nothing in BeyondAI
(`StudyService.GetLoginStateAsync`/`StudyEndpoints`), NASA-TLX (`auto-start.component.ts`/
`session-finished.ts`), or REI-40/Big Five's `server.mjs` files reads `ExperimentalSessionId`
today; a real technical gate would mean touching all 4 repos and was deliberately deferred.

- `Sql/010_participant_session_scheduled_time.sql` — `ParticipantSession.ScheduledTime` (TIME,
  nullable). Applied and verified live.
- `experimental-sessions/routes.mjs`: `POST /:id/assign` now **requires** a `time` (`HH:MM`) —
  400s without one; stores it into `ScheduledTime`. `POST /:id/unassign` clears `ScheduledTime`
  back to `NULL` alongside `ExperimentalSessionId` (a future reassignment always gets a fresh
  time). `GET /:id` formats it via `TO_CHAR(..., 'HH24:MI')` and sorts assigned rows by time.
- **Soft enforcement**: `participants/routes.mjs`'s `GET /` now also selects
  `ps."ExperimentalSessionId"` per session (`experimentalSessionId` in the response). The
  Participants list (`participants-list.component.html`) shows a ⚠ warning badge next to any
  session that's `isFinished && !experimentalSessionId` — the visible sign that a session was
  actually conducted without ever being scheduled through an experimental session, which is what
  this whole feature exists to let a researcher catch and fix themselves.
- List page (`experimental-sessions-list.component.ts/.html/.scss`) redesigned: inline create
  form removed, replaced by a "+ Nova eksperimentalna sesija" button top-right of the page header
  that opens a modal (Date required, Label/Notes optional) — same backdrop/card visual pattern
  already used twice (`chart-modal`/`answer-detail-modal`), duplicated once more per this
  project's established per-component styling convention.
- Detail page (`experimental-session-detail.component.ts/.html`): assign control gained a
  required `<input type="time">` (Assign button stays disabled until participant+session+time are
  all set); the assigned-sessions table gained a "Vreme" column. Per-row `Notes`
  (comments)/Unassign were already correct from Phase B, unchanged.

Verified live end-to-end (temp experimental sessions on replication 1, cleaned up after): assign
without a time correctly 400s, assign with a time round-trips `scheduledTime` correctly in the
detail response, unassign clears both `ExperimentalSessionId` and `ScheduledTime` (confirmed via
direct SQL), and the soft-enforcement signal was confirmed at the API level — flipped
`ParticipantSession.IsFinished = TRUE` directly via SQL for an unassigned row and confirmed
`GET /api/admin/participants` returns `experimentalSessionId: null` for exactly that session (the
condition the frontend badge renders on), then reverted the test row. `ng build` clean.

### Experimental Sessions: assign-conflict guard, per-session tags, calendar (2026-08-14)

Third follow-up round on Experimental Sessions, after live use with the 3 seeded real sessions
(participants 001/002/003 × Intro/AI/Report).

**Assign-conflict guard**: `POST /:id/assign` (`experimental-sessions/routes.mjs`) now checks the
target `ParticipantSession` row's *current* `ExperimentalSessionId` before overwriting — if it's
already set to a *different* experimental session, returns `409 {error:'ALREADY_ASSIGNED'}`
instead of silently stealing it. Re-submitting into the *same* session (e.g. just to change the
time) still works. Frontend's `sessionOptions` picker (detail page) also filters out
already-assigned-elsewhere sessions so the conflict is rarely even reachable through the UI;
`assignParticipantSession` returns a result-union (matching `updateStudyConfig`'s established
pattern) so the 409 shows a specific translated message. Verified live: a steal attempt correctly
409s, a same-session reassign correctly succeeds.

**Per-session tags**: `Sql/011_participant_session_tags.sql` adds `ParticipantSession.Tags`
(JSONB, default `[]`). New `PUT /participant-session-tags` (registered before the generic `PUT
/:id`, same ordering rule as the notes route — bit this bug once already) replaces the full tag
array atomically; validates ≤8 tags, each either one of a fixed `PREDEFINED_TAGS` vocabulary
(`SUCCESS/CORRUPTED/INTERRUPTED/TECHNICAL_ISSUE/REPEAT_NEEDED/NOTEWORTHY`) or a custom string
(≤30 chars, safe charset). Frontend: `experimental-sessions/session-tags.ts` mirrors the
vocabulary with sr/en labels + a `positive/negative/neutral` tone per tag (reuses existing
`--color-accent`/`--color-error`/muted tokens — no new design-system additions); detail page
renders removable pill chips per participant-session next to Notes, with a "+" picker for
not-yet-applied predefined tags plus a free-text custom-tag input. Verified live: predefined +
custom tag both round-trip correctly; an XSS-shaped custom tag (`<script>`) is correctly rejected
by the charset validator.

**Calendar view**: added `@angular/cdk` (first UI-kit dependency in this app — used only for
`DragDropModule`, the official low-footprint drag-and-drop primitive, rather than hand-rolling
native HTML5 DnD). New `/calendar` page + nav entry: a month grid (Monday-first) grouping all of
the scope's `ExperimentalSession` rows by date (client-side, off the existing `GET
/api/admin/experimental-sessions` — no new backend endpoint needed), each session rendered as a
draggable chip. Dropping a chip on a different day calls the *existing* `PUT /:id` with the new
date (same endpoint the detail page's general-info form already used) — confirmed via a direct
API call simulating the drop. Clicking (not dragging) a chip navigates to that session's detail
page; clicking empty day space opens the same create-modal pattern as the list page, pre-filled
with that date. Scope note: only whole experimental-session day-blocks are draggable (not
individual participant-sessions) and it's a month view only (not a week/agenda view) — both
explicit, asked-and-confirmed scope decisions, not oversights.

**Recurring gotcha, hit a third time this project**: passing text containing an em dash ("—") or
other non-ASCII characters through `curl -d '...'` in this Windows git-bash environment corrupts
it to `�` before it reaches the server — this actually clobbered real seeded data twice during
this round's live verification (both times caught immediately via the very next read-back query
and fixed with a direct Node/`@neondatabase/serverless` UTF-8-safe `UPDATE`). **Lesson: never pass
non-ASCII text through an inline `curl -d`/bash string on this machine — always write it via a
short Node script (template literal) instead**, exactly like the migration runners already do.

### Experimental Sessions: delete + date-loading bug fix (2026-08-14)

**Root-caused and fixed the "datum ne učitava dobro" edit bug**: `ExperimentalSession.SessionDate`
was being returned to the frontend as a full ISO timestamp (e.g.
`"2026-08-17T22:00:00.000Z"`) instead of a plain `YYYY-MM-DD` string — a native `<input
type="date">` silently fails to populate from anything but an exact `YYYY-MM-DD` value, which is
why the detail page's edit form always looked "empty"/uneditable for an existing session. Root
cause: the neon serverless driver parses a Postgres `DATE` column using a **local-timezone** `Date`
constructor (confirmed by testing — a stored `'2026-08-18'` round-trips as a JS `Date` whose
*local* getters give `2026-08-18` but whose UTC getters/`toISOString()` show the previous day),
so `JSON.stringify`'s default `toISOString()` serialization was silently shifting the date. Fixed
with a `dateOnly()` helper in `experimental-sessions/routes.mjs` (reads the Date via local getters,
formats `YYYY-MM-DD` manually — never `toISOString()`/`getUTC*` on this value anywhere) applied in
both `GET /` and `GET /:id`. Verified live: detail response now returns `sessionDate: "2026-08-18"`
exactly.

**Delete**: new `DELETE /:id` — since `ParticipantSession.ExperimentalSessionId` is a plain FK with
no `ON DELETE` clause, a raw delete would fail with a constraint violation once anything is
assigned; instead it first detaches (unassigns) every `ParticipantSession` row pointing at it
(`ExperimentalSessionId`/`ScheduledTime` → `NULL`) and *keeps* those rows' `Notes`/`Tags` — deleting
the containing experimental session doesn't destroy per-participant history, it just un-schedules
it. `admin-api.service.ts` gained `deleteExperimentalSession`; the detail page gained a "Obriši
sesiju" button (top-right, red-styled, native `confirm()` guard — no dedicated confirm-modal
component exists in this app yet, judged not worth building for one destructive action). Verified
live end-to-end with a real assigned+noted+tagged participant-session: delete succeeded (no FK
error), the participant-session survived with its Notes/Tags intact and assignment cleared, then
reassigned back to its original experimental session/time to restore the seeded dataset.

### Google Calendar sync for Experimental Sessions — per-researcher OAuth (2026-08-14)

Each researcher connects their OWN Google account (Admin Dashboard → Settings → "Poveži Google
nalog") so that scheduled participant-sessions they assign become real Calendar events, inviting
the participant. First designed as a single fixed Gmail account, then explicitly redesigned
per-researcher after the user pointed out this platform will have multiple independent
researchers across replications — a fixed account would have dumped everyone's schedule into one
person's personal calendar. Full step-by-step setup guide for researchers is
`admin-dashboard-andrejkatin/docs/google-calendar-setup.md` (also published as a shareable
Artifact) — read that before repeating any of this setup for a new researcher.

**Schema** (`Sql/012_google_calendar_sync.sql` + `013_researcher_calendar_oauth.sql`):
`ParticipantSession.GoogleCalendarEventId` (which event) + `GoogleCalendarResearcherId` (whose
calendar it lives on — needed because unassign/reschedule/delete can be triggered by a *different*
researcher than the one who originally assigned it, but only the owning researcher's OAuth token
can patch/delete an event on their own calendar). `Researcher.GoogleCalendarRefreshTokenEnc`
(AES-256-GCM encrypted, key from `CALENDAR_TOKEN_ENCRYPTION_KEY` —
`server/calendar/tokenCrypto.mjs`), `GoogleCalendarEmail`, `GoogleCalendarConnectedAt`.

**Backend**: `server/calendar/googleOAuth.mjs` (consent-URL builder + code-exchange, one shared
OAuth *client registration* — `GOOGLE_CALENDAR_CLIENT_ID`/`SECRET`, a "Web application" type
client — used by every researcher; it's the resulting refresh token that's personal),
`googleCalendar.mjs` (create/update/delete event — every call takes an explicit `refreshToken` for
whichever researcher owns the event, not a global client), `server/calendar/routes.mjs`
(`GET /status`, `GET /connect?token=` — a real full-page navigation so the JWT travels as a query
param instead of a header, `GET /oauth2callback` — public, Google calls this, authenticated via a
short-lived signed `state` value instead of a session, `POST /disconnect` — best-effort Google
token revoke then clears the DB columns regardless). Every calendar call from
`experimental-sessions/routes.mjs` (assign/unassign/reschedule-cascade/delete-cascade) is
best-effort: a Calendar API failure is logged and swallowed, never blocks or fails the DB
operation that triggered it; a researcher who hasn't connected (or has disconnected) simply gets
no event, no error.

**Operational constraint, not silently designed around**: `calendar.events`/`userinfo.email` are
Google "sensitive scopes" — publishing for public use needs Google's verification review (days to
weeks, privacy policy hosting, etc.), overkill for a handful of known researchers. Staying in
**Testing** mode avoids that but requires each researcher's Google account to be added by hand as
a **Test user** first (Google Cloud Console → left nav "Google Auth Platform" → **Audience** tab —
not the old "OAuth consent screen" page name, Google moved this in a UI reorg — → Test users → Add
users), or they'll hit "Access blocked: this app's request is invalid" before ever reaching the
consent screen. Up to 100 test users — far more than this study needs. This is the one recurring
manual step whoever manages the Google Cloud project has to do for every new researcher; documented
in full in the setup guide referenced above.

**Two real setup gotchas hit and fixed live** (both now called out in the guide): (1) the Google
Cloud Console's OAuth consent screen UI was reorganized into "Google Auth Platform" with an
"Audience" tab — Test users lives there now, not under a page literally named "OAuth consent
screen"; (2) enabling the OAuth client alone isn't enough — the **Calendar API itself** must be
separately enabled (APIs & Services → Library → "Google Calendar API" → Enable), missed
initially and caught via a clear `googleapis` error (`"Google Calendar API has not been used in
project ... or it is disabled"`) surfaced in the best-effort log, not a silent failure.

Verified live end-to-end via a real OAuth connection (katin.andrej96@gmail.com) and a temporary
experimental session (id 9, deleted after) built around participant `003`'s real AI-session
assignment (temporarily unassigned from experimental session 4, restored after): assign → real
Calendar event confirmed via a direct `calendar.events.get` call (correct 45-min duration,
participant as attendee, `Europe/Belgrade` timezone); reschedule → same event confirmed moved to
the new date, same time-of-day; unassign → event confirmed `status: "cancelled"` on Google's side,
DB columns cleared; delete-cascade → event confirmed cancelled, the participant-session row
survived with `Notes`/`Tags` intact; disconnect → status flips to not-connected, a subsequent
assign correctly created no event and logged no error. Participant `003`'s AI-session assignment
was restored to its original experimental session (4) and time (11:00) afterward.

### Participant import redesign — 3-sheet Excel template (2026-08-14)

Redesigned `admin-dashboard-andrejkatin`'s "Import participants" feature end-to-end (frontend
`excel-import.service.ts` + `participant-import.component.*`, backend
`server/participants/routes.mjs`'s `POST /import`) per the user's explicit spec, refined through
several rounds of clarification. Template went from 3 sheets (Participants/Sessions/PrAssignments)
to **Instructions, Participants, Tasks** — the old separate "Sessions" sheet (AI/Report
counterbalancing order) and "PrAssignments" sheet (PR pinning) are merged into one **Tasks**
sheet (`ParticipantId, Session, PrNumber`), and a proposed 4th "Sessions" reference sheet was
explicitly dropped after being flagged as likely to confuse (see below).

**Key behavioral changes**:
- **Intro is never listed** in the Tasks sheet — the backend inserts it automatically
  (`SequenceOrder=1`, no PR — Intro has no PR review) the first time a participant gets any
  session at all. The researcher only ever writes AI/REPORT rows.
- **AI/Report order is now derived from ROW ORDER**, per participant, instead of an explicit
  `SessionOrder`/`Session` enum column — whichever of the two session types appears **first** for
  a given participant in the Tasks sheet becomes their `SequenceOrder=2` (right after Intro), the
  other becomes `3`. This directly replaces the old `AI_FIRST`/`REPORT_FIRST` column with "just
  order the rows however you want the study to actually run" — the user's explicit ask, so
  counterbalancing stays fully random per participant without needing a dedicated column.
  Backend logic (`POST /import`): groups Tasks rows by participant preserving array order, tracks
  `usedSeqs` per participant from existing DB rows, assigns `usedSeqs.has(2) ? 3 : 2` to each new
  row — correct whether a participant gets both rows in one import or one now/one later (partial,
  idempotent imports were already a design invariant here, preserved).
- **Participants sheet gained `Email`/`Language` columns** (the `Participant` table already had
  both, just never exposed here) — only applied on `INSERT` for brand-new participants; an
  existing participant (same `ParticipantId`) is still skipped/untouched entirely, unchanged from
  before.
- **A 4th "Sessions" sheet was proposed, then explicitly dropped.** `Sessions` is a fixed,
  globally-shared 3-row lookup table (`1=Intro,2=AI,3=Report`), hardcoded by name/id across this
  entire ecosystem (BeyondAI, NASA-TLX, REI-40/Big Five) — letting an Excel sheet edit or extend
  it would silently create rows nothing else could interpret. Flagged this clearly rather than
  building it; the user's own conclusion was to drop the sheet and instead constrain the Tasks
  sheet's `Session` column to a real Excel dropdown (`AI`/`REPORT` only).
- **Two-library split for template generation vs. parsing**: the free/OSS `xlsx` (SheetJS)
  package — already used and working for *parsing* uploads — can't *write* cell data-validation
  dropdowns. Added `exceljs` (new dependency) for `downloadTemplate()` only (it fully supports
  writing `dataValidation`), left `parseFile()` on `xlsx` unchanged in principle (just adapted to
  the new sheet/column set) rather than risking a rewrite of already-working, tested parse logic.
  Confirmed via a direct Node round-trip test that a workbook written by `exceljs` reads back
  correctly through `xlsx`.
- Backend result shape unified: `{ participants: {imported, skippedExisting}, tasks: {introAdded,
  assigned, errors} }` (`errors[].reasonCode` ∈ `NOT_FOUND | WRONG_REPLICATION | PR_NOT_FOUND |
  SESSION_NOT_FOUND`, same stable-code-for-i18n pattern as before) — replaces the old separate
  `sessions`/`prAssignments` result blocks.

Verified live against the real Neon DB via direct API calls (temp participant `importtest01`,
cleaned up after): a Tasks sheet with **REPORT listed before AI** for the same participant
produced `Intro(1)→Report(2)→AI(3)` exactly, with `PrConfigId` correctly resolved on both;
re-running the identical import was fully idempotent (no duplicate rows, no errors); an unknown
PR number correctly surfaced `PR_NOT_FOUND` per-row while still creating the session (falls back
to the replication's active PR, same as the old PrAssignments behavior); an unknown participant
correctly surfaced `NOT_FOUND`. `ng build` clean throughout (the `participant-import` lazy chunk
grew to ~2.1MB from bundling `exceljs`, only loaded when that page is visited).

### PR Configuration page cleanup (2026-08-14)

Four polish fixes to `admin-dashboard-andrejkatin`'s PR Configuration page
(`src/app/pr-config/`):
- **Autofill background fixed globally** (`src/styles.scss`'s `.text-input`/`.select-input`
  block): the PAT field showing white/yellow in dark mode was Chrome's own `-webkit-autofill`
  styling, not an app bug — nothing in this codebase neutralized it. Added the standard
  `-webkit-autofill` inset-box-shadow override (delayed 9999s transition to prevent a pre-JS
  flash) — applies app-wide, not just this page.
- **3 help icons + 1 shared modal**: small "?" buttons next to Owner+Repo ("GitHub link"), PR
  Number, and Token, each opening the same modal (`helpTopic` signal, `@switch`) with concrete
  steps — reuses the calendar-help modal shell pattern from `configuration.component.scss`
  (duplicated per this project's established per-component convention). New
  `pr-config.component.scss` (was previously empty).
- **Removed real-project example placeholders** from Owner/Repo inputs (`beyondai-researchgroup`,
  `TokenPaymentSystemSolution`); Token's placeholder is now a bullet-dot string instead of a
  fake-token-shaped example.
- **Tasks sheet's PrNumber column now has a real, validated dropdown**, sourced live from the
  selected replication's actual `ReplicationPrConfig` rows (no cap ever existed on that table —
  confirmed no `LIMIT`/row-count check anywhere). Since the list is genuinely unbounded, an inline
  Excel list formula (~255-char limit) wasn't viable — `buildTasksSheet` (`excel-import.service.ts`)
  now writes a `veryHidden` reference sheet (`_PrNumbers`) and points column C's `dataValidation`
  at that range, with `errorStyle: 'error'` (hard block, not just cosmetic) — confirmed via raw
  XLSX XML inspection that Excel actually enforces it. `ParticipantImportComponent` fetches
  `getPrConfigs(replicationId)` fresh before both template download and file parsing (no caching,
  so a just-added PR is picked up immediately); `parseFile` gained an optional `validPrNumbers`
  cross-check as client-side defense in depth on top of the Excel-side block. A replication with
  zero PR configs still downloads a working template — dropdown/validation is simply skipped, with
  an Instructions-sheet note explaining why.

Verified: `ng build` clean; a Node round-trip test (exceljs write → raw XML inspection) confirmed
the hidden sheet (`state="veryHidden"`) and hard-stop dataValidation formula are actually written
correctly, not just assumed.

### EEG chart readability fix (2026-08-14, admin-dashboard-andrejkatin)

Participant Detail's EEG section (Insight-5 band-power/composite-index charts) was reported as
showing "almost nothing readable." Two concrete causes, confirmed against a real stored recording:

- **Band-power chart plotted all 5 bands on one shared Y-axis** — band magnitudes differ a lot by
  nature (theta/alpha ~20-26 vs gamma ~0.5-0.56 in the real data checked), so the smaller bands
  visually flattened to near-zero lines. Fixed by splitting into 5 small-multiple charts
  (`server/eeg/insight5.mjs`'s `downsampleSeries` unchanged in shape, `participant-detail
  .component.ts/.html` renders 5 separate `app-line-chart` instances, each auto-scaled).
- **No smoothing** — ratio metrics (Engagement/Cognitive Load/Frontal Asymmetry) and band series
  were jittery at ~600-point bin resolution. Added a centered `movingAverage` (window 9,
  null-tolerant) applied to the *display* series only — `buildSegments`'s per-segment true means
  still come from unsmoothed rows.
- **Signal quality** (device-reported 0-100% Cortex contact quality) was parsed but discarded —
  now surfaced as an overall stat line + per-segment values (stat tiles, not a 6th chart line,
  since a lone scalar isn't a time series).

Verified live against participant `001`'s real 16287-row recording: `avgSignalQuality` computed
correctly (90.4%), band magnitude disparity confirmed exactly as diagnosed.

### EEG device-type gating + upload format validation (2026-08-14, admin-dashboard-andrejkatin)

Study Configuration's `eegDeviceType` field (previously free text) is now a dropdown: **Emotiv
Insight 5** or **Drugi uređaj** (Other, with an optional free-text name for metadata only). This
drives 3 things:

- **Upload validation**: `POST /api/admin/eeg/:participantId` now rejects (`400 INVALID_FORMAT`,
  file not stored) a CSV that doesn't match the Insight-5 29-column header *when the replication is
  configured for that device* — for any other device, any CSV is accepted unvalidated (download/
  preview-only, as before).
- **Interpretation gating**: `GET /.../interpretation` short-circuits to `{recognized:false}`
  without attempting to parse at all when the configured device isn't Insight-5 (defense in depth,
  even against a coincidentally-matching CSV from a different device).
- **Participant Detail UI**: shows the full chart suite only for Insight-5; otherwise a hint
  explaining automatic charts aren't available for that device, upload/download still work.

`RECOGNIZED_DEVICE` constant lives in `server/eeg/insight5.mjs` (single source of truth for what
the backend treats as parseable); `EEG_DEVICE_INSIGHT5` in `configuration.component.ts` must match
it exactly (both literal `'Emotiv Insight 5'`). Verified live via a temp participant/replication:
bad-format upload on an Insight-5 replication → 400, nothing stored; same file on an "Other"
replication → 201, stored unvalidated; interpretation on the "Other" replication → recognized:false
with no parse attempt.

### REI short-form variant (2026-08-15, rei40-andrejkatin + admin-dashboard-andrejkatin)

Added a second, shorter REI questionnaire selectable per-replication via the existing
`Rei40Variant` config (previously only `'v1'` was ever real, despite the plumbing already existing
end-to-end since an earlier phase).

**Sourcing constraint, re-confirmed**: the official Norris/Pacini/Epstein (1998) REI-10 is an
unpublished instrument — checked directly against `sjdm.org/dmidi` (the standard public repository
for these scales), only 2 of its 10 items are publicly available anywhere, same wall hit
documented in an earlier phase of this project. Per the user's explicit decision, the new variant
is **not** the official REI-10 — it's a transparent 10-item subset of the REI-40 item pool this
app already has full, legitimate text for (no fabricated content, no new translations). Never
labeled "REI-10" anywhere — code uses `variant` value `'short'`, UI copy says "Skraćena verzija
(10 stavki, iz REI-40 seta)".

**Item selection** (`rei40-andrejkatin/src/app/data/rei40-short-items.ts`): Rationality side = RA
`{1,2,6}` + RE `{11,12}`; Experientiality side = EA `{21,22,26}` + EE `{31,34}` — balanced across
the original 4 facets and reverse-scoring. Reports only `rationality`/`experientiality` composites
(mean of each 5-item side), not RA/RE/EA/EE facet scores — 2-3 items isn't a meaningful facet
measurement, and this mirrors the real REI-10's own 2-composite reported shape.

**`test.component.ts`** branches on `state.rei40Variant` (2-section short-form layout vs the
existing 4-section REI-40 layout) rather than a separate page/route — one flow to keep correct
instead of two drifting copies; the existing template was already generic enough (`section
.subscale`/`item.id` driven) to need zero HTML changes.

**`server.mjs`**: `validatePayload`/insert now handle the 4 facet scores as optional (short form
sends `null`), `variant` restricted to a `['v1', 'short']` allowlist. Also **closed a pre-existing
gap** found during this work: `POST /api/result` previously upserted unconditionally
(`ON CONFLICT ... DO UPDATE`) — only the magic-link path's `ALREADY_COMPLETED` check stopped a
resubmit, so the dev-login path had no independent guard. Now checks for an existing `Rei40Result`
row up front and returns `409 ALREADY_COMPLETED` instead of upserting, for both variants.

**Schema drift caught and fixed**: `Sql/008_rei40_answers_and_variant.sql` already declared the 4
facet columns nullable, but the live Neon table (created ad-hoc before that migration was written)
had `NOT NULL` on all 4 anyway — undetected until now since every prior real submission (`'v1'`)
always supplied all 6 scores. New `Sql/014_rei40_nullable_facet_scores.sql` drops those 4
constraints (idempotent); applied directly to Neon and verified via `information_schema.columns`.

**Admin Dashboard**: `study-config/routes.mjs`'s `REI40_VARIANTS` grew to `['v1', 'short']`
(existing lock-once-any-result-exists logic needed no change, and now meaningfully prevents
switching variants mid-replication). `results/routes.mjs`'s REI-40 raw/export selects now include
`Variant`. `participant-detail.component.ts`'s REI-40 bar chart branches on the result's `Variant`
— short-form rows render a 2-bar Rationality/Experientiality chart instead of defaulting the NULL
facet columns to a misleading `0`. `rei40-items-meta.ts` (answer-detail-modal's item-text lookup)
gained a `'short'` entry — the modal's `[variant]` input plumbing already existed unused from the
earlier infra-only phase, so this was a pure data addition, no component changes.

Verified live end-to-end via a temp replication + 2 temp participants (cleaned up after): dev-login
and magic-link resolution both return `rei40Variant: 'short'` correctly; a 10-item submit stores
`Rationality`/`Experientiality` populated with `RationalAbility` etc. `NULL` and `Variant: 'short'`;
a second submit attempt correctly 409s; a second magic-link visit correctly shows
`ALREADY_COMPLETED`; Study Configuration's `PUT` correctly 409s a `v1`↔`short` switch attempt once
a result exists, but allows a same-value resubmit; the results API returns `Variant` and null
facets correctly. Participant `001`'s real `'v1'` data (RationalAbility 3.20, Rationality 2.70)
confirmed completely unaffected throughout. `ng build` clean on both apps.

### Two small fixes — REI variant label i18n, stale over-limit tag data (2026-08-17)

- **`configuration.component.ts`'s REI variant dropdown labels were hardcoded Serbian strings**
  (`variantLabel()` returned literal `'REI-40 (40 stavki, 4 podskale)'` etc. regardless of UI
  language) — reported as still showing Serbian with English selected. Moved to
  `STUDY_CONFIG.REI40_VARIANT_OPTION_V1`/`_SHORT` i18n keys (sr/en). Also fixed a second latent
  bug while touching this: `rei40VariantOptions` was a plain `signal<SelectOption[]>` populated
  once at load time with pre-baked label strings, so even after the i18n fix, switching language
  *after* the page had already loaded wouldn't re-translate the dropdown (only a fresh
  replication-switch/reload would). Changed to `rei40VariantIds` (raw ids) + a `computed` deriving
  labels via `variantLabel()`/`this.t()` each time — same pattern `eegDeviceOptions` already used
  correctly from the start.
- **Experimental Sessions tag picker's "+" button silently didn't appear for one specific session**
  (participant `001`'s AI session) — root cause was stale data, not a code bug: that row had 6
  tags stored in `ParticipantSession.Tags` from before `MAX_TAGS` was reduced to 1
  (`session-tags.ts`/`experimental-sessions/routes.mjs`), and the template only renders the add
  button `@if (row.tags.length < maxTags)` — `6 < 1` is always false. Cleared that row's `Tags` to
  `[]` directly (confirmed via the `GET /api/admin/experimental-sessions/:id` response afterward);
  no other assigned `ParticipantSession` row had more than 1 tag, so this was an isolated leftover
  from testing the tags feature before the 1-tag cap existed, not a systemic issue.

### Consent email instrument labels: "Psihološki test N" → "Upitnik N" (2026-08-18)

The 2026-08-13 anti-priming pass had already removed literal "REI-40"/"Big Five" text from both
`consent-andrejkatin/server/email/consentEmail.mjs` and `admin-dashboard-andrejkatin/server/
email/regenerateLinksEmail.mjs` in favor of generic numbered labels — but those labels read
"Psihološki test 1"/"Psihološki test 2" (sr) / "Psychological Test 1"/"Psychological Test 2" (en),
not what the user wanted. Changed both files' `rei40Label`/`bigfiveLabel` copy to "Upitnik 1"/
"Upitnik 2" (sr) and "Questionnaire 1"/"Questionnaire 2" (en) — the two spots are each label's one
appearance in its own link block, matching "na dva mesta" from the report. Kept both email
templates in sync per `regenerateLinksEmail.mjs`'s own doc comment. Verified by actually calling
`buildConsentEmail()` for both languages and regex-checking the rendered HTML contains the new
labels and no `REI-40`/`Big Five` substring.

### Platform-ification Phase A — full rename: Replication → Research (2026-08-18)

First of 4 planned phases turning this from a one-study dashboard into a general multi-experiment
platform (Phase B: researchers as many-to-many; Phase C: configurable Consent Form builder;
Phase D: optional psychological tests + configurable thank-you flow — none started yet). Phase A
is a **full rename**, DB included (user's explicit choice over a UI-only-labels recommendation):

- **New migration** `Sql/015_rename_replication_to_research.sql` — `ALTER TABLE/COLUMN ... RENAME`
  (table `Replication`→`Research`, dependent table `ReplicationPrConfig`→`ResearchPrConfig`,
  column `ReplicationId`→`ResearchId` on `Researcher`/`Participant`/`ParticipantSession`/
  `ResearchPrConfig`/`ExperimentalSession`, plus every index/constraint carrying the old name —
  including auto-generated PK/NOT NULL constraint names, which needed a broader `pg_constraint`
  sweep than FK-only after an initial pass missed them). Historical migrations 002-009 left
  untouched on purpose (record of what was actually run). **Applied live to Neon** — verified via
  `information_schema`/`pg_constraint`/`pg_indexes` that zero `replicat*` identifiers remain
  (excluding Postgres's own unrelated `pg_replication_origin*` system catalog) and that existing
  data survived (participant `001`'s real REI-40 scores, the seeded research row).
- **`CodeReviewAI.Api`**: `StudyService.cs`'s 2 raw-SQL identifiers + doc-comment sweep.
- **`admin-dashboard-andrejkatin`** (heaviest consumer, ~30 files): backend routes/scope/JWT
  (`resolveResearchScope`, JWT payload `researchId`), `server/replications/`→`server/researches/`
  folder+mount-path rename; frontend types/services/routes (`ResearchSummary`/`Detail`/`Input`,
  `researches-store.service.ts`, `/researches` route, `ResearchesManageComponent`), i18n —
  English strings mechanically via a longest-pattern-first sed sweep (`REPLICATIONS`→`RESEARCHES`
  before `REPLICATION`→`RESEARCH`, etc., so plurals didn't come out as "Researchs"); **Serbian
  `sr.json` prose fixed by hand** afterward, not sed — "replikacija" (feminine) → "istraživanje"
  (neuter) changes grammatical agreement throughout each sentence (ovoj→ovom, novu→novo, etc.),
  not just the noun.
- **`rei40-andrejkatin`**: 2 JOIN lines in `server.mjs` + comment sweep.
- **`Nasa-TLX-FullImplementation-AndrejKatin`**: 1 JOIN line in `api/_lib/db.ts` + comment sweep
  (including the `sr.json` Serbian phrase, same by-hand-grammar approach as above).
- **`bigfive-andrejkatin`**: confirmed zero references, untouched.

**A real bug caught during verification, not just assumed away**: the FK-constraint-rename DO
block in the migration only matched `contype = 'f'`, missing auto-generated PRIMARY KEY and named
NOT NULL constraints that also carried the old identifier (`Replication_pkey`,
`Participant_ReplicationId_not_null`, etc.) — caught by re-querying `pg_constraint` after the
first pass and finding ~20 leftover names; fixed by broadening the filter to match any constraint
type, re-applied, re-verified clean. Also hit the `%I`-double-quotes-an-already-quoted-
`regclass::text` bug once (a table name coming from `conrelid::regclass::text` is pre-quoted when
needed; feeding it through `format('...%I...', ...)` again produced `"""Researcher"""` and a
"relation does not exist" error) — fixed by using `%s` for that one substitution, `%I` only for
the genuinely-unquoted `conname` values.

Verified live end-to-end post-migration: `rei40-andrejkatin`'s dev-login (`GET /api/participant/
001`) resolves `rei40Variant` correctly through the renamed join; `admin-dashboard`'s researches
list/study-config/results-raw endpoints all return correct data; NASA-TLX's `tlx-config` endpoint
(restarted from a fresh `ng build` — the already-running SSR process was serving a stale
pre-rename bundle, the same "stale process" gotcha hit repeatedly in this project) returns correct
config. `dotnet build`+`dotnet test` (39/39), both `ng build`s (admin-dashboard, NASA-TLX
browser+SSR), all touched `.mjs` `node --check` — all clean.

**Resolved (2026-08-18)**: pushed a narrow commit of just the rename-related files
(`StudyEndpoints.cs`/`IStudyService.cs`/`StudyService.cs`/`Sql/015_...sql`, plus the never-before-
committed `Sql/` folder) — deliberately excluding the unrelated uncommitted frontend work noted
above, which is still sitting in the working tree untouched. Render's autoDeploy picked up the
push on its own (confirmed live via `POST /api/study/start-review` successfully querying
`ResearchPrConfig`/`ResearchId`). NASA-TLX needed a manual `npx vercel login` (a browser-only
Google/GitHub login does NOT authenticate the CLI — `vercel login` is a separate step that must
be run from the terminal) + `npx vercel --prod` from the NASA-TLX repo root — deployed and
aliased to `https://beyondai-nasa-tlx.vercel.app`, confirmed live via `GET
/api/db/tlx-config/001`. Both production surfaces now match the renamed schema.

### Platform-ification Phase B — researchers as many-to-many (2026-08-18)

Second of the 4 platform-ification phases (see Phase A above). Previously `Researcher.ResearchId`
was a single nullable FK with a DB CHECK (`IsSuperAdmin=TRUE ⇔ ResearchId IS NULL`) — one
researcher could belong to at most one research, and there was no management UI (accounts were
hand-inserted via `scripts/hash-password.mjs` + a manual Neon INSERT).

**Schema** (`Sql/016_researcher_research_many_to_many.sql`, applied live to Neon): new
`ResearcherResearch` join table (`ResearcherId`, `ResearchId`, composite PK, `ON DELETE CASCADE`
both ways); existing single-`ResearchId` assignments backfilled into it; the old
`Researcher_Scope_CHK` constraint and the `Researcher.ResearchId` column both dropped. The
superadmin-implies-no-assignment rule is no longer DB-enforced — it's an application-layer rule
in `server/researchers/routes.mjs` now (a superadmin's `researchIds` is always forced to `[]` on
create/edit, regardless of what's submitted).

**Backend**: `scope.mjs`'s `resolveResearchScope` changed from an equality check to a membership
check against `researcher.researchIds` (an array baked into the JWT at login, same
resolved-once-per-login convention `isSuperAdmin` already used — an assignment change takes
effect on the researcher's next login, not instantly). Behavior by assignment count when no
explicit `researchId` is requested: exactly one → auto-selected (same convenience as before
Phase B); zero → throws (misconfigured account); more than one → throws, requiring the caller to
specify which one (the frontend always shows a picker in this case). Every downstream route
(`participants`, `results`, `experimental-sessions`, `eeg`, `study-config`) needed **zero
changes** — they all go through `resolveResearchScope`, so the membership-vs-equality swap was
fully isolated to `scope.mjs`. New `server/researchers/routes.mjs` (mounted at
`/api/admin/researchers`, superadmin-only): `GET /` (list with assigned researches), `POST /`
(create — bcrypt hash via the same `bcryptjs` convention as `hash-password.mjs`; a non-superadmin
needs ≥1 `researchIds`), `PUT /:id` (edit username/role/password — password only changes if a
non-empty `newPassword` is sent — and replaces the assignment set wholesale via delete-then-
reinsert), `DELETE /:id` (refuses to delete the last remaining superadmin, checked via a live
`COUNT(*) WHERE IsSuperAdmin=TRUE` — otherwise the dashboard could lock itself out of superadmin
access with no way back short of a direct SQL fix).

**Frontend**: `AuthService`'s `Researcher` interface gained `researches: {id,name}[]` (replacing
the old singular `researchId`/`researchName`). `DashboardShellComponent`'s research picker now
shows for **any** researcher assigned to more than one research, not just superadmin (a locked
label still shows for the single-assignment case, unchanged UX from before Phase B). New
`/researchers` page (superadmin-only, `ResearchersManageComponent` — same
add/edit-form-plus-list-table pattern as the existing `/researches` page): username/password
fields, a superadmin toggle (reuses the existing `.toggle-switch` styling), and a scrollable
checkbox list of every research (hidden when the superadmin toggle is on, since assignments are
irrelevant then) for `researchIds`. New nav entry "Istraživači"/"Researchers", right below the
existing "Istraživanja"/"Researches" one. `AdminApiService` gained
`getResearchers`/`createResearcher`/`updateResearcher`/`deleteResearcher`. New i18n namespace
`RESEARCHERS.*` (sr/en) added; also fixed a stray leftover from the Phase A rename sweep while in
this file — `RESEARCHES.SUBMIT_ADD` still read "Dodaj repliku" (a `sed` pass miss, since
"repliku" is an inflected form the fixed pattern list didn't cover), corrected to "Dodaj
istraživanje".

Verified live end-to-end via a temp superadmin + temp scoped researcher (both created directly
via a throwaway SQL insert / the new API, deleted after): login for both correctly returns
`researchIds`/`researches` in the new shape; the temp scoped researcher (assigned to research 1)
could `GET /api/admin/researches/1` but got a 403 `ScopeForbiddenError` on a nonexistent research
id 999, confirming membership-based scope resolution actually rejects out-of-scope ids; `POST
/api/admin/researchers` with an empty `researchIds` on a non-superadmin correctly 400s; `PUT`
edit and `DELETE` both round-tripped correctly. `ng build --configuration development` clean;
`node --check` clean on every touched `.mjs`. The last-remaining-superadmin delete guard was
verified by code review only (not live-tested against the real `andrej` account, to avoid any
risk to production superadmin access) — its logic is a plain `COUNT(*) <= 1` check, low risk.

**Not done**: no data migration path for reassigning research access after the fact was needed
beyond the edit form itself (delete-then-reinsert the whole set is simple and correct at this
scale). Phase C (Consent Form builder) and Phase D (optional psych-tests + thank-you flow) still
not started — see [[platform_ification_project]] memory / this file's Phase A section for the
overall roadmap.

### Platform-ification Phase C — configurable Consent Form (2026-08-18)

Third of the 4 platform-ification phases. Replaces `consent-andrejkatin`'s single fixed
`CONSENT.SECTION1..5` i18n text — shared by every research regardless of what that particular
study actually involves — with per-research sections a researcher can freely add/remove/reorder/
edit through a new Admin Dashboard page. Turned out the "give the Consent app research-awareness"
prerequisite from the original plan was smaller than expected: `Participant.ResearchId` already
existed (added back in the original multi-university "Replication" work) and was already
populated on every participant — only the Consent app's own `server.mjs` never queried it.

**Schema** (`Sql/017_consent_sections.sql`, applied live to Neon): new `ConsentSection` table
(`ResearchId` FK `ON DELETE CASCADE`, `SortOrder`, `TitleSr`/`TitleEn` nullable, `BodySr`/`BodyEn`
required); `Research` gains two nullable `ConsentCheckboxTextSr`/`En` columns for the agreement
checkbox's text. Seeded 12 default sections for research Id=1 directly in the migration, split
one-paragraph-per-section from the original fixed text (confirmed default, per earlier
instruction) — where the original had a single heading covering 3 plain paragraphs (e.g.
"Dobrovoljnost učešća..."), the 2nd/3rd paragraphs became sections with a `NULL` title
(continuation of the previous heading) rather than inventing sub-headings that didn't exist.

**Scope boundary, explicit not accidental**: only the section list + the agreement checkbox text
became per-research configurable. The form's top header (`CONSENT.HEADING`, `RESEARCH_TITLE`,
`RESEARCH_TEAM`) stays static app-level i18n, unconfigured — the original request's focus was
specifically "svaka sekcija", and adding a 3rd configurable surface (header) wasn't asked for;
flagged here in case a future phase should extend it.

**Backend** (`admin-dashboard-andrejkatin/server/consent-sections/routes.mjs`, mounted at
`/api/admin/consent-sections`): `requireAuth` only, not superadmin-gated — same pattern as
`study-config/routes.mjs`, since this is per-research instrument config a scoped researcher
legitimately owns for their own research. `GET /:researchId` checks the research exists **before**
lazily seeding defaults (a bug caught live: seeding against a nonexistent research threw a raw FK-
violation 500 instead of a clean 404 — existence check reordered to run first). `PUT /:researchId`
replaces the whole section set atomically (delete-then-reinsert, `SortOrder` = array index — same
pattern Phase B's `ResearcherResearch` PUT already used, makes drag-reorder trivial: the client
just resubmits its whole reordered array) plus updates the two checkbox-text columns. Default
content lives in `server/consent-sections/defaults.mjs` — **duplicated** verbatim into
`consent-andrejkatin/server/consent-defaults.mjs` (these two apps share no package; same
"duplicated per this project's established convention" pattern as `regenerateLinksEmail.mjs` vs
`consentEmail.mjs` — keep both in sync if the default text ever changes) since the Consent app
needs its own auto-seed-on-first-read fallback independent of whether an admin ever opened the
config page for that research first.

**Frontend** (`admin-dashboard-andrejkatin`): new `/consent-form` page, own nav entry
("Obrazac pristanka"/"Consent form", per explicit request — not folded into the existing
Configuration page), scoped to the sidebar's selected research via the same
`toObservable(scope.selectedResearchId)` reactive-load pattern `ConfigurationComponent` already
used. Drag-to-reorder via `@angular/cdk`'s `DragDropModule` (already a dependency, same module the
Calendar page uses) — `moveItemInArray` on drop. Each section card: title fields (SR/EN, optional
— empty renders no heading, for continuation paragraphs) + body textareas (SR/EN, required),
remove button, drag handle. The checkbox text fields render in their own block below the
reorderable list, structurally separate — **not** wrapped in `cdkDrag`, so it's physically
impossible to drag it into the middle of the section list, matching the explicit "fiksiran na
kraju, samo tekst editabilan" confirmation from the original planning pass.

**`consent-andrejkatin` (research-aware consent flow)**: `GET /api/participant/:id` now joins
through to the participant's `ResearchId`, lazily seeds defaults if empty (same logic as the admin
backend, duplicated), and returns `consentSections`/`checkboxTextSr`/`checkboxTextEn` alongside
the existing `exists`/`alreadyConsented`/`language` fields — falls back to the original fixed
defaults if a participant has no `ResearchId` on file (a pre-Phase-A/B row, or the unused classic-
loader flow) rather than erroring. `StateService`'s `ConsentState` carries the fetched section
list + checkbox text from login through to the consent page (avoids a second round-trip);
`ConsentComponent` resolves each section's/the checkbox's display text from the participant's
already-locked `lang` via a `computed`, replacing the old hardcoded `@if`/`SECTION1..5` template
with a single `@for` over the fetched list. The now-unused `CONSENT.SECTION1..5`/`TASK_LABEL`/etc.
i18n keys were left in place (dead but harmless, still function as the ultimate fallback content
baked into `defaults.mjs`/`consent-defaults.mjs`) rather than deleted.

Verified live end-to-end (temp superadmin researcher, cleaned up after; real participant `001`'s
data touched during PUT testing and explicitly restored to the original 12 sections/checkbox text
afterward, confirmed via a direct DB re-check and a fresh `GET /api/participant/001` showing the
original content again): `GET /api/admin/consent-sections/1` returns exactly the 12 seeded
sections; `GET .../999` correctly 404s post-fix (repro'd the FK-violation 500 first, then
confirmed the fix); `PUT` correctly replaces the section set and checkbox text; `GET
/api/participant/001` (consent-andrejkatin) returns the same 12 sections with correct Serbian
diacritics end-to-end (č/ć/ž/đ survived the generated-SQL migration, the DB round-trip, and the
JSON response). `ng build --configuration development` clean on both
`admin-dashboard-andrejkatin` and `consent-andrejkatin`; `node --check` clean on every touched
`.mjs`.

**Not done**: Phase D (optional psych-tests toggle + configurable thank-you flow) not started —
see [[platform_ification_project]] memory for the overall roadmap.

### Platform-ification Phase D — optional psych-tests toggle + thank-you flow (2026-08-18)

Fourth and final planned phase. A research can now opt out of psychological tests (REI-40/Big
Five) entirely — useful for a study design that doesn't need them — via a toggle on the
Configuration page; when off, consent no longer issues survey tokens and the participant gets a
fixed-template thank-you email instead, with researchers following up directly.

**Schema** (`Sql/018_psych_tests_toggle.sql`, applied live): `Research` gains `UsesPsychTests`
(`BOOLEAN NOT NULL DEFAULT TRUE` — every existing research keeps today's behavior unchanged) and
`StudyDisplayName` (`VARCHAR(200)`, nullable — falls back to the research's own `Name` in code
when unset, so the field is never blank in practice). Seeded research 1's `StudyDisplayName` from
its `Name` for a sane starting value.

**Backend**: `study-config/routes.mjs`'s GET/PUT extended with the two new fields (now 7 columns
total, still a narrower PUT than `researches/routes.mjs`'s own — same non-overlapping-field-sets
pattern as EEG/TLX/REI-40). `studyDisplayName` is required non-empty server-side (falls back to
`Name` only when reading a pre-Phase-D research that never got one explicitly set).

**Frontend** (`admin-dashboard-andrejkatin`): Configuration page's Study Configuration section
gained a 4th subsection ("Psihološki testovi") — a toggle (reusing the existing `.toggle-switch`
styling) + a `studyDisplayName` text field, both wired into the same reactive form/save flow the
other 3 subsections already used. No new page/nav entry — this slots into the existing
Configuration page alongside EEG/TLX/REI-40, unlike Phase C's Consent Form which got its own nav
entry per explicit request.

**`consent-andrejkatin`**: `POST /api/consent/submit` now joins `Participant`→`Research` to read
`UsesPsychTests`/`StudyDisplayName` (defaults to `true`/research `Name` for a participant with no
`ResearchId`, same "never break, fall back to original behavior" rule the Phase C consent-section
resolution already used) and branches: `true` → unchanged REI-40/Big Five token issuance + the
existing "here are your links" email; `false` → skips token issuance entirely and sends a new
fixed-template thank-you email (`server/email/thankYouEmail.mjs`, same visual identity as
`consentEmail.mjs` — only the study's display name is interpolated in, HTML-escaped since it's
researcher-supplied free text rather than fixed copy). The `/done` page's message is unchanged
either way — a participant can't tell from `/done` alone whether links were issued, which is fine
since it's mail-delivered regardless (matches the original plan exactly).

**Not done, matches original scope**: the thank-you email's body text is fixed, not free-form —
only the study display name is configurable, per explicit confirmation in the original planning
pass ("Samo naziv istraživanja").

Verified live end-to-end via a fully isolated temp research + temp participant (both deleted
after, real research 1/participants 001-003 never touched): `GET study-config/:id` on a fresh
research correctly defaults `usesPsychTests: true`; a first consent submit issued both
`SurveyAccessToken` rows as before; toggling `usesPsychTests` to `false` via `PUT study-config`
then resubmitting consent left both tokens' `CreatedAt` **unchanged** (proving no re-issuance
happened, not just that the endpoint returned `ok: true`) and sent the thank-you email path
successfully. `ng build --configuration development` clean on both
`admin-dashboard-andrejkatin` and `consent-andrejkatin`; `node --check` clean on every touched
`.mjs`.

**All 4 platform-ification phases (A-D) are now done.** See [[platform_ification_project]] for
the full roadmap record.

### Task Configuration project — Phase 1: pluggable task types (2026-08-19)

New 3-phase project (separate from platform-ification A-D above): generalizes
`admin-dashboard-andrejkatin`'s old "PR Configuration" page into "Task Configuration" — a
research picks what *kind* of task its participants' materials/results center on, via a new
dropdown, and the page layout switches accordingly. Three types: **Pull Request Review** (today's
exact behavior), **Google Forms** (a saved reference link + mandatory multi-file survey-results
upload/download, plus an opt-in sandboxed R-script analysis step — Phases 2/3), and **Generic /
Custom Task** (my suggestion, accepted: free-text instructions + the same file-upload capability,
kept extensible for whatever task type comes up next). Confirmed explicit scope boundaries:
**purely administrative** — does not touch the BeyondAI participant-facing review flow
(`code-review-ai`) at all; Google Forms integration stays at "save the link, no parsing" (no
public API exists to read a form's structure without the owner's OAuth); the R-analysis sandbox
(Phase 3, not started yet) will use real Docker execution with **no network access**, so only
whatever R packages are pre-baked into the image are ever available to a script.

**Phase 1 — done (2026-08-19): TaskType foundation + page rename, PR Review layout unchanged.**
`Sql/019_task_type.sql` (applied live): `Research.TaskType VARCHAR(30) NOT NULL DEFAULT
'PR_REVIEW'` — every existing research keeps today's exact behavior. New
`server/task-config/routes.mjs` (`requireAuth`-only, scope-guarded, same narrow-PUT pattern as
`study-config/routes.mjs`) — `GET`/`PUT /:researchId` for just this one column so far, validated
against an in-code `TASK_TYPES` allowlist (same low-cost extensibility pattern this codebase
already uses for `REI40_VARIANTS` — a future 4th type is one array entry + one small component +
one `@switch` case).

Frontend: old standalone `/pr-config` route + `PrConfigComponent` are gone — the page is now
`/task-config` → `TaskConfigComponent` (nav label "Konfiguracija zadatka"/"Task configuration"),
which loads/saves the selected research's `TaskType` via a new `<app-select>` dropdown and
`@switch`es the layout below it. The old PR-config form/history-table/help-modals moved verbatim
into a new child component, `pr-review-task.component.ts/.html` (`src/app/task-config/pr-review-
task/`) — same logic, only the outer `page-header` was stripped since the host page owns that
now; the old `PR_CONFIG.*` i18n keys were kept as-is (still accurate, unchanged content) rather
than renamed. `google-forms-task.component`/`generic-task.component` are stub placeholders today
("coming soon" hints) — Phase 2 fills them in. Task-type switches are optimistic (layout swaps
immediately, rolls back with an error message if the `PUT` fails).

Verified live via a temp research + temp superadmin (both deleted after): a brand-new research's
`GET /api/admin/task-config/:id` correctly defaults to `PR_REVIEW`; `PUT` to `GOOGLE_FORMS`
persists and reads back correctly; an invalid value correctly 400s; and — critically — research
1's **real** `TaskType` (still `PR_REVIEW`) and its real `ResearchPrConfig` history (the live demo
PR, `beyondai-researchgroup/TokenPaymentSystemSolution#1`) were both confirmed completely
unaffected by this refactor. `ng build --configuration development` clean; `node --check` clean
on every touched `.mjs`.

**Not started**: Phase 2 (multi-file upload primitive; Google Forms link + mandatory file upload;
Generic layout) and Phase 3 (sandboxed R script execution via a new local Docker image — real new
infrastructure, its own setup doc, not started). See [[task_configuration_project]] memory for the
full roadmap and phase design.

### Task Configuration project — Phase 2: file uploads, Google Forms link, Generic layout (2026-08-19)

**Schema** (`Sql/020_task_files_and_google_forms.sql`, applied live): `Research` gains
`GoogleFormsUrl`/`TaskInstructions` (both nullable `TEXT`); new `TaskFile` table
(`ResearchId` FK `ON DELETE CASCADE`, `OriginalFilename`, `ContentType`, `FileContent` `BYTEA`,
`FileSizeBytes`, `UploadedAt`) — no uniqueness constraint, many files per research (unlike
`EegRecording`'s one-per-participant), `BYTEA` instead of `EegRecording`'s `TEXT` since survey
exports/attachments can be binary (XLSX, PDF, etc.), not just CSV.

**Backend**: new `server/task-files/routes.mjs` — `GET /:researchId` (list metadata), `POST
/:researchId` (multer `upload.array('files', 10)`, same error-wrapper pattern as EEG's single-
file upload, one `INSERT ... RETURNING` per file), `GET /:researchId/:fileId/download` (streams
raw bytes with `Content-Disposition`), `DELETE /:researchId/:fileId` — all scope-guarded via the
usual `resolveResearchScope`. Caught the same bug class fixed once already in Phase C's
consent-sections route (existence-check-before-insert to avoid a raw FK-violation 500) by writing
it correctly the first time here. `server/task-config/routes.mjs` gained a **second**, separate
`PUT /:researchId/details` endpoint for `googleFormsUrl`/`taskInstructions` — kept apart from the
existing `PUT /:researchId` (which only ever touches `taskType`) since they save on different
triggers: the Task Type dropdown auto-saves the instant it changes, while the Google Forms/Generic
forms save via an explicit button, like every other per-research settings form in this app.

**Frontend**: new reusable `TaskFileListComponent` (`src/app/task-config/task-file-list/`) —
multi-file `<input>`, per-row download/delete, upload-in-progress state — shared by both
`GoogleFormsTaskComponent` (URL field + this file list, labeled "Rezultati ankete"/"Survey
results") and `GenericTaskComponent` (instructions `<textarea>` + this same file list, labeled
"Prateći materijali"/"Supporting materials"). `AdminApiService` gained
`getTaskFiles`/`uploadTaskFiles`/`downloadTaskFile`/`deleteTaskFile` (download follows the
existing `fetch`+`blob`+synthesized-`<a>` pattern from EEG's `downloadEeg`) and
`updateTaskDetails`.

Verified live via a temp research + temp superadmin (both deleted after): uploaded a text CSV and
a 1KB random-bytes binary file in one request, downloaded both back and confirmed **byte-for-byte
identical MD5 checksums** against the originals (proves the `BYTEA` round-trip is truly
binary-safe, not just "happens to work for text"); listed (both present, newest-first), deleted
one (list correctly shrank to the other); saved a Google Forms URL + task instructions via the
new `details` endpoint and confirmed both persisted and read back correctly. `ng build
--configuration development` clean; `node --check` clean on every touched `.mjs`.

**Not started**: Phase 3 (sandboxed R script execution — new Docker image, `AnalysisScript`/
`AnalysisRun`/`AnalysisRunPlot` tables, `--network none` execution runner). See
[[task_configuration_project]] memory for the full roadmap.

### Task Configuration project — Phase 3: sandboxed R script execution (2026-08-19)

Fills in the Google Forms task type's opt-in "R analysis" step — a researcher uploads one `.R`
script (most-recent-wins) that runs against the research's uploaded survey-result files inside a
sandboxed Docker container, producing plots + an interpreted text summary.

**New Docker image** (`docker/r-runner/Dockerfile`, `docker/r-runner/README.md`): `rocker/r-ver:
4.4.1` (version-pinned R) + `ggplot2`/`dplyr`/`tidyr`/`readr`/`psych`/`jsonlite` baked in via
`install2.r`; runs as a fixed non-root `uid:gid 1000:1000` matching the runner's `--user`
flag. No `ENTRYPOINT`/`CMD` — always invoked as `Rscript /script.R`. New
`docs/task-r-analysis-setup.md` (Docker Desktop prerequisite, build command, package list,
`/data`+`/output` convention, an example script, troubleshooting) — full walkthrough for whoever
sets this up, kept separate from the code since it's a one-time human task, same treatment as
`docs/google-calendar-setup.md`.

**Schema** (`Sql/021_r_analysis.sql`, applied live): `AnalysisScript` (`UNIQUE(ResearchId)` —
enforces the most-recent-wins single-script-per-research design at the DB level, not just in
application code), `AnalysisRun` (`Status` ∈ PENDING/RUNNING/SUCCESS/FAILED/TIMEOUT,
StdOut/StdErr/ResultsText), `AnalysisRunPlot` (BYTEA `ImageData` per generated PNG).

**Backend**: new `server/analysis/runner.mjs` — `executeRun(researchId, runId)`, invoked
fire-and-forget from the route handler (HTTP response returns the run's id immediately; the
frontend polls for status). No new npm dependency — shells out to the `docker` CLI directly via
`child_process.execFile('docker', [...])`. Per run: writes every `TaskFile` row's bytes into a
fresh temp dir's `data/` subfolder (filenames sanitized via `path.basename` + a character
allowlist, defense against path traversal since the original filename is user-supplied), writes
the script to `script.R`, then spawns
`docker run --rm --network none --memory=512m --cpus=1 --read-only --tmpfs /tmp --user 1000:1000
-v <tmp>/data:/data:ro -v <tmp>/output:/output -v <tmp>/script.R:/script.R:ro task-r-runner
Rscript /script.R` with a 120s `execFile` timeout (Node kills the process and reports `killed:
true, signal: 'SIGTERM'` on timeout, mapped to `Status = 'TIMEOUT'`; any other non-zero exit →
`FAILED`; zero exit → `SUCCESS`). Reads back every `/output/*.png` into `AnalysisRunPlot` rows
and `/output/results.txt` into `ResultsText`, always cleans up the temp dir in a `finally` block
(confirmed live — see verification below), and releases a simple in-memory
`Set<researchId>`-based one-run-at-a-time-per-research lock regardless of outcome. New
`server/analysis/routes.mjs` — script upload/metadata, run list/create/detail, plot image
serving — same `requireAuth`+`resolveResearchScope` pattern as the rest of Task Configuration; a
`POST .../runs` while the research already has a run in flight 409s with `ALREADY_RUNNING`, one
with no script uploaded yet 400s with `NO_SCRIPT`.

**Frontend**: new `RAnalysisComponent` (`src/app/task-config/r-analysis/`), embedded in
`GoogleFormsTaskComponent` — collapsed behind a toggle by default (no separate DB "enabled"
flag; an uploaded script is itself the opt-in signal). Script upload, a Run button, a run-history
table (status badge, polls every 2s while any run is PENDING/RUNNING), and a results viewer for
the selected run — plots as `<img>` (fetched as a Bearer-authenticated blob → `ObjectURL`, since
`<img src>` can't carry an auth header), `ResultsText` in a `<pre>` with copy-to-clipboard and
download-as-`.txt` buttons, `StdErr` shown for FAILED/TIMEOUT runs.

**Live verification**: `ng build --configuration development` clean, `node --check` clean on
every touched `.mjs`, and the full HTTP-layer plumbing was verified live end-to-end via a temp
research + temp superadmin (both deleted after) — script upload/metadata round-trips correctly,
`POST .../runs` without a script correctly 400s, a real run correctly transitions
`PENDING → RUNNING → FAILED` and captures a clear error message, the per-research lock correctly
releases after a run finishes (a second run could start immediately after), the temp directory is
confirmed deleted after the run (no leftover `task-r-run-*` folders), and 404s work correctly for
a nonexistent run/plot.

The Docker sandbox itself couldn't be exercised from this session's own tools (no `docker` binary
on `PATH` in that environment) — that gap is now closed: the user hit `wsl --status` reporting
virtualization disabled in firmware, enabled it in BIOS/UEFI, re-ran `wsl.exe --install
--no-distribution`, and **`docker build -t task-r-runner docker/r-runner` succeeded** (~106s, all
6 packages installed cleanly) and `docker run --rm task-r-runner Rscript -e "library(ggplot2);
library(psych); print('OK')"` printed `OK` — confirms the image builds correctly and every baked-
in package loads. (First attempt using bash-style single-quote R syntax failed with `Error:
unexpected end of input` — a `cmd.exe` argument-quoting artifact, not a real R/Docker problem;
`cmd.exe` doesn't treat `'...'` as a quoted unit the way bash/PowerShell do, so the `-e` argument
was split mid-string. Fixed by using double quotes for the outer `cmd.exe` argument and single
quotes for the inner R string literal.)

**Full end-to-end verification, now complete (2026-08-19, later the same day)**: once the user's
Docker Desktop was working, this session's own tools also gained `docker` access (the earlier
session had simply started before Docker Desktop came up) — ran the complete pipeline for real
against a temp research (deleted after), no mocking:
- A normal script (reads an uploaded CSV, `hist()`s a column to `/output/summary_plot.png`,
  writes `summary()` to `/output/results.txt`) → `SUCCESS` in ~1.2s; downloaded the plot back and
  confirmed real PNG magic bytes (800×600, matches the script's `png()` call exactly);
  `ResultsText` matched the expected `summary()` output character-for-character.
- A syntactically invalid script → `FAILED` in ~1s, `StdErr` captured the real R parser error
  (`unexpected symbol in "this is"`).
- A script calling `download.file("http://example.com", ...)` → `SUCCESS`, but the call itself
  failed inside the container with `Couldn't resolve host name` — proves `--network none` is
  genuinely enforced (DNS resolution itself is blocked), not just configured and silently
  ineffective.
- A script with `Sys.sleep(8)` → confirmed the per-research lock: a second `POST .../runs` while
  it was still running correctly 409'd `ALREADY_RUNNING`, and immediately succeeded once the
  first run finished.
- Temp directory cleanup reconfirmed with real (not just failed-to-spawn) executions — zero
  leftover `task-r-run-*` folders after any of the above.

**Task Configuration's Phase 3 — and the whole 3-phase project — is now fully verified end to
end, no outstanding gaps.**

**Task Configuration's 3-phase project is now feature-complete** (Phases 1-3 all done), pending
that one piece of user-side Docker verification. See [[task_configuration_project]] memory for
the full record.

### Results menu configurability + R analysis results view (2026-08-19)

Follow-up to Task Configuration: the admin dashboard's Results nav group ("Rezultati") is now
per-research configurable instead of always showing all four items, and the sandboxed R-analysis
output (Task Configuration Phase 3) is now also reachable from Results, not just from the Task
Configuration page's own admin form.

**New `Research.UsesTlx` column** (`Sql/022_uses_tlx_toggle.sql`, applied live — `BOOLEAN NOT
NULL DEFAULT TRUE`, same shape as `UsesPsychTests`): drives only whether the "NASA-TLX" item
appears in *this admin dashboard's* Results menu. Deliberately does **not** touch the separate
NASA-TLX app's own participant-facing flow — TLX still runs for every participant in the fixed
Intro→AI→Report study regardless of this flag; it only hides the results *view* here, mirroring
how `UsesPsychTests` already worked for REI-40/Big Five. `study-config/routes.mjs`'s GET/PUT
extended (now 8 columns); Configuration page's TLX subsection gained a toggle above the existing
`tlxCalculateScores`/`tlxIncludeWeightings` checkboxes (those two only render when TLX is on).

**`DashboardShellComponent` now fetches per-research config reactively** (same
`toObservable(scope.selectedResearchId)` pattern every other scoped page uses) and drives the
Results nav group: NASA-TLX shown iff `usesTlx`; REI-40 **and** Big Five shown iff
`usesPsychTests` (REI-40's label switches to "REI (kratka verzija)"/"REI (short version)" when
`rei40Variant === 'short'`, via a new `NAV.RESULTS_REI40_SHORT` i18n key — was previously always
hardcoded "REI-40" regardless of variant); a new "R analiza"/"R analysis" item shown iff
`TaskType === 'GOOGLE_FORMS'` **and** an `AnalysisScript` row exists for that research. All
defaults fail open (show the item) except R analysis (fails closed) — matches the existing
`getTlxConfig`-frontend-fallback precedent of never blocking a researcher out of a page over a
transient fetch error, while not cluttering the menu with an opt-in feature nobody has used yet.

**New read-only `/results/r-analysis` page** (`ResultsRAnalysisComponent`) — script metadata, run
history table, and a results viewer (plots + interpreted text, copy/download) for a selected run,
auto-selecting the most recent run on load. Deliberately a *viewer only*: upload/run-trigger
controls stay on Task Configuration's existing `RAnalysisComponent`, reached from a different
route — this page reuses the same `AdminApiService` methods and duplicates the same view markup/
styles (`results-r-analysis.component.scss` mirrors `r-analysis.component.scss` verbatim, same
per-component-styling convention used throughout this project) rather than sharing a component
across two different feature areas with no common parent.

Verified live via a direct JWT-signed request against research 1 (no temp research needed — pure
read/write round-trip on additive fields, no destructive risk): `GET study-config/1` correctly
returned `usesTlx: true` only after restarting the API server (hit the project's own
documented "stale process" gotcha — the already-running `node server/index.mjs` was serving
pre-edit code); a `PUT` toggling `usesTlx` to `false` and back round-tripped correctly and
research 1's real config was confirmed fully restored afterward; `task-config/1` confirmed
`taskType: 'GOOGLE_FORMS'` and `analysis/1/script` confirmed `exists: false` (so the "R analiza"
item correctly stays hidden for research 1 today, since no script has been uploaded there — the
earlier Phase 3 E2E verification used a temp research, not this one). `ng build --configuration
development` clean (twice — once mid-edit with an expected `usesTlx` type error while the
Configuration form was only half-updated, then clean after finishing); the frontend dev server's
own hot-reload log confirmed each rebuild step compiled with zero errors.

### Admin dashboard follow-up round: menu-stale bug fix, creation-time instruments, chip checkboxes (2026-08-20)

Three of a five-part follow-up request against `admin-dashboard-andrejkatin` (the other two —
researcher profile system and notifications — are large, separately planned, not started yet;
full roadmap in the [[admin_dashboard_followup_2026_08_20]] memory).

**Bug root cause (found via a dedicated Explore agent, not guessed)**: `DashboardShellComponent`
owned private `showTlxResults`/`showPsychTestResults` signals refreshed only when
`scope.selectedResearchId` changed — saving Configuration for the *already-selected* research
never re-fetched, so the Results menu kept showing NASA-TLX/REI-40/Big Five tabs a researcher had
just turned off until they switched research and back. **Fix**: new
`src/app/services/study-config-store.service.ts`, a shared signal store both
`DashboardShellComponent` (reads) and `ConfigurationComponent` (writes, then calls `refresh()`)
inject instead of each keeping a private copy. Verified by code review + clean builds (this is a
pure client-state-timing bug, not observable via a backend curl check).

**Creation-time instrument fields**: `ResearchesManageComponent`'s create form (not the edit
form) gained `usesTlx`/`usesPsychTests` toggles + a `taskType` dropdown, reusing
`configuration.component.html`'s toggle-switch pattern. `server/researches/routes.mjs`'s
`POST /` now accepts and explicitly `INSERT`s these columns (previously relied on DB column
defaults — same "silent default masking user intent" issue class as the bug above) — `EEG`
and `rei40Variant` stay edit-only on purpose (EEG was already deliberately deferred; REI variant
has its own post-first-result lock). Also fixed while in this area: creating a research now
auto-selects it (`scope.select(newId)`) instead of leaving the researcher to find it in the
picker themselves. Verified live: created a temp research with TLX off/psych tests off/
GOOGLE_FORMS directly from the form, confirmed `GET study-config`/`task-config` reflected exactly
that with zero follow-up edits; deleted the temp research after.

**Chip-checkbox styling fix**: the research-assignment picker on `ResearchersManageComponent`
used a bare, unstyled native `<input type="checkbox">` list — the one remaining spot in the app
not using this design system's fully custom controls. New shared `.chip-checkbox-list`/
`.chip-checkbox` classes in `src/styles.scss` (same hide-native-input-behind-a-styled-`<label>`
technique as the existing `.segmented-control`, adapted for a wrapping multi-select set instead
of a fixed radio group) replace it. `ng build` clean; visual check pending the user's own look in
both themes.

### Researcher profile system — email login, invites, self-service profile (2026-08-20)

Fourth part of the same follow-up round (see the section above) — the big one. Replaces
username+password researcher accounts with a full profile system: email+password login, a
superadmin-issued magic-link invite (no plaintext password ever emailed), and a self-service
`/profile` page. `Sql/023_researcher_profiles.sql` applied live to Neon: `Researcher` gains
`Email`/`FirstName`/`LastName`/`DateOfBirth`/`AcademicStatus` (CHECK-constrained allowlist —
`PHD_STUDENT`/`MASTER`/`DOCTOR`)/`Country`/`AvatarImage`+`AvatarContentType`
(`BYTEA`)/`MustChangePassword`/`Language`; `Username`/`PasswordHash` kept (not dropped — new
accounts just mirror `Email` into the now-vestigial `Username` to satisfy its still-NOT-NULL
constraint, nothing reads it going forward). New `ResearcherInviteToken` table, same shape as
`consent-andrejkatin`'s `SurveyAccessToken` precedent (opaque `crypto.randomBytes(24)
.toString('base64url')` token, 48h expiry, no separate "used" flag — completion inferred from
`MustChangePassword` flipping to `FALSE`). The real superadmin account (`andrej`) was backfilled
with `Email = katin.andrej96@gmail.com` directly in the migration — confirmed explicit choice,
tested first (login lookup logic review + a temp-account round-trip) before anything else in this
phase, given the lockout risk of changing the login field itself.

**Backend**: `auth/routes.mjs`'s `POST /login` switched from `Username` to
`LOWER("Email") = LOWER(...)` lookup, 403s `MUST_CHANGE_PASSWORD` if reachable (shouldn't be, in
practice — a temp password is random and never shown to anyone). `researchers/routes.mjs`'s
`POST /` (superadmin-only, creating a brand-new account) no longer takes a password at all —
generates a random one server-side (bcrypt-hashed, satisfies `PasswordHash NOT NULL`, never
surfaced anywhere), sets `MustChangePassword = TRUE`, issues an invite token, and sends the email
via `server/email/researcherInviteEmail.mjs` (same table-based-HTML/`buildXEmail(lang,{url})`
pattern as `regenerateLinksEmail.mjs`) through the existing `mailer.mjs`, unmodified. Unlike
best-effort side-effects elsewhere in this app, a failed invite-email send here is surfaced back
to the superadmin (`inviteEmailSent: false`) rather than swallowed — an account with a random,
never-shown password and no delivered invite is otherwise a dead end. New public (no
`requireAuth`) `server/researcher-invite/routes.mjs` — `GET/POST /api/researcher-invite/:token`
(resolve / accept-and-set-password), same `NOT_FOUND`/`EXPIRED`/`ALREADY_USED` 3-tier error shape
as the REI-40/Big Five magic-link precedent. New `server/researcher-profile/routes.mjs`
(`requireAuth`-only, always operates on `req.researcher.id`, never a path param — no cross-
researcher access surface at all): `GET/PUT /me`, `POST /me/password` (requires current password
re-entry), `POST /me/avatar` + `GET /:id/avatar` (multer memoryStorage, same wrapper-middleware
error pattern as `eeg`/`task-files`, served inline for `<img src>` use, not force-download), and
`DELETE /me/researches/:researchId` ("leave this research" self-service, refuses if it's the
researcher's last assignment and they're not a superadmin). "Add an existing researcher to a
research" doesn't get its own endpoint — the frontend just resends that researcher's existing
profile fields to the existing `PUT /:id` with an expanded `researchIds` set, no new invite.

**Bug caught and fixed during verification**: `DateOfBirth` round-tripped as a shifted ISO
timestamp (`1995-12-31T23:00:00.000Z` for a stored `1996-01-01`) — the exact same neon-serverless
local-timezone `DATE`-parsing bug `experimental-sessions/routes.mjs` hit and fixed before.
Extracted that fix into a new shared `server/date-only.mjs` (`dateOnly()`) instead of copy-pasting
a second time, applied in both `researcher-profile/routes.mjs` and `researchers/routes.mjs`.

**Frontend**: `AuthService`'s `Researcher` interface swapped `username` for
`email`/`firstName`/`lastName`; login form now takes an email. New `/accept-invite/:token`
route (`AcceptInviteComponent`, outside the authenticated shell — same precedent as
`rei40-andrejkatin`'s `LinkAccessComponent`) resolves the token and sets a new password before
ever reaching `/login`. New `/profile` route (`ProfileComponent`): edit own info (never
`isSuperAdmin`/`researchIds`/`Email` — those stay admin-only), change password, avatar upload, and
a "Moja istraživanja" list with a confirm-guarded "Napusti istraživanje" per row. `GlobalHeaderComponent`
gained a small circular avatar button (initials fallback via an `<img>` `(error)` handler, not a
separate `hasAvatar` round-trip) in its previously-empty `.header-controls` slot, opening a
dropdown to `/profile` or logout — a new tiny `AvatarRefreshService` lets `ProfileComponent` tell
the header's avatar `<img>` to cache-bust right after a re-upload (they're siblings in the app
shell, not parent/child). `ResearchersManageComponent` reworked into two flows: "Novi istraživač"
(full profile form, sends an invite, no password field) and "Postojeći istraživač" (pick from a
dropdown, just expand `researchIds`, no new invite) via a `.segmented-control` switch. New static
`src/app/data/countries.ts` (~190 entries) feeds the existing `app-select` component for the
Country field — no prior country-dropdown precedent anywhere in this app family.

**Verified live end-to-end** (temp researcher via a Gmail `+`-alias of the user's own real
address, deleted after each round): create → real invite email sent → token resolved → password
accepted → `ALREADY_USED` on a second resolve → login with new email+password → `GET/PUT /me` →
change password (old password correctly rejected after the change, new one accepted) →
last-research leave guard correctly 400s → `DateOfBirth` round-trip fix confirmed
(`1996-01-01` exactly, not shifted). `ng build --configuration development` and `npx tsc --noEmit`
both clean throughout; `node --check` clean on every touched `.mjs`.

### Quick polish + existing-account backfill, before Notifications (2026-08-20)

Three small fixes requested alongside starting the Notification phase:
- `ResearchersManageComponent`'s New/Existing mode switch (`.segmented-control`) sat flush against
  the form card below it — added `margin-bottom: 24px` scoped to that page.
- `ProfileComponent`'s avatar section had no explicit instructions — the upload button's own text
  now reads "Otpremi sliku sa računara"/"Upload picture from your computer" (was just "Otpremi
  sliku"/"Upload picture"), plus a new hint line above it ("Dodajte profilnu sliku koja će se
  prikazivati pored Vašeg imena...") and a format/size hint below ("PNG, JPG, WEBP ili GIF, do
  3MB.").
- The 3 pre-existing `Researcher` rows from before the profile-system migration
  (`andrej`/`testresearcher`/`vladman`) had `NULL` FirstName/LastName (and 2 of the 3 had no
  Email at all) — showed as blank/`NULL` in the Researchers list table. Backfilled directly via
  SQL per the user's own confirmation of the correct values: `andrej` → Andrej Katin (email
  already set to `katin.andrej96@gmail.com` from the earlier migration); `vladman` → Vladimir
  Mandic, email `beyondai.researchgroup@gmail.com`; `testresearcher` → Test Researcher (name
  explicitly "doesn't matter" per the user), email `katin@uns.ac.rs`. All 3 accounts can now log
  in under the new email+password system with their existing passwords unchanged — only the login
  *field* changed, not any password.

### Notification system — bell icon, toasts, participant/session/reminder triggers (2026-08-20)

Fifth and final part of the same-day follow-up round (see the sections above) — bell icon +
unread badge + dropdown list in `GlobalHeaderComponent`, auto-dismissing toast popups, triggered
by participant-list imports, Experimental Session creation, and a day-before-scheduled-session
reminder.

**Schema** (`Sql/024_notifications.sql`, applied live): `Notification` (`ResearcherId` FK,
`ResearchId` FK nullable, `Type`, `Message` — pre-rendered display text in the recipient's own
`Researcher.Language` at insert time, not an i18n key + params, since there's no client-side
notification-body renderer, `IsRead`, `CreatedAt`), two indexes (list-by-researcher,
dedup-lookup-by-research+type+day).

**Backend**: new `server/notifications/create.mjs`'s `notifyResearchMembers(sql, {researchId,
type, actorResearcherId, buildMessage})` — resolves live `ResearcherResearch` membership **plus**
every superadmin (a fresh `UNION`-shaped query each call, not the JWT's cached `researchIds`,
since this must reach every *current* member — not just the acting researcher's own stale-by-
design view), excludes the actor, inserts one row per recipient with `buildMessage(lang)` called
per-recipient so sr/en researchers each get their own language. Wired into
`participants/routes.mjs`'s `POST /import` and `experimental-sessions/routes.mjs`'s `POST /`,
both fire-and-forget **after** the response is sent, each in its own try/catch — exact same
pattern already established for Google Calendar sync in this codebase. New
`server/notifications/routes.mjs` (`GET /`, `GET /unread-count`, `POST /:id/read`,
`POST /read-all`, all scoped to `req.researcher.id`). New `server/notifications/reminderJob.mjs`
— an in-process `setInterval` (60min, plus once immediately at boot so a fresh deploy doesn't
wait a full interval) since no scheduled-job/cron infrastructure exists anywhere in this app and
none is being introduced (confirmed choice); queries every research with a `ParticipantSession`
scheduled (`ExperimentalSessionId` set + `ScheduledTime` set) for tomorrow, deduped to at most one
`SESSION_REMINDER` notification per `(research, day)` regardless of how many times the interval
ticks that day, checked via `WHERE "ResearchId"=... AND "Type"='SESSION_REMINDER' AND
"CreatedAt"::date = CURRENT_DATE`.

**Frontend**: new `NotificationsService` (polls `GET /unread-count`+`GET /` every 30s while
logged in, started/stopped via an `effect()` on `auth.researcher()` inside
`GlobalHeaderComponent` — same conditional-polling discipline as `r-analysis.component.ts`'s
existing run-status poll, just continuous instead of scoped to one active run) and a new
`ToastService` (a real dismissible queue, generalizing the `r-analysis` `copied`-flag idiom that
was this app's only prior toast-adjacent precedent) + `<app-toast-container>` mounted once in
`app.component.html`. `GlobalHeaderComponent`'s previously-theme-only `.header-controls` slot
gained a 🔔 button (unread-count badge, opens a dropdown list, mark-one/mark-all-read) right next
to the profile avatar built earlier the same day. New notifications observed on a poll (not
already known from the previous poll) also fire a toast — first load only primes the "known ids"
set silently, so logging in doesn't toast every historical unread item at once.

**Verified live end-to-end** (temp `ResearcherResearch` membership + a real 2-researcher
scenario, cleaned up after): creating an Experimental Session as researcher A correctly produced
exactly one notification for researcher B (a fellow member) and **zero** for A themselves;
importing a participant list, same actor-exclusion check; `mark read`/unread-count round-tripped
correctly; the reminder job's dedup was proven with a real seeded tomorrow-dated session — first
`runSessionReminderCheck()` call produced exactly 2 rows (one per research-1 member), a second
immediate call produced zero additional rows. (Caught and fixed a red herring in the verification
script itself, not the app: computing "tomorrow" via `Date.toISOString()` in Node hit the exact
documented local-timezone `DATE`-parsing shift this project has hit before — fixed by pulling the
date as `::text` directly from Postgres instead, same lesson `date-only.mjs` already encodes for
application code.) `ng build --configuration development` and `npx tsc --noEmit` both clean.

**All 5 parts of the 2026-08-20 admin-dashboard follow-up round are now done.**

### Modular research platform — Phase 0: `SessionFlowMode` safeguard (2026-09-04, in progress)

New multi-phase project (separate from platform-ification A–D and Task Configuration above):
evolving `admin-dashboard-andrejkatin` + this app + `Nasa-TLX-FullImplementation-AndrejKatin` into
a genuinely modular platform other researchers could assemble their own studies on — instrument
registry, flexible/reorderable session flow, a study-builder wizard, standardized export,
researcher roles, multi-tenant isolation, external API. Full 8-phase roadmap in the plan file from
the planning session that scoped this (`create-a-claude-md-file-cozy-hinton.md` as of
2026-09-04) — a fresh plan file may exist by the time you read this if a later phase went through
its own planning pass.

**Key finding that shaped the whole roadmap**: `admin-dashboard-andrejkatin` is already
research-agnostic (`Research.TaskType`/`UsesPsychTests`/`UsesTlx`/`Rei40Variant` are already
per-research config) — the hardcoding lives entirely in the participant-facing apps: this app's
`StudyService.cs` (`SessionNamesById = {1:"Intro",2:"AI",3:"Report"}`, the fixed 3-row `Sessions`
table) and NASA-TLX's session-id-1/2/3 mapping in `AutoStartComponent`. So the generalization work
is really "admin-dashboard becomes system-of-record for a research's step order, participant apps
learn to ask it instead of hardcoding the answer" — gated the whole way by a per-research safeguard
so the user's own real study never depends on any of the new, unproven general-platform code paths.

**Phase 0 — done and live-verified (2026-09-04)**: `Research.SessionFlowMode` column
(`admin-dashboard-andrejkatin`'s Neon DB — `VARCHAR(20) NOT NULL DEFAULT 'DEDICATED'`, CHECK
`IN ('DEDICATED','GENERAL')`), applied live via a temp Node script (this app has no committed
`Sql/` migrations checked into this checkout — they're applied directly against Neon, deleted
after). Every existing research (including id 1, the real study) defaults to and stays
`DEDICATED`, untouched by the migration. `study-config`'s `GET` now also returns
`sessionFlowMode` (read-only — deliberately excluded from that route's `PUT`, so nothing on that
page can ever change it; only a future Study Builder wizard, Phase 4, will ever create a
`GENERAL` research).

`StudyService.GetLoginStateAsync` (`backend/CodeReviewAI.Api/Services/StudyService.cs`) now joins
`Research.SessionFlowMode` for the participant's `ResearchId` alongside the existing
`Language`/`ConsentGivenAt` lookup. `DEDICATED` (or a legacy row with no research at all) falls
through to the exact code path that existed before this change, byte for byte — the fixed
`SessionNamesById`, the fixed `Sessions` table join, `COALESCE(SequenceOrder, SessionId)`
ordering. A non-`DEDICATED` value throws `NotSupportedException` rather than silently falling
through to logic that would be wrong for a real `GENERAL` research — deliberate fail-loud, since
no research is `GENERAL` today and this branch has no live callers until Phase 3 builds the actual
resolution logic.

Verified live: `dotnet build` clean, `dotnet test` 39/39 green (zero regression); migration applied
and confirmed research id 1 reads back `SessionFlowMode: 'DEDICATED'`; `GET
/api/admin/study-config/1` (admin-dashboard, restarted to pick up the change) confirmed returns
`sessionFlowMode: "DEDICATED"` alongside the existing fields; a real `POST /api/study/login` for
test participant `001` against the restarted backend returned the exact same response as before
this change (`{"allFinished":false,"sessionId":1,"sessionName":"Intro","language":"sr"}`),
confirming the `DEDICATED` branch is truly a no-op.

**Phase 1 — done and live-verified (2026-09-04): Instrument Registry.** New `Instrument` catalog
table (`admin-dashboard-andrejkatin`'s Neon DB — 8 seeded rows: `NASA_TLX`, `REI40`, `BIGFIVE`,
`EEG`, `PR_REVIEW`, `GOOGLE_FORMS`, `GENERIC`, `R_ANALYSIS`) + `ResearchInstrument` join table
(`ResearchId`/`InstrumentId`, unique pair). Purely additive cataloging — the legacy
`UsesTlx`/`UsesPsychTests`/`TaskType` columns on `Research` are **not** dropped, and a one-time
backfill populated `ResearchInstrument` rows from those legacy booleans for every existing
research so the registry agrees with them from day one (confirmed by direct comparison: research
1 → `NASA_TLX`/`REI40`/`BIGFIVE`/`EEG`/`GOOGLE_FORMS`; research 10 → `GOOGLE_FORMS` only — exactly
matching their legacy toggle values).

New `server/instruments/routes.mjs` (`requireAuth`-only, scope-guarded — same pattern as
`task-config`/`study-config`): `GET /` (full catalog), `GET /:researchId` (a research's enabled
instrument ids), `PUT /:researchId` (replace the whole set — same delete-then-reinsert pattern as
`consent-sections/routes.mjs`, validates every id exists in the catalog before writing anything so
a bad id 400s clean instead of a raw FK-violation 500). `study-config`'s own PUT is untouched —
this is a separate, independent write path.

New `/instruments` admin page (`InstrumentsComponent`, own nav entry "Instrumenti"/"Instruments",
scoped to the sidebar's selected research via the same `toObservable(scope.selectedResearchId)`
reactive-load pattern every other scoped page uses) — a `.chip-checkbox-list` picker (the
2026-08-20 precedent), optimistic toggle-per-chip with rollback on failure, matching
`task-config.component.ts`'s optimistic-save style.

Verified live: catalog GET returns all 8 seeded instruments; research 1's GET matches the
backfill exactly; a temp research's PUT round-trips correctly (empty → `[NASA_TLX, REI40]` →
confirmed via re-GET), an invalid instrument id correctly 400s, temp research deleted after.
`npx tsc --noEmit` and `ng build --configuration development` both clean; the running dev server's
own hot-reload log confirmed both the new `instruments-component` chunk and the updated
`dashboard-shell-component` (new nav entry) compiled with zero errors.

**Phase 2 — done and live-verified (2026-09-04): flexible, reorderable session flow.** New
`ResearchSessionStep` table (`Id`, `ResearchId`, `SortOrder`, `InstrumentId` — nullable FK into
Phase 1's registry, for a bare step with no instrument, e.g. Intro — `Label`, `IsRequired`).
Created empty for every research; only ever read once Phase 3 wires `GENERAL`-mode participant
login to it, so this table has zero live callers today and zero effect on any active study —
confirmed live (research id 1 has 0 rows in it after this phase).

New `server/session-flow/routes.mjs` (`requireAuth`-only, scope-guarded): `GET /:researchId`,
`PUT /:researchId` — same delete-then-reinsert-whole-array pattern as `consent-sections`/
`instruments` (`SortOrder` = array index), validates every referenced instrument id exists in the
Phase 1 catalog before writing.

New `/session-flow` admin page (`SessionFlowComponent`, own nav entry "Tok sesija"/"Session flow")
— follows `consent-form.component`'s drag-reorder pattern verbatim (`DragDropModule`,
`cdkDropList`/`cdkDrag`/`cdkDragHandle`, same drag-handle SVG, `cdk-drag-preview`/
`cdk-drag-placeholder` states), one card per step with a label input, an `app-select` instrument
picker (sourced from Phase 1's catalog, plus a "— no instrument —" option), and a required-step
`.toggle-switch`.

Verified live via a temp research (deleted after): started empty; PUT with a custom 2-step flow
(bare "Intro" step, then a `NASA_TLX`-linked step) round-tripped correctly with resolved
`instrumentCode`/`instrumentLabel`; a reorder PUT correctly persisted the new `SortOrder`
(confirmed via re-GET showing the swapped order); an invalid `instrumentId` correctly 400s with
nothing written. `npx tsc --noEmit` and `ng build --configuration development` both clean.

**Phase 3 — done and live-verified (2026-09-04), with one sub-piece explicitly deferred:
cross-app next-step resolution — the piece that actually activates `GENERAL` mode.**

New `ParticipantSessionStep` table (admin-dashboard's Neon DB — `ParticipantId`,
`ResearchSessionStepId` FK, `IsFinished`, `FinishedAt`, unique pair) — mirrors
`ParticipantSession`'s own shape (`IsFinished` flag) but points at Phase 2's flexible
`ResearchSessionStep` instead of the fixed `Sessions` table. Deliberately a uniform "done" signal
regardless of which instrument a step wraps (or none), rather than introspecting each
instrument's own bespoke results table (`TlxResult`/`Rei40Result`/`BigFiveResult`/...), which
would need one-off logic per instrument type and wouldn't cover a bare step at all.

New public (no `requireAuth` — same precedent as `researcher-invite`/`consent-andrejkatin`'s
participant-facing routes, since the caller is a participant-facing app, not a logged-in
researcher) `server/next-step/routes.mjs`, mounted at `/api/next-step`: `GET /:participantId`
(resolves `Research.SessionFlowMode` for that participant; `DEDICATED` → `404 NOT_APPLICABLE`,
`GENERAL` → walks `ResearchSessionStep` in `SortOrder`, returns the first step with no
`IsFinished=TRUE` `ParticipantSessionStep` row, or `{allFinished:true, step:null}`) and
`POST /:participantId/:stepId/finish` (the generic completion write-back, upserts
`ParticipantSessionStep`).

**`code-review-ai`'s `StudyService.GetLoginStateAsync`** now actually implements the `GENERAL`
branch (previously a hard-fail stub from Phase 0) via a new `GetGeneralLoginStateAsync` — calls
the admin-dashboard endpoint above (new named `HttpClient("admin-dashboard")` registration in
`Program.cs`, base URL from new `AdminDashboard:BaseUrl` config key, `http://localhost:4312` by
default) instead of the hardcoded `Sessions` JOIN the `DEDICATED` branch still uses untouched.
**Scope boundary, deliberate**: this app can only itself drive a bare step (no instrument — an
Intro-style step) or a `PR_REVIEW`-instrument step, since those are the only two shapes its own
`StartReview`/chat/decision flow knows how to run — resolving any other instrument
(`NASA_TLX`/`REI40`/etc.) throws `NotSupportedException` with a clear message rather than
mis-running an instrument it can't actually handle; `GlobalExceptionMiddleware` sanitizes this to
a generic 500 for the client exactly like every other unhandled exception in this app, full detail
still in the server log.

**Deliberately not built in this phase — the actual cross-app redirect**: when the resolved step's
instrument isn't one this app can run, a participant should be sent to whichever app *does* handle
it (NASA-TLX for `NASA_TLX`, etc.) instead of hitting this 500. That needs either the Instrument
catalog's unused `ParticipantAppUrl` field populated with real per-environment URLs, a new field on
`StudyLoginState`/the `/api/study/login` response signaling "redirect externally", frontend
handling of that signal, and — separately — NASA-TLX's own `AutoStartComponent` generalized the
same way (asking the same next-step resolver instead of its hardcoded sessionId-1/2/3 mapping) plus
a completion write-back call to `/api/next-step/:participantId/:stepId/finish` from every
participant-facing app once it finishes its part of a step. None of that is built yet — flagged
explicitly rather than silently left implicit, since it's real remaining work before a `GENERAL`
research spanning more than one app is actually usable end to end.

Verified live: **research 1 (the real study) reconfirmed completely unaffected** —
`POST /api/study/login` for participant `001` returns the exact same response before and after
every change in this phase. A temp `GENERAL` research + participant: first login correctly
resolved a bare step (`sessionId` = the `ResearchSessionStep`'s own id, `sessionName` = its label);
marking that step finished via the admin-dashboard endpoint then logging in again correctly
advanced to the next step; since that next step was `NASA_TLX`-instrument, login correctly 500'd
with the exact expected `NotSupportedException` message captured in the server log (confirmed via
direct log inspection), proving both the resolver and the scope boundary work as designed.
`dotnet build`/`dotnet test` (39/39) both clean.

**Phase 4 — done and live-verified (2026-09-04): Study Builder wizard.**

New superadmin-only `/study-builder` page (`StudyBuilderComponent`, own nav entry "Kreator
studije"/"Study builder", right above "Istraživanja"/"Researches") — a 5-step guided flow:
basics → Consent Form → instrument picker → session flow → done. Deliberately reuses the
already-working per-research pages **verbatim** as wizard steps
(`ConsentFormComponent`/`InstrumentsComponent`/`SessionFlowComponent`, Phases 2-3's own pages) —
they already reactively load off `ScopeService.selectedResearchId`, so once step 1 creates the
research and calls `scope.select(newId)` (the same auto-select convenience the 2026-08-20
creation-time-fields work added to `ResearchesManageComponent`), every later step "just works"
with zero duplicated logic. Only step 1 (a subset of `ResearchesManageComponent`'s own create
form) and step 5 (a plain confirmation) are new markup. Both `InstrumentsComponent` and
`SessionFlowComponent` gained a small `hideHeader` input (default `false`) so their own
`.page-header` doesn't duplicate the wizard's own step heading when embedded — standalone use via
their own nav routes is unaffected.

**The created research always gets `SessionFlowMode: 'GENERAL'` explicitly** — `POST
/api/admin/researches` (`server/researches/routes.mjs`) gained an optional `sessionFlowMode`
field, restricted server-side to accept *only* the literal `'GENERAL'` when present (any other
value 400s) — the wizard is the one and only caller that ever sends it; every other creation path
(the plain `ResearchesManageComponent` form) omits it and gets the column's own `DEDICATED`
default untouched. This keeps Phase 0's safeguard structural rather than a mere default: there is
no UI path anywhere that lets a client request `DEDICATED` explicitly.

Verified live: a bogus `sessionFlowMode` value correctly 400s; the wizard's exact call shape
(`sessionFlowMode: 'GENERAL'` alongside the existing creation-time instrument fields) correctly
creates a research with `sessionFlowMode: 'GENERAL'` in the response; a plain creation with the
field omitted still correctly defaults to `DEDICATED`, confirming the two paths stay independent.
`npx tsc --noEmit` and `ng build --configuration development` both clean; the dev server's
hot-reload log confirmed the new `study-builder-component` chunk and the updated
`instruments`/`session-flow`/`consent-form`/`dashboard-shell` chunks all compiled with zero
errors. Visual/interactive confirmation of the wizard flow in an actual browser is the user's own
to do — this session's verification stayed at the build + API level, per this project's usual
split between what Claude verifies directly and what needs a human's eyes.

**Not started**: the cross-app redirect continuation of Phase 3 (see above).

### Modular research platform — Phase 5: standardized export (2026-09-04)

New `server/results-export/routes.mjs`, mounted at its **own** base path
(`/api/admin/results-export`), deliberately separate from `results/routes.mjs`'s existing
query-param-scoped `/tlx/export`/`/rei40/export`/`/bigfive/export` routes rather than added into
that same router — this project has hit the "a route registered after a path-segment-colliding
parameterized route silently steals its matches" bug class twice before
(`experimental-sessions/routes.mjs`, `task-config/routes.mjs`'s explicit ordering comments); a
fully separate router sidesteps that risk entirely. **The existing bespoke routes are left
completely untouched** — this phase is purely additive, zero regression risk to the
currently-relied-upon export links on the Results pages.

One common shape: `GET /api/admin/results-export/:researchId/:instrumentCode/export` (`NASA_TLX`/
`REI40`/`BIGFIVE`/`R_ANALYSIS`), same `requireAuth` + `resolveResearchScope` pattern as every
other results route. `NASA_TLX`/`REI40`/`BIGFIVE` **deliberately duplicate** (not share a function
with) the old routes' row-fetching queries, rather than refactoring both to call one shared
helper — a shared helper would mean any future edit to this new endpoint could silently change the
old routes' behavior too, exactly the risk this phase exists to avoid; byte-identical output is
verified directly instead of guaranteed by shared code. `R_ANALYSIS` has no per-participant
tabular rows, so its "export" is the most recent successful run's `ResultsText` as a `.txt`
download (same content the R Analysis page's own download button already offers) rather than a
forced CSV shape — an honest shape difference under one endpoint/auth pattern, not a fake
uniformity. New `AdminApiService.buildResultsExportUrl()` frontend helper added for future use
(e.g. a unified export picker) but **not wired into any existing download button** — those keep
using their own bespoke calls, untouched.

Verified live against real research 1 data (read-only, no writes, nothing to clean up): the new
endpoint's `NASA_TLX`/`REI40`/`BIGFIVE` CSV output is **byte-for-byte identical** (MD5-confirmed)
to the existing bespoke `/tlx/export`/`/rei40/export`/`/bigfive/export` routes' output;
`R_ANALYSIS` correctly 404s `NO_SUCCESSFUL_RUN` (research 1 has no uploaded script, matching a
prior phase's own verification notes); an invalid `instrumentCode` correctly 400s.
`npx tsc --noEmit` and `ng build --configuration development` both clean.

### Modular research platform — Phase 6: researcher role granularity (2026-09-04)

New `ResearcherResearch.Role` column (`VARCHAR(20) NOT NULL DEFAULT 'OWNER'`, CHECK
`IN ('OWNER','COLLABORATOR','VIEWER')`) — sits on top of the existing many-to-many (Phase B of
platform-ification) without changing what `resolveResearchScope`/`scope.mjs` does (membership —
"can this researcher see this research at all" — stays exactly as it was); this phase answers the
separate question "what are they allowed to *do* there". **Every existing assignment defaulted to
`OWNER`** when the column was added (Postgres backfills `ADD COLUMN...DEFAULT` for existing rows)
— confirmed live, so this phase changed **zero** existing researcher's access.

New `server/roles.mjs`'s `hasMinRole(researcher, researchId, allowedRoles)` — superadmin always
passes (mirrors `resolveResearchScope`'s own bypass), otherwise checks the JWT-embedded
`researchRoles` map (`{researchId: role}`, resolved once at login exactly like `researchIds`/
`isSuperAdmin` already are — same login-time staleness trade-off). `auth/routes.mjs`'s login query
now also selects `Role`; `jwt.mjs`/`middleware.mjs` thread `researchRoles` through the
token/`req.researcher` (the latter needed an explicit addition — it whitelists fields off the
verified payload rather than spreading it, so a new claim doesn't silently reach routes without
being added here).

**Reference enforcement — one route, by design, not project-wide**: `DELETE
/api/admin/experimental-sessions/:id` (the plan's own named example — a genuinely destructive
action) now calls `hasMinRole(req.researcher, session.ResearchId, ['OWNER','COLLABORATOR'])`
before deleting, 403ing `INSUFFICIENT_ROLE` for a `VIEWER`. Extending the same one-line check to
other destructive routes project-wide is flagged as future work, not done in this pass.

`server/researchers/routes.mjs`'s `POST`/`PUT` gained an optional `researchRoles` map alongside
`researchIds` — any id not present in it (including every call shape from before this phase
existed) defaults to `OWNER`, so this is fully backward-compatible. `ResearchersManageComponent`
(both "Novi istraživač" and "Postojeći istraživač" flows, plus edit-mode restore) gained a compact
role `app-select` next to each checked research chip, defaulting to `OWNER` the moment a chip is
checked — new `.chip-checkbox-with-role`/`.role-select` styling pairs the two visually inside the
existing wrapping `.chip-checkbox-list`.

Verified live: the real `vladman` assignment (research 10) confirmed still `OWNER` after the
migration, and `GET /api/admin/researchers` confirmed it surfaces `role: "OWNER"` correctly
through the list endpoint. A temp research + temp experimental session + a forged `VIEWER`-role
JWT: read access (`GET` the list) still works; the `DELETE` correctly 403s `INSUFFICIENT_ROLE`
and the session is confirmed still present in the DB afterward (not a silent no-op 200); the same
session with a `COLLABORATOR`-role token correctly deletes it. `npx tsc --noEmit` and
`ng build --configuration development` both clean (`researchers-manage-component` chunk grew
~4KB, matching the added role-picker markup).

### Modular research platform — Phase 7: multi-tenant isolation review (2026-09-04)

A design/audit pass, per the plan — not new code. Documents exactly what's shared across every
research in the one Neon DB today, and what would actually need to change before onboarding a
research group outside BeyondAI. Produces a decision, not a speculative migration.

**Already properly isolated** (per-research FK + `resolveResearchScope` on every route —
confirmed by re-reading, not assumed): `TaskFile`, `EegRecording`, `AnalysisScript`/`AnalysisRun`/
`AnalysisRunPlot`, `ConsentSection`, `Research`'s own config columns (`TaskType`/`UsesTlx`/
`UsesPsychTests`/`SessionFlowMode`/etc.), `Notification` (`ResearchId` nullable but always set for
research-triggered ones), `ExperimentalSession`/`ParticipantSessionStep`, `Instrument`/
`ResearchInstrument`/`ResearchSessionStep` (Phases 1-2), and researcher access itself
(`ResearcherResearch` membership + Phase 6's `Role`). All of this already scales to multiple
independent researches with zero cross-contamination risk — confirmed throughout every phase of
this project via live scope-check tests (a scoped researcher 403s on another research's id).

**The one real gap: `Participant.ParticipantId` is a GLOBAL namespace, not per-research.**
Confirmed directly in the schema (`Sql/001_document_existing_study_schema.sql`):
`"ParticipantId" VARCHAR(50) UNIQUE NOT NULL` — a bare, database-wide unique string, not a
`(ResearchId, ParticipantId)` compound key. Every downstream participant table inherits this
design: `Rei40Result`/`BigFiveResult`'s primary key **is** `ParticipantId` alone
(`Sql/008_rei40_answers_and_variant.sql`), and `TlxResult`/`ChatMessage`/`ReviewDecision`/
`ParticipantSession`/`ParticipantSessionStep` all key off the same bare string. **This means two
different research groups cannot both use the natural, obvious participant-numbering scheme
(`"001"`, `"002"`, ...) without an id collision** — today it works only because every participant
id in the system has, by administrator discipline, been unique across the whole database, not
because anything enforces it. This is the one concrete blocker for a second, independent research
group — not a hypothetical one.

**Everything else is lower-stakes or already fine as designed**:
- No database-level Row-Level-Security backstop exists anywhere in this schema — isolation is
  entirely enforced by every route remembering to call `resolveResearchScope`. In practice this
  has held up consistently (every route audited across 6 phases of this project follows the
  pattern), but it's a single point of failure model, not a DB-enforced guarantee. Worth hardening
  eventually, not urgent — the actual attack surface is "a future contributor forgets the scope
  check on a new route," which code review already catches today.
- The `Instrument` catalog (Phase 1) and the fixed `Sessions` table are deliberately global/shared
  — that's correct by design (a catalog of known instrument *types*, and a legacy fixed lookup
  only `DEDICATED`-mode research reads), not a gap.
- Outbound email (researcher invites, participant magic-links, thank-you emails) all sends from
  one shared Gmail identity (`GMAIL_USER`) regardless of which research triggered it — a branding/
  trust question for an external research group (their participants would see BeyondAI's sender
  address), not a data-isolation question. Worth solving with a per-research reply-to/display-name
  if it ever matters, not a blocking technical gap.

**Recommendation — do NOT build isolation speculatively.** The participant-namespace gap is the
one item that actually blocks a second independent research group, and the correct fix (migrating
every participant-keyed table to a `(ResearchId, ParticipantId)` compound key, across `code-review-
ai`, NASA-TLX, REI-40, Big Five, Consent, and admin-dashboard) is real, cross-repo, and risky
enough that it should be scoped and built once there's an actual second group with their own
participant-numbering scheme — not before, per this project's own established discipline of not
building speculative infrastructure ahead of real need (see the REI-10 "infra-only for now"
precedent from the REI variant work). Everything else audited above is either already fine or a
minor hardening item, not a blocker.

### Modular research platform — Phase 8: external API/webhook (2026-09-04, deliberately deferred)

The plan's own text scopes this as "lowest priority — build once an actual external tool needs
it." Phase 7's audit just above reached the same conclusion independently (don't build isolation
infrastructure speculatively) — building an authenticated webhook + per-research API-key system
for a third-party tool that doesn't exist yet would be exactly the kind of speculative
infrastructure this project has consistently avoided elsewhere (REI-10 "infra-only for now", the
Phase 7 conclusion immediately above). **Deliberately not built.** Revisit when a real external
tool needs to push results in — the Phase 5 export shape's schema is already the natural template
to invert for an import endpoint when that day comes.

**All 8 phases of the modular-platform plan are now resolved** — 0 through 7 built and
live-verified (or, for Phase 7, audited and documented), Phase 8 explicitly and deliberately
deferred pending real need. See [[modular_platform_project]] memory for the full roadmap record.
The user's own real study (research id 1) was reconfirmed unchanged after every phase that could
plausibly have touched the shared DB, per this project's Phase-0-first safety design.

### Overview page: gate stats/charts by which instruments a research actually uses (2026-09-04)

Follow-up after the modular-platform work above — `OverviewComponent` (admin-dashboard's home
page) used to unconditionally fetch NASA-TLX/REI-40/Big Five aggregate results and render an
Intro/AI/Report session-breakdown chart + a not-started/in-progress/completed funnel, for every
research regardless of what it actually uses. Harmless while every research was the one real
study, but a `GENERAL`-mode research (from the Study Builder wizard) has no fixed session
structure at all, so those charts rendered as meaningless all-zero bars labeled "Intro/AI/Report".

**Confirmed decision**: gate on the Study Configuration toggles (`usesTlx`/`usesPsychTests` via
`StudyConfigStoreService` — the same signal that already drives the Results-menu and, critically,
actually determines whether that data can exist), **not** the new Instruments-registry page —
that page is still purely informational (doesn't control real data collection), so gating on it
would risk the home page disagreeing with what data genuinely exists. A `GENERAL`-mode research's
session/completion charts are hidden entirely, no replacement (a flexible-session-flow-based chart
is separate, bigger future work).

`StudyConfigStoreService` gained a `sessionFlowMode` signal (`'DEDICATED'`/`'GENERAL'`, fails open
to `'DEDICATED'` on error, same convention as every other field there), read from
`getStudyConfig()`'s existing `sessionFlowMode` field. **Caught and fixed a real bug while wiring
this up**: `StudyConfig`'s TypeScript interface (`admin-api.service.ts`) never actually declared
`sessionFlowMode`, even though the backend has returned it since this morning's Phase 0 work — the
field was silently untyped/inaccessible from the frontend until now (both `updateStudyConfig`'s
body type and `ConfigurationComponent.submit()`'s `Omit<...>` needed `'sessionFlowMode'` added to
their exclusion list too, since that PUT never sends it).

`OverviewComponent`: the research-switch subscription now `await`s `studyConfigStore.load(id)`
before its own `load()` runs (the store's signals must reflect the *new* research before `load()`
reads them to decide what to fetch — a real race avoided, not just theoretical, since both loads
fire from the same `scope.selectedResearchId` change). `load()` skips the TLX/REI-40/Big Five
`getResults` calls entirely when the corresponding toggle is off (`Promise.resolve(null)` in their
place — `resultsTotal`'s existing `?? 0` fallback already handles a `null` signal correctly, no
change needed there). New `showResultsStat`/`showSessionCharts` computed signals gate the "Results
Total" stat, the "Sessions Done" stat, and the whole chart-grid block in the template.

Verified live (API-contract level — the gating itself is plain Angular signal/computed logic,
confirmed correct via clean `tsc`/build): research 1 reconfirmed `{usesTlx: true, usesPsychTests:
true, sessionFlowMode: 'DEDICATED'}` — zero behavior change for the real study; a temp `DEDICATED`
research with only `usesTlx` on returns exactly that combination (would fetch only TLX, show both
stats/charts); a temp `GENERAL` research returns `sessionFlowMode: 'GENERAL'` (would hide the
Sessions stat and both charts — the exact bug scenario the user reported after trying the wizard).
`npx tsc --noEmit` and `ng build --configuration development` both clean; the running dev server's
hot-reload log confirmed the updated `overview-component` chunk compiled with zero errors.

**Follow-up correction (same day)**: the `DEDICATED`-only gate wasn't enough — the user's actual
research at hand is `DEDICATED` (created via the plain research form, not the wizard) but has
`TaskType: 'GOOGLE_FORMS'` and zero participants imported yet, so `showSessionCharts` stayed
`true` and the Intro/AI/Report chart still rendered, just as an empty all-zero breakdown. Clarified
directly with the user: the actual complaint was the **empty** chart, not the task type as such.
Fix: `showSessionCharts` now also requires `participantsCount() > 0` — a fresh `DEDICATED`
research with nobody imported yet hides the chart exactly like a `GENERAL` research does, and it
reappears the moment participants are actually imported. Verified live: a temp `DEDICATED`/
`GOOGLE_FORMS`/zero-participant research confirmed `0` participants via the real API (gate false);
research 1 reconfirmed `3` participants (gate stays true, zero change for the real study).
`tsc`/`ng build` clean.

### Task-type-aware import template + wired-up results export (2026-09-04)

Two more `admin-dashboard-andrejkatin` follow-ups surfaced while the user reviewed a real
Google-Forms-task-type research's Task Configuration/import flow.

**Import template now branches on `TaskType`.** `ExcelImportService.downloadTemplate()` gained a
`taskType` parameter — for `PR_REVIEW` it's byte-for-byte the existing 3-sheet template
(Instructions + Participants + Tasks with the Session/PrNumber dropdowns); for `GOOGLE_FORMS`/
`GENERIC` it now downloads only Instructions (shortened, explains there's no PR-review concept
for this research) + Participants — no Tasks sheet, no `_PrNumbers` hidden sheet, since there's
no PR to assign. `parseFile()` needed **no change** — it already treated a missing "Tasks" sheet
as zero tasks, not an error (only errors when *both* sheets are absent), and
`server/participants/routes.mjs`'s `POST /import` already coerced a missing/empty `tasks` array
to `[]` (`Array.isArray(tasks) ? tasks : []`) — so the whole fix was purely template-generation +
UI, zero backend changes. `ParticipantImportComponent` now fetches the selected research's
`TaskType` reactively (`getTaskConfig`, same `toObservable(scope.selectedResearchId)` pattern
`TaskConfigComponent` itself uses) and hides the Tasks-related summary card/result line when it
isn't `PR_REVIEW`.

**Wired up the previously-dead Phase-5 results-export endpoint.** `buildResultsExportUrl()` and
`GET /api/admin/results-export/:researchId/:instrumentCode/export` existed since the
modular-platform project's Phase 5 but confirmed via grep to have zero UI callers. New
`AdminApiService.exportResultsFile()` (same authenticated fetch+blob+synthetic-`<a>` mechanics as
`exportCsv()`/`downloadTaskFile()`/`downloadEeg()` — a plain `<a href>` can't carry the Bearer
token `requireAuth` needs; filename read from the response's `Content-Disposition` header).
Wired into a new button on `ResultsRAnalysisComponent` only — NASA-TLX/REI-40/Big Five keep their
existing bespoke `EXPORT_CSV` buttons untouched (Phase 5 already verified those produce
byte-identical output, no reason to add a second button that does the same thing). R Analysis had
no per-instrument export via this route before; the new button downloads the **most recent
successful run's** results (distinct from the existing per-run "Preuzmi rezultate" button, which
downloads whichever run is currently selected/already loaded client-side) — shown only when at
least one run has ever succeeded.

Verified live via a temp `GOOGLE_FORMS` research (deleted after, against the local dev DB — not
production): a Participants-only import (empty `tasks: []`) against it correctly imports the
participant with **zero** `ParticipantSession` rows created (confirmed via direct DB count); the
new results-export route correctly 404s `NO_SUCCESSFUL_RUN` for a research with no analysis run.
`tsc --noEmit`/`ng build --configuration development` both clean.

### Platform improvements round 2 — 5 items (2026-09-04, in progress)

New multi-item project (separate from platform-ification A-D, Task Configuration, and the
modular-platform 0-8 projects above): 5 of 8 further platform directions the user picked from a
brainstorm. Execution order (smallest/lowest-risk first, since items 1 and 3 touch a shared
production Neon DB): **5 → 4 → 2 → 1 → 3**. Full design in the plan file from the planning session
that scoped this (`create-a-claude-md-file-cozy-hinton.md` as of 2026-09-04).

**Item 5 — done and live-verified (2026-09-04): per-research email sender display name.**
`server/email/mailer.mjs` (both `admin-dashboard-andrejkatin` and `consent-andrejkatin`, kept in
sync per this project's established duplication convention) — `sendMail({to, subject, html,
fromName})` gained an optional `fromName`, defaulting to the original hardcoded `"BeyondAI
Research Group"` literal so every pre-existing caller is unaffected. New nullable
`Research.EmailSenderName` (`VARCHAR(150)`, applied live) — fallback chain `EmailSenderName` →
`StudyDisplayName` → `Name` → the hardcoded default, same discipline `StudyDisplayName` itself
already uses. `study-config/routes.mjs`'s GET/PUT extended (now 9 columns);
`ConfigurationComponent`'s "Psihološki testovi" subsection gained an optional text field below
`studyDisplayName`. Threaded through the 3 **per-research participant-facing** email builders —
`regenerateLinksEmail.mjs` (admin-dashboard), `consentEmail.mjs`/`thankYouEmail.mjs`
(consent-andrejkatin) — each of which also renders the brand name into the email's HTML header
block (not just the `from` header), escaped via each file's own local `escapeHtml()` (matching
`thankYouEmail.mjs`'s pre-existing pattern for `studyDisplayName`) since it's researcher-supplied
free text. `mailer.mjs` itself also strips quotes/CR/LF before interpolating into the quoted
`from` header — defense against header injection via a researcher-typed name, not just cosmetic.
`researcherInviteEmail.mjs` (not research-scoped — a researcher can belong to many researches) is
deliberately left on the hardcoded default, untouched.

Verified live: a template-only check confirmed a name containing `<script>` renders HTML-escaped
and the override correctly replaces the header text while an omitted override still falls back to
the old hardcoded literal; a temp research's `study-config` GET/PUT round-tripped
`emailSenderName` correctly; a real consent-submit send (temp research with `UsesPsychTests =
FALSE`, so exactly one real Gmail send — the thank-you email) was sent successfully with the
overridden sender name threaded all the way through. `tsc --noEmit`/`ng build --configuration
development` (admin-dashboard) clean; `node --check` clean on every touched `.mjs` in both apps.

**Item 4 — done and live-verified (2026-09-04): GENERAL-mode session-flow chart on Overview.**
New `GET /api/admin/session-flow/:researchId/progress` (`session-flow/routes.mjs`, authenticated
+ scope-guarded — unlike `next-step/routes.mjs`'s public participant-facing reads, this is
consumed by the researcher-facing Overview page) — per-step finished/total counts (out of the
research's participant count) plus a not-started/in-progress/completed funnel, computed off
`ParticipantSessionStep` instead of the fixed `ParticipantSession` shape the DEDICATED-mode chart
uses. `OverviewComponent`'s old "hide entirely for GENERAL" gate is replaced by a real branch:
`showDedicatedCharts`/`showGeneralCharts` (both still requiring `participantsCount() > 0` and, for
GENERAL, at least one configured step — an empty chart stays exactly as wrong as it was for the
DEDICATED case fixed earlier the same day), with a single template-facing `activeSessionChart*`/
`activeCompletionChartDatasets` pair resolving to whichever mode is live so the HTML renders one
`chart-grid` block instead of duplicating the whole card markup. "Sessions Done" stat's
`sessionsDone`/`sessionsTotal` also branch the same way (GENERAL sums each step's finished count;
`participantsCount × stepCount` is the GENERAL equivalent of the DEDICATED "×3").

Verified live: a fresh `GENERAL` research with zero steps correctly reports zero steps (chart
stays hidden); after creating a 3-step flow and seeding 3 temp participants at 0/3, 1/3, and 3/3
step completion, the progress endpoint's per-step finished counts and the not-started/in-progress/
completed funnel matched hand-computed expectations exactly (2/1/1 per step, 1/1/1 funnel); both
existing `DEDICATED` researches on the dev DB reconfirmed unchanged (`SessionFlowMode` untouched).
`tsc --noEmit`/`ng build --configuration development` clean; `node --check` clean.

**Item 2 — done and live-verified (2026-09-04): Instrument Registry kept in sync (not yet
authoritative).** New `server/instruments/sync.mjs` — `syncStudyConfigInstruments()` (NASA_TLX/
REI40/BIGFIVE/EEG, owned by `study-config`'s toggles) and `syncTaskTypeInstrument()` (PR_REVIEW/
GOOGLE_FORMS/GENERIC, owned by `task-config`'s `TaskType`), each surgically adding/removing only
the `ResearchInstrument` rows for the codes it owns — any other row (a manually-added `R_ANALYSIS`
pick on the `/instruments` page, or a future instrument type) is left completely untouched.
Wired into `study-config/routes.mjs`'s PUT, `task-config/routes.mjs`'s PUT, and
`researches/routes.mjs`'s POST (creation-time) — the three places that write these legacy columns
now can never let the registry drift again. `instruments/routes.mjs`'s own PUT (the manual chip
picker) is untouched — deliberately still the one place a researcher can add something the legacy
columns don't model. One-time backfill (temp script, deleted after) brought both pre-existing
researches' `ResearchInstrument` sets to exactly match their legacy flags — confirmed via a
direct comparison, not assumed.

**Deliberately not done this round**: no route was switched to *read* from the registry instead
of the legacy columns — real behavior (Results-menu gating, Overview gating, consent-andrejkatin's
token issuance, EEG upload validation) keeps reading the legacy columns unchanged. This is a
scoped, lower-risk step (a trustworthy mirror) that sets up a safe future promotion to
authoritative, not the promotion itself.

Verified live: the backfill script's own comparison confirmed an exact match for both existing
researches; a temp research's manually-added `R_ANALYSIS` instrument survived untouched through
both a `usesTlx=false` save (correctly removed `NASA_TLX`, kept `REI40`/`BIGFIVE`/`R_ANALYSIS`)
and a `taskType` change (correctly swapped `PR_REVIEW`→`GOOGLE_FORMS`, kept `R_ANALYSIS`); a real
`POST /api/admin/researches` call (not a raw SQL insert) confirmed the registry is seeded
correctly at creation time too. `node --check` clean on every touched `.mjs`; both real
researches' `ResearchInstrument` rows reconfirmed to contain only their own data after cleanup.

**Item 1 — done and live-verified (2026-09-04): Participant ID becomes research-scoped;
`Participant.Guid` is the real join key.** The biggest, highest-risk piece of this round — touches
the shared Neon DB's schema and 6 repos' backend code. DB/backend only this round, per the
confirmed decision: every bare-ParticipantId login form (Consent app, NASA-TLX, REI-40/Big Five
dev-login, code-review-ai study login) is visually unchanged; a genuine collision now fails loud
(`409 AMBIGUOUS_PARTICIPANT_ID`) instead of silently resolving whichever row came back first.

**Schema** (temp Node script, deleted after — same discipline as every migration in this project):
`Participant` gains `Guid UUID UNIQUE NOT NULL DEFAULT gen_random_uuid()`; its uniqueness moved
from a bare global `UNIQUE(ParticipantId)` to `UNIQUE(ResearchId, ParticipantId)` — the actual
"participant id is scoped per research" change. All 9 participant-keyed tables
(`ParticipantSession`, `ReviewDecision`, `ChatMessage`, `EegRecording`, `SurveyAccessToken`,
`Rei40Result`, `BigFiveResult`, `TlxResult`, `ParticipantSessionStep`) gained a `ParticipantGuid
UUID NOT NULL` column + FK to `Participant.Guid` (backfilled from the then-still-globally-unique
`ParticipantId`, verified zero-NULL before `SET NOT NULL`), and every constraint that used to key
on `ParticipantId` (PK/unique/FK) now keys on `ParticipantGuid` instead — found via a live
`pg_constraint` sweep, not assumed, same technique as the Replication→Research rename. The old
`ParticipantId` VARCHAR column stays on every table (denormalized, display/filter convenience) —
not dropped.

**The key mechanism: a generic `set_participant_guid` BEFORE INSERT trigger**, installed on all 9
tables — auto-populates `ParticipantGuid` from the row's own `ParticipantId` value, `RAISE
EXCEPTION PARTICIPANT_NOT_FOUND` for zero matches, `RAISE EXCEPTION AMBIGUOUS_PARTICIPANT_ID` for
2+. This means almost every existing `INSERT ... (ParticipantId, ...)` call site across every repo
kept working completely unchanged — the trigger fires before Postgres even checks `ON CONFLICT`,
so an `INSERT ... ON CONFLICT (ParticipantGuid, ...)` correctly finds/creates the right row without
the application ever computing a Guid itself, and fails loud with a clear Postgres exception if the
bare `ParticipantId` alone can't be resolved unambiguously.

**What actually needed application-code changes** (systematically swept for, not just the two
things named in the plan):
- Every `ON CONFLICT (ParticipantId, ...)` clause across the whole platform had to move to
  `(ParticipantGuid, ...)` to match the moved constraint — found via grep in every repo:
  `SurveyAccessToken` (admin-dashboard's regenerate-links, consent-andrejkatin's submit),
  `ParticipantSession` (admin-dashboard's Excel import), `ParticipantSessionStep`
  (admin-dashboard's next-step finish), `EegRecording` (admin-dashboard's upload), `BigFiveResult`
  (bigfive-andrejkatin — this one would have 500'd on every real submission if missed), `TlxResult`
  (NASA-TLX's `api/db/result.ts` **and** its local-dev `server.ts` mirror), `ReviewDecision`
  (code-review-ai's `StudyService.cs`). REI-40/Big Five's `Rei40Result` insert and code-review-ai's
  `ChatMessage` insert use plain `INSERT` (no `ON CONFLICT`), so the trigger alone covers them with
  zero code change.
- **The Excel-import collision check** (`participants/routes.mjs`) — the one place that literally
  *depended on* global uniqueness — switched from a global existence check to
  `WHERE "ResearchId" = $1 AND "ParticipantId" = $2`; also now resolves and threads each
  participant's `Guid` explicitly through the Tasks-sheet's `ParticipantSession` inserts (rather
  than leaving it to the trigger), since an import already knows exactly which research's
  participant it means and the trigger would otherwise correctly refuse to guess once that id is
  ambiguous elsewhere.
- **`participants/:participantId/regenerate-links`** — was a bare `WHERE ParticipantId = $1 LIMIT
  1`; now uses a new shared `server/participants/resolve.mjs` (`resolveParticipantByBareId`,
  returns the row / `null` / an `AMBIGUOUS_PARTICIPANT` symbol) and passes the resolved `Guid`
  explicitly into the `SurveyAccessToken` inserts.
- **`eeg/routes.mjs`'s `authorizeForParticipant`** — rewritten to return `{researchId,
  participantGuid}` (was just `ResearchId`) and fail loud on ambiguity; every one of its 6 GET/
  POST/DELETE call sites now queries `EegRecording` by `ParticipantGuid` instead of the bare
  `ParticipantId` — `EegRecording` has no `ResearchId` column of its own, so this was the only way
  to keep it correctly scoped once collisions are possible.
- **Latent cross-research JOIN leaks, found and fixed by grep, not part of the original plan
  text**: `results/routes.mjs`, `results-export/routes.mjs`, `study-config/routes.mjs`'s
  `isRei40VariantLocked`, `session-flow/routes.mjs`'s progress endpoint, and
  `participants/routes.mjs`'s own participant list — all joined a result/session table to
  `Participant` via the bare `ParticipantId` string with no `ResearchId` check on the child side.
  Once two researches can share an id, that join pattern could silently attach the WRONG
  research's session/result data to a participant purely because the string matched — confirmed
  live (a two-research collision test showed research B's session data leaking into research A's
  participant list before the fix, correctly separated after). All swapped to join on
  `p."Guid" = child."ParticipantGuid"`.
- **`next-step/routes.mjs`** (public, participant-facing) — `resolveMode()` now detects 2+ matches
  and both its GET and POST/finish handlers return `409 AMBIGUOUS_PARTICIPANT_ID`.
- **`consent-andrejkatin`'s `GET /api/participant/:id` and `POST /api/consent/submit`** — the true
  first-touch public entry point for the whole participant-facing family — both now detect
  ambiguity and fail loud, matching the plan's own emphasis that this is where it matters most.
- **`code-review-ai`'s `StudyService.GetLoginStateAsync`** — the real production login for the
  actual study — gained an `AmbiguousParticipantId` flag on `StudyLoginState`; both `/api/study/
  login` and `/api/study/start-review` return `409 AMBIGUOUS_PARTICIPANT_ID` before any other check.

**Deliberately not hardened this round** (documented, not silently skipped): REI-40/Big Five's own
`GET /api/participant/:id` dev-login paths (distinct from the real magic-link flow, which was
never ambiguous since tokens already fully resolve the participant) and NASA-TLX's
`getTlxConfigForParticipant` helper still do a bare, unscoped lookup — a genuine collision there
would silently apply the wrong research's TLX scoring config (not misattribute stored results,
since `TlxResult` itself is fully protected by the Guid/trigger mechanism) rather than fail loud.
Low severity relative to the flagship entry points fixed above; flagged as a fast-follow rather
than chased indefinitely in this already-large round.

Verified live end-to-end through the REAL apps, not just direct SQL: imported the same
`ParticipantId` into two temp researches via the real Excel-import endpoint (previously would have
been silently skipped/misattributed) — both succeeded, each research's own participants list
correctly showed only its own session data (research A's Intro session, research B's AI session,
no cross-contamination, confirming the JOIN fixes); `consent-andrejkatin`, `code-review-ai`'s
`/api/study/login`, and `regenerate-links` all correctly returned `409 AMBIGUOUS_PARTICIPANT_ID`
for that same colliding id; a normal (non-colliding) real `BigFive` submission via
`bigfive-andrejkatin`'s actual endpoint succeeded with the fixed `ON CONFLICT` target and a
correctly-populated `ParticipantGuid`; the trigger's three cases (normal/not-found/ambiguous) were
each exercised directly and produced exactly the expected outcome. Real data reconfirmed
byte-identical before/after (`Participant`: 3, `ParticipantSession`: 9, `ChatMessage`: 36,
`SurveyAccessToken`: 6, all others unchanged). `dotnet build`+`dotnet test` (39/39),
`tsc --noEmit`/`ng build` (admin-dashboard, NASA-TLX — browser + SSR), `node --check` on every
touched `.mjs`/`.ts` across all 6 repos — all clean.

**Item 3 — done and live-verified (2026-09-04): Custom Instrument Builder, full participant-
facing flow.** A researcher defines their own questionnaire in admin-dashboard; a participant on
a `GENERAL` session-flow step fills it out through code-review-ai's own login screen; results
appear in the same generic viewer NASA-TLX/REI-40/Big Five already use. Built on top of item 1's
Guid scheme from day one — `CustomInstrumentResponse.ParticipantGuid` references
`Participant.Guid` directly, no bare-ParticipantId ambiguity risk anywhere in this feature.

**Schema**: `CustomInstrument` (`ResearchId` FK, `TitleSr`/`TitleEn`, `DescriptionSr`/`En`),
`CustomInstrumentItem` (`SortOrder`, `TextSr`/`En`, `ItemType` CHECK `IN ('LIKERT_5','TEXT',
'NUMBER')`, `ReverseScored`), `CustomInstrumentResponse` (`ParticipantGuid` FK, `Answers JSONB`,
`UNIQUE(CustomInstrumentId, ParticipantGuid)` — one response per participant, same one-shot shape
as `Rei40Result`/`BigFiveResult`). `ResearchSessionStep` gains a nullable `CustomInstrumentId` FK
alongside the existing `InstrumentId` (a step references at most one of {bare, catalog
instrument, custom instrument} — enforced both client-side and server-side in `session-flow/
routes.mjs`'s PUT).

**Admin backend** (`server/custom-instruments/routes.mjs`, same `requireAuth`+
`resolveResearchScope`+delete-then-reinsert pattern as every other feature route): CRUD for
instrument+items, plus `GET /:researchId/:instrumentId/results?mode=raw|aggregate` and a matching
`/export`. Deliberately computes aggregates **in JavaScript** (fetch every response's raw
`Answers` JSONB, compute mean/stddev per item) rather than building dynamic per-item SQL — items
are admin-authored and few, so this sidesteps dynamic-column-list SQL injection risk entirely for
a negligible compute cost. A reverse-scored `LIKERT_5` item's raw 1-5 answer is transformed
(`6 - v`) only at aggregate-read time, never on write — the stored `Answers` stays an honest
record of what was actually picked. `session-flow/routes.mjs`'s GET/PUT extended to read/write
`CustomInstrumentId` (validated to exist for the *same* research); `SessionFlowComponent`'s single
instrument dropdown now offers catalog instruments and custom instruments together via a
composite `catalog:<id>`/`custom:<id>` option-value scheme.

**Admin frontend**: new `/custom-instruments` page (own nav entry) — list + inline builder
(mode-switched by a signal, not a separate route) with CDK drag-reorder for items (same pattern
`consent-form`/`session-flow` already use), a type dropdown per item (Likert-5/Text/Number) with a
reverse-scored toggle shown only for Likert items. New `/custom-instruments/:id/results` page
reuses the **existing** `AggregateSummaryComponent`/`RawTableComponent` directly (not the
`results-instrument.component.html` shell, which is tied to the fixed `getResults()` API shape) —
confirms those two components were already generic enough for a fully dynamic column/dimension
list built from the instrument's own items, no changes needed to either.

**Public participant-facing surface** (extends the existing `next-step` API rather than a
parallel one): `GET /api/next-step/:participantId`'s resolved step now inlines the full item list
when it has a `CustomInstrumentId`, so the participant-facing caller needs no second authenticated
round trip. New public `POST /api/next-step/:participantId/:stepId/submit-custom-instrument` —
validates every item has an answer of the right shape/type, upserts `CustomInstrumentResponse`,
and marks the `ParticipantSessionStep` finished in the same call (one transaction, not two).

**Participant-facing UI lives in `code-review-ai`'s own frontend** — the existing GENERAL-mode
step orchestrator (modular-platform Phase 3), not a new 7th app. `StudyService.
GetGeneralLoginStateAsync` no longer throws for a custom-instrument step — it resolves the title/
item text to the participant's own locked language and returns a new `CustomInstrumentStepInfo` on
`StudyLoginState`; `/api/study/login`'s response carries it as `customInstrument`. New `/api/study/
custom-instrument/submit` endpoint proxies to admin-dashboard's public submit endpoint via the
already-configured `"admin-dashboard"` named `HttpClient` — the browser only ever talks to
code-review-ai's own backend, no new CORS surface. `StudyLoginComponent` (embedded directly in the
existing component, not a separate one — reuses its `.loader-card`/`.field` styling in scope
already, avoiding duplicated styles) renders a radio group (Likert-5), number input, or textarea
per item, and intercepts *before* the DEDICATED-mode Intro/AI/Report `sessionName` routing (a
custom-instrument step's `sessionName` is the step's own label, never "AI"/"Report", and must never
reach `startReview` — that endpoint is PR-review-specific).

Verified live end-to-end through the real apps, no mocking: created a temp `GENERAL` research +
a 3-item custom instrument (2 Likert, one reverse-scored, plus one free-text) via the real admin
API, placed it as a session-flow step, resolved `/api/study/login` for a temp participant through
code-review-ai's actual running backend — the item list arrived correctly and `sessionId` matched
the step's own id; submitted real answers through the same backend (proxying through to
admin-dashboard) — `CustomInstrumentResponse` landed with the correct `ParticipantGuid`,
`ParticipantSessionStep.IsFinished` flipped, and a second login call correctly reported
`allFinished: true`; the aggregate endpoint's computed means matched hand-calculated expectations
exactly, including the reverse-scored item's `6 - v` transform (raw answer 2 → mean 4, matching
the non-reversed item's raw answer 4 → mean 4). `dotnet build`+`dotnet test` (39/39),
`tsc --noEmit`/`ng build` (both `admin-dashboard-andrejkatin` and `code-review-ai` frontends),
`node --check` on every touched `.mjs` — all clean. All temp data confirmed fully cleaned up
afterward; both real researches' data reconfirmed untouched.

**All 5 items of the "platform improvements round 2" project are now done and live-verified.**

### Modular research platform, take two — true modularity, replacing the GENERAL-mode design (2026-09-07)

The user corrected a fundamental mistake in "platform improvements round 2" above: `code-review-ai`
(PR review) and EEG are specific to the user's own study, not generic platform primitives a
`GENERAL`-mode research could compose into a flexible step order. Three real scenarios: (1) the
user runs their own PR-review study, unchanged; (2) someone else runs the user's PR-review
research with minor config tweaks; (3) someone else runs their own unrelated research — pure
participant/instrument management, no code-review-ai, no EEG at all.

**Part A — removed the wrong design.** `SessionFlowMode`/`ResearchSessionStep`/
`ParticipantSessionStep`/`Instrument`/`ResearchInstrument`/`CustomInstrument*` — all code, routes,
nav pages, and DB tables deleted (zero live research ever used GENERAL mode, confirmed safe).
Replaced by simple per-research boolean/enum toggles already established on `Research`
(`TaskType`, `UsesTlx`, `UsesPsychTests`) plus a new `UsesConsentForm`. `StudyService.
GetLoginStateAsync` now checks `Research.TaskType === 'PR_REVIEW'` (400 `NOT_APPLICABLE`
otherwise) instead of `SessionFlowMode` — a participant on a non-PR-review research can never
accidentally start a PR review. Study Builder wizard simplified from 5 steps to 3 (basics+modules
→ Consent Form → done); `ConsentFormComponent` gained a `hideHeader` input for embedding.

**Part B — non-superadmin team management.** A researcher who is `OWNER` (Phase 6 role) of a
research can add any existing researcher (new `GET /api/admin/researchers/directory`, no
sensitive fields) to it via new `GET/PUT/DELETE /api/admin/researches/:id/team/:researcherId` —
only the superadmin still creates brand-new accounts. New "Tim" section on
`ConfigurationComponent`, visible only to an OWNER of the selected research.

**Part C — Consent Form became opt-in.** New `Research.UsesConsentForm` (default `TRUE` — every
existing research, including the real study, keeps requiring consent exactly as before). Nav
entry hides itself when off; `StudyService`'s consent check became `usesConsentForm &&
!consentGiven`.

**Part D — NASA-TLX became a real standalone module**, not just something reachable through
code-review-ai's own handoff. `SurveyAccessToken.SurveyType` CHECK gained `'NASA_TLX'`. New
`'Samostalna sesija'` (standalone) session label for a flat, non-counterbalanced administration —
no `dbSessionId` is set for it, so nothing tries to mark a fixed `ParticipantSession` row
finished. NASA-TLX gained `GET /api/db/link/:token` (standalone Vercel function +
local-dev mirror) and a `LinkAccessComponent`, mirroring REI-40/Big Five's own magic-link pattern
exactly. New shared `consent-andrejkatin/server/tokens/issueSurveyLinks.mjs` mints whichever of
REI40/BIGFIVE/NASA_TLX apply for a research, used by the consent-submit email flow.

**Two real bugs found and fixed while building this** (not part of the original plan, discovered
via code review while implementing the identical pattern elsewhere): (1) REI-40's and Big Five's
own `/api/link/:token` handlers still joined `SurveyAccessToken` to `Participant` via the bare
(no-longer-globally-unique, per-research-scoped since the "platform improvements round 2"
project) `ParticipantId` string instead of `ParticipantGuid` — a genuine cross-research-collision
risk the prior round's own audit missed for this specific spot; fixed both to join on
`ParticipantGuid` (the Token itself already uniquely resolves the participant, so the JOIN should
follow that, not re-derive it from a colliding string). (2) admin-dashboard's `regenerate-links`
minted REI40/BIGFIVE tokens *unconditionally*, ignoring `UsesPsychTests` entirely — now correctly
gated per-module (REI40+BIGFIVE by `UsesPsychTests`, NASA_TLX by `UsesTlx`), with a new
`NO_LINKS_TO_ISSUE` 400 when a research uses neither.

**Part E — consent-off participant portal.** When `UsesConsentForm=false`,
`consent-andrejkatin`'s `GET /api/participant/:id` skips consent-section fetching entirely; new
`POST /api/links/issue` resolves the participant's module links on the spot (no email) — a new
`resolveOrIssuePortalLinks` variant of the token helper is built for *repeat visits*: a module
with an existing result reports `status:'completed'` instead of a link (nothing re-touched), an
incomplete module's still-valid token is *reused* rather than replaced (a link already opened in
another tab keeps working), only a genuinely missing/expired token gets freshly minted. New
`/links` route (`ModuleLinksComponent`) renders "Upitnik N" cards with an Open button or a
completed badge — anti-priming maintained throughout, the instrument type never reaches the UI.

**Part F — EEG hidden for any non-PR-review research.** `ConfigurationComponent`'s EEG subsection
now gated behind `taskType === 'PR_REVIEW'`, with an explanatory hint otherwise.

**Part G — a real Google Forms integration**, going beyond the earlier "save the link, no
parsing" constraint by adding a genuine per-researcher Google OAuth connection (separate from the
existing Calendar one — `server/google-forms/`, own encrypted-token DB columns/env vars/setup
doc, since a form might be owned under a different Google account than the one used for
scheduling) that reads a form's live question structure via the Forms API into a new
`TaskFormQuestion` table — always still editable by hand regardless of connection state, so a
researcher who never connects anything can fully describe their CSV's columns manually. New
`server/task-files/descriptive-stats.mjs` (+ `csv-parse` dependency) computes real count/mean/
stddev/min/max (NUMBER/LIKERT), a frequency table (CHOICE), or a bare count (TEXT) from the
research's most-recently-uploaded CSV, matched against `TaskFormQuestion` by column header; an
unmapped column still gets a best-effort summary (numeric if ≥80% of values parse, else a bare
count) instead of being silently dropped. New `FormStructureComponent` (read-via-OAuth button or
manual table) and `DescriptiveStatsComponent` (per-column stat cards), both embedded in
`GoogleFormsTaskComponent`.

**A third bug found and fixed while building this**: Calendar's own OAuth `connect`/
`oauth2callback` redirects still targeted `/settings`, a route that stopped existing once Settings
merged into `/configuration` months ago — the connect confirmation banner never actually showed;
corrected for both Calendar and the new Forms flow. New `docs/google-forms-setup.md` documents the
one-time Google Cloud Console setup (separate OAuth client from Calendar's, enable the Forms API,
add researchers as Test users) — **not yet completed** (real `GOOGLE_FORMS_CLIENT_ID`/`SECRET`
are still placeholders in `.env`), so the actual OAuth connect/read-structure round trip hasn't
been exercised live yet; everything else (manual question CRUD, descriptive-stats computation
against a real uploaded CSV, matching hand-calculated values exactly) was.

**Part H — an in-browser R script editor.** New `RScriptEditorComponent` wraps a manually
assembled CodeMirror 6 `EditorView` (line numbers, undo history, R syntax highlighting via
`@codemirror/legacy-modes`, line wrapping) with `[content]`/`(contentChange)` bindings matching
this codebase's existing hand-written-form-control convention; `::ng-deep` styles CodeMirror's
imperatively-inserted DOM, which Angular's own emulated view encapsulation never touches.
`RAnalysisComponent`: choosing a file now also loads its text into the embedded editor; the Run
button re-uploads whatever's currently in the editor (via the *existing* script-upload endpoint,
unchanged) before starting a run, so a run always executes what's visible, not just the
originally-uploaded bytes — and can now also start from a script typed from scratch with no file
ever chosen. Zero backend changes. The `codemirror` meta-package was tried first for `basicSetup`
then removed again as unused dead weight once extensions were assembled by hand.

Verified live end to end across every part, through the real running apps (admin-dashboard,
code-review-ai, consent-andrejkatin, NASA-TLX, REI-40, Big Five) against the shared dev Neon DB —
real Gmail sends for the email-issuing paths, real HTTP round trips for every new endpoint, real
CSV upload + computed-stats matched against hand-calculated expectations, a real script-content
replace-before-run round trip (Part H's own Docker container execution step is unchanged by this
part and wasn't re-run in this round specifically since Docker Desktop wasn't up at that moment —
it was already fully verified live in the original Task Configuration Phase 3 work). Both real
researches on the shared dev DB reconfirmed byte-identical after every part; zero leftover temp
data at the end. `dotnet build`+`dotnet test` (39/39), `tsc --noEmit`/`ng build` clean across all
six repos, `node --check` clean on every touched `.mjs`.

**All 8 parts (A-H) of this platform re-architecture are now done.**

### Generic Task: multi-task assignment + per-app participant timer (2026-09-11, admin-dashboard-andrejkatin)

Not previously documented here. Generic Task (`Research.TaskType === 'GENERIC'`) gained the
ability to define several distinct tasks (each with its own written instructions and/or an
uploaded PDF) and assign exactly one task per participant (`GenericTask` table,
`Participant.GenericTaskId`) — purely administrative at the time, no participant-facing surface.
Alongside it, Study Configuration gained an optional, independently-configurable countdown timer
per participant-facing app the research actually uses (Code Review AI, REI-40, Big Five,
NASA-TLX — 8 `Research.Timer*Enabled`/`Timer*Minutes` columns), with auto-submit-on-expiry wired
into all four apps (`ReviewDecisionType.TimedOut` on Code Review AI; `IsTimedOut` columns on
`Rei40Result`/`BigFiveResult`/`TlxResult`). A real pre-existing bug was fixed in passing: the
Participants list's inner `JOIN "ParticipantSession"` excluded every sessionless participant
(every GENERIC/GOOGLE_FORMS research's imports) — changed to `LEFT JOIN`. All of this was built
and live-verified against local Postgres only, per this project's standing policy, and never
applied to the production Neon database.

### Generic Task participant app: per-task timer + file-upload submission (2026-09-14)

Gives the Generic Task feature above a real participant-facing surface: a per-participant magic
link opens a brand-new small app showing only the assigned task's text (+ optional PDF) and a
countdown (duration set per-task by the researcher, not per-research), with a drag-and-drop
upload zone at the bottom. The researcher defines, per task, which file type(s) are expected (a
fixed 7-category checkbox set — PDF/Word/Excel/PowerPoint/ZIP/Image/Text, each mapped to concrete
extensions) and whether one or several files may be uploaded. Results are download-only —
per-participant or as one `.zip` for everyone — with no interpretation of the uploaded files.

**Schema** (temp script against local Postgres): `GenericTask` gains `TimerMinutes` (required,
1–180), `AllowedFileTypes TEXT[]` (empty = unrestricted), `AllowMultipleFiles`. New
`GenericTaskSubmission` (`ParticipantGuid` **UNIQUE** — one submission ever per participant,
mirrors `Rei40Result`'s one-shot shape — `GenericTaskId`, `IsTimedOut`, `SubmittedAt`) and
`GenericTaskSubmissionFile` (`SubmissionId` FK `ON DELETE CASCADE`, `OriginalFilename`,
`ContentType`, `FileContent BYTEA`, `FileSizeBytes`). `SurveyAccessToken.SurveyType` gained
`'GENERIC_TASK'` — Neon has no CHECK constraint on this column, but **local Postgres did** (a
schema-drift gap found live, not assumed): `REI40`/`BIGFIVE`/`NASA_TLX`/`CONSENT_ENTRY` only,
fixed by widening the constraint to include `GENERIC_TASK`.

**New standalone app `task-app-andrejkatin`** (ports 4304 ng / 4314 API) — cloned from REI-40's
shell (same dual-mode `db.mjs`/`ThemeService`/`global-header` pattern), magic-link only
(`/`, `/link/:token`, `/task`, `/done` — no dev-login route, unlike REI-40/Big Five). Files are
queued client-side (drag-drop or browse; removable chips) and sent as one multipart POST on
"Pošalji zadatak"; the shared `TimerDisplayComponent` (copied in) drives `onTimerExpired()`, which
submits whatever's queued (possibly zero files) tagged `isTimedOut: true` — bypassing the manual
≥1-file requirement but never fabricating a file the participant didn't have ready. A failed
submit shows a "Pokušaj ponovo" retry that resends in the *same* mode (timed-out retries stay
timed-out). Backend `server.mjs`'s `resolveTaskLink()` resolves the participant's **currently**-
assigned `GenericTask` live via `Participant.GenericTaskId` at every request — never baked into
the token — so a researcher reassigning the task in admin takes effect on an already-issued link
immediately, exactly like REI-40's `Rei40Variant` resolution. `POST /api/link/:token/submit`
re-validates file count/type server-side regardless of what the client already filtered.

**Link issuance reuses the existing consent/portal machinery** — `consent-andrejkatin/server/
tokens/issueSurveyLinks.mjs`'s `MODULE_DEFS` list and both `issueSurveyLinks` (real email) and
`resolveOrIssuePortalLinks` (consent-off portal, no email) gained a `GENERIC_TASK` entry, gated on
`Participant.GenericTaskId != null` — not a research-level toggle, since the per-participant
assignment itself is the "on" signal. `admin-dashboard-andrejkatin`'s `regenerate-links` endpoint
got the same one-line addition. Labeled plainly "Zadatak"/"Task" everywhere a participant sees it
(email cards, the consent-off `/links` portal) — no anti-priming numbering, since a task the
participant already knows they're doing isn't a disguised psychometric instrument. Both REI-40's
and Big Five's own `/api/link/:token` handlers were found, while building this, to still join
`SurveyAccessToken`→`Participant` via the bare `ParticipantId` string instead of `ParticipantGuid`
— **not** touched in this round (out of scope), flagged for a fast-follow.

**Results**: new `admin-dashboard-andrejkatin/server/generic-task-results/routes.mjs` (its own
router, mounted at its own path — same anti-route-collision discipline as `task-config`/
`experimental-sessions`) — submission list, single-file download, and a `.zip` export (new
`archiver` dependency; note its v8 API is a from-scratch rewrite — `new ZipArchive({...})`, not
the classic `archiver('zip', {...})` factory function). New `/results/generic-task` page and
Results-menu nav entry, shown iff `taskType === 'GENERIC'` (same `DashboardShellComponent`
reactive-gating pattern the R-analysis entry already uses).

Verified live end-to-end through all 3 real running apps (admin-dashboard, consent-andrejkatin,
task-app-andrejkatin) against local Postgres, no mocking: task creation/validation; live
reassignment reflected on an already-issued link without reissuing; magic-link resolution;
PDF round-trip (byte-identical MD5); multi-file and single-file+type-restricted submissions;
wrong-file-type and too-many-files rejections (both server-enforced); manual submit correctly
requires ≥1 file while a timed-out submit correctly accepts zero; the one-shot `ALREADY_COMPLETED`
guard on both re-resolve and resubmit; `regenerate-links`' `NO_LINKS_TO_ISSUE` case and its correct
reissue-of-an-already-completed-task case; the results list, single-file download, and `.zip`
export (unzipped and diffed against the originals — correct per-participant folders, zero folder
for a zero-file timed-out submission, correct 404 on an empty research); and the real emailed
`/api/consent/submit` path (not just the portal path) actually minting the `GENERIC_TASK` token
end-to-end. Real research 1/10 and participants 001–003 reconfirmed untouched throughout.
`npx tsc --noEmit` + `ng build --configuration development` clean on `admin-dashboard-andrejkatin`,
`consent-andrejkatin`, and the new `task-app-andrejkatin`; `node --check` clean on every touched
`.mjs` across all three repos.

**Not done / deliberately out of scope this round**: `task-app-andrejkatin` has not been deployed
publicly (Vercel/Render) — local dev only, matching the Generic Task/Timer work it builds on;
its `.env` carries the same production Neon connection string as every sibling app for whenever
that deployment happens. The local-Postgres `SurveyType` CHECK-constraint fix above needs to be
re-applied (or confirmed already absent, matching Neon) before this ever touches production data.

### Bug fix: consent/regenerate-links emails wrongly included a standalone NASA-TLX link for PR_REVIEW research (2026-09-17)

Caught live while preparing example emails for a demo: the real study's (`Research` id 1,
`TaskType = 'PR_REVIEW'`) consent email listed **three** module links (REI-40, Big Five,
NASA-TLX) instead of two. For this study, NASA-TLX is filled out **exclusively** via Code Review
AI's own decision→NASA-TLX handoff (`AppComponent.onDecisionSubmitted`'s `window.location.href`
redirect straight after a review decision is submitted) — never as an independent magic link.
The standalone NASA-TLX module (Part D of the platform re-architecture, 2026-09-07) was built for
the opposite case — a research with no Code Review AI flow at all — but its gate
(`Research.UsesTlx`) got reused unconditionally everywhere a link is issued, so a PR_REVIEW
research with `UsesTlx = true` (true by default, and needed to keep the Results-menu's NASA-TLX
tab visible) incorrectly also got a standalone link. A participant clicking that link could fill
out NASA-TLX completely detached from the AI/Report review it's meant to measure — a real
correctness risk for the study's data, not just a cosmetic email issue.

**Fix**: a standalone NASA-TLX link/token is now only ever minted when `UsesTlx` is on **and**
the research's `TaskType !== 'PR_REVIEW'` — a small `includesStandaloneTlxLink(usesTlx, taskType)`
helper (duplicated in both repos per this project's established cross-repo convention) applied at
all three mint sites: `consent-andrejkatin/server.mjs`'s `/api/consent/submit` (the real email) and
`/api/links/issue` (the consent-off portal), and `admin-dashboard-andrejkatin/server/participants/
routes.mjs`'s `regenerate-links`. `Research.UsesTlx` keeps its original, narrower meaning
unchanged everywhere else (Results-menu visibility, Overview gating) — only the link-issuance call
sites gained the extra `TaskType` check. A PR_REVIEW research with `UsesPsychTests = false` and no
assigned Generic Task now correctly falls through to the thank-you email instead of a links email
with nothing useful in it, since a standalone TLX link is never on the table for that research
type either.

Verified live against real research 1 (participants 001/003) and a temp non-PR_REVIEW research
(deleted after): re-running `/api/consent/submit` for participant 001 refreshed the REI-40/Big
Five tokens' `CreatedAt` but left the pre-existing NASA_TLX token **completely untouched**
(proving it was excluded from the rebuilt email), and `regenerate-links` for participant 003
minted only REI-40/Big Five, no NASA_TLX row at all — while a temp `GOOGLE_FORMS`-type research
still correctly got a standalone NASA_TLX token, confirming the legitimate Part D use case still
works. A stray NASA_TLX token that had been wrongly minted for participant 002 during this bug's
own live discovery was deleted afterward as cleanup, not another fix. `node --check` clean on
both touched `.mjs` files; both API servers restarted to pick up the change (this project's usual
stale-process gotcha).

**Not yet done**: this fix has not been applied to production Neon — `consent-andrejkatin` and
`admin-dashboard-andrejkatin` are not deployed there yet per this project's standing local-dev-only
policy for the Generic Task/timer work adjacent to it. Re-confirm before ever emailing a real
study participant from a deployed instance.

### Hybrid review mode — experimental, participant "004" only (2026-09-17)

New third `ReviewMode.Hybrid`: the same static documentation Report mode already serves, reused
byte-for-byte, but reorganized into collapsible accordion sections (first section open, the rest
collapsed, freely togglable — not a strict single-open accordion) with a toggle button that opens
a chat drawer on the side, so the participant can use both the documentation and the AI chat at
once. Every expand/collapse — and the resulting dwell time — is written to the DB, **only** for
Hybrid sessions, to let the researcher later see what participants actually engaged with. Purely
experimental: reachable only by a brand-new fixed test participant **"004"**
(`Study:TestParticipantIds`/`Study:TestParticipantFixedSessions`), **local Postgres only** — no
production Neon/Render/Vercel changes.

**Schema** (`Sql/025_hybrid_mode.sql`, applied to local Postgres): a 4th row in the shared
`Sessions` table (`Id=4, Name='Hybrid'`) — also read by the sibling NASA-TLX app, see below — plus
a new `HybridSectionEngagement` table (`ParticipantId`/`ParticipantGuid`, `SessionId`, `SectionId`,
`SectionTitle`, `Action` CHECK `IN ('Expand','Collapse')`, `DurationSeconds` nullable/only-on-
Collapse, `CreatedAt`), with the same `set_participant_guid` trigger every other participant-linked
table already uses to auto-populate `ParticipantGuid` from `ParticipantId` on insert. Participant
`004` (`ResearchId=1`, the real PR_REVIEW research) and its `ParticipantSession` row
(`SessionId=4`) were seeded directly via SQL, same as every other one-time local-only data step in
this project.

**Backend**: `ReviewMode.Hybrid` (+ new `HybridSectionAction` enum). `StudyService.
SessionNamesById` gained `[4]="Hybrid"`; new best-effort `SaveHybridSectionEventAsync` (identical
try/catch/log-only pattern to `SaveChatMessageAsync`/`SaveDecisionAsync`). New
`POST /api/session/{id}/hybrid/section-event` (`ReviewSessionEndpoints.cs`), resolving
`ParticipantId`/`StudySessionId` off the in-memory `ReviewSession` exactly like `ChatStream`/
`SubmitDecision` already do — no server-side mode check needed, the frontend only ever calls this
when `sectioned=true`. `GenerateReport`'s existing Report-only gate now also allows `Hybrid`
(same static doc, same `Session:UseStaticReport` path); the EEG start-marker ternary became a
3-way switch (`HYBRID_START` added).

**Frontend**: `ReportViewComponent` gained a `sectioned` input — Report mode's own rendering is
byte-for-byte unchanged when it's `false` (the default), this is a pure extension not a fork.
Splits the streamed markdown on `^##\s+` into a preamble (the `# Title` line) + an array of
`{id: 'section-N', title, bodyMarkdown}` (confirmed live: exactly 18 sections/language, IDs are
positional so they stay stable across an SR/EN language switch); first section auto-expands on
load. Dwell time is tracked client-side (`Map<sectionId, openedAtMs>`) and flushed as a `Collapse`
event on manual collapse, on `beforeunload`/`visibilitychange` (tab hidden — restarts the clock
rather than losing idle time), and on a language switch (treated as a clean "close everything"
boundary) — the unload-time call uses a raw `fetch(..., {keepalive: true})`
(`SessionService.recordHybridSectionEvent`) since `HttpClient` has no `keepalive` option and the
call must survive a hard navigation. Search stays fully DOM-based (collapsed sections are hidden
via CSS, never removed) — the one addition is that landing on a match inside a collapsed section
auto-expands it first (recording a real `Expand` event), matching a manual click.
`AppComponent` gained a Hybrid branch (`<app-report-view [sectioned]="true">`) plus a floating 💬
button and a slide-in chat drawer (`hybridChatOpen` signal) — a wholly separate overlay, not
integrated with the existing 3-panel resize/collapse mechanism; the drawer's `<app-chat>` has no
`#chatRef` (quoting-into-chat from the diff viewer was deliberately not extended to Hybrid mode,
to avoid the `@ViewChild` timing gotcha of a conditionally-rendered chat instance).

**Sibling NASA-TLX app — a required fix, not optional**: the shared `Sessions` table gaining a 4th
row meant `Nasa-TLX-FullImplementation-AndrejKatin`'s hardcoded 1–3 assumptions would otherwise
silently break the handoff. `DB_SESSION_TO_TLX`/`SessionId` type gained `4: 'Hibridna sesija'`
(named descriptively rather than continuing the `'Sesija N'` sequence, since Hybrid isn't step 3 of
the normal progression); `api/db/session-finished.ts` **and** its local-dev mirror `server.ts`
(both needed the identical fix) had their `sessionId` range check widened `1–3 → 1–4`, and their
EEG-stop condition widened `=== 3` → `=== 3 || === 4` (Hybrid is participant 004's sole, terminal
session, so it should also stop the recorder — leaving this at `=== 3` would have left a dangling
recorder for any Hybrid run). `login.component.ts`'s `Record<SessionId,string>` exhaustiveness map
and a new `LOGIN.SESSION_HYBRID` i18n key were added too (never actually reachable there — same
"present purely for exhaustiveness" treatment as the pre-existing `'Samostalna sesija'` entry).

**A real, pre-existing environment issue found while verifying, unrelated to this feature**: the
GitHub PAT on file (both in user-secrets and — a separate, actually-authoritative-for-research-1
storage — the `ResearchPrConfig.GitHubToken` column) had gone stale, returning
`"The provided GitHub token is invalid or lacks the required permissions"` for **every**
participant (001 included), not just 004 — confirmed via a direct GitHub API call with the old
token. Root-caused further: even a fresh personal-account PAT couldn't see the
`beyondai-researchgroup` org at all (404 on the org itself) — the demo repo's real owner account
needed its own token. Fixed by setting a working `beyondai-researchgroup`-owned PAT in **both**
places (`dotnet user-secrets set "GitHub:PersonalAccessToken"` and a direct `UPDATE
"ResearchPrConfig" SET "GitHubToken" = ... WHERE "ResearchId" = 1 AND "IsActive" = TRUE`) — the
latter is the one `GetPrConfigForParticipantAsync` actually reads for any research with an active
PR config row, confirmed via source; the user-secrets copy is only the legacy fallback for a
participant with no research-scoped config at all.

Verified live end-to-end through the real running apps, no mocking: `POST /api/study/login` for
`004` returns `sessionId:4, sessionName:"Hybrid"`; a real `start-review` call opened a genuine
review session against the demo PR; `GenerateReport` streamed the static doc for a `Hybrid`-mode
session (previously would 400); a real browser (Playwright, headless Chromium) logged in as `004`,
confirmed all 18 accordion sections rendered with only the first expanded, expanded a second
section live (multiple sections open simultaneously, confirmed by screenshot), and opened/used the
chat drawer (suggestion chips, input, disclaimer all present and functional) — `HybridSectionEngagement`
correctly recorded both the automatic first-section `Expand` on load and the manually-clicked
second section's `Expand`, from the real click handlers, not a simulated API call. A submitted
decision persisted `ReviewMode='Hybrid'` in `ReviewDecision`. On the NASA-TLX side:
`GET /api/db/tlx-config/004` resolved correctly and `POST /api/db/session-finished` with
`sessionId:4` (previously would 400) succeeded and flipped `ParticipantSession.IsFinished` to
`TRUE`. Non-regression reconfirmed: participants 001/002/003 log in completely unchanged
(`Intro`/`Report`/`AI` respectively). `dotnet build`+`dotnet test` (39/39),
`tsc --noEmit`+`ng build --configuration development` on both `code-review-ai`'s frontend and
`Nasa-TLX-FullImplementation-AndrejKatin` (browser + SSR) — all clean.

### Cloud storage for logs & uploads — Cloudflare R2 (2026-10-01)

The Activity Log (a real research instrument) is the first thing this backend persists that
genuinely needed to survive hosting, and its local-disk-based implementation (`ActivityLog:Directory`,
only ever set in `appsettings.Development.json`) silently did nothing in production — the
directory was never configured there, so `CreateLogFile` always returned `null` and nothing ever
reached the database at all once deployed.

**Root fix (works even without R2)**: `ActivityLogService` no longer touches the filesystem —
it accumulates each session's CSV rows in an in-memory `StringBuilder` (keyed by an opaque log
id, `ConcurrentDictionary<string, StringBuilder>`), freed once a session is truly torn down
(`DeleteSession`, `SessionCleanupService`'s eviction sweep). `StudyService.SaveActivityLogAsync`
reads that buffer's content directly instead of `File.ReadAllTextAsync` — Postgres (Neon in
production) already persisted the finished CSV correctly regardless of hosting, so this alone
makes the Activity Log work in production with zero new infrastructure.

**Additional durable copy — Cloudflare R2**: new `IBlobStorageService`/`R2BlobStorageService`
(`AWSSDK.S3` — R2 speaks the real S3 protocol), gated on `CloudStorage:R2:{AccountId,
AccessKeyId,SecretAccessKey,Bucket}`. When configured, `SaveActivityLogAsync` also uploads the
full CSV to R2 and stores the key in a new `ActivityLog.StorageKey` column
(`Sql/028_cloud_storage_keys.sql`, also relaxes `RawCsv` to nullable), leaving `RawCsv` empty for
that row; unconfigured (the default), everything behaves exactly as before this feature existed.
Same dual-path pattern was applied across `admin-dashboard-andrejkatin`'s own uploads — Task
Files, EEG recordings, Generic Task submissions (read side only so far — the write side lives in
`task-app-andrejkatin`, not yet updated), R-analysis plots, researcher avatars — each gaining its
own `StorageKey` column, with every read route falling back to the original BYTEA/TEXT column
when it's `NULL`. Full setup walkthrough:
`admin-dashboard-andrejkatin/docs/cloudflare-r2-setup.md`.

Verified live (R2 unconfigured, local Postgres): a full login → start-review → decision →
session-teardown cycle for a real test participant produced the expected
`"Saved activity log (2 rows, storage=postgres)"` log line and a DB row with `StorageKey: null`
and the correct `RawCsv` content — confirming the in-memory-buffer path works end to end without
any R2 credentials. Task File upload/download/delete, avatar upload/download (including the
`HasAvatar` flag) all reconfirmed unchanged via direct API calls. `dotnet build`+`dotnet test`
(40/40) clean; every touched `.mjs` passes `node --check`. **Not yet tested against a real R2
bucket** (no credentials existed yet at the time of this change) — that verification, plus
`task-app-andrejkatin`'s own write-side wiring and any production (Render/Neon) rollout, are
explicit follow-ups once real Cloudflare credentials are available.

### Chained participant emails: Consent magic-link → REI-40/Big Five → Intro session → done message (2026-10-01)

Closes the gap where the whole participant-facing flow (Consent → REI-40/Big Five → Code Review
AI Intro → NASA-TLX) could only be entered by a participant typing their own Participant ID — no
researcher-triggered one-click email existed anywhere, and nothing auto-advanced a participant
from one stage to the next via email. **Local Postgres only**, like every other in-progress
feature in this family of repos — none of this touches production Neon yet.

**Admin Dashboard — "Send consent email" button**: new `POST /api/admin/participants/:id/send-
consent-email` (`admin-dashboard-andrejkatin/server/participants/routes.mjs`) mints a
`CONSENT_ENTRY` `SurveyAccessToken` (reusing the already-existing `buildConsentLinkEmail`
template, previously only used by the bulk CSV mailing-list flow) and emails it — the one thing
missing from this project's already-complete `CONSENT_ENTRY` magic-link infrastructure (resolution
side already existed end-to-end: `consent-andrejkatin`'s `GET /api/link/:token` +
`LinkAccessComponent`, and the bare-ID login form was already `403 PERSONAL_LINK_REQUIRED`-gated
to test participants only). New button lives on Participant Detail's Timeline, next to the
existing "Mark baseline" button, visible only when the research uses a consent form; its label
switches to "resend" once consent has already been given. Re-sendable at any time (no server-side
"already consented" block — `consent-andrejkatin`'s own link resolution already routes an
already-consented participant straight to `/done`).

**REI-40/Big Five browser tab title de-anonymized**: `<title>REI-40</title>`/`<title>Big
Five</title>` → `<title>BeyondAI</title>` in both apps' `index.html` — the one remaining
participant-visible leak of which instrument is being filled out (everything else was already
anti-primed, confirmed by grep: no in-app header/i18n string names the instrument either).

**New automatic "Intro" email once BOTH REI-40 and Big Five are complete**: neither app had an
`server/email/` folder before this — both gained a duplicated `mailer.mjs` (verbatim copy of
`admin-dashboard-andrejkatin`'s) and a new `introTaskEmail.mjs` (single-CTA-button email, modeled
on `consentLinkEmail.mjs`'s visual shell). `rei40-andrejkatin/server.mjs`'s and
`bigfive-andrejkatin/server.mjs`'s own `POST /api/result` (real-participant branch) each gained an
identical, fire-and-forget `maybeSendIntroEmail(sql, participantGuid)` call right after their
successful response — checks `TaskType === 'PR_REVIEW'`, checks both `Rei40Result` AND
`BigFiveResult` exist for the participant, then does an atomic claim
(`UPDATE "Participant" SET "IntroEmailSentAt" = NOW() WHERE ... IS NULL RETURNING ...` — new
nullable `Participant.IntroEmailSentAt` column, `Sql/029_intro_email_sent_at.sql`) so that
whichever app's submission happens to complete the pair (a real race, since both could finish
within moments of each other) is the only one that actually mints a `CODE_REVIEW` token
(90-day TTL, same type/lifetime code-review-ai's `ResolveParticipantIdByLinkTokenAsync` and admin-
dashboard's existing "Generate CODE_REVIEW link" button already use) and sends the email — never
twice. The link lands the participant on their Intro session automatically (already-existing
`GetLoginStateAsync` behavior, zero code-review-ai changes needed).

**NASA-TLX: session-aware "Intro done" message**: `results.component.ts`'s
`finishStudySession()` used to show the exact same generic `SESSION_DONE_*` popup (and redirect
back into code-review-ai via `resolveBeyondAiUrl`) for every session type. Now, specifically for
`dbSessionId === 1` (Intro), it shows a new `showIntroDonePopup` instead — "you finished the Intro
session, researchers will contact you about further steps" — whose OK button (`closeIntroDonePopup`)
does **not** redirect back into code-review-ai (AI/Report needs an in-person Baseline measurement;
looping back would just surface the Baseline-pending banner). Sessions 2/3/4 keep the exact
original behavior.

Verified live end-to-end through the real running apps (no mocking), via a temp real (non-test)
participant on research 1 (PR_REVIEW, UsesConsentForm+UsesPsychTests both true), cleaned up after:
send-consent-email → real `CONSENT_ENTRY` token minted → `consent-andrejkatin`'s `/api/link/:token`
correctly resolved to the (not-yet-consented) consent form → `/api/consent/submit` correctly set
`ConsentGivenAt` and minted REI40+BIGFIVE tokens → REI-40 submission (201) left `IntroEmailSentAt`
`NULL` (sibling not done) → Big Five submission (201) flipped `IntroEmailSentAt` to a real
timestamp and minted a `CODE_REVIEW` token, with no error logged from either app's fire-and-forget
hook (confirming the real Gmail SMTP send succeeded) → that `CODE_REVIEW` token resolved correctly
via code-review-ai's real `/api/study/link-login`, landing on `sessionId:1/"Intro"` as expected.
Separately confirmed NASA-TLX's `/api/db/session-finished` still works unchanged for `sessionId:1`
(flips `IsFinished`/`FinishedAt`) — the popup branch itself is a pure client-side signal/template
change, confirmed via clean `tsc`+`ng build` (browser+SSR). REI-40/Big Five tab titles reconfirmed
live via the actual running dev servers (`curl` against both, both now show `BeyondAI`).
`dotnet build`+`dotnet test` (40/40), `npx tsc --noEmit`+`ng build --configuration development`
(admin-dashboard-andrejkatin, Nasa-TLX-FullImplementation-AndrejKatin browser+SSR), `node --check`
on every touched `.mjs` across `admin-dashboard-andrejkatin`/`rei40-andrejkatin`/
`bigfive-andrejkatin` — all clean. `nodemailer` added as a new dependency to both `rei40-
andrejkatin` and `bigfive-andrejkatin` (neither had any email capability before this).

### Demographic Questionnaire — new standalone app + admin-configurable questions (2026-10-01)

New feature: a one-time, per-participant demographic form (gender, employment, age, education,
field/role, programming/professional experience, dominant language, AI-tool usage frequency +
attitude + free-text explanation — 12 questions adapted from a researcher-supplied Google Form,
with name/surname/participant-ID fields dropped since the magic link already identifies the
participant). Admin-configurable per research (add/remove/reorder questions and, for single-choice
questions, their options — bilingual SR/EN throughout, one option per question can be flagged
"Other, please specify"). Sent in the **same email** as REI-40/Big Five, labeled "Demografski
upitnik"/"Demographic questionnaire" — explicitly outside the numbered "Upitnik N" sequence, same
treatment as the existing GENERIC_TASK "Zadatak" card. **Local Postgres only**, like every other
in-progress feature in this family of repos.

**Schema** (`Sql/030_demographic_questionnaire.sql`): `Research.UsesDemographics BOOLEAN DEFAULT
FALSE` (an explicit, researcher-toggled boolean — same shape as `UsesPsychTests`/`UsesTlx`, not a
derived `EXISTS(...)` check and not a per-participant signal like `GenericTaskId`, since the
questionnaire is identical for every participant in a research); `DemographicQuestion`
(`ResearchId`, `SortOrder`, `QuestionType` CHECK `IN ('TEXT','SINGLE_CHOICE')`, `PromptSr`/`En`);
`DemographicQuestionOption` (`QuestionId` FK `ON DELETE CASCADE`, `SortOrder`, `LabelSr`/`En`,
`IsOtherSpecify` — deliberately per-OPTION not per-QUESTION, so an admin just flags a normal
option row); `DemographicResponse` (`ParticipantGuid UNIQUE` — one-shot, same shape as
`Rei40Result`/`BigFiveResult`/`GenericTaskSubmission`, `Answers JSONB`). `SurveyAccessToken
.SurveyType` CHECK widened to include `'DEMOGRAPHIC'`. Research 1 (the real PR_REVIEW study) got
`UsesDemographics = TRUE` and all 12 questions seeded directly in the migration (same precedent
as `017_consent_sections.sql`'s research-1 seed) — ready to test live immediately.

**New standalone app `demographics-andrejkatin`** (ports 4305 ng / 4315 API) — cloned from
`task-app-andrejkatin`'s shell (magic-link only: `/`, `/link/:token`, `/form`, `/done`, no
dev-login route), stripped of file-upload/multer/timer (none needed here). `server.mjs`'s
`resolveDemographicLink()` joins `SurveyAccessToken(SurveyType='DEMOGRAPHIC')` → `Participant` →
`Research` with the same error precedence as every sibling app
(`NOT_FOUND`→404/`EXPIRED`→410/`NOT_ACTIVE`→403/`ALREADY_COMPLETED`→409); the question list is
resolved **live** from `DemographicQuestion`/`Option` at every request, never baked into the
token, so an admin's edit takes effect on an already-issued link immediately (same principle as
REI-40's `Rei40Variant` resolution). `FormComponent` renders each question in order — `TEXT` as a
textarea, `SINGLE_CHOICE` as a group of selectable buttons (same visual idiom as rei40-
andrejkatin's own Likert buttons, generalized to a variable option count) — and reveals a
free-text field when the selected option has `isOtherSpecify`. Server-side required-field
validation mirrors the client (every question mandatory, no per-question "required" toggle in
v1, matching the source form). `<title>Demografski upitnik</title>` — a descriptive, non-
anonymized title, matching `task-app-andrejkatin`'s own precedent (unlike REI-40/Big Five's
generic "BeyondAI" title — a demographic form isn't a disguised psychometric instrument a
participant could skew by advance knowledge).

**Admin Dashboard**: new `/demographic-questions` page (own nav entry, modeled directly on
`ConsentFormComponent`'s drag-reorder pattern — `@angular/cdk`'s `DragDropModule`, delete-then-
reinsert-whole-array `PUT`, `SortOrder = array index`) with a **second, nested** `cdkDropList` for
each `SINGLE_CHOICE` question's own reorderable options list — one level deeper than Consent
Form's own single-level pattern. New `/results/demographic` page (raw response list + a
per-question frequency breakdown computed in JS from the stored `Answers` JSONB — not dynamic
per-question SQL, same safety-motivated computation shape the old, fully-removed Custom Instrument
Builder used — plus CSV export), gated in the Results menu on `usesDemographics` like every other
opt-in results entry. Study Configuration gained a "Demografski upitnik" toggle section (own
`server/demographic-questions/routes.mjs` + `defaults.mjs` for lazy-seeding any other research
that turns the toggle on with zero authored questions, mirroring Consent Form's own dual
migration-seed + lazy-seed approach).

**Token/email wiring** — the `DEMOGRAPHIC` type was threaded through every existing REI-40-
adjacent mint/email site: `consent-andrejkatin/server/tokens/issueSurveyLinks.mjs` (new gated
mint block → `demographicUrl`, new `MODULE_DEFS` entry for the consent-off portal),
`consentEmail.mjs` (new `demographicLabel`, appended as a third non-numbered card — identical
mechanism to the existing `taskUrl`/`taskLabel` treatment), `server.mjs`'s `/api/consent/submit`
and `/api/links/issue` (both now read/thread `usesDemographics`), `module-links.component.ts`
(the consent-off `/links` portal's numbering-exclusion logic generalized from a binary `isTask`
flag to a `kind: 'questionnaire'|'task'|'demographic'` enum). Mirrored in
`admin-dashboard-andrejkatin/server/participants/routes.mjs`'s `regenerate-links` handler (new
gated mint block, `NO_LINKS_TO_ISSUE` gate extended) and `regenerateLinksEmail.mjs` (same
non-numbered-card mechanism). `DEMOGRAPHIC` added to `LINK_TYPES`/`linkUrlFor` (per-research-
universal like REI40/BIGFIVE, so it appears in Participant Detail's Links table — unlike
NASA_TLX/GENERIC_TASK, which are per-participant/standalone and deliberately excluded from that
list) and to the frontend `LinkType` union.

Verified live end-to-end through the real running apps (admin-dashboard, consent-andrejkatin,
demographics-andrejkatin), no mocking, via a temp real (non-test) participant on research 1:
confirmed `study-config`/`demographic-questions` endpoints return the seeded config → "Send
consent email" → consent submit correctly minted REI40+BIGFIVE+**DEMOGRAPHIC** tokens together →
the demographic magic link resolved all 12 questions correctly → a complete submission (including
an Other-specify answer) succeeded (201) → `DemographicResponse` row confirmed with all 12 answer
keys → re-resolving the same link correctly 409'd `ALREADY_COMPLETED` → the new Results page's raw
list, aggregate frequency breakdown, and CSV export all returned correct data → Participant
Detail's Links table correctly listed/regenerated the `DEMOGRAPHIC` row. **Regression check**: a
second real research (`UsesDemographics=false`) confirmed its consent-submit still mints **only**
REI40+BIGFIVE (no `DEMOGRAPHIC` token) — existing behavior completely unchanged. `dotnet` untouched
(no C#/code-review-ai code changes this round beyond the migration file); `npx tsc --noEmit`+
`ng build --configuration development` clean on `admin-dashboard-andrejkatin`,
`consent-andrejkatin`, and the new `demographics-andrejkatin`; `node --check` clean on every
touched `.mjs` across all three repos. All temp data cleaned up; real researches (1/10/90) and
participants (001-005) reconfirmed untouched.

### Post-session upitnik — nakon NASA-TLX, pre nego što je sesija stvarno završena (2026-10-01)

Kratak, fiksni (ne admin-konfigurabilan) 5-pitanjski upitnik koji se popunjava ODMAH posle NASA-TLX
skale, PRE nego što se study-flow sesija (`ParticipantSession.IsFinished`) stvarno smatra
završenom — to flipovanje je sada odloženo dok se ovaj upitnik ne preda. 3 pitanja su fiksna
Likert (1-5): jasnoća zadatka, lakoća snalaženja, zadovoljstvo sopstvenim učinkom; 4. Likert
pitanje varira tekstom po tipu sesije (Intro/AI/Report/Hybrid — pita o korisnosti uvoda, AI
asistenta, dokumentacije, ili kombinacije oboje za Hybrid); 5. je slobodan tekst, opciono. **Local
Postgres only**, kao svaka druga feature u razvoju trenutno u ovom projektu.

**Schema** (`Sql/031_post_session_questionnaire.sql`): nova `PostSessionResponse` tabela
(`ParticipantGuid` + `SessionId` UNIQUE — jedan odgovor po učesniku po sesiji, isti oblik kao
`TlxResult`; `SessionId` je **brojčani** `Sessions.Id` 1-4, za razliku od `TlxResult.SessionId`
koji je NASA-TLX-ova sopstvena tekstualna oznaka poput "Uvodna sesija" — ovo razlikovanje je
izazvalo stvaran bug, vidi ispod). Koristi isti `set_participant_guid()` trigger koji je već
instaliran u lokalnoj bazi (korišćen i za `HybridSectionEngagement`).

**NASA-TLX** (`Nasa-TLX-FullImplementation-AndrejKatin`): `results.component.ts`'s `autoSave()`
posle uspešnog čuvanja TLX rezultata više ne zove `finishStudySession` direktno — umesto toga,
kada je `session.dbSessionId` definisan (study-flow sesije; samostalni magic-link tok iz Part D
bez `dbSessionId` je nepromenjen), navigira na novu `/post-session` rutu
(`PostSessionComponent`, `canActivate: [sessionGuard]`, dodat novi `post-session` slučaj u
`session.guard.ts`). `finishStudySession`/`showSessionDonePopup`/`showIntroDonePopup`/
`closeSessionDonePopup`/`closeIntroDonePopup` i njihov template/CSS su u potpunosti premešteni iz
`ResultsComponent` u `PostSessionComponent` — tamo se sada pozivaju tek posle uspešnog
`POST /api/db/post-session-response`. Novi `DatabaseService.savePostSessionResponse()`; novi
`validatePostSessionPayload()` u `api/_lib/db.ts` (deljeno); nova ruta duplirana na oba uobičajena
mesta — `server.ts` (lokalni dev) i standalone `api/db/post-session-response.ts` (Vercel), isti
obrazac kao svaka druga DB ruta u ovoj app. Q4 teksta biraju se preko `q4LabelKey` computed-a
(`POST_SESSION.Q4_INTRO/AI/REPORT/HYBRID` i18n ključevi) na osnovu `dbSessionId`-a.

**Admin Dashboard**: rezultati se **ne** prikazuju kao posebna Results stranica — umesto toga,
`server/results/routes.mjs`'s `/tlx` raw (ne-aggregate) upit proširen je sa `LEFT JOIN
"PostSessionResponse"` + nova `"DbSessionId"` kolona na postojećem NASA-TLX redu u Participant
Detail-u, dostupno preko novog dugmeta "Dopunski podaci" (`RawTableComponent`'s postojeći
`rowActionLabel`/`rowAction` mehanizam, isti kao REI-40/Big Five "Prikaži odgovore") koje otvara
novi `PostSessionAnswersModalComponent`. Pitanja/odgovori su dupliranI u novom
`src/app/data/post-session-questions-meta.ts` (isti obrazac kao `rei40-items-meta.ts`).

**Pravi bug nađen i ispravljen pre prvog uspešnog pokretanja**: direktan `psr."SessionId" =
t."SessionId"` JOIN je pao sa `operator does not exist: integer = character varying` —
`TlxResult.SessionId` je NASA-TLX-ova tekstualna oznaka ("Uvodna sesija"/"Sesija 1"/"Sesija
2"/"Hibridna sesija"), ne brojčani `Sessions.Id` koji `PostSessionResponse`/`ParticipantSession`/
`HybridSectionEngagement` koriste. Ispravljeno preko `CASE t."SessionId" WHEN 'Uvodna sesija'
THEN 1 ...END` mosta u SQL-u (identično NASA-TLX-ovom sopstvenom `DB_SESSION_TO_TLX` mapiranju u
`utils/study.ts`), selektovano i kao `"DbSessionId"` kolona za frontend modal da ne mora sam da
rekreira to mapiranje.

Uživo verifikovano kroz prave servere (NASA-TLX `:4000`, admin-dashboard `:4312`), bez mokovanja,
preko privremenog pravog učesnika na research 1: TLX sačuvan → `ParticipantSession.IsFinished`
ostaje `false` (bitna provera — ranije bi odmah bio `true`) → validacija (pogrešan `sessionId`,
nedostajuće `q4`) ispravno 400-uje → predaja upitnika uspeva (201) → ponovna predaja ispravno
ažurira isti red (`ON CONFLICT`, ne duplira) → `POST /api/db/session-finished` tek sada flip-uje
`IsFinished` na `true` → admin `/tlx` raw red ispravno nosi `DbSessionId`+`PostSessionAnswers`.
Regresija: TLX red bez ikad popunjenog post-session upitnika ispravno vraća
`PostSessionAnswers: null`. Sav privremeni test-podatak obrisan; pravi podaci (istraživanja 1/10,
učesnici 001-005, uklj. participant `001`-ov pravi `RawTLX: 52.50`) potvrđeno netaknuti.
`npx tsc --noEmit` + `ng build --configuration development` (browser i SSR) čisto na NASA-TLX-u i
admin-dashboard-u; `node --check` čisto na svim dodirnutim `.mjs`/`.ts` fajlovima.

### Bug fix: "Generate" link button nije poštovao research-level toggle (2026-10-02)

Generički `POST /:participantId/links/:type` (dugme "Generate" pored svakog reda u Links tabeli
u Participant Detail-u) je mintovao token za bilo koji tip bez provere da li istraživanje tog
učesnika uopšte koristi tu instrumentu — za razliku od `regenerate-links`, koji je to već ispravno
radio. Uočeno uživo: korisnik je kliknuo "Generate" za DEMOGRAPHIC na učesniku čije istraživanje
ima `UsesDemographics=false` i nula podešenih pitanja, dobio je ispravno formiran, ali potpuno
prazan link. Ispravljeno dodavanjem iste provere (`UsesPsychTests` za REI40/BIGFIVE,
`UsesDemographics` za DEMOGRAPHIC) pre mintovanja — `400 NOT_APPLICABLE` ako instrumenta nije u
upotrebi. `participant-detail.component.ts`'s `linkTypes` je sada `computed` koji filtrira ove
tipove van prikaza (nove `usesPsychTests`/`usesDemographics` signale popunjava `getStudyConfig()`
poziv u `load()`) — dugme se više uopšte ne prikazuje za instrument koji istraživanje ne koristi,
backend provera je odbrana u dubini. Pogrešno mintovan token za stvarnog učesnika obrisan ručno.

### Linkovi: 7 dana umesto 24h, CODE_REVIEW trajan umesto 90 dana (2026-10-02)

Na zahtev korisnika pred javni deploy: svi emailovani/generisani participant-facing magic-linkovi
(CONSENT_ENTRY, REI40, BIGFIVE, NASA_TLX, DEMOGRAPHIC, GENERIC_TASK) sada važe **7 dana** umesto
24h — `TOKEN_TTL_MS` promenjen u `admin-dashboard-andrejkatin/server/participants/routes.mjs`,
`admin-dashboard-andrejkatin/server/consent-form/mailing-list.mjs`, i
`consent-andrejkatin/server/tokens/issueSurveyLinks.mjs` (3 mesta gde se ovaj TTL koristi — svaki
poziv unutar tih fajlova automatski nasleđuje novu vrednost preko iste konstante, nema potrebe
menjati svako mesto pojedinačno). CODE_REVIEW link (lični link učesnika za ceo tok studije) je sada
**efektivno trajan** — `CODE_REVIEW_TOKEN_TTL_MS` promenjen sa 90 dana na 100 godina u
`admin-dashboard-andrejkatin/server/participants/routes.mjs`, `rei40-andrejkatin/server.mjs`, i
`bigfive-andrejkatin/server.mjs` (mintuje se i tamo, preko automatskog "Intro" mejla posle
REI-40/Big Five). Namerno **ne** `ExpiresAt NULL` (što bi zahtevalo izmenu svake provere isteka u
svakoj app-i) već samo veoma daleki datum — svaka postojeća `NOW() < ExpiresAt` provera nastavlja
da radi bez ikakve izmene. `ResearcherInviteToken`/tim-pozivnice (48h) namerno netaknuti — to je
odvojen sistem za naloge istraživača, ne participant-facing studijski linkovi. Uživo verifikovano
(`node --check` svuda, pravi API pozivi): novi REI40 link ~7.00 dana, novi CODE_REVIEW link
~100.0 godina od trenutka generisanja.

### Production deployment — all 8 apps live, Neon replaced with the local DB (2026-10-02)

Everything that was "local Postgres only" above (025–031 migrations, Generic Task, Hybrid mode,
Demographic Questionnaire, Post-session questionnaire, chained emails, 7-day links) is now in
production. The six apps that had never been deployed are live for the first time.

**Hosting pattern for the 6 Node/Angular apps**: Angular build on **Vercel** (team
`beyond-ai-research`), Express API on **Render** (free, region `virginia` next to Neon `us-east-1`).
Each repo's `vercel.json` rewrites `/api/:path*` to its Render URL, then SPA-falls back to
`/index.html` — the Angular code's relative `/api` calls work unchanged, no CORS involved. Render
start command is `node server.mjs` (admin: `node server/index.mjs`), build `npm ci --omit=dev`, Node
22 (`engines`); never set `PORT`/`DB_MODE` there. `.vercelignore` keeps `.env*` and server code out of
Vercel uploads (Vercel CLI only auto-ignores `.env.local`, **not** `.env`).

| App | Frontend (Vercel) | API (Render service id) | GitHub (public) |
|---|---|---|---|
| Admin Dashboard | beyondai-admin.vercel.app | beyondai-admin-api (`srv-davpjq60tbcc73esvci0`) | beyondai-admin-dashboard |
| Consent | beyondai-consent.vercel.app | beyondai-consent-api (`srv-davpjqmgekts73evrr90`) | beyondai-consent |
| REI-40 | beyondai-survey-a.vercel.app | beyondai-survey-a-api (`srv-davpjr49v7es738n7ulg`) | beyondai-rei40 |
| Big Five | beyondai-survey-b.vercel.app | beyondai-survey-b-api (`srv-davpjrgu01pc73fm0p90`) | beyondai-bigfive |
| Task app | beyondai-task.vercel.app | beyondai-task-api (`srv-davpjru0tbcc73esvk1g`) | beyondai-task-app |
| Demographics | beyondai-demographics.vercel.app | beyondai-demographics-api (`srv-davpjse7bikc73evm02g`) | beyondai-demographics |
| Code Review AI | beyondai-code-review.vercel.app | beyondai-backend (`srv-d9gc06mpbkes73cdv87g`, oregon, Docker) | beyondai-code-review |
| NASA-TLX | beyondai-nasa-tlx.vercel.app | (Vercel functions under `/api/db`) | beyondai-nasa-tlx |

REI-40/Big Five are deliberately named `survey-a`/`survey-b` — participants see these domains in
their emails, so the instrument names must not leak (anti-priming).

**Database**: production Neon was replaced with an exact copy of the local Postgres (user's choice):
backup of the old Neon first (`C:\Users\Andrej\Documents\neon-backups\neon-pre-deploy-2026-10-02.{sql,dump}`),
then one `psql --single-transaction -v ON_ERROR_STOP=1` run of `DROP SCHEMA public CASCADE; CREATE
SCHEMA public;` + `pg_dump --no-owner --no-privileges` of the local container — atomic, any error
would have rolled back. Verified afterward: 333 columns / 33 tables / 31 sequences identical, row
counts equal. Both sides are Postgres 18.6; pg_dump/psql run via Docker (`postgres:18` image, Neon
**direct** host — drop `-pooler` from the hostname). Rollback = same procedure with the backup file.
Order mattered: the new Code Review AI/NASA-TLX code needs the new schema, so the swap happened
immediately before their redeploy.

**Email in production goes through a Google Apps Script relay**, not SMTP. Render free blocks
outbound SMTP (confirmed in Render logs: `ENETUNREACH ...:465` / `ETIMEDOUT`, requests hung until
Vercel's 120 s proxy timeout). The Gmail API route was abandoned (the Google Cloud OAuth app could
not be published). `server/email/mailer.mjs` (identical in admin/consent/rei40/bigfive) POSTs to the
Apps Script web app when `MAIL_RELAY_URL`+`MAIL_RELAY_SECRET` are set and falls back to Gmail SMTP
otherwise (local dev). Apps Script intermittently answers the first call after idling with a
transient HTML error page, so the mailer retries non-JSON answers up to 3 times; a JSON
`{ok:false}` is final. Quota: 100 recipients/day (consumer Gmail). Script source + setup:
`admin-dashboard-andrejkatin/docs/email-relay-setup.md`.

**Other things fixed or learned along the way**:
- NASA-TLX `SESSION_IDS` was missing `'Hibridna sesija'` → every Hybrid TLX result 400'd. Fixed.
- `environment.prod.ts` `consentAppUrl` was still localhost. Fixed.
- Production Angular builds failed the 4 kB `anyComponentStyle` budget (admin global header 4.5 kB);
  raised to 8/16 kB in all 6 apps.
- Render can't fetch private GitHub repos unless its GitHub App is linked from the Render dashboard;
  the user chose to make the 6 repos public instead (scanned: no secrets in history).
- Render auto-deploy fires on push for the 6 new services, but still not for `beyondai-backend` —
  trigger that one with `POST /v1/services/srv-d9gc06mpbkes73cdv87g/deploys`.
- `vercel link` appends a `VERCEL_OIDC_TOKEN` to the repo's `.env.local` (existing local settings are
  kept) and needs `.vercel` in `.gitignore`.
- New repos (and NASA-TLX from now on) commit as `BeyondAI Research Group
  <beyondai.researchgroup@gmail.com>` via repo-local git config — the global identity is personal.
  NASA-TLX's older, already-public commits still carry the personal identity (left as is).
- Tokens are passed to `git push` via `-c http.extraheader`, never stored in remote URLs.

**Not working in production (free-tier limits, accepted)**: R analysis (needs `docker run`);
`reminderJob` only runs while the admin API is awake; Render free sleeps after ~15 min idle, so the
first API call after that waits ~15–50 s; EEG control (local Emotiv recorder) is local-only; Google
Calendar/Forms OAuth need the new redirect URIs
(`https://beyondai-admin.vercel.app/api/admin/{calendar,google-forms}/oauth2callback`) added in
Google Cloud Console before they work online.

Verified live end to end through the production URLs with a temporary participant on research 90
(deleted afterward): send-consent-email → real email delivered → consent link resolves → consent
submit → REI-40/Big Five/Demographic tokens minted with production domains (not localhost) → each
resolves on its own app → Code Review link-login works → NASA-TLX post-session response saved.

### Next planned improvements
- Add a `UserSecretsId` reminder to the README / onboarding docs
- Persist session ID in `sessionStorage` so a browser refresh reconnects to the same session
- Write Angular component unit tests (Jasmine)
- Parallelize per-file full-content fallback fetches in `OctokitApiAdapter` (currently sequential)
- Throttle the post-reply suggestions call (currently one extra Claude call per chat turn)
