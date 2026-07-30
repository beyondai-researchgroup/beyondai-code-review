import { Injectable, signal, computed } from '@angular/core';

export interface TourStep {
  /** CSS selector of the element to spotlight, or null for a centered card with no target. */
  target: string | null;
  title: string;
  body: string;
  placement?: 'top' | 'bottom' | 'left' | 'right' | 'center';
  /**
   * When true, the spotlighted target stays genuinely clickable instead of being covered by
   * the overlay — used for steps where the participant does something for real (e.g. typing
   * and sending an actual chat question) rather than just reading about it.
   */
  interactive?: boolean;
  /**
   * When true, the tooltip's own Skip/Back/Done buttons are hidden — the step can only be left
   * by code calling TourService.end()/next() from a real app event (e.g. an actual form submit),
   * not by the participant bailing out of the tour. Use for steps where the underlying action is
   * mandatory and must not be skippable.
   */
  hideNav?: boolean;
  /** Runs just before the step is shown — e.g. select a file, expand a panel, open a modal. */
  beforeShow?: () => void;
  /** Runs when leaving this step (forward, back, skip, or on tour end while on this step). */
  afterLeave?: () => void;
}

/**
 * Drives the guided-tour overlay (spotlight + tooltip) used by the Intro study session so a
 * participant can self-serve learn the review UI instead of a researcher narrating it live.
 * Generic — any feature could reuse it by calling start() with its own step list.
 */
@Injectable({ providedIn: 'root' })
export class TourService {
  private readonly _steps = signal<TourStep[]>([]);
  private readonly _index = signal(0);
  private readonly _active = signal(false);

  readonly steps = this._steps.asReadonly();
  readonly index = this._index.asReadonly();
  readonly active = this._active.asReadonly();

  readonly currentStep = computed<TourStep | null>(() => this._steps()[this._index()] ?? null);
  readonly isFirst = computed(() => this._index() === 0);
  readonly isLast = computed(() => this._index() === this._steps().length - 1);

  start(steps: TourStep[]): void {
    if (steps.length === 0) return;
    this._steps.set(steps);
    this._index.set(0);
    this._active.set(true);
    steps[0].beforeShow?.();
  }

  next(): void {
    const steps = this._steps();
    const i = this._index();
    if (i >= steps.length - 1) {
      this.end();
      return;
    }
    steps[i].afterLeave?.();
    this._index.set(i + 1);
    steps[i + 1].beforeShow?.();
  }

  back(): void {
    const steps = this._steps();
    const i = this._index();
    if (i <= 0) return;
    steps[i].afterLeave?.();
    this._index.set(i - 1);
    steps[i - 1].beforeShow?.();
  }

  end(): void {
    const steps = this._steps();
    const i = this._index();
    steps[i]?.afterLeave?.();
    this._active.set(false);
    this._steps.set([]);
    this._index.set(0);
  }
}
