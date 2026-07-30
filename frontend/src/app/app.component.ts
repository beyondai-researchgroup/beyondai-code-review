import { Component, signal, computed, inject, HostListener, ViewChild } from '@angular/core';
import { PrLoadedEvent } from './features/pr-loader/pr-loader.component';
import { StudyLoginComponent } from './features/study-login/study-login.component';
import { ChatComponent } from './features/chat/chat.component';
import { FileListComponent } from './features/file-list/file-list.component';
import { DiffViewerComponent, QuotedCode } from './features/diff-viewer/diff-viewer.component';
import { PrDescriptionComponent } from './features/pr-description/pr-description.component';
import { ReportViewComponent } from './features/report-view/report-view.component';
import { FinishReviewModalComponent } from './features/finish-review-modal/finish-review-modal.component';
import { TourOverlayComponent } from './shared/tour-overlay/tour-overlay.component';
import { SessionService } from './core/services/session.service';
import { StudyStateService } from './core/services/study-state.service';
import { I18nService } from './core/services/i18n.service';
import { ThemeService } from './core/services/theme.service';
import { TourService, TourStep } from './core/services/tour.service';
import { PrFile } from './core/models/pr-file.model';
import { ReviewMode } from './core/models/review-mode.model';
import { ReviewDecision } from './core/models/review-decision.model';
import { environment } from '../environments/environment';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [StudyLoginComponent, ChatComponent, FileListComponent, DiffViewerComponent, PrDescriptionComponent, ReportViewComponent, FinishReviewModalComponent, TourOverlayComponent],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})
export class AppComponent {
  private readonly sessionService = inject(SessionService);
  private readonly studyState = inject(StudyStateService);
  private readonly tourService = inject(TourService);
  readonly i18n = inject(I18nService);
  readonly themeService = inject(ThemeService);
  readonly t = this.i18n.t;

  readonly ReviewMode = ReviewMode;

  /**
   * The Intro session's tutorial ends by having the participant actually submit a decision —
   * that submission is what triggers the mandatory handoff to NASA-TLX. So in this session the
   * finish-review modal must not be dismissable any other way (X button, Escape, backdrop click)
   * once opened; AI/Report sessions keep the normal cancel-anytime behavior.
   */
  readonly isIntroSession = computed(() => this.studyState.state()?.sessionId === 1);

  readonly logoSrc = computed(() =>
    this.themeService.theme() === 'light'
      ? 'assets/beyondai-favicon-light.svg'
      : 'assets/beyondai-favicon.svg'
  );

  readonly sessionId = signal<string | null>(null);
  readonly reviewMode = signal<ReviewMode>(ReviewMode.Ai);
  readonly showFinishModal = signal(false);
  readonly prFiles = signal<PrFile[]>([]);
  readonly prTitle = signal<string>('');
  readonly prDescription = signal<string | null>(null);
  readonly prShortSummary = signal<string | null>(null);
  readonly prAuthor = signal<string>('');
  readonly prHeadBranch = signal<string>('');
  readonly prBaseBranch = signal<string>('');
  readonly selectedFileName = signal<string | null>(null);
  readonly showDescription = signal(false);

  @ViewChild('chatRef') private chatRef?: ChatComponent;

  readonly fileListCollapsed = signal(false);
  readonly diffCollapsed = signal(false);
  readonly chatCollapsed = signal(false);

  // ── Panel widths (%) for drag-to-resize ──────────────────────────────────
  readonly leftWidth = signal(20);
  readonly rightWidth = signal(40);

  readonly hasDiff = computed(() => !!(this.selectedFileName() || this.showDescription()));
  readonly midWidth = computed(() => 100 - this.leftWidth() - this.rightWidth());
  readonly chatWidth = computed(() => this.hasDiff() ? this.rightWidth() : 100 - this.leftWidth());

  private activeResizer: 'left' | 'mid' | null = null;
  private resizeStartX = 0;
  private resizeStartLeft = 0;
  private resizeStartRight = 0;
  private resizeContainerW = 0;

  onResizeStart(handle: 'left' | 'mid', event: MouseEvent): void {
    if (handle === 'left' && this.fileListCollapsed()) return;
    if (handle === 'mid' && (this.diffCollapsed() || this.chatCollapsed())) return;
    this.activeResizer = handle;
    this.resizeStartX = event.clientX;
    this.resizeStartLeft = this.leftWidth();
    this.resizeStartRight = this.rightWidth();
    const el = document.querySelector('.workspace-body') as HTMLElement;
    this.resizeContainerW = el?.offsetWidth ?? 0;
    event.preventDefault();
  }

