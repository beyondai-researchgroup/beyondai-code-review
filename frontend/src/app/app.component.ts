import { Component, signal, computed, inject, HostListener, ViewChild } from '@angular/core';
import { PrLoadedEvent } from './features/pr-loader/pr-loader.component';
import { StudyLoginComponent } from './features/study-login/study-login.component';
import { ChatComponent } from './features/chat/chat.component';
import { FileListComponent } from './features/file-list/file-list.component';
import { DiffViewerComponent, QuotedCode } from './features/diff-viewer/diff-viewer.component';
import { PrDescriptionComponent } from './features/pr-description/pr-description.component';
import { ReportViewComponent } from './features/report-view/report-view.component';
import { FinishReviewModalComponent } from './features/finish-review-modal/finish-review-modal.component';
import { SessionService } from './core/services/session.service';
import { StudyStateService } from './core/services/study-state.service';
import { I18nService } from './core/services/i18n.service';
import { ThemeService } from './core/services/theme.service';
import { PrFile } from './core/models/pr-file.model';
import { ReviewMode } from './core/models/review-mode.model';
import { ReviewDecision } from './core/models/review-decision.model';
import { environment } from '../environments/environment';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [StudyLoginComponent, ChatComponent, FileListComponent, DiffViewerComponent, PrDescriptionComponent, ReportViewComponent, FinishReviewModalComponent],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})
export class AppComponent {
  private readonly sessionService = inject(SessionService);
  private readonly studyState = inject(StudyStateService);
  readonly i18n = inject(I18nService);
  readonly themeService = inject(ThemeService);
  readonly t = this.i18n.t;

  readonly ReviewMode = ReviewMode;

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
    // Hand off to the NASA-TLX workload-assessment app once the reviewer's decision is
    // recorded, passing the study context (participant, session, locked language) so
    // the TLX starts without its own login. Navigate only after the DELETE settles —
    // a synchronous redirect would abort the in-flight request and leave the session
    // (holding the PAT) alive on the server until the cleanup timeout.
    const study = this.studyState.state();
    const params = new URLSearchParams({
      participantId: study?.participantId ?? '',
      sessionId: String(study?.sessionId ?? ''),
      lang: study?.lang ?? this.i18n.lang()
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
