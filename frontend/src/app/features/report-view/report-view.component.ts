import { Component, input, signal, inject, effect, ViewChild, ElementRef, HostListener, OnDestroy } from '@angular/core';
import { MarkdownModule } from 'ngx-markdown';
import { Subscription } from 'rxjs';
import { SessionService } from '../../core/services/session.service';
import { I18nService } from '../../core/services/i18n.service';

/** One `##`-level documentation section, for Hybrid mode's accordion. */
interface ReportSection {
  id: string;
  title: string;
  bodyMarkdown: string;
}

@Component({
  selector: 'app-report-view',
  standalone: true,
  imports: [MarkdownModule],
  templateUrl: './report-view.component.html',
  styleUrl: './report-view.component.scss'
})
export class ReportViewComponent implements OnDestroy {
  readonly sessionId = input.required<string>();

  /**
   * Reorganizes the exact same content into collapsible, single-open accordion sections instead
   * of one scrollable block. Used by both Report and Hybrid modes; Report mode's own content and
   * length are completely unchanged, only the presentation is. Defaults to false so any future
   * unconfigured embed keeps the plain scrollable rendering.
   */
  readonly sectioned = input<boolean>(false);

  /**
   * Experimental Hybrid mode only (participant "004") — records expand/collapse engagement (and
   * dwell time) to the study database. Decoupled from `sectioned` because Report mode is also
   * sectioned now but must NEVER write to `HybridSectionEngagement`.
   */
  readonly trackEngagement = input<boolean>(false);

  private readonly sessionService = inject(SessionService);
  private readonly i18n = inject(I18nService);
  readonly t = this.i18n.t;

  readonly reportText = signal<string>('');
  readonly streaming = signal(true);
  readonly error = signal(false);

  readonly searchTerm = signal('');
  readonly matchCount = signal(0);
  readonly currentMatchIndex = signal(0);

  readonly preamble = signal<string>('');
  readonly sections = signal<ReportSection[]>([]);
  /** Single-open accordion — expanding a section auto-collapses whatever was open before. */
  readonly activeSectionId = signal<string | null>(null);

  /** ms epoch per currently-open section id — internal bookkeeping only, not reactive state. */
  private readonly sectionOpenedAt = new Map<string, number>();

  @ViewChild('content') private contentRef?: ElementRef<HTMLElement>;

  private activeSubscription?: Subscription;

  constructor() {
    // Re-fetches the report whenever the UI language changes, so an already-open
    // Report panel switches language without requiring the user to reload the PR.
    effect(() => {
      this.i18n.lang();
      this.stream(false);
    }, { allowSignalWrites: true });
  }

  ngOnDestroy(): void {
    this.flushOpenSections();
  }

  /** Best-effort — a section left open when the tab is closed mid-unload. */
  @HostListener('window:beforeunload')
  onBeforeUnload(): void {
    this.flushOpenSections();
  }

  /**
   * A backgrounded tab isn't a real "collapse" — flush what's elapsed so far as a Collapse event,
   * then restart the dwell clock for each still-open section, so idle-tab time isn't silently
   * folded into the next segment's duration when the participant comes back.
   */
  @HostListener('document:visibilitychange')
  onVisibilityChange(): void {
    if (document.hidden) this.flushOpenSections(true);
  }

  toggleSection(id: string): void {
    this.activeSectionId() === id ? this.collapseSection(id) : this.expandSection(id);
  }

  expandSection(id: string): void {
    if (this.activeSectionId() === id) return;
    // Single-open accordion — collapse whatever else is open first (recording its own dwell time)
    // before activating the new one.
    const previous = this.activeSectionId();
    if (previous) this.collapseSection(previous);
    this.activeSectionId.set(id);
    this.sectionOpenedAt.set(id, Date.now());
    this.emitSectionEvent(id, 'Expand');
  }

  private collapseSection(id: string): void {
    const openedAt = this.sectionOpenedAt.get(id);
    const durationSeconds = openedAt ? Math.round((Date.now() - openedAt) / 1000) : undefined;
    if (this.activeSectionId() === id) this.activeSectionId.set(null);
    this.sectionOpenedAt.delete(id);
    this.emitSectionEvent(id, 'Collapse', durationSeconds);
  }

