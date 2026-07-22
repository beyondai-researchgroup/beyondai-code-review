import { Injectable, signal, computed } from '@angular/core';
import { translations, Lang } from '../i18n/translations';

@Injectable({ providedIn: 'root' })
export class I18nService {
  readonly lang = signal<Lang>(
    (localStorage.getItem('lang') as Lang | null) ?? 'sr'
  );

  readonly t = computed(() => translations[this.lang()]);

  toggle(): void {
    this.set(this.lang() === 'sr' ? 'en' : 'sr');
  }

  set(lang: Lang): void {
    this.lang.set(lang);
    localStorage.setItem('lang', lang);
  }
}
