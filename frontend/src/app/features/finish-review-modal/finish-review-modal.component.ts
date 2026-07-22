import {
  Component,
  ElementRef,
  HostListener,
  OnInit,
  ViewChild,
  computed,
  inject,
  input,
  output,
  signal
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { SessionService } from '../../core/services/session.service';
import { I18nService } from '../../core/services/i18n.service';
import { ReviewDecision, ReviewDecisionType } from '../../core/models/review-decision.model';

@Component({
  selector: 'app-finish-review-modal',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './finish-review-modal.component.html',
  styleUrl: './finish-review-modal.component.scss'
})
export class FinishReviewModalComponent implements OnInit {
  readonly sessionId = input.required<string>();

  readonly closed = output<void>();
  readonly decisionSubmitted = output<ReviewDecision>();

  private readonly sessionService = inject(SessionService);
  readonly i18n = inject(I18nService);
  readonly t = this.i18n.t;

  readonly ReviewDecisionType = ReviewDecisionType;

  /** Mirrors the backend's MaxDecisionCommentLength — longer comments are rejected with 400. */
  readonly maxCommentLength = 2000;

  readonly comment = signal('');
  readonly submitting = signal(false);
  readonly submittingDecision = signal<ReviewDecisionType | null>(null);
  readonly errorMessage = signal<string | null>(null);

  readonly canSubmit = computed(() => this.comment().trim().length > 0 && !this.submitting());

  @ViewChild('textarea') private textareaRef!: ElementRef<HTMLTextAreaElement>;

  ngOnInit(): void {
    setTimeout(() => this.textareaRef?.nativeElement.focus(), 50);
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (!this.submitting()) this.close();
  }

  close(): void {
    this.closed.emit();
  }

  submit(decision: ReviewDecisionType): void {
    const trimmed = this.comment().trim();
    if (!trimmed || this.submitting()) return;

    this.submitting.set(true);
    this.submittingDecision.set(decision);
    this.errorMessage.set(null);

    this.sessionService
      .submitDecision(this.sessionId(), { decision, comment: trimmed })
      .subscribe({
        next: result => this.decisionSubmitted.emit(result),
        error: () => {
          this.errorMessage.set(this.t().decisionError);
          this.submitting.set(false);
          this.submittingDecision.set(null);
        }
      });
  }
}
