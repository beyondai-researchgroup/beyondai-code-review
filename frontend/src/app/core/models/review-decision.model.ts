export enum ReviewDecisionType {
  Accepted = 'Accepted',
  Rejected = 'Rejected',
  /** Per-app participant timer (2026-09-11) — recorded when the research's Code Review timer
   *  expired and the app auto-submitted on the reviewer's behalf. Never coerced to Accepted or
   *  Rejected — the reviewer never actually made that choice. */
  TimedOut = 'TimedOut'
}

export interface SubmitDecisionRequest {
  decision: ReviewDecisionType;
  comment: string;
}

export interface ReviewDecision extends SubmitDecisionRequest {
  decidedAt: string;
  /** Opaque NASA-TLX handoff token — set for a real (non-test) participant, null for a test
   *  participant (who has no personal link and uses the legacy query-param handoff instead). */
  handoffToken?: string | null;
}