  private emitSectionEvent(id: string, action: 'Expand' | 'Collapse', durationSeconds?: number): void {
    const title = this.sections().find(s => s.id === id)?.title ?? id;

    if (this.trackEngagement()) {
      // Hybrid mode only — the dedicated, research-specific DB table. The backend endpoint this
      // hits also mirrors the row into the general activity-log CSV, so no separate call is needed
      // here for Hybrid.
      this.sessionService.recordHybridSectionEvent(this.sessionId(), {
        sectionId: id,
        sectionTitle: title,
        action,
        durationSeconds
      });
      return;
    }

    // Report mode (sectioned, but never writes to the Hybrid-only HybridSectionEngagement table) —
    // still feed the general, session-wide activity log directly.
    const now = new Date();
    const startedAt = durationSeconds !== undefined ? new Date(now.getTime() - durationSeconds * 1000) : now;
    this.sessionService.recordActivity(this.sessionId(), {
      eventType: `Section${action}`,
      detail: title,
      startedAt,
      endedAt: now
    });
  }

  /** Public API for cross-component "open this section" requests (e.g. a clicked AI chat link) —
   *  reuses the same single-open expand path a manual click or search match takes, so it counts
   *  as real engagement and scrolls the section into view. No-ops for an unknown/stale id. */
  expandAndScrollTo(id: string): void {
    if (!this.sections().some(s => s.id === id)) return;
    this.expandSection(id);
    this.contentRef?.nativeElement
      .querySelector<HTMLElement>(`[data-section-id="${id}"]`)
      ?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }

  private flushOpenSections(keepOpen = false): void {
    for (const id of [...this.sectionOpenedAt.keys()]) {
      const openedAt = this.sectionOpenedAt.get(id)!;
      const durationSeconds = Math.round((Date.now() - openedAt) / 1000);
      this.emitSectionEvent(id, 'Collapse', durationSeconds);
      if (keepOpen) {
        this.sectionOpenedAt.set(id, Date.now());
      } else {
        this.sectionOpenedAt.delete(id);
        if (this.activeSectionId() === id) this.activeSectionId.set(null);
      }
    }
  }

