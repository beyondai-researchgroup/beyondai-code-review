import { Injectable, signal } from '@angular/core';

export interface StudyState {
  participantId: string;
  /** Study session id from the shared Sessions table: 1=Intro, 2=AI, 3=Report. */
  sessionId: number;
  sessionName: string;
  lang: 'sr' | 'en';
  /** Per-app participant timer (2026-09-11), from /api/study/start-review's own response —
   *  null when disabled. Applies to the review session that was just started. */
  timerMinutes: number | null;
  /** Whether this is a fixed, repeatable-use test participant (Participant.IsTestParticipant) —
   *  drives the NASA-TLX handoff shape (legacy query params vs an opaque token) and whether
   *  `linkToken` below is meaningful at all. */
  isTestParticipant: boolean;
  /** The personal-link token this participant logged in with — null for a test participant (they
   *  use the bare-id form). Re-sent on start-review as defense in depth, and used to build the
   *  "come back to your own link" fallback if the NASA-TLX handoff token mint ever fails. */
  linkToken: string | null;
}

const STORAGE_KEY = 'study-state';

/**
 * Holds the logged-in participant's study context (who they are, which study
 * session is running, which language was locked at login). Persisted in
 * sessionStorage so the NASA-TLX handoff parameters survive a page refresh.
 */
@Injectable({ providedIn: 'root' })
export class StudyStateService {
  private readonly _state = signal<StudyState | null>(this.restore());

  readonly state = this._state.asReadonly();

  set(state: StudyState): void {
    this._state.set(state);
    try { sessionStorage.setItem(STORAGE_KEY, JSON.stringify(state)); } catch { /* storage unavailable */ }
  }

  clear(): void {
    this._state.set(null);
    try { sessionStorage.removeItem(STORAGE_KEY); } catch { /* storage unavailable */ }
  }

  private restore(): StudyState | null {
    try {
      const raw = sessionStorage.getItem(STORAGE_KEY);
      if (!raw) return null;
      const s = JSON.parse(raw) as StudyState;
      return s.participantId && s.sessionId ? s : null;
    } catch {
      return null;
    }
  }
}
