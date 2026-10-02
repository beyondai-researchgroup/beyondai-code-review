import { Component, computed, inject, output, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { environment } from '../../../environments/environment';
import { SessionService, StudyLoginResponse } from '../../core/services/session.service';
import { StudyStateService } from '../../core/services/study-state.service';
import { I18nService } from '../../core/services/i18n.service';
import { ThemeService } from '../../core/services/theme.service';
import { ReviewMode } from '../../core/models/review-mode.model';
import { PrLoadedEvent } from '../pr-loader/pr-loader.component';

/**
 * Study-flow login page: the participant enters only their Participant ID. Language is no
 * longer picked here (Phase D) — it's locked once at the separate Consent app and propagated
 * via Participant.Language, applied automatically from the login response. Consent itself is a
 * hard prerequisite too: a participant who hasn't completed it yet is shown a link to the
 * Consent app instead of the mode-choice screen. The next unfinished session (Intro → AI →
 * Report) is looked up in the shared study database; the PR under review is preconfigured on
 * the backend, so no GitHub data is requested here. The original PR-loader page is kept in the
 * codebase but no longer used.
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
  readonly consentRequired = signal(false);
  readonly baselineRequired = signal(false);
  readonly sessionName = signal<string | null>(null);
  readonly consentAppUrl = environment.consentAppUrl;

  /** True while a `?link=` token from the URL is being auto-resolved on load — the bare-id form
   *  (test-participants-only) stays hidden the whole time, including on a resolution failure
   *  (an invalid/expired link never falls back to letting someone type an id instead). */
  readonly resolvingLink = signal(false);
  readonly linkInvalid = signal(false);

  /**
   * Intro sessions no longer let the participant pick a mode — the guided tour now shows both
   * modes live regardless, so there's nothing left for them to choose. Every Intro run starts
   * in this fixed mode, always in the same order, for consistency across participants.
   */
  private static readonly INTRO_START_MODE = ReviewMode.Ai;

  private dbSessionId: number | null = null;
  private resolvedParticipantId: string | null = null;
  private linkToken: string | null = null;
  private isTestParticipant = false;

  readonly logoSrc = computed(() =>
    this.themeService.theme() === 'light'
      ? 'assets/beyondai-favicon-light.svg'
      : 'assets/beyondai-favicon.svg'
  );

  constructor() {
    // No Angular Router in this app (see main.ts) — read the personal-link token straight off
    // the URL. Present → this is a real participant, auto-login via the token, bare-id form
    // never shown. Absent → the bare-id form renders as before, for test participants only.
    const params = new URLSearchParams(window.location.search);
    const token = params.get('link');
    if (token) {
      // Strip the token from the visible URL/history immediately — same reasoning as an OAuth
      // callback: a token that lingers in the address bar is one that can end up bookmarked,
      // shared in a screenshot, or left in browser history.
      window.history.replaceState({}, '', window.location.pathname);
      this.loginViaLink(token);
    }
  }

  onIdInput(): void {
    this.errorMessage.set(null);
    this.allDone.set(false);
    this.consentRequired.set(false);
    this.baselineRequired.set(false);
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
    this.consentRequired.set(false);
    this.baselineRequired.set(false);

    this.sessionService.studyLogin(id).subscribe({
      next: res => this.handleLoginResponse(res, id, null),
      error: err => {
        this.loading.set(false);
        this.errorMessage.set(this.extractError(err));
      }
    });
  }

  private loginViaLink(token: string): void {
    this.resolvingLink.set(true);
    this.linkInvalid.set(false);

    this.sessionService.studyLinkLogin(token).subscribe({
      next: res => this.handleLoginResponse(res, res.participantId ?? '', token),
      error: () => {
        this.resolvingLink.set(false);
        this.linkInvalid.set(true);
      }
    });
  }

  private handleLoginResponse(res: StudyLoginResponse, id: string, linkToken: string | null): void {
    if (res.consentRequired) {
      this.loading.set(false);
      this.resolvingLink.set(false);
      this.consentRequired.set(true);
      return;
    }
    if (res.baselineRequired) {
      this.loading.set(false);
      this.resolvingLink.set(false);
      this.baselineRequired.set(true);
      return;
    }
    if (res.allFinished) {
      this.loading.set(false);
      this.resolvingLink.set(false);
      this.allDone.set(true);
      return;
    }
    // Locked once at the Consent app — applied here instead of letting the participant
    // pick a language on this screen.
    if (res.language) this.i18n.set(res.language);

    this.resolvedParticipantId = id;
    this.linkToken = linkToken;
    this.isTestParticipant = res.isTestParticipant ?? false;
    this.dbSessionId = res.sessionId ?? null;
    this.sessionName.set(res.sessionName ?? null);

    if (res.sessionName === 'AI') {
      this.startReview(ReviewMode.Ai);
    } else if (res.sessionName === 'Hybrid') {
      this.startReview(ReviewMode.Hybrid);
    } else if (res.sessionName === 'Report') {
      this.startReview(ReviewMode.Report);
    } else {
      // Intro session: always starts the same way, no mode choice — the guided tour
      // shows both modes live regardless of which one the session starts in.
      this.startReview(StudyLoginComponent.INTRO_START_MODE);
    }
  }

  private startReview(mode: ReviewMode): void {
    const id = this.resolvedParticipantId!;
    this.loading.set(true);
    this.errorMessage.set(null);

    this.sessionService.studyStartReview(id, mode, this.i18n.lang(), this.linkToken).subscribe({
      next: res => {
        this.studyState.set({
          participantId: id,
          sessionId: this.dbSessionId!,
          sessionName: this.sessionName() ?? '',
          lang: this.i18n.lang(),
          timerMinutes: res.timerMinutes ?? null,
          isTestParticipant: this.isTestParticipant,
          linkToken: this.linkToken
        });
        this.loading.set(false);
        this.resolvingLink.set(false);
        this.prLoaded.emit({ sessionId: res.sessionId, summary: res.summary, reviewMode: mode });
      },
      error: err => {
        this.loading.set(false);
        this.resolvingLink.set(false);
        this.errorMessage.set(this.extractError(err));
      }
    });
  }

  private extractError(err: unknown): string {
    if (err instanceof HttpErrorResponse) {
      if (err.status === 404) return this.t().studyParticipantNotFound;
      if (err.status === 403 && (err.error as { error?: string } | null)?.error === 'PERSONAL_LINK_REQUIRED') {
        return this.t().studyPersonalLinkRequired;
      }
      const body = err.error as { detail?: string } | null;
      if (body?.detail) return body.detail;
    }
    return this.t().genericError;
  }
}
