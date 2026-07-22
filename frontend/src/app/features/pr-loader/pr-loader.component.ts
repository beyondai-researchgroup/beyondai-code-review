import { Component, output, signal, computed, inject } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { SessionService } from '../../core/services/session.service';
import { I18nService } from '../../core/services/i18n.service';
import { ThemeService } from '../../core/services/theme.service';
import { PrSummaryResponse } from '../../core/models/pr-summary.model';
import { ReviewMode } from '../../core/models/review-mode.model';

export { ReviewMode };

export interface PrLoadedEvent {
  sessionId: string;
  summary: PrSummaryResponse;
  reviewMode: ReviewMode;
}

@Component({
  selector: 'app-pr-loader',
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: './pr-loader.component.html',
  styleUrl: './pr-loader.component.scss'
})
export class PrLoaderComponent {
  readonly prLoaded = output<PrLoadedEvent>();

  private readonly fb = inject(FormBuilder);
  private readonly sessionService = inject(SessionService);
  readonly i18n = inject(I18nService);
  private readonly themeService = inject(ThemeService);
  readonly t = this.i18n.t;

  readonly loading = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly selectedMode = signal<ReviewMode>(ReviewMode.Ai);

  readonly ReviewMode = ReviewMode;

  readonly logoSrc = computed(() =>
    this.themeService.theme() === 'light'
      ? 'assets/beyondai-favicon-light.svg'
      : 'assets/beyondai-favicon.svg'
  );

  readonly form = this.fb.nonNullable.group({
    repoUrl: ['', [Validators.required, Validators.pattern(/^https:\/\/github\.com\/[^/]+\/[^/]+/)]],
    prNumber: [null as unknown as number, [Validators.required, Validators.min(1)]],
    token: ['', Validators.required]
  });

  submit(): void {
    if (this.form.invalid || this.loading()) return;

    const { repoUrl, prNumber, token } = this.form.getRawValue();
    const parsed = this.parseRepoUrl(repoUrl);
    if (!parsed) {
      this.errorMessage.set(this.t().invalidRepoUrl);
      return;
    }

    this.loading.set(true);
    this.errorMessage.set(null);

    const mode = this.selectedMode();
    let sessionId = '';
    this.sessionService.createSession().subscribe({
      next: id => {
        sessionId = id;
        this.sessionService.loadPr(id, token, parsed.owner, parsed.repo, prNumber, mode).subscribe({
          next: summary => {
            this.loading.set(false);
            this.prLoaded.emit({ sessionId, summary, reviewMode: mode });
          },
          error: err => {
            this.loading.set(false);
            this.errorMessage.set(this.extractError(err));
            this.sessionService.deleteSession(sessionId).subscribe();
          }
        });
      },
      error: err => {
        this.loading.set(false);
        this.errorMessage.set(this.extractError(err));
      }
    });
  }

  private parseRepoUrl(url: string): { owner: string; repo: string } | null {
    try {
      const u = new URL(url.trim());
      const parts = u.pathname.replace(/^\//, '').replace(/\/$/, '').split('/');
      if (parts.length < 2 || !parts[0] || !parts[1]) return null;
      return { owner: parts[0], repo: parts[1] };
    } catch {
      return null;
    }
  }

  private extractError(err: unknown): string {
    // HttpErrorResponse only *implements* the Error interface (no prototype link),
    // so it needs its own branch — the backend's message lives in the response body.
    if (err instanceof HttpErrorResponse) {
      const body = err.error as { detail?: string } | null;
      if (body?.detail) return body.detail;
      return this.t().genericError;
    }
    if (err instanceof Error) return err.message;
    return this.t().genericError;
  }
}
