import { Component, computed, inject, input } from '@angular/core';
import { I18nService } from '../core/services/i18n.service';
import { ThemeService } from '../core/services/theme.service';

/**
 * Shared top nav bar — structurally uniform with the sibling study apps' own
 * `app-global-header` (NASA-TLX, REI-40, Big Five, Admin Dashboard): same brand-logo-wrap box,
 * same .header-brand/.header-controls/.toggle-group markup and class names, same dormant
 * (commented-out) language toggle now that language is locked at login everywhere. Keeps
 * BeyondAI's own CSS custom property names/palette (--bg-header, --header-text, --header-ctrl-*)
 * since each app legitimately keeps its own brand colors — "uniform" here means structure and
 * behavior, not identical hex values. The one BeyondAI-specific addition is the PR-title
 * breadcrumb, shown only once a review session is active.
 */
@Component({
  selector: 'app-global-header',
  standalone: true,
  imports: [],
  template: `
    <header class="global-header">
      <div class="header-brand">
        <div class="brand-logo-wrap">
          <img [src]="logoSrc()" alt="BeyondAI logo" class="brand-logo" aria-hidden="true" />
        </div>
        <span class="brand-name">BeyondAI Research Group</span>
        @if (prTitle()) {
          <span class="header-divider" aria-hidden="true"></span>
          <span class="pr-title">{{ prTitle() }}</span>
        }
      </div>
      <div class="header-controls">
        <div class="toggle-group" [attr.aria-label]="'Tema'">
          <button
            class="toggle-group__btn"
            type="button"
            [class.toggle-group__btn--active]="themeService.theme() === 'dark'"
            (click)="setTheme('dark')"
            title="Tamna tema / Dark theme"
          >☾</button>
          <button
            class="toggle-group__btn"
            type="button"
            [class.toggle-group__btn--active]="themeService.theme() === 'light'"
            (click)="setTheme('light')"
            title="Svetla tema / Light theme"
          >☀</button>
        </div>

        <!-- Language toggle disabled: the language is chosen once on the login page and locked.
             Re-enable by uncommenting if needed.
        <div class="toggle-group" [attr.aria-label]="'Jezik / Language'">
          <button
            class="toggle-group__btn"
            type="button"
            [class.toggle-group__btn--active]="i18n.lang() === 'sr'"
            (click)="setLang('sr')"
          >SR</button>
          <button
            class="toggle-group__btn"
            type="button"
            [class.toggle-group__btn--active]="i18n.lang() === 'en'"
            (click)="setLang('en')"
          >EN</button>
        </div>
        -->
      </div>
    </header>
  `,
  styles: [`
    .global-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 0.75rem 1.25rem;
      background: var(--bg-header);
      flex-shrink: 0;
      border-bottom: 1px solid var(--border);
      gap: 1rem;
    }

    .header-brand {
      display: flex;
      align-items: center;
      gap: 10px;
      min-width: 0;
      flex: 1;
    }

    .brand-logo-wrap {
      width: 44px;
      height: 44px;
      border-radius: 8px;
      overflow: hidden;
      flex-shrink: 0;
      display: flex;
      align-items: center;
      justify-content: center;
      background: var(--bg-header);
    }

    .brand-logo {
      width: 44px;
      height: 44px;
      object-fit: contain;
      display: block;
    }

    .brand-name {
      font-size: 16px;
      font-weight: 600;
      color: var(--header-text);
      letter-spacing: 0.02em;
      white-space: nowrap;
      flex-shrink: 0;
    }

    .header-divider {
      width: 1px;
      height: 20px;
      background: var(--border);
      flex-shrink: 0;
    }

    .pr-title {
      font-size: 0.9375rem;
      font-weight: 500;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
      color: var(--text-muted);
      min-width: 0;
    }

    .header-controls {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      flex-shrink: 0;
    }

    .toggle-group {
      display: flex;
      border: 1px solid var(--header-ctrl-border);
      border-radius: 6px;
      overflow: hidden;
    }

    .toggle-group__btn {
      padding: 0.3rem 0.65rem;
      background: transparent;
      color: var(--header-ctrl-color);
      border: none;
      font-size: 0.8rem;
      cursor: pointer;
      transition: background 0.15s, color 0.15s;
      line-height: 1.4;

      &:not(:last-child) {
        border-right: 1px solid var(--header-ctrl-border);
      }

      &--active {
        background: var(--header-ctrl-active-bg);
        color: var(--header-ctrl-active-color);
        font-weight: 600;
        cursor: default;
      }

      &:not(.toggle-group__btn--active):hover {
        color: var(--header-ctrl-hover-color);
      }
    }
  `],
})
export class GlobalHeaderComponent {
  readonly i18n = inject(I18nService);
  readonly themeService = inject(ThemeService);

  /** Breadcrumb shown next to the brand once a review session is active — null/absent elsewhere. */
  readonly prTitle = input<string | null>(null);

  readonly logoSrc = computed(() =>
    this.themeService.theme() === 'light'
      ? 'assets/beyondai-favicon-light.svg'
      : 'assets/beyondai-favicon.svg'
  );

  setTheme(theme: 'dark' | 'light'): void {
    if (this.themeService.theme() !== theme) this.themeService.toggle();
  }

  setLang(lang: 'sr' | 'en'): void {
    if (this.i18n.lang() !== lang) this.i18n.toggle();
  }
}
