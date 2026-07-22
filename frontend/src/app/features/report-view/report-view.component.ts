import { Component, input, signal, inject, effect, ViewChild, ElementRef } from '@angular/core';
import { MarkdownModule } from 'ngx-markdown';
import { Subscription } from 'rxjs';
import { SessionService } from '../../core/services/session.service';
import { I18nService } from '../../core/services/i18n.service';

@Component({
  selector: 'app-report-view',
  standalone: true,
  imports: [MarkdownModule],
  templateUrl: './report-view.component.html',
  styleUrl: './report-view.component.scss'
})
export class ReportViewComponent {
  readonly sessionId = input.required<string>();

  private readonly sessionService = inject(SessionService);
  private readonly i18n = inject(I18nService);
  readonly t = this.i18n.t;

  readonly reportText = signal<string>('');
  readonly streaming = signal(true);
  readonly error = signal(false);

  readonly searchTerm = signal('');
  readonly matchCount = signal(0);
  readonly currentMatchIndex = signal(0);

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
    this.currentMatchIndex.set(0);
    if (matches.length > 0) {
      matches[0].classList.add('search-hit--active');
      matches[0].scrollIntoView({ block: 'center', behavior: 'smooth' });
    }
  }

  private setActiveMatch(index: number): void {
    const container = this.contentRef?.nativeElement;
    if (!container) return;

    const matches = container.querySelectorAll<HTMLElement>('mark.search-hit');
    matches[this.currentMatchIndex()]?.classList.remove('search-hit--active');
    matches[index]?.classList.add('search-hit--active');
    matches[index]?.scrollIntoView({ block: 'center', behavior: 'smooth' });
    this.currentMatchIndex.set(index);
  }

  private unwrapHighlights(container: HTMLElement): void {
    container.querySelectorAll('mark.search-hit').forEach(mark => {
      mark.replaceWith(document.createTextNode(mark.textContent ?? ''));
    });
    container.normalize();
  }

  private stream(regenerate: boolean): void {
    this.activeSubscription?.unsubscribe();
    this.streaming.set(true);
    this.error.set(false);
    this.reportText.set('');
    this.searchTerm.set('');
    this.matchCount.set(0);

    this.activeSubscription = this.sessionService.generateReport(this.sessionId(), regenerate).subscribe({
      next: chunk => this.reportText.update(t => t + chunk),
      error: () => {
        this.error.set(true);
        this.streaming.set(false);
      },
      complete: () => this.streaming.set(false)
    });
  }
}
