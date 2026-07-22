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

### Next planned improvements
- Add a `UserSecretsId` reminder to the README / onboarding docs
- Persist session ID in `sessionStorage` so a browser refresh reconnects to the same session
- Write Angular component unit tests (Jasmine)
- Parallelize per-file full-content fallback fetches in `OctokitApiAdapter` (currently sequential)
- Throttle the post-reply suggestions call (currently one extra Claude call per chat turn)
