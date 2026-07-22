import { PrFile } from './pr-file.model';

export interface PrSummaryResponse {
  title: string;
  author: string;
  baseBranch: string;
  headBranch: string;
  description: string | null;
  files: PrFile[];
  totalAdditions: number;
  totalDeletions: number;
  shortSummary?: string | null;
}
