import { Component, input, output, inject, signal, ViewChild, ElementRef, DestroyRef } from '@angular/core';
import { NgClass } from '@angular/common';
import { toObservable, takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { switchMap, catchError, EMPTY } from 'rxjs';
import { SessionService } from '../../core/services/session.service';
import { I18nService } from '../../core/services/i18n.service';

interface DiffLine {
  type: 'added' | 'removed' | 'hunk' | 'context';
  content: string;
  lineNumber: number | null;
}

export interface QuotedCode {
  fileName: string;
  startLine: number;
  endLine: number;
  code: string;
}

interface QuotePopup {
  top: number;
  left: number;
}

@Component({
  selector: 'app-diff-viewer',
  standalone: true,
  imports: [NgClass],
  templateUrl: './diff-viewer.component.html',
  styleUrl: './diff-viewer.component.scss'
})
export class DiffViewerComponent {
  readonly sessionId = input.required<string>();
  readonly fileName = input<string | null>(null);
  readonly quotingEnabled = input<boolean>(true);

  readonly quote = output<QuotedCode>();

  private readonly sessionService = inject(SessionService);
  private readonly destroyRef = inject(DestroyRef);
  readonly i18n = inject(I18nService);
  readonly t = this.i18n.t;

  readonly loading = signal(false);
  readonly lines = signal<DiffLine[]>([]);
  readonly errorMessage = signal<string | null>(null);
  readonly quotePopup = signal<QuotePopup | null>(null);

  @ViewChild('diffBody') private diffBodyRef?: ElementRef<HTMLElement>;
  private pendingQuote: QuotedCode | null = null;

  constructor() {
    toObservable(this.fileName)
      .pipe(
        switchMap(file => {
          if (!file) {
            this.lines.set([]);
            this.errorMessage.set(null);
            this.loading.set(false);
            return EMPTY;
          }
          this.loading.set(true);
          this.lines.set([]);
          this.errorMessage.set(null);
          return this.sessionService.getFilePatch(this.sessionId(), file).pipe(
            catchError(() => {
              this.loading.set(false);
              this.errorMessage.set(this.t().diffError);
              return EMPTY;
            })
          );
        }),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe(patch => {
        this.loading.set(false);
        if (!patch) {
          this.errorMessage.set(this.t().diffUnavailable);
        } else {
          this.lines.set(this.parsePatch(patch));
        }
      });
  }

  private parsePatch(patch: string): DiffLine[] {
    let oldLine = 0;
    let newLine = 0;

    return patch.split('\n').map(content => {
      const hunkMatch = content.match(/^@@ -(\d+)(?:,\d+)? \+(\d+)(?:,\d+)? @@/);
      if (hunkMatch) {
        oldLine = Number(hunkMatch[1]) - 1;
        newLine = Number(hunkMatch[2]) - 1;
        return { type: 'hunk' as const, content, lineNumber: null };
      }
      // "\ No newline at end of file" is a marker, not file content — it must not
      // advance either counter or every line number after it shifts by one.
      if (content.startsWith('\\')) {
        return { type: 'context' as const, content, lineNumber: null };
      }
      if (content.startsWith('+')) {
        newLine += 1;
        return { type: 'added' as const, content, lineNumber: newLine };
      }
      if (content.startsWith('-')) {
        oldLine += 1;
        return { type: 'removed' as const, content, lineNumber: oldLine };
      }
      oldLine += 1;
      newLine += 1;
      return { type: 'context' as const, content, lineNumber: newLine };
    });
  }

  lineClass(type: DiffLine['type']): string {
    return `line line--${type}`;
  }

  onSelectionMouseUp(): void {
    if (!this.quotingEnabled()) return;

    const container = this.diffBodyRef?.nativeElement;
    const selection = window.getSelection();

    if (!container || !selection || selection.isCollapsed || !selection.toString().trim()) {
      this.quotePopup.set(null);
      this.pendingQuote = null;
      return;
    }
    if (!container.contains(selection.anchorNode) || !container.contains(selection.focusNode)) {
      this.quotePopup.set(null);
      this.pendingQuote = null;
      return;
    }

    const range = selection.getRangeAt(0);
    const lineEls = Array.from(container.querySelectorAll<HTMLElement>('.line'));
    const spannedLines = lineEls
      .filter(el => range.intersectsNode(el))
      .map(el => this.lines()[Number(el.dataset['index'])])
      .filter((line): line is DiffLine => !!line && line.lineNumber !== null);

    if (spannedLines.length === 0) {
      this.quotePopup.set(null);
      this.pendingQuote = null;
      return;
    }

    // Removed lines carry OLD-file numbers; prefer added/context lines (new-file
    // numbering) for the quoted range so `file:start-end` points at the head branch.
    // A selection of removed lines only falls back to the old numbering.
    const rangeSource = spannedLines.some(line => line.type !== 'removed')
      ? spannedLines.filter(line => line.type !== 'removed')
      : spannedLines;
    const startLine = rangeSource[0].lineNumber!;
    const endLine = rangeSource[rangeSource.length - 1].lineNumber!;
    const code = spannedLines.map(line => line.content.replace(/^[+\- ]/, '')).join('\n');

    this.pendingQuote = { fileName: this.fileName()!, startLine, endLine, code };

    const rect = range.getBoundingClientRect();
    this.quotePopup.set({ top: rect.top - 38, left: rect.left });
  }

  onDiffScroll(): void {
    // The popup is positioned with fixed viewport coordinates captured at selection
    // time — scrolling the diff would leave it floating over the wrong line.
    if (this.quotePopup()) {
      this.quotePopup.set(null);
      this.pendingQuote = null;
    }
  }

  confirmQuote(): void {
    if (this.pendingQuote) {
      this.quote.emit(this.pendingQuote);
    }
    this.quotePopup.set(null);
    this.pendingQuote = null;
    window.getSelection()?.removeAllRanges();
  }
}
