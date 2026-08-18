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

**⚠ Pending, needs the user's call — not done automatically**: `CodeReviewAI.Api` is deployed live
on Render and the NASA-TLX `tlx-config` function is deployed live on Vercel, both querying the
now-renamed schema by the OLD identifiers until redeployed with this fix — they are currently
broken (or will 500 on the affected code paths) for real study participants. Redeploying wasn't
done as part of this pass because `code-review-ai`'s git working tree has a large amount of
**unrelated, already-uncommitted frontend work** (`app.component.*`, `global-header/`,
`translations.ts`, environment files, favicon — not part of this rename) sitting alongside the
rename changes; committing+pushing everything together to fix an urgent production issue would
also ship untested unrelated work. Needs a deliberate choice: a narrow commit of just the
rename-related files (`StudyEndpoints.cs`/`IStudyService.cs`/`StudyService.cs`/`Sql/015_...sql`),
or reviewing/handling the other pending frontend work first.

### Next planned improvements
- Add a `UserSecretsId` reminder to the README / onboarding docs
- Persist session ID in `sessionStorage` so a browser refresh reconnects to the same session
- Write Angular component unit tests (Jasmine)
- Parallelize per-file full-content fallback fetches in `OctokitApiAdapter` (currently sequential)
- Throttle the post-reply suggestions call (currently one extra Claude call per chat turn)
