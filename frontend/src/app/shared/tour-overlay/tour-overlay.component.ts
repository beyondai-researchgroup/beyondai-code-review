import { Component, HostListener, effect, inject, signal } from '@angular/core';
import { NgStyle } from '@angular/common';
import { TourService } from '../../core/services/tour.service';
import { I18nService } from '../../core/services/i18n.service';

interface Rect {
  top: number;
  left: number;
  width: number;
  height: number;
}

/**
 * Renders the spotlight backdrop + tooltip for whatever step TourService currently points at.
 * `.tour-backdrop` is a real full-viewport div (always present while a step is active) that
 * blocks every click — a `clip-path` polygon punches an actual hole over the target rect, so
 * (unlike a box-shadow "hole", which only dims visually and is never hit-tested) the background
 * genuinely cannot be clicked through. `.tour-spotlight` sits on top of that hole purely to
 * control whether the target itself is clickable: pointer-events:auto for read-only steps,
 * pointer-events:none (letting the click fall through the hole to the real element) only when
 * the step is explicitly marked `interactive`.
 */
@Component({
  selector: 'app-tour-overlay',
  standalone: true,
  imports: [NgStyle],
  templateUrl: './tour-overlay.component.html',
  styleUrl: './tour-overlay.component.scss',
})
export class TourOverlayComponent {
  readonly tour = inject(TourService);
  readonly i18n = inject(I18nService);
  readonly t = this.i18n.t;

  private static readonly PAD = 8;
  private static readonly MAX_ATTEMPTS = 20;
  private static readonly RETRY_MS = 50;

  readonly rect = signal<Rect | null>(null);
  readonly stepIndex = () => this.tour.index() + 1;
  readonly stepCount = () => this.tour.steps().length;

  constructor() {
    effect(() => {
      // Re-run whenever the active step changes.
      const step = this.tour.currentStep();
      if (!step) {
        this.rect.set(null);
        return;
      }
      this.locate(step.target, 0);
    }, { allowSignalWrites: true });
  }

  @HostListener('window:resize')
  @HostListener('window:scroll')
  onViewportChange(): void {
    const step = this.tour.currentStep();
    if (step) this.locate(step.target, 0);
  }

  private locate(selector: string | null, attempt: number): void {
    if (!selector) {
      this.rect.set(null);
      return;
    }
    const el = document.querySelector(selector);
    if (!el) {
      if (attempt < TourOverlayComponent.MAX_ATTEMPTS) {
        setTimeout(() => this.locate(selector, attempt + 1), TourOverlayComponent.RETRY_MS);
      } else {
        // Selector never resolved (e.g. panel collapsed unexpectedly) — fall back to a
        // centered card rather than leaving a stale/wrong spotlight on screen.
        this.rect.set(null);
      }
      return;
    }
    const r = el.getBoundingClientRect();
    const pad = TourOverlayComponent.PAD;
    this.rect.set({ top: r.top - pad, left: r.left - pad, width: r.width + pad * 2, height: r.height + pad * 2 });
  }

  backdropClipPath(): string {
    const r = this.rect();
    if (!r) return 'none';
    const vw = window.innerWidth;
    const vh = window.innerHeight;
    const l = Math.max(0, r.left);
    const t = Math.max(0, r.top);
    const right = Math.min(vw, r.left + r.width);
    const bottom = Math.min(vh, r.top + r.height);
    // Outer ring (full viewport) + inner ring (the hole), joined so the `evenodd` fill rule
    // cancels out the inner rect: the region between the two rings stays opaque/hit-testable,
    // the hole itself becomes transparent and click-through.
    return `polygon(evenodd, 0px 0px, ${vw}px 0px, ${vw}px ${vh}px, 0px ${vh}px, 0px 0px, ${l}px ${t}px, ${l}px ${bottom}px, ${right}px ${bottom}px, ${right}px ${t}px, ${l}px ${t}px)`;
  }

  tooltipStyle(): Record<string, string> {
    const r = this.rect();
    const step = this.tour.currentStep();
    const placement = step?.placement ?? (r ? 'bottom' : 'center');
    if (!r || placement === 'center') {
      return { position: 'fixed', top: '50%', left: '50%', transform: 'translate(-50%, -50%)' };
    }
    // No CSS transforms here — every branch computes the final on-screen top/left directly
    // and clamps it against an estimated box size, so the tooltip never renders partly (or
    // fully) outside the viewport regardless of how close the target is to an edge.
    const gap = 16;
    const width = 360;
    const heightEstimate = 260;
    const vw = window.innerWidth;
    const vh = window.innerHeight;
    const maxLeft = Math.max(12, vw - width - 12);
    const maxTop = Math.max(12, vh - heightEstimate - 12);
    let top: number;
    let left: number;
    switch (placement) {
      case 'top':
        left = clamp(r.left + r.width / 2 - width / 2, 12, maxLeft);
        top = clamp(r.top - gap - heightEstimate, 12, maxTop);
        break;
      case 'left':
        left = clamp(r.left - gap - width, 12, maxLeft);
        top = clamp(r.top, 12, maxTop);
        break;
      case 'right':
        left = clamp(r.left + r.width + gap, 12, maxLeft);
        top = clamp(r.top, 12, maxTop);
        break;
      case 'bottom':
      default:
        left = clamp(r.left + r.width / 2 - width / 2, 12, maxLeft);
        top = clamp(r.top + r.height + gap, 12, maxTop);
        break;
    }
    return { position: 'fixed', left: `${left}px`, top: `${top}px`, width: `${width}px` };
  }

  next(): void { this.tour.next(); }
  back(): void { this.tour.back(); }
  skip(): void { this.tour.end(); }
}

function clamp(v: number, min: number, max: number): number {
  return Math.max(min, Math.min(v, max));
}
