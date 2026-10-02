import { Component, signal, computed, inject, HostListener, ViewChild } from '@angular/core';
import { PrLoadedEvent } from './features/pr-loader/pr-loader.component';
import { GlobalHeaderComponent } from './global-header/global-header.component';
import { StudyLoginComponent } from './features/study-login/study-login.component';
import { ChatComponent } from './features/chat/chat.component';
import { FileListComponent } from './features/file-list/file-list.component';
import { DiffViewerComponent, QuotedCode } from './features/diff-viewer/diff-viewer.component';
import { PrDescriptionComponent } from './features/pr-description/pr-description.component';
import { ReportViewComponent } from './features/report-view/report-view.component';
import { FinishReviewModalComponent } from './features/finish-review-modal/finish-review-modal.component';
import { TourOverlayComponent } from './shared/tour-overlay/tour-overlay.component';
import { TimerDisplayComponent } from './shared/timer-display/timer-display.component';
import { SessionService } from './core/services/session.service';
import { StudyStateService } from './core/services/study-state.service';
import { I18nService } from './core/services/i18n.service';
import { ThemeService } from './core/services/theme.service';
import { TourService, TourStep } from './core/services/tour.service';
import { PrFile } from './core/models/pr-file.model';
import { ReviewMode } from './core/models/review-mode.model';
import { ReviewDecision, ReviewDecisionType } from './core/models/review-decision.model';
import { environment } from '../environments/environment';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [GlobalHeaderComponent, StudyLoginComponent, ChatComponent, FileListComponent, DiffViewerComponent, PrDescriptionComponent, ReportViewComponent, FinishReviewModalComponent, TourOverlayComponent, TimerDisplayComponent],
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

  readonly sessionId = signal<string | null>(null);
  readonly reviewMode = signal<ReviewMode>(ReviewMode.Ai);
  readonly showFinishModal = signal(false);

  // Per-app participant timer (2026-09-11) — from Research.TimerCodeReviewEnabled/Minutes,
  // captured at start-review time into StudyStateService. null means disabled.
  readonly timerMinutes = computed(() => this.studyState.state()?.timerMinutes ?? null);
  /** Guards against a double-submit (the timer firing again after a manual submit already went
   *  through, or vice versa) and hides the countdown once a decision has been recorded either
   *  way. */
  readonly reviewFinished = signal(false);
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
  @ViewChild('hybridReportRef') private hybridReportRef?: ReportViewComponent;

  readonly fileListCollapsed = signal(false);
  readonly diffCollapsed = signal(false);
  readonly chatCollapsed = signal(false);

  /** Hybrid mode only (participant "004") — toggles the slide-out chat drawer over the
   *  documentation view. A wholly separate overlay, not integrated with the 3-panel resize/
   *  collapse mechanism above. */
  readonly hybridChatOpen = signal(false);

  // ── Panel widths (%) for drag-to-resize ──────────────────────────────────
  readonly leftWidth = signal(20);
  readonly rightWidth = signal(40);

  readonly hasDiff = computed(() => !!(this.selectedFileName() || this.showDescription()));
  readonly midWidth = computed(() => 100 - this.leftWidth() - this.rightWidth());
  readonly chatWidth = computed(() => this.hasDiff() ? this.rightWidth() : 100 - this.leftWidth());

  /** Hybrid mode only — the docs/chat split within .hybrid-panel-stack, draggable via the
   *  horizontal divider between them. Persists across chat close/reopen within the session. */
  readonly hybridDocsHeightPct = signal(50);

  private activeResizer: 'left' | 'mid' | 'hybrid-vert' | null = null;
  private resizeStartX = 0;
  private resizeStartY = 0;
  private resizeStartLeft = 0;
  private resizeStartRight = 0;
  private resizeStartDocsPct = 0;
  private resizeContainerW = 0;
  private resizeContainerH = 0;
  private resizeActivityLabel = '';
  private resizeActivityStartedAt: Date | null = null;
  private resizeActivityBefore = 0;

  onResizeStart(handle: 'left' | 'mid', event: MouseEvent): void {
    if (handle === 'left' && this.fileListCollapsed()) return;
    if (handle === 'mid' && (this.diffCollapsed() || this.chatCollapsed())) return;
    this.activeResizer = handle;
    this.resizeStartX = event.clientX;
    this.resizeStartLeft = this.leftWidth();
    this.resizeStartRight = this.rightWidth();
    const el = document.querySelector('.workspace-body') as HTMLElement;
    this.resizeContainerW = el?.offsetWidth ?? 0;
    this.resizeActivityLabel = handle === 'left' ? 'file/diff divider' : 'diff/documentation divider';
    this.resizeActivityStartedAt = new Date();
    this.resizeActivityBefore = handle === 'left' ? this.leftWidth() : this.rightWidth();
    event.preventDefault();
  }

  /** Hybrid mode only — dragging the horizontal divider between the documentation pane (top) and
   *  the chat pane (bottom) inside .hybrid-panel-stack. Reuses the same generic resizer machinery
   *  as onResizeStart above, just tracking a vertical delta against the stack's height instead of
   *  a horizontal delta against the workspace's width. */
  onHybridVerticalResizeStart(event: MouseEvent): void {
    this.activeResizer = 'hybrid-vert';
    this.resizeStartY = event.clientY;
    this.resizeStartDocsPct = this.hybridDocsHeightPct();
    const el = document.querySelector('.hybrid-panel-stack') as HTMLElement;
    this.resizeContainerH = el?.offsetHeight ?? 0;
    this.resizeActivityLabel = 'documentation/chat divider';
    this.resizeActivityStartedAt = new Date();
    this.resizeActivityBefore = this.hybridDocsHeightPct();
    event.preventDefault();
  }

  @HostListener('document:mousemove', ['$event'])
  onMouseMove(event: MouseEvent): void {
    if (this.activeResizer === 'hybrid-vert') {
      if (!this.resizeContainerH) return;
      const deltaPct = ((event.clientY - this.resizeStartY) / this.resizeContainerH) * 100;
      const MIN = 20;
      const next = Math.min(100 - MIN, Math.max(MIN, this.resizeStartDocsPct + deltaPct));
      this.hybridDocsHeightPct.set(next);
      return;
    }

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
    if (this.activeResizer && this.resizeActivityStartedAt) {
      const after = this.activeResizer === 'hybrid-vert'
        ? this.hybridDocsHeightPct()
        : this.activeResizer === 'left' ? this.leftWidth() : this.rightWidth();
      this.logActivity(
        'PanelResized',
        `${this.resizeActivityLabel}: ${this.resizeActivityBefore.toFixed(1)}% -> ${after.toFixed(1)}%`,
        this.resizeActivityStartedAt,
        new Date()
      );
    }
    this.activeResizer = null;
    this.resizeActivityStartedAt = null;
  }

  // ── Activity log ──────────────────────────────────────────────────────────
  // Applies to every mode/participant, not just Hybrid — see session.service.ts's
  // recordActivity() and the backend's IActivityLogService (local-dev-only, best-effort).

  /** No-ops without an active review session. */
  private logActivity(eventType: string, detail?: string | null, startedAt?: Date, endedAt?: Date): void {
    const id = this.sessionId();
    if (!id) return;
    this.sessionService.recordActivity(id, { eventType, detail, startedAt, endedAt });
  }

  /** Catch-all: literally every click while a session is active, generically described — the
   *  exhaustive net underneath the richer, specifically-instrumented events elsewhere in this
   *  file (chat messages, doc search, section engagement, decisions, resizes, ...), which carry
   *  actual content a generic click description can't. Some overlap between the two is expected. */
  @HostListener('document:click', ['$event'])
  onGlobalClick(event: MouseEvent): void {
    if (!this.sessionId()) return;
    const target = event.target as HTMLElement | null;
    const el = target?.closest<HTMLElement>('button, a, [role="button"], input, select, textarea');
    const label = el?.getAttribute('aria-label') || el?.getAttribute('title') || el?.textContent?.trim().slice(0, 60) || '';
    const tag = (el ?? target)?.tagName.toLowerCase() ?? 'unknown';
    this.logActivity('Click', label ? `${tag}: ${label}` : tag);
  }

  /** Every key press while a session is active — what the participant types in the AI chat, the
   *  documentation search, the decision comment, etc. — one row per key, tagged with the field it
   *  went to. Rows reach the backend as independent fire-and-forget requests, so reconstruct the
   *  exact sequence from StartedAt, not from row order in the file. */
  @HostListener('document:keydown', ['$event'])
  onGlobalKeydown(event: KeyboardEvent): void {
    if (!this.sessionId()) return;
    const key = event.key === ' ' ? 'Space' : event.key;
    // AltGr (e.g. typing @ { } on a European layout) reports Ctrl+Alt too — don't log that as a chord.
    const altGr = event.getModifierState?.('AltGraph') ?? false;
    const modifiers = altGr ? [] : [
      event.ctrlKey && key !== 'Control' ? 'Ctrl' : '',
      event.altKey && key !== 'Alt' ? 'Alt' : '',
      event.metaKey && key !== 'Meta' ? 'Meta' : ''
    ].filter(Boolean);
    this.logActivity('KeyPress', `${[...modifiers, key].join('+')} in ${this.describeKeyTarget(event.target)}`);
  }

  /** "chat/textarea", "report-view/input", ... — nearest app-* component plus the element itself. */
  private describeKeyTarget(target: EventTarget | null): string {
    let el = target as HTMLElement | null;
    const field = el?.tagName?.toLowerCase() ?? 'page';
    while (el && !el.tagName.toLowerCase().startsWith('app-')) el = el.parentElement;
    return el ? `${el.tagName.toLowerCase().slice(4)}/${field}` : field;
  }

  /** Safety net alongside the DeleteSession-triggered "SessionEnded" row — covers an abrupt tab
   *  close that might race it. Uses the same keepalive-fetch path (see session.service.ts). */
  @HostListener('window:beforeunload')
  onBeforeUnload(): void {
    this.logActivity('TabClosed');
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
    this.hybridChatOpen.set(false);

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
      next: () => { this.reviewMode.set(mode); this.logActivity('ModeSwitched', mode); },
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
              // Gates "Dalje" until a real reply has actually streamed back — chatRef itself
              // enforces the 1-message cap (maxMessages, bound in the template only for Intro),
              // so this only needs to confirm that one exchange has genuinely completed.
              canAdvance: () => !!this.chatRef && this.chatRef.userMessageCount() >= 1 && !this.chatRef.streaming(),
            },
          ]
        : [
            { target: '.chat-panel', placement: 'left' as const, title: tt.reportTitle, body: tt.reportBody, beforeShow: () => this.switchToMode(mode) },
            {
              // Spotlights the whole documentation panel (not just the search box) and makes it
              // genuinely interactive — the participant can actually scroll/read, expand other
              // sections, and try the search, at their own pace, not just look at it.
              target: '.chat-panel',
              placement: 'left' as const,
              title: tt.searchTitle,
              body: tt.searchBody,
              interactive: true,
              beforeShow: () => this.switchToMode(mode),
            },
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
        // Opens the modal directly (not openFinishModal()) — this is the tour, not the participant
        // clicking the decision button, so it must not log an activity row or fire an EEG marker.
        beforeShow: () => this.showFinishModal.set(true),
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
    this.logActivity('QuoteToChat', `${quoted.fileName}:${quoted.startLine}-${quoted.endLine}`);
  }

  /** Hybrid mode only — a clicked "Više informacija: <section>" link in the chat drawer expands
   *  and scrolls to that section in the documentation pane above it. */
  onHybridSectionLinkClick(sectionId: string): void {
    this.hybridReportRef?.expandAndScrollTo(sectionId);
    this.logActivity('HybridSectionLinkClicked', sectionId);
  }

  onFileSelected(fileName: string | null): void {
    this.selectedFileName.set(fileName);
    this.showDescription.set(false);
    if (!fileName) {
      this.fileListCollapsed.set(false);
      this.diffCollapsed.set(false);
      this.chatCollapsed.set(false);
    }
    this.logActivity('FileSelected', fileName ?? '(deselected)');
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
    this.logActivity('DescriptionToggled', next ? 'opened' : 'closed');
  }

  openFinishModal(): void {
    this.showFinishModal.set(true);
    this.logActivity('FinishModalOpened');
  }

  onModalClosed(): void {
    this.showFinishModal.set(false);
    this.logActivity('FinishModalClosed');
  }

  onDecisionSubmitted(decision: ReviewDecision): void {
    this.finishAndHandoff(decision.handoffToken ?? null);
  }

  /**
   * Fired by <app-timer-display> exactly once, on expiry. Bypasses FinishReviewModalComponent
   * entirely (its own canSubmit gate requires a non-empty comment the participant may never have
   * typed) and submits directly with a fixed placeholder comment plus the dedicated `TimedOut`
   * decision outcome — never Accepted/Rejected, since the reviewer never actually made that
   * choice and fabricating one would corrupt the study's decision data.
   */
  onTimerExpired(): void {
    if (this.reviewFinished()) return; // already decided (manually or by a prior expiry) — no-op
    this.reviewFinished.set(true);
    this.logActivity('TimerExpired');

    const id = this.sessionId();
    if (!id) return; // nothing to submit against (shouldn't happen — the timer only renders once a session exists)

    this.sessionService
      .submitDecision(id, { decision: ReviewDecisionType.TimedOut, comment: this.t().studyTimerExpiredComment })
      .subscribe({
        next: result => this.finishAndHandoff(result.handoffToken ?? null),
        error: err => {
          console.error('[Timer] auto-submit on expiry failed:', err);
          // Still hands off — an unrecorded decision shouldn't strand the participant on a
          // screen with an expired timer and nothing they can do; matches this method's own
          // deleteSession error path just below, which redirects regardless of outcome. No
          // handoff token was minted (the submit itself failed) — finishAndHandoff falls back
          // to the legacy shape for a test participant, or back to the participant's own
          // personal link for a real one (see its own doc comment).
          this.finishAndHandoff(null);
        }
      });
  }

  /**
   * Hands off to the NASA-TLX workload-assessment app once the reviewer's decision is recorded.
   * A real (non-test) participant carries an opaque `handoffToken` (minted server-side on decision
   * submit) instead of their raw participant id/session number in the URL — NASA-TLX's own
   * `/start` only accepts that token now, not a bare id, since it's otherwise reachable by anyone
   * who knows/guesses a participant id. A test participant has no personal link at all, so it
   * keeps the legacy query-param shape unchanged. In the rare case a real participant's handoff
   * token mint failed (the decision-submit request itself errored), there's no safe NASA-TLX URL
   * to build — send them back to their own personal Code Review link instead of a dead-end
   * NASA-TLX redirect; the study session isn't marked finished by this app either way, so
   * re-entering is harmless and they can try again.
   */
  private finishAndHandoff(handoffToken: string | null): void {
    this.showFinishModal.set(false);
    this.reviewFinished.set(true);
    // The Intro tour's last step only ends here, on a real submit — see buildIntroTourSteps.
    if (this.tourService.active()) this.tourService.end();
    const study = this.studyState.state();
    const redirect = () => {
      if (study?.isTestParticipant) {
        const params = new URLSearchParams({
          participantId: study.participantId,
          sessionId: String(study.sessionId),
          lang: study.lang,
          theme: this.themeService.theme()
        });
        window.location.href = `${environment.nasaTlxStartUrl}?${params}`;
      } else if (handoffToken) {
        const params = new URLSearchParams({ h: handoffToken, theme: this.themeService.theme() });
        window.location.href = `${environment.nasaTlxStartUrl}?${params}`;
      } else if (study?.linkToken) {
        window.location.href = `${window.location.origin}${window.location.pathname}?link=${study.linkToken}`;
      } else {
        window.location.href = window.location.origin;
      }
    };
    // Navigate only after the DELETE settles — a synchronous redirect would abort the in-flight
    // request and leave the session (holding the PAT) alive on the server until the cleanup timeout.
    const id = this.sessionId();
    if (!id) {
      redirect();
      return;
    }
    this.sessionService.deleteSession(id).subscribe({ complete: redirect, error: redirect });
  }
}
