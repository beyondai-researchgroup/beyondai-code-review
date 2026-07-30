import { Component, computed, inject, output, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { SessionService } from '../../core/services/session.service';
import { StudyStateService } from '../../core/services/study-state.service';
import { I18nService } from '../../core/services/i18n.service';
import { ThemeService } from '../../core/services/theme.service';
import { Lang } from '../../core/i18n/translations';
import { ReviewMode } from '../../core/models/review-mode.model';
import { PrLoadedEvent } from '../pr-loader/pr-loader.component';

/**
 * Study-flow login page: the participant enters only their Participant ID and
 * picks a language (locked afterwards). The next unfinished session (Intro →
 * AI → Report) is looked up in the shared study database; the PR under review
 * is preconfigured on the backend, so no GitHub data is requested here.
 * The original PR-loader page is kept in the codebase but no longer used.
 */
@Component({
  selector: 'app-study-login',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './study-login.component.html',
  styleUrl: './study-login.component.scss'
})
export class StudyLoginComponent {
  readonly prLoaded = output<PrLoadedEvent>();

  private readonly sessionService = inject(SessionService);
  private readonly studyState = inject(StudyStateService);
  readonly i18n = inject(I18nService);
  private readonly themeService = inject(ThemeService);
  readonly t = this.i18n.t;

  readonly ReviewMode = ReviewMode;

  readonly participantId = signal('');
  readonly loading = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly allDone = signal(false);
  readonly sessionName = signal<string | null>(null);

  /**
   * Intro sessions no longer let the participant pick a mode — the guided tour now shows both
   * modes live regardless, so there's nothing left for them to choose. Every Intro run starts
   * in this fixed mode, always in the same order, for consistency across participants.
   */
  private static readonly INTRO_START_MODE = ReviewMode.Ai;

  private dbSessionId: number | null = null;

  readonly logoSrc = computed(() =>
    this.themeService.theme() === 'light'
      ? 'assets/beyondai-favicon-light.svg'
      : 'assets/beyondai-favicon.svg'
  );

  setLang(lang: Lang): void {
    // Language is chosen here once and locked for the rest of the run
    // (the nav-bar toggle is disabled in the study flow).
    this.i18n.set(lang);
  }

  onIdInput(): void {
    this.errorMessage.set(null);
    this.allDone.set(false);
  }

  login(): void {
    const id = this.participantId().trim();
    if (!id) {
      this.errorMessage.set(this.t().studyParticipantRequired);
      return;
    }
    if (this.loading()) return;

    this.loading.set(true);
    this.errorMessage.set(null);
    this.allDone.set(false);

    this.sessionService.studyLogin(id).subscribe({
      next: res => {
        if (res.allFinished) {
          this.loading.set(false);
          this.allDone.set(true);
          return;
        }
        this.dbSessionId = res.sessionId ?? null;
        this.sessionName.set(res.sessionName ?? null);

        if (res.sessionName === 'AI') {
          this.startReview(ReviewMode.Ai);
        } else if (res.sessionName === 'Report') {
          this.startReview(ReviewMode.Report);
        } else {
          // Intro session: always starts the same way, no mode choice — the guided tour
          // shows both modes live regardless of which one the session starts in.
          this.startReview(StudyLoginComponent.INTRO_START_MODE);
        }
      },
      error: err => {
        this.loading.set(false);
        this.errorMessage.set(this.extractError(err));
      }
    });
  }

  private startReview(mode: ReviewMode): void {
    const id = this.participantId().trim();
    this.loading.set(true);
    this.errorMessage.set(null);

    this.sessionService.studyStartReview(id, mode, this.i18n.lang()).subscribe({
      next: res => {
        this.studyState.set({
          participantId: id,
          sessionId: this.dbSessionId!,
          sessionName: this.sessionName() ?? '',
          lang: this.i18n.lang()
        });
        this.loading.set(false);
        this.prLoaded.emit({ sessionId: res.sessionId, summary: res.summary, reviewMode: mode });
      },
      error: err => {
        this.loading.set(false);
        this.errorMessage.set(this.extractError(err));
      }
    });
  }

  private extractError(err: unknown): string {
    if (err instanceof HttpErrorResponse) {
      if (err.status === 404) return this.t().studyParticipantNotFound;
      const body = err.error as { detail?: string } | null;
      if (body?.detail) return body.detail;
    }
    return this.t().genericError;
  }
}
