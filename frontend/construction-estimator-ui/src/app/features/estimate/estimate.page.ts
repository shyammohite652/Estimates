import { DatePipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input, signal, effect, untracked } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatIconModule } from '@angular/material/icon';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Observable, filter, finalize, switchMap, tap } from 'rxjs';
import { EstimatorApi } from '../../core/api/estimator-api.service';
import { Notifier } from '../../core/http/notifier.service';
import { EstimateDetails, EstimateHistoryEntry, EstimateLine } from '../../core/models';
import { ConfirmDialog, ConfirmDialogData } from '../../shared/confirm-dialog';
import { StatusChip } from '../../shared/status-chip';
import { LineDialog, LineDialogData, LineDialogResult } from './line.dialog';

/** Errors are already reported by the error interceptor; local handlers only reset state. */
const ignore = (): void => undefined;

const BASE_COLUMNS = ['code', 'description', 'unit', 'quantity', 'rate', 'markup', 'net', 'total'];

/**
 * Estimate view. Every button is driven by `estimate.capabilities`, which the server computes for the
 * caller. Hiding a button is a courtesy — the API rejects the action regardless (DESIGN.md §3).
 */
@Component({
  selector: 'app-estimate-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink, DatePipe, DecimalPipe, MatCardModule, MatButtonModule, MatIconModule, MatTableModule,
    MatPaginatorModule, MatProgressBarModule, MatExpansionModule, MatTooltipModule, StatusChip,
  ],
  templateUrl: './estimate.page.html',
  styleUrl: './estimate.page.scss',
})
export class EstimatePage {
  /** Route parameter (withComponentInputBinding). */
  readonly id = input.required<string>();

  private readonly api = inject(EstimatorApi);
  private readonly dialog = inject(MatDialog);
  private readonly notifier = inject(Notifier);

  protected readonly estimate = signal<EstimateDetails | null>(null);
  protected readonly lines = signal<EstimateLine[]>([]);
  protected readonly history = signal<EstimateHistoryEntry[]>([]);
  protected readonly totalLines = signal(0);
  protected readonly pageIndex = signal(0);
  protected readonly pageSize = signal(100);
  protected readonly loadingHeader = signal(true);
  protected readonly loadingLines = signal(true);
  protected readonly busy = signal(false);
  protected readonly notFound = signal(false);

  protected readonly caps = computed(() => this.estimate()?.capabilities);
  protected readonly showLabour = computed(() => this.caps()?.canViewLabourCost ?? false);
  protected readonly columns = computed(() => [
    ...BASE_COLUMNS.slice(0, 5),
    ...(this.showLabour() ? ['labourRate'] : []),
    ...BASE_COLUMNS.slice(5),
    ...(this.showLabour() ? ['labourAmount'] : []),
    ...(this.caps()?.canEditLines ? ['actions'] : []),
  ]);
  protected readonly hasActions = computed(() => {
    const c = this.caps();
    return !!c && (c.canSubmit || c.canApprove || c.canReject || c.canReopen);
  });

  constructor() {
    effect(() => {
      const id = this.id();
      untracked(() => this.loadAll(id));
    });
  }

  // ---------------------------------------------------------------- loading

  private loadAll(id: string): void {
    this.pageIndex.set(0);
    this.loadHeader(id);
    this.loadLines();
    this.loadHistory();
  }

  private loadHeader(id = this.id()): void {
    this.loadingHeader.set(true);
    this.api.estimate(id).subscribe({
      next: (e) => { this.estimate.set(e); this.loadingHeader.set(false); },
      error: (err) => { this.loadingHeader.set(false); if (err?.status === 404) this.notFound.set(true); },
    });
  }

  protected loadLines(): void {
    this.loadingLines.set(true);
    this.api.lines(this.id(), this.pageIndex() + 1, this.pageSize()).subscribe({
      next: (page) => {
        this.lines.set(page.items);
        this.totalLines.set(page.totalCount);
        this.loadingLines.set(false);
      },
      error: () => this.loadingLines.set(false),
    });
  }

  private loadHistory(): void {
    this.api.history(this.id()).subscribe({ next: (h) => this.history.set(h), error: () => {} });
  }

  protected onPage(e: PageEvent): void {
    this.pageIndex.set(e.pageIndex);
    this.pageSize.set(e.pageSize);
    this.loadLines();
  }

  // ---------------------------------------------------------------- lines

  protected addLine(): void {
    const e = this.estimate();
    if (!e) return;
    this.openLineDialog({ estimateId: e.id, pricingDate: e.pricingDate })
      .pipe(switchMap((r) => this.api.addOrAdjustLine(e.id, r)))
      .subscribe({ next: () => this.afterLineChange('Line saved'), error: ignore });
  }

