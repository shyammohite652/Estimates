import { ChangeDetectionStrategy, Component } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

export interface NewEstimateResult { description: string; pricingDate: string; }

@Component({
  selector: 'app-new-estimate-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, MatDialogModule, MatButtonModule, MatFormFieldModule, MatInputModule],
  template: `
    <h2 mat-dialog-title>New estimate</h2>
    <form [formGroup]="form" (ngSubmit)="0">
      <mat-dialog-content class="form">
        <mat-form-field appearance="outline">
          <mat-label>Description</mat-label>
          <input matInput formControlName="description" maxlength="500" cdkFocusInitial />
          @if (form.controls.description.hasError('required')) { <mat-error>Required</mat-error> }
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>Pricing date</mat-label>
          <input matInput type="date" formControlName="pricingDate" />
          <mat-hint>Catalogue rates in force on this date are used. It must not be in the future to submit.</mat-hint>
        </mat-form-field>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancel</button>
        <button mat-flat-button type="submit" [disabled]="form.invalid" [mat-dialog-close]="form.getRawValue()">Create</button>
      </mat-dialog-actions>
    </form>
  `,
  styles: `.form { display: flex; flex-direction: column; gap: 8px; min-width: min(440px, 80vw); padding-top: 8px !important; }`,
})
export class NewEstimateDialog {
  protected readonly form = new FormGroup({
    description: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(500)] }),
    pricingDate: new FormControl(new Date().toISOString().slice(0, 10), { nonNullable: true, validators: [Validators.required] }),
  });
}
