export enum ReviewDecisionType {
  Accepted = 'Accepted',
  Rejected = 'Rejected'
}

export interface SubmitDecisionRequest {
  decision: ReviewDecisionType;
  comment: string;
}

export interface ReviewDecision extends SubmitDecisionRequest {
  decidedAt: string;
}
