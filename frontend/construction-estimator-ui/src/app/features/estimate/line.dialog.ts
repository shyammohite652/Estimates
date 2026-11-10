import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { debounceTime, distinctUntilChanged, filter, startWith, switchMap } from 'rxjs';
import { EstimatorApi } from '../../core/api/estimator-api.service';
import { CatalogueItem, EstimateLine } from '../../core/models';
import { greaterThan, maxDecimals } from '../../shared/validators';

export interface LineDialogData {
  estimateId: string;
  pricingDate: string;
  /** Present when editing an existing line. */
  line?: EstimateLine;
}

export interface LineDialogResult {
  catalogueItemId: string;
  quantity: number;
  markupPercent: number;
}

@Component({
  selector: 'app-line-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, DecimalPipe, MatDialogModule, MatButtonModule, MatFormFieldModule, MatInputModule, MatAutocompleteModule],
  template: `
    <h2 mat-dialog-title>{{ data.line ? 'Edit line' : 'Add line' }}</h2>
    <form [formGroup]="form" (ngSubmit)="save()">
      <mat-dialog-content class="form">
        @if (data.line; as line) {
          <div class="item-fixed">
            <span class="mono">{{ line.itemCode }}</span> — {{ line.itemDescription }}
            <div class="muted small">Rate {{ line.unitRate | number: '1.2-2' }} / {{ line.unit }}, effective {{ line.rateEffectiveFrom }}</div>
          </div>
        } @else {
          <mat-form-field appearance="outline">
            <mat-label>Catalogue item</mat-label>
            <input matInput [formControl]="search" [matAutocomplete]="auto" placeholder="Search by code or description" cdkFocusInitial />
            <mat-autocomplete #auto="matAutocomplete" [displayWith]="display" (optionSelected)="pick($event.option.value)">
              @for (item of options(); track item.id) {
                <mat-option [value]="item" [disabled]="item.rate === null">
                  <span class="mono">{{ item.code }}</span> — {{ item.description }}
                  <span class="muted small">
                    @if (item.rate !== null) { · {{ item.rate | number: '1.2-2' }} / {{ item.unit }} }
                    @else { · no rate on {{ data.pricingDate }} }
                  </span>
                </mat-option>
              }
            </mat-autocomplete>
            <mat-hint>Priced on {{ data.pricingDate }}. Adding an item already on the estimate adjusts that line.</mat-hint>
          </mat-form-field>
        }

        <div class="row">
          <mat-form-field appearance="outline">
            <mat-label>Quantity</mat-label>
            <input matInput type="number" formControlName="quantity" step="0.001" min="0" inputmode="decimal" />
            @if (selected(); as item) { <span matTextSuffix>{{ item.unit }}</span> }
            @let q = form.controls.quantity;
            @if (q.hasError('required')) { <mat-error>Required</mat-error> }
            @else if (q.hasError('greaterThan')) { <mat-error>Must be greater than 0</mat-error> }
            @else if (q.hasError('maxDecimals')) { <mat-error>At most 3 decimal places</mat-error> }
            @else if (q.hasError('max')) { <mat-error>Too large</mat-error> }
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>Markup</mat-label>
            <input matInput type="number" formControlName="markupPercent" step="0.01" min="0" max="100" inputmode="decimal" />
            <span matTextSuffix>%</span>
            @let m = form.controls.markupPercent;
            @if (m.hasError('required')) { <mat-error>Required</mat-error> }
            @else if (m.hasError('min') || m.hasError('max')) { <mat-error>Between 0 and 100</mat-error> }
            @else if (m.hasError('maxDecimals')) { <mat-error>At most 2 decimal places</mat-error> }
          </mat-form-field>
        </div>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancel</button>
        <button mat-flat-button type="submit" [disabled]="form.invalid">{{ data.line ? 'Save' : 'Add' }}</button>
      </mat-dialog-actions>
    </form>
  `,
  styles: `
    .form { display: flex; flex-direction: column; gap: 8px; min-width: min(520px, 82vw); padding-top: 8px !important; }
    .row { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; }
    @media (max-width: 520px) { .row { grid-template-columns: 1fr; } }
    .item-fixed { padding: 8px 0 12px; }
    .small { font-size: 12px; }
  `,
})
export class LineDialog {
  protected readonly data = inject<LineDialogData>(MAT_DIALOG_DATA);
  private readonly ref = inject(MatDialogRef<LineDialog, LineDialogResult>);
  private readonly api = inject(EstimatorApi);

  protected readonly options = signal<CatalogueItem[]>([]);
  protected readonly selected = signal<CatalogueItem | null>(null);
  protected readonly search = new FormControl<string | CatalogueItem>('', { nonNullable: true });

  protected readonly form = new FormGroup({
    catalogueItemId: new FormControl(this.data.line?.catalogueItemId ?? '', { nonNullable: true, validators: [Validators.required] }),
    quantity: new FormControl<number | null>(this.data.line?.quantity ?? null, {
      validators: [Validators.required, greaterThan(0), maxDecimals(3), Validators.max(99_999_999.999)],
    }),
    markupPercent: new FormControl<number | null>(this.data.line?.markupPercent ?? 0, {
      validators: [Validators.required, Validators.min(0), Validators.max(100), maxDecimals(2)],
    }),
  });

  constructor() {
    if (!this.data.line) {
      this.search.valueChanges.pipe(
        startWith(''),
        filter((v): v is string => typeof v === 'string'),
        debounceTime(200),
        distinctUntilChanged(),
        switchMap((term) => this.api.catalogue(this.data.pricingDate, term)),
        takeUntilDestroyed(inject(DestroyRef)),
      ).subscribe((items) => this.options.set(items));
    }
  }

  protected display = (item: CatalogueItem | string | null): string =>
    item && typeof item === 'object' ? `${item.code} — ${item.description}` : (item ?? '');

  pick(item: CatalogueItem): void {
    this.selected.set(item);
    this.form.controls.catalogueItemId.setValue(item.id);
  }

  save(): void {
    if (this.form.invalid) return;
    const v = this.form.getRawValue();
    this.ref.close({ catalogueItemId: v.catalogueItemId, quantity: Number(v.quantity), markupPercent: Number(v.markupPercent) });
  }
}
