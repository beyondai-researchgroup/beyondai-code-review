import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { PrSummaryResponse } from '../models/pr-summary.model';
import { ReviewMode } from '../models/review-mode.model';
import { ReviewDecision, SubmitDecisionRequest } from '../models/review-decision.model';
import { I18nService } from './i18n.service';

interface SessionCreatedResponse {
  sessionId: string;
}

export interface StudyLoginResponse {
  allFinished: boolean;
  consentRequired?: boolean;
  /** True when the next session is this participant's first experimental session and the
   *  researcher hasn't marked the baseline (EEG) measurement done yet in the Admin Dashboard —
   *  nothing for the participant to do but wait. */
  baselineRequired?: boolean;
  /** Only present on a link-login response — Login's own caller already knows the id (they typed
   *  it); LinkLogin's caller only ever held an opaque token, never the participant id itself. */
  participantId?: string;
  sessionId?: number;
  sessionName?: string;
  /** Locked once at the Consent app (Participant.Language) — applied instead of a picker here. */
  language?: 'sr' | 'en';
  /** Per-app participant timer (2026-09-11) — null when disabled. StartReviewResponse's own copy
   *  is the one actually applied (see study-login.component.ts); this is just shape parity. */
  timerMinutes?: number | null;
  /** Whether this participant is a fixed, repeatable-use test participant — false/absent for
   *  every real participant, who only ever reached this response via a personal link. */
  isTestParticipant?: boolean;
}

export interface StartReviewResponse {
  sessionId: string;
  summary: PrSummaryResponse;
  /** Per-app participant timer (2026-09-11) — null when disabled. */
  timerMinutes?: number | null;
}

interface LoadPrRequest {
  gitHubToken: string;
  owner: string;
  repo: string;
  prNumber: number;
  reviewMode: ReviewMode;
  lang: string;
}

@Injectable({ providedIn: 'root' })
export class SessionService {
  private readonly http = inject(HttpClient);
  private readonly i18n = inject(I18nService);
  private readonly apiUrl = `${environment.apiUrl}/session`;

  /** Creates a new review session and returns its ID. */
  createSession(): Observable<string> {
    return this.http
      .post<SessionCreatedResponse>(`${this.apiUrl}`, {})
      .pipe(map(res => res.sessionId));
  }

  /** Loads a GitHub PR into an existing session. */
  loadPr(
    sessionId: string,
    token: string,
    owner: string,
    repo: string,
    prNumber: number,
    reviewMode: ReviewMode = ReviewMode.Ai
  ): Observable<PrSummaryResponse> {
    const body: LoadPrRequest = { gitHubToken: token, owner, repo, prNumber, reviewMode, lang: this.i18n.lang() };
    return this.http.post<PrSummaryResponse>(
      `${this.apiUrl}/${sessionId}/load-pr`,
      body
    );
  }