  protected editLine(line: EstimateLine): void {
    const e = this.estimate();
    if (!e) return;
    this.openLineDialog({ estimateId: e.id, pricingDate: e.pricingDate, line })
      .pipe(switchMap((r) => this.api.updateLine(e.id, line.id, { quantity: r.quantity, markupPercent: r.markupPercent })))
      .subscribe({ next: () => this.afterLineChange('Line updated'), error: ignore });
  }

  protected removeLine(line: EstimateLine): void {
    this.confirm({
      title: 'Remove line',
      message: `Remove ${line.itemCode} — ${line.itemDescription} from this estimate?`,
      confirmLabel: 'Remove',
      destructive: true,
    }).pipe(switchMap(() => this.api.removeLine(this.id(), line.id)))
      .subscribe({ next: () => this.afterLineChange('Line removed'), error: ignore });
  }

  private openLineDialog(data: LineDialogData): Observable<LineDialogResult> {
    return this.dialog.open<LineDialog, LineDialogData, LineDialogResult>(LineDialog, { data, autoFocus: 'first-tabbable' })
      .afterClosed().pipe(filter((r): r is LineDialogResult => !!r));
  }

  private afterLineChange(message: string): void {
    this.notifier.success(message);
    this.loadHeader();
    this.loadLines();
  }

  // ---------------------------------------------------------------- lifecycle

  protected submit(): void {
    this.transition(
      { title: 'Submit for review', message: 'Lines are locked once submitted. Continue?', confirmLabel: 'Submit' },
      () => this.api.submit(this.id()), 'Estimate submitted');
  }

  protected approve(): void {
    this.transition(
      { title: 'Approve estimate', message: 'Approval makes this estimate the commercial record. It cannot be changed afterwards.', confirmLabel: 'Approve' },
      () => this.api.approve(this.id()), 'Estimate approved');
  }

  protected reject(): void {
    this.confirm({
      title: 'Reject estimate', message: 'Tell the estimator what needs to change.', confirmLabel: 'Reject',
      destructive: true, textPrompt: { label: 'Reason', maxLength: 1000 },
    }).pipe(switchMap((reason) => this.run(this.api.reject(this.id(), String(reason)))))
      .subscribe({ next: () => this.afterTransition('Estimate rejected'), error: ignore });
  }

  protected reopen(): void {
    this.transition(
      { title: 'Reopen as draft', message: 'Return this estimate to Draft so it can be reworked?', confirmLabel: 'Reopen' },
      () => this.api.reopen(this.id()), 'Estimate returned to draft');
  }

  protected correctDescription(): void {
    const e = this.estimate();
    if (!e) return;
    this.confirm({
      title: 'Correct description',
      message: 'Corrections are recorded in the history with the previous text. Amounts and status are not affected.',
      confirmLabel: 'Save', textPrompt: { label: 'Description', initial: e.description, maxLength: 500 },
    }).pipe(switchMap((d) => this.run(this.api.correctDescription(e.id, String(d)))))
      .subscribe({ next: () => this.afterTransition('Description updated'), error: ignore });
  }

  private transition(dialog: ConfirmDialogData, action: () => Observable<EstimateDetails>, message: string): void {
    this.confirm(dialog).pipe(switchMap(() => this.run(action()))).subscribe({ next: () => this.afterTransition(message), error: ignore });
  }

  /** Runs a lifecycle request: shows progress, adopts the returned estimate, re-syncs on failure. */
  private run(request: Observable<EstimateDetails>): Observable<EstimateDetails> {
    this.busy.set(true);
    return request.pipe(
      tap({ next: (e) => this.estimate.set(e), error: () => this.loadHeader() }),
      finalize(() => this.busy.set(false)),
    );
  }

  private afterTransition(message: string): void {
    this.notifier.success(message);
    this.loadHistory();
  }

  private confirm(data: ConfirmDialogData): Observable<true | string> {
    return this.dialog.open<ConfirmDialog, ConfirmDialogData, boolean | string>(ConfirmDialog, { data })
      .afterClosed().pipe(filter((r): r is true | string => r === true || (typeof r === 'string' && r.length > 0)));
  }

  protected historyLabel(h: EstimateHistoryEntry): string {
    switch (h.action) {
      case 'Created': return 'Created';
      case 'Submitted': return 'Submitted for review';
      case 'Approved': return 'Approved';
      case 'Rejected': return 'Rejected';
      case 'Reopened': return 'Returned to draft';
      case 'DescriptionCorrected': return 'Description corrected';
    }
  }
}
