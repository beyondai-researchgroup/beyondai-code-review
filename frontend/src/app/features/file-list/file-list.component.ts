import { Component, input, output, signal, inject, effect } from '@angular/core';
import { NgClass } from '@angular/common';
import { PrFile } from '../../core/models/pr-file.model';
import { I18nService } from '../../core/services/i18n.service';
import { SessionService } from '../../core/services/session.service';

type RepoCtxState = 'idle' | 'loading' | 'loaded' | 'error';

@Component({
  selector: 'app-file-list',
  standalone: true,
  imports: [NgClass],
  templateUrl: './file-list.component.html',
  styleUrl: './file-list.component.scss'
})
export class FileListComponent {
  readonly files = input.required<PrFile[]>();
  readonly sessionId = input.required<string>();
  readonly shortSummary = input<string | null>(null);
  readonly descriptionActive = input<boolean>(false);
  readonly fileSelected = output<string | null>();
  readonly descriptionClicked = output<void>();
  readonly finishClicked = output<void>();

  readonly i18n = inject(I18nService);
  private readonly sessionService = inject(SessionService);
  readonly t = this.i18n.t;

  readonly selectedFileName = signal<string | null>(null);
  readonly repoCtxState = signal<RepoCtxState>('idle');

  constructor() {
    effect(() => {
      const sid = this.sessionId();
      if (!sid || this.repoCtxState() !== 'idle') return;
      this.repoCtxState.set('loading');
      this.sessionService.loadRepoContext(sid).subscribe({
        next: () => this.repoCtxState.set('loaded'),
        error: () => this.repoCtxState.set('error')
      });
    }, { allowSignalWrites: true });
  }

  selectFile(fileName: string): void {
    const next = this.selectedFileName() === fileName ? null : fileName;
    this.selectedFileName.set(next);
    this.fileSelected.emit(next);
  }

  badgeClass(status: string): string {
    switch (status.toLowerCase()) {
      case 'added':   return 'badge badge--added';
      case 'removed': return 'badge badge--removed';
      default:        return 'badge badge--modified';
    }
  }

  badgeLabel(status: string): string {
    switch (status.toLowerCase()) {
      case 'added':   return this.t().statusAdded;
      case 'removed': return this.t().statusRemoved;
      default:        return this.t().statusModified;
    }
  }
}