  /**
   * Streams an AI report for a Report-mode session via SSE.
   * Emits text chunks as they arrive; completes when [DONE] is received.
   */
  generateReport(sessionId: string, regenerate = false): Observable<string> {
    return new Observable<string>(subscriber => {
      const controller = new AbortController();
      const params = new URLSearchParams();
      if (regenerate) params.set('regenerate', 'true');
      params.set('lang', this.i18n.lang());
      const url = `${this.apiUrl}/${sessionId}/report/generate?${params}`;

      fetch(url, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({}),
        signal: controller.signal
      })
        .then(async response => {
          if (!response.ok) {
            const errorText = await response.text();
            subscriber.error(new Error(`HTTP ${response.status}: ${errorText}`));
            return;
          }

          const reader = response.body!.getReader();
          const decoder = new TextDecoder();
          let buffer = '';

          try {
            while (true) {
              const { done, value } = await reader.read();
              if (done) break;

              buffer += decoder.decode(value, { stream: true });
              const lines = buffer.split('\n');
              buffer = lines.pop() ?? '';

              for (const line of lines) {
                const trimmed = line.trim();
                if (trimmed === 'data: [DONE]') {
                  subscriber.complete();
                  return;
                }
                if (trimmed.startsWith('data: ')) {
                  const json = trimmed.slice('data: '.length);
                  try {
                    const event = JSON.parse(json) as { text?: string };
                    if (event.text) subscriber.next(event.text);
                  } catch { }
                }
              }
            }
          } catch (err) {
            if ((err as Error).name !== 'AbortError') subscriber.error(err);
          }

          subscriber.complete();
        })
        .catch(err => {
          if ((err as Error).name !== 'AbortError') subscriber.error(err);
        });

      return () => controller.abort();
    });
  }

  /**
   * Switches an Intro-session's mode in place (AI ↔ Report) so the guided tour can show both
   * live on the same demo PR. The backend rejects this for real AI/Report study sessions.
   */
  switchMode(sessionId: string, mode: ReviewMode): Observable<{ mode: ReviewMode }> {
    return this.http.post<{ mode: ReviewMode }>(
      `${this.apiUrl}/${sessionId}/mode`,
      { mode }
    );
  }

  /** Submits the human reviewer's own decision and comment for the current session. */
  submitDecision(sessionId: string, payload: SubmitDecisionRequest): Observable<ReviewDecision> {
    return this.http.post<ReviewDecision>(
      `${this.apiUrl}/${sessionId}/decision`,
      payload
    );
  }

  /** Asks Claude for 4 contextual follow-up question suggestions based on session history. */
  getSuggestions(sessionId: string): Observable<string[]> {
    const params = new URLSearchParams({ lang: this.i18n.lang() });
    return this.http
      .post<{ suggestions: string[] }>(`${this.apiUrl}/${sessionId}/chat/suggestions?${params}`, {})
      .pipe(map(r => r.suggestions));
  }

  /** Returns the PR summary for an already-loaded session. */
  getPrSummary(sessionId: string): Observable<PrSummaryResponse> {
    return this.http.get<PrSummaryResponse>(
      `${this.apiUrl}/${sessionId}/pr-summary`
    );
  }

  /**
   * Streams AI response chunks using the Fetch API + ReadableStream.
   * HttpClient is intentionally avoided here — it buffers SSE responses.
   */
  streamChat(sessionId: string, message: string): Observable<string> {
    return new Observable<string>(subscriber => {
      const controller = new AbortController();
      const url = `${this.apiUrl}/${sessionId}/chat/stream`;

      fetch(url, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ message, lang: this.i18n.lang() }),
        signal: controller.signal
      })
        .then(async response => {
          if (!response.ok) {
            const errorText = await response.text();
            subscriber.error(new Error(`HTTP ${response.status}: ${errorText}`));
            return;
          }

          const reader = response.body!.getReader();
          const decoder = new TextDecoder();
          let buffer = '';

          try {
            while (true) {
              const { done, value } = await reader.read();
              if (done) break;

              buffer += decoder.decode(value, { stream: true });

              // Split on newlines; keep any incomplete trailing line in the buffer.
              const lines = buffer.split('\n');
              buffer = lines.pop() ?? '';

              for (const line of lines) {
                const trimmed = line.trim();
                if (trimmed === 'data: [DONE]') {
                  subscriber.complete();
                  return;
                }
                if (trimmed.startsWith('data: ')) {
                  const json = trimmed.slice('data: '.length);
                  try {
                    const event = JSON.parse(json) as { text?: string };
                    if (event.text) {
                      subscriber.next(event.text);
                    }
                  } catch {
                    // Malformed SSE line — skip.
                  }
                }
              }
            }
          } catch (err) {
            if ((err as Error).name !== 'AbortError') {
              subscriber.error(err);
            }
          }

          subscriber.complete();
        })
        .catch(err => {
          if ((err as Error).name !== 'AbortError') {
            subscriber.error(err);
          }
        });

      // Teardown: abort the fetch when the Observable is unsubscribed.
      return () => controller.abort();
    });
  }

  /** Returns the unified diff patch for a single file in the loaded PR. */
  getFilePatch(sessionId: string, fileName: string): Observable<string | null> {
    const params = new URLSearchParams({ fileName });
    return this.http
      .get<{ patch: string | null }>(`${this.apiUrl}/${sessionId}/file-patch?${params}`)
      .pipe(map(res => res.patch));
  }

  /**
   * Fetches the repository file tree and key file contents and stores them in the session.
   * Requires the PR to be loaded first (token/owner/repo are reused from that call).
   */
  loadRepoContext(sessionId: string): Observable<{ loaded: boolean; charCount: number }> {
    return this.http.post<{ loaded: boolean; charCount: number }>(
      `${this.apiUrl}/${sessionId}/load-repo-context`,
      {}
    );
  }

  /** Deletes a session immediately. */
  deleteSession(sessionId: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${sessionId}`);
  }

  /**
   * Records one expand/collapse event for a Hybrid-mode documentation accordion section.
   * Best-effort, fire-and-forget — never blocks the participant. Uses a raw `fetch` with
   * `keepalive: true` (not HttpClient, which has no such option) so the call survives a
   * `beforeunload`/hard-navigation moment, e.g. flushing a still-open section right as the
   * decision-submit handoff redirects away from this app.
   */
  recordHybridSectionEvent(
    sessionId: string,
    payload: { sectionId: string; sectionTitle: string; action: 'Expand' | 'Collapse'; durationSeconds?: number }
  ): void {
    fetch(`${this.apiUrl}/${sessionId}/hybrid/section-event`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
      keepalive: true
    }).catch(() => { /* best-effort, never blocks the participant */ });
  }

  /**
   * Records one participant activity entry (a click, a resize, a search, ...) to the session's
   * activity-log CSV file (local-dev-only; a silent no-op backend-side when disabled). Best-effort,
   * fire-and-forget — same `fetch` + `keepalive: true` pattern as `recordHybridSectionEvent` above,
   * since some callers (e.g. a `beforeunload` flush) need the call to survive a hard navigation.
   */
  recordActivity(
    sessionId: string,
    event: { eventType: string; detail?: string | null; startedAt?: Date; endedAt?: Date | null }
  ): void {
    fetch(`${this.apiUrl}/${sessionId}/activity-log`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        eventType: event.eventType,
        detail: event.detail ?? null,
        startedAt: (event.startedAt ?? new Date()).toISOString(),
        endedAt: event.endedAt ? event.endedAt.toISOString() : null
      }),
      keepalive: true
    }).catch(() => { /* best-effort, never blocks the participant */ });
  }

  // ── Study flow ────────────────────────────────────────────────────────────

  /** Validates the participant and returns their next unfinished study session. Test participants
   *  only — every other participant logs in via {@link studyLinkLogin} instead. */
  studyLogin(participantId: string): Observable<StudyLoginResponse> {
    return this.http.post<StudyLoginResponse>(
      `${environment.apiUrl}/study/login`,
      { participantId }
    );
  }

  /** Resolves a personal-link token and logs in exactly like {@link studyLogin} — the only entry
   *  point for every non-test participant. */
  studyLinkLogin(token: string): Observable<StudyLoginResponse> {
    return this.http.post<StudyLoginResponse>(
      `${environment.apiUrl}/study/link-login`,
      { token }
    );
  }

  /** Creates a review session preloaded with the configured demo PR in the given mode. `linkToken`
   *  is required (and re-validated server-side) for every non-test participant; omit for a test
   *  participant, who has no personal link. */
  studyStartReview(
    participantId: string,
    reviewMode: ReviewMode,
    lang: string,
    linkToken: string | null = null
  ): Observable<StartReviewResponse> {
    return this.http.post<StartReviewResponse>(
      `${environment.apiUrl}/study/start-review`,
      { participantId, reviewMode, lang, linkToken }
    );
  }
}
