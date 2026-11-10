import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { EstimateStatus } from '../core/models';

@Component({
  selector: 'app-status-chip',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<span class="status-chip" [class]="'status-' + status().toLowerCase()">{{ status() }}</span>`,
  styles: `
    .status-chip { display: inline-block; padding: 2px 10px; border-radius: 999px; font-size: 12px;
      font-weight: 600; letter-spacing: .02em; border: 1px solid currentColor; }
    .status-draft { color: var(--status-draft); }
    .status-submitted { color: var(--status-submitted); }
    .status-approved { color: var(--status-approved); }
    .status-rejected { color: var(--status-rejected); }
  `,
})
export class StatusChip {
  readonly status = input.required<EstimateStatus>();
}
