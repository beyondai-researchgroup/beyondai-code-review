import {
  Component,
  input,
  signal,
  computed,
  ViewChild,
  ElementRef,
  AfterViewChecked,
  OnInit,
  inject
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

  /** Mirrors the backend's MaxChatMessageLength — messages longer than this are rejected with 400. */
  readonly maxMessageLength = 8000;

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
    if (this.streaming()) return;
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