  /** Splits the raw markdown on top-level `## ` headings into a preamble + accordion sections. */
  private splitIntoSections(raw: string): { preamble: string; sections: ReportSection[] } {
    const headingRe = /^##\s+.+$/gm;
    const matches = [...raw.matchAll(headingRe)];
    if (matches.length === 0) return { preamble: raw, sections: [] };

    const preamble = raw.slice(0, matches[0].index).trim();
    const sections = matches.map((m, i) => {
      const start = m.index!;
      const end = i + 1 < matches.length ? matches[i + 1].index! : raw.length;
      const title = m[0].replace(/^##\s+/, '').trim();
      const bodyMarkdown = raw.slice(start + m[0].length, end).trim();
      return { id: `section-${i + 1}`, title, bodyMarkdown };
    });
    return { preamble, sections };
  }

  retry(): void {
    this.stream(false);
  }

  onSearchInput(term: string): void {
    this.searchTerm.set(term);
    this.runSearch(term);
  }

  onSearchKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter') {
      event.preventDefault();
      event.shiftKey ? this.prevMatch() : this.nextMatch();
    } else if (event.key === 'Escape') {
      this.clearSearch();
    }
  }

  clearSearch(): void {
    this.searchTerm.set('');
    this.runSearch('');
  }

  nextMatch(): void {
    const total = this.matchCount();
    if (total === 0) return;
    this.setActiveMatch((this.currentMatchIndex() + 1) % total);
  }

  prevMatch(): void {
    const total = this.matchCount();
    if (total === 0) return;
    this.setActiveMatch((this.currentMatchIndex() - 1 + total) % total);
  }

  private lastLoggedSearchTerm = '';

  private runSearch(term: string): void {
    const container = this.contentRef?.nativeElement;
    if (!container) return;

    this.unwrapHighlights(container);

    const needle = term.trim().toLowerCase();
    if (!needle) {
      this.matchCount.set(0);
      this.currentMatchIndex.set(0);
      return;
    }

    if (needle !== this.lastLoggedSearchTerm) {
      this.lastLoggedSearchTerm = needle;
      this.sessionService.recordActivity(this.sessionId(), { eventType: 'DocSearch', detail: needle });
    }

    const walker = document.createTreeWalker(container, NodeFilter.SHOW_TEXT);
    const textNodes: Text[] = [];
    let node: Node | null;
    while ((node = walker.nextNode())) {
      textNodes.push(node as Text);
    }

    for (const textNode of textNodes) {
      const text = textNode.textContent ?? '';
      const lower = text.toLowerCase();
      if (!lower.includes(needle)) continue;

      const fragment = document.createDocumentFragment();
      let cursor = 0;
      let matchStart = lower.indexOf(needle, cursor);
      while (matchStart !== -1) {
        if (matchStart > cursor) {
          fragment.appendChild(document.createTextNode(text.slice(cursor, matchStart)));
        }
        const mark = document.createElement('mark');
        mark.className = 'search-hit';
        mark.textContent = text.slice(matchStart, matchStart + needle.length);
        fragment.appendChild(mark);
        cursor = matchStart + needle.length;
        matchStart = lower.indexOf(needle, cursor);
      }
      if (cursor < text.length) {
        fragment.appendChild(document.createTextNode(text.slice(cursor)));
      }
      textNode.replaceWith(fragment);
    }

    const matches = container.querySelectorAll<HTMLElement>('mark.search-hit');
    this.matchCount.set(matches.length);
    // currentMatchIndex must already be 0 before setActiveMatch(0) runs — it uses the PREVIOUS
    // index to find (and unhighlight) the old active match, so it can't be the same call that
    // both resets it and moves to it.
    this.currentMatchIndex.set(0);
    if (matches.length > 0) {
      // Routed through setActiveMatch (not just highlighting matches[0] directly) so the very
      // first match found while typing also auto-expands its section, exactly like stepping to
      // it with Enter/arrows already did — previously only those did, not the initial hit.
      this.setActiveMatch(0);
    }
  }

  private setActiveMatch(index: number): void {
    const container = this.contentRef?.nativeElement;
    if (!container) return;

    const matches = container.querySelectorAll<HTMLElement>('mark.search-hit');
    matches[this.currentMatchIndex()]?.classList.remove('search-hit--active');
    const match = matches[index];
    if (this.sectioned() && match) {
      // A match inside a collapsed section is still in the DOM (hidden via CSS, never removed),
      // so the search itself already found it — expand it before scrolling, exactly as a manual
      // click would, so this counts as real engagement too.
      const sectionEl = match.closest<HTMLElement>('.report-view__section');
      const sectionId = sectionEl?.dataset['sectionId'];
      if (sectionId) this.expandSection(sectionId);
    }
    match?.classList.add('search-hit--active');
    match?.scrollIntoView({ block: 'center', behavior: 'smooth' });
    this.currentMatchIndex.set(index);
  }

  private unwrapHighlights(container: HTMLElement): void {
    container.querySelectorAll('mark.search-hit').forEach(mark => {
      mark.replaceWith(document.createTextNode(mark.textContent ?? ''));
    });
    container.normalize();
  }

  private stream(regenerate: boolean): void {
    // A language switch mid-read is a genuine "close everything" boundary — flush whatever
    // dwell time had accumulated before resetting, same as a real collapse would.
    this.flushOpenSections();
    this.activeSubscription?.unsubscribe();
    this.streaming.set(true);
    this.error.set(false);
    this.reportText.set('');
    this.searchTerm.set('');
    this.matchCount.set(0);
    this.preamble.set('');
    this.sections.set([]);
    this.activeSectionId.set(null);

    this.activeSubscription = this.sessionService.generateReport(this.sessionId(), regenerate).subscribe({
      next: chunk => this.reportText.update(t => t + chunk),
      error: () => {
        this.error.set(true);
        this.streaming.set(false);
      },
      complete: () => {
        this.streaming.set(false);
        if (this.sectioned()) {
          const { preamble, sections } = this.splitIntoSections(this.reportText());
          this.preamble.set(preamble);
          this.sections.set(sections);
        }
      }
    });
  }
}