  @HostListener('document:mousemove', ['$event'])
  onMouseMove(event: MouseEvent): void {
    if (!this.activeResizer || !this.resizeContainerW) return;
    const deltaPct = ((event.clientX - this.resizeStartX) / this.resizeContainerW) * 100;
    const MIN_LEFT = 14, MAX_LEFT = 46;
    const MIN_MID = 20, MIN_RIGHT = 20;

    if (this.activeResizer === 'left') {
      const newLeft = Math.min(MAX_LEFT, Math.max(MIN_LEFT, this.resizeStartLeft + deltaPct));
      const remaining = 100 - newLeft;
      const needsMin = this.hasDiff() ? MIN_MID + MIN_RIGHT : MIN_RIGHT;
      if (remaining >= needsMin) this.leftWidth.set(newLeft);
    } else {
      // Moving mid-right divider: right panel grows/shrinks, mid absorbs the rest.
      const maxRight = 100 - this.leftWidth() - MIN_MID;
      const newRight = Math.min(maxRight, Math.max(MIN_RIGHT, this.resizeStartRight - deltaPct));
      this.rightWidth.set(newRight);
    }
  }

  @HostListener('document:mouseup')
  onMouseUp(): void {
    this.activeResizer = null;
  }

  setTheme(theme: 'dark' | 'light'): void {
    if (this.themeService.theme() !== theme) this.themeService.toggle();
  }

  setLang(lang: 'sr' | 'en'): void {
    if (this.i18n.lang() !== lang) this.i18n.toggle();
  }

  onPrLoaded(event: PrLoadedEvent): void {
    this.prFiles.set(event.summary.files);
    this.prTitle.set(event.summary.title);
    this.prDescription.set(event.summary.description);
    this.prShortSummary.set(event.summary.shortSummary ?? null);
    this.prAuthor.set(event.summary.author);
    this.prHeadBranch.set(event.summary.headBranch);
    this.prBaseBranch.set(event.summary.baseBranch);
    this.reviewMode.set(event.reviewMode);
    this.sessionId.set(event.sessionId);
    this.selectedFileName.set(null);
    this.showDescription.set(false);

    // Intro (study session 1) is self-guided: instead of a researcher narrating the UI live,
    // a spotlight tour walks the participant through every part of it on the real demo PR.
    if (this.studyState.state()?.sessionId === 1) {
      setTimeout(() => this.tourService.start(this.buildIntroTourSteps(event.reviewMode)), 300);
    }
  }

  /**
   * Switches an Intro session's mode in place (AI ↔ Report) so the tour can show both live on
   * the same demo PR. Best-effort: if the backend call fails, the panel just stays on whatever
   * mode it already showed — a missed live-switch step degrades gracefully, it doesn't block.
   */
  private switchToMode(mode: ReviewMode): void {
    const id = this.sessionId();
    if (!id) return;
    this.sessionService.switchMode(id, mode).subscribe({
      next: () => this.reviewMode.set(mode),
      error: err => console.error('[Tour] Mode switch failed:', err),
    });
  }

