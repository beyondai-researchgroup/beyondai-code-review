import { Component, inject, input, output } from '@angular/core';
import { I18nService } from '../../core/services/i18n.service';

/**
 * Shown right after the review decision is recorded, before the hand-off to NASA-TLX: thanks the
 * participant and reminds them what is still left (workload scale + a short questionnaire, a few
 * minutes in total). Pilot feedback: the instant redirect felt abrupt, and participants had
 * forgotten what came after the PR review.
 */
@Component({
  selector: 'app-next-steps-modal',
  standalone: true,
  templateUrl: './next-steps-modal.component.html',
  styleUrl: './next-steps-modal.component.scss'
})
export class NextStepsModalComponent {
  /** True when the review time ran out (the decision was submitted automatically). */
  readonly timedOut = input<boolean>(false);
  /** False until the session cleanup request has settled — the Continue button waits for it. */
  readonly ready = input<boolean>(true);

  readonly continued = output<void>();

  readonly t = inject(I18nService).t;
}
