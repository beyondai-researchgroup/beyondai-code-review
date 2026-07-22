import { Component, input, inject } from '@angular/core';
import { MarkdownModule } from 'ngx-markdown';
import { I18nService } from '../../core/services/i18n.service';

@Component({
  selector: 'app-pr-description',
  standalone: true,
  imports: [MarkdownModule],
  templateUrl: './pr-description.component.html',
  styleUrl: './pr-description.component.scss'
})
export class PrDescriptionComponent {
  readonly title       = input.required<string>();
  readonly author      = input.required<string>();
  readonly headBranch  = input.required<string>();
  readonly baseBranch  = input.required<string>();
  readonly description = input<string | null>(null);

  readonly i18n = inject(I18nService);
  readonly t = this.i18n.t;
}