  /**
   * Builds the Intro-session guided tour. Both modes are shown live, on the same demo PR: the
   * mode the participant picked on the mode-choice screen first, then the tour calls the
   * mode-switch endpoint to flip the very same session to the other mode for real (not a mock
   * preview) before switching back for the decision step, so the recorded practice decision
   * still reflects the mode the participant originally chose.
   */
  private buildIntroTourSteps(initialMode: ReviewMode): TourStep[] {
    const tt = this.t().tour;
    const otherMode = initialMode === ReviewMode.Ai ? ReviewMode.Report : ReviewMode.Ai;
    const firstFile = this.prFiles()[0]?.fileName ?? null;

    const expandPanels = () => {
      this.fileListCollapsed.set(false);
      this.diffCollapsed.set(false);
      this.chatCollapsed.set(false);
    };

    // Every mode-specific step re-asserts its own mode in beforeShow — not just the dedicated
    // switchStep — so the displayed panel stays correct regardless of navigation direction:
    // stepping "Nazad" back across a mode boundary must revert the live panel too, not just
    // the tooltip text.
    const modeSteps = (mode: ReviewMode): TourStep[] =>
      mode === ReviewMode.Ai
        ? [
            { target: '.diff-viewer', placement: 'left' as const, title: tt.quoteTitle, body: tt.quoteBody, beforeShow: () => this.switchToMode(mode) },
            { target: '.chat-panel', placement: 'left' as const, title: tt.chatTitle, body: tt.chatBody, beforeShow: () => this.switchToMode(mode) },
            {
              target: '.input-row',
              placement: 'top' as const,
              title: tt.askQuestionTitle,
              body: tt.askQuestionBody,
              interactive: true,
              beforeShow: () => this.switchToMode(mode),
            },
          ]
        : [
            { target: '.chat-panel', placement: 'left' as const, title: tt.reportTitle, body: tt.reportBody, beforeShow: () => this.switchToMode(mode) },
            { target: '.report-view__search', placement: 'bottom' as const, title: tt.searchTitle, body: tt.searchBody, beforeShow: () => this.switchToMode(mode) },
          ];

    const switchStep = (toMode: ReviewMode): TourStep => ({
      target: '.chat-panel',
      placement: 'left',
      title: toMode === ReviewMode.Report ? tt.switchToReportTitle : tt.switchToAiTitle,
      body: toMode === ReviewMode.Report ? tt.switchToReportBody : tt.switchToAiBody,
      beforeShow: () => this.switchToMode(toMode),
    });

    return [
      {
        target: null,
        placement: 'center',
        title: tt.welcomeTitle,
        body: tt.welcomeBody,
        beforeShow: () => { expandPanels(); this.showDescription.set(false); },
      },
      {
        target: '.section-files',
        placement: 'right',
        title: tt.fileListTitle,
        body: tt.fileListBody,
      },
      {
        target: '.section-summary',
        placement: 'right',
        title: tt.summaryTitle,
        body: tt.summaryBody,
      },
      {
        target: '.diff-viewer',
        placement: 'right',
        title: tt.diffTitle,
        body: tt.diffBody,
        beforeShow: () => { if (firstFile) this.onFileSelected(firstFile); },
      },
      ...modeSteps(initialMode),
      switchStep(otherMode),
      ...modeSteps(otherMode),
      {
        target: '.finish-btn',
        placement: 'right',
        title: tt.decisionBtnTitle,
        body: tt.decisionBtnBody,
        // Defensive: closes any stray modal so it doesn't linger open behind this step's
        // spotlight on the decision button (the finish-modal step itself can no longer be
        // navigated back out of, but this guards against any other path landing here).
        beforeShow: () => { this.switchToMode(initialMode); this.onModalClosed(); },
      },
      {
        target: '.modal-dialog',
        placement: 'left',
        title: tt.finishModalTitle,
        body: tt.finishModalBody,
        beforeShow: () => this.openFinishModal(),
        // Interactive + hideNav: the modal is genuinely usable right away, and the tooltip's own
        // Skip/Back/Done buttons are hidden — the only way to leave this step (and end the tour)
        // is a real decision submit (wired in onDecisionSubmitted), since that submit is what
        // triggers the mandatory NASA-TLX handoff. No afterLeave: nothing should close the modal
        // on the way out, it stays open for the submit that's already in flight.
        interactive: true,
        hideNav: true,
      },
    ];
  }

  onQuoteToChat(quoted: QuotedCode): void {
    this.chatRef?.insertQuote(quoted);
  }

  onFileSelected(fileName: string | null): void {
    this.selectedFileName.set(fileName);
    this.showDescription.set(false);
    if (!fileName) {
      this.fileListCollapsed.set(false);
      this.diffCollapsed.set(false);
      this.chatCollapsed.set(false);
    }
  }

  onShowDescription(): void {
    const next = !this.showDescription();
    this.showDescription.set(next);
    this.selectedFileName.set(null);
    if (!next) {
      this.fileListCollapsed.set(false);
      this.diffCollapsed.set(false);
      this.chatCollapsed.set(false);
    }
  }

  openFinishModal(): void {
    this.showFinishModal.set(true);
  }

  onModalClosed(): void {
    this.showFinishModal.set(false);
  }

  onDecisionSubmitted(_decision: ReviewDecision): void {
    this.showFinishModal.set(false);
    // The Intro tour's last step only ends here, on a real submit — see buildIntroTourSteps.
    if (this.tourService.active()) this.tourService.end();
    // Hand off to the NASA-TLX workload-assessment app once the reviewer's decision is
    // recorded, passing the study context (participant, session, locked language) so
    // the TLX starts without its own login. Navigate only after the DELETE settles —
    // a synchronous redirect would abort the in-flight request and leave the session
    // (holding the PAT) alive on the server until the cleanup timeout.
    const study = this.studyState.state();
    const params = new URLSearchParams({
      participantId: study?.participantId ?? '',
      sessionId: String(study?.sessionId ?? ''),
      lang: study?.lang ?? this.i18n.lang(),
      theme: this.themeService.theme()
    });
    const redirect = () => window.location.href = `${environment.nasaTlxStartUrl}?${params}`;
    const id = this.sessionId();
    if (!id) {
      redirect();
      return;
    }
    this.sessionService.deleteSession(id).subscribe({ complete: redirect, error: redirect });
  }
}
