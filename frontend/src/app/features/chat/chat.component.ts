import {
  Component,
  input,
  signal,
  computed,
  ViewChild,
  ElementRef,
  AfterViewChecked,
  OnInit,
  inject,
  Output,
  EventEmitter
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MarkdownModule } from 'ngx-markdown';
import { SessionService } from '../../core/services/session.service';
import { I18nService } from '../../core/services/i18n.service';
import { ChatMessage } from '../../core/models/chat-message.model';
import { QuotedCode } from '../diff-viewer/diff-viewer.component';

const EXTENSION_TO_LANGUAGE: Record<string, string> = {
  cs: 'csharp', ts: 'typescript', html: 'html', scss: 'scss', css: 'css',
  json: 'json', js: 'javascript', sql: 'sql', sol: 'solidity', md: 'markdown'
};

@Component({
  selector: 'app-chat',
  standalone: true,
  imports: [FormsModule, MarkdownModule],
  templateUrl: './chat.component.html',
  styleUrl: './chat.component.scss'
})
export class ChatComponent implements OnInit, AfterViewChecked {
  readonly sessionId = input.required<string>();

  /**
   * Hybrid mode only — the AI's replies can include a `[label](#section-N)` link pointing back
   * into the documentation pane. The backend only ever produces such links for Hybrid sessions,
   * so this is a harmless no-op for AI/Report mode's own chat instance (no such link ever appears
   * there); no mode flag is needed on this component.
   */
  @Output() sectionLinkClicked = new EventEmitter<string>();

  /** Mirrors the backend's MaxChatMessageLength — messages longer than this are rejected with 400. */
  readonly maxMessageLength = 8000;

  /**
   * Intro's guided-tour "ask one question" step only — caps how many messages the participant may
   * send in this chat instance (backend enforces the same cap for session 1 — see ChatStream's own
   * guard). Null (default) means no cap, unchanged behavior for the real AI/Report chat.
   */
  readonly maxMessages = input<number | null>(null);
  readonly userMessageCount = computed(() => this.messages().filter(m => m.role === 'user').length);
  readonly limitReached = computed(() => {
    const max = this.maxMessages();
    return max !== null && this.userMessageCount() >= max;
  });

  private readonly sessionService = inject(SessionService);
  readonly i18n = inject(I18nService);
  readonly t = this.i18n.t;

  readonly messages = signal<ChatMessage[]>([]);
  readonly inputText = signal('');
  readonly streaming = signal(false);
  readonly suggestions = signal<string[]>([]);
  readonly loadingSuggestions = signal(false);

  // Length guard matters for programmatic inserts (quote-to-chat) — the textarea's
  // maxlength attribute only constrains typing, not values set from code.
  readonly sendDisabled = computed(() =>
    this.streaming() ||
    this.limitReached() ||
    this.inputText().trim().length === 0 ||
    this.inputText().length > this.maxMessageLength
  );

  @ViewChild('messagesEnd') private messagesEnd!: ElementRef<HTMLDivElement>;
  @ViewChild('messageInput') private messageInput?: ElementRef<HTMLTextAreaElement>;

  private shouldScrollToBottom = false;

  ngOnInit(): void {
    this.suggestions.set(this.t().chips.slice(0, 4));
  }

  ngAfterViewChecked(): void {
    if (this.shouldScrollToBottom) {
      this.messagesEnd?.nativeElement.scrollIntoView({ behavior: 'smooth' });
      this.shouldScrollToBottom = false;
    }
  }

  /** Intercepts clicks on a documentation-section link ([label](#section-N)) in a rendered AI
   *  reply — prevents the dead in-page anchor jump and emits the section id instead, so the
   *  parent can expand/scroll to it in the (separately rendered) documentation pane. */
  onMessagesAreaClick(event: MouseEvent): void {
    const anchor = (event.target as HTMLElement).closest('a');
    const href = anchor?.getAttribute('href') ?? '';
    const match = href.match(/^#(section-\d+)$/);
    if (match) {
      event.preventDefault();
      this.sectionLinkClicked.emit(match[1]);
    }
  }

  insertQuote(quoted: QuotedCode): void {
    const lang = EXTENSION_TO_LANGUAGE[quoted.fileName.split('.').pop()?.toLowerCase() ?? ''] ?? '';
    const header = quoted.startLine === quoted.endLine
      ? `${quoted.fileName}:${quoted.startLine}`
      : `${quoted.fileName}:${quoted.startLine}-${quoted.endLine}`;
    const block = `\`${header}\`\n\`\`\`${lang}\n${quoted.code}\n\`\`\`\n`;

    this.inputText.update(current => current ? `${current}\n${block}` : block);
    this.messageInput?.nativeElement.focus();
  }

  sendChip(chip: string): void {
    if (this.streaming() || this.limitReached()) return;
    this.inputText.set(chip);
    this.send();
  }

  send(): void {
    if (this.sendDisabled()) return;
    const text = this.inputText().trim();

    const userMsg: ChatMessage = { role: 'user', content: text, timestamp: new Date() };
    this.messages.update(msgs => [...msgs, userMsg]);
    this.inputText.set('');
    this.shouldScrollToBottom = true;

    const aiMsg: ChatMessage = { role: 'assistant', content: '', timestamp: new Date() };
    this.messages.update(msgs => [...msgs, aiMsg]);

    this.streaming.set(true);

    this.sessionService.streamChat(this.sessionId(), text).subscribe({
      next: chunk => {
        this.messages.update(msgs => {
          const copy = [...msgs];
          const last = copy[copy.length - 1];
          copy[copy.length - 1] = { ...last, content: last.content + chunk };
          return copy;
        });
        this.shouldScrollToBottom = true;
      },
      error: () => {
        this.streaming.set(false);
        this.messages.update(msgs => {
          const copy = [...msgs];
          const last = copy[copy.length - 1];
          copy[copy.length - 1] = {
            ...last,
            content: last.content || this.t().aiError
          };
          return copy;
        });
      },
      complete: () => {
        this.streaming.set(false);
        this.shouldScrollToBottom = true;
        this.refreshSuggestions();
      }
    });
  }

  onEnter(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.send();
    }
  }

  private refreshSuggestions(): void {
    if (this.loadingSuggestions()) return;
    this.loadingSuggestions.set(true);
    this.sessionService.getSuggestions(this.sessionId()).subscribe({
      next: items => {
        if (items.length > 0) this.suggestions.set(items);
      },
      complete: () => this.loadingSuggestions.set(false),
      error: () => this.loadingSuggestions.set(false)
    });
  }
}
