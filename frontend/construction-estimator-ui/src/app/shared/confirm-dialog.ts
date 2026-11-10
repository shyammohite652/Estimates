import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

export interface ConfirmDialogData {
  title: string;
  message: string;
  confirmLabel: string;
  /** When set, the dialog asks for text (e.g. a rejection reason) and returns it. */
  textPrompt?: { label: string; initial?: string; maxLength: number };
  destructive?: boolean;
}

/** Returns `true`, the entered text, or `undefined` when cancelled. */
@Component({
  selector: 'app-confirm-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatDialogModule, MatButtonModule, MatFormFieldModule, MatInputModule, ReactiveFormsModule],
  template: `
    <h2 mat-dialog-title>{{ data.title }}</h2>
    <mat-dialog-content>
      <p>{{ data.message }}</p>
      @if (data.textPrompt; as prompt) {
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>{{ prompt.label }}</mat-label>
          <textarea matInput [formControl]="text" rows="3" [maxlength]="prompt.maxLength" cdkFocusInitial></textarea>
          <mat-hint align="end">{{ text.value?.length ?? 0 }} / {{ prompt.maxLength }}</mat-hint>
          @if (text.hasError('required')) { <mat-error>Required</mat-error> }
        </mat-form-field>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Cancel</button>
      <button mat-flat-button [class.destructive]="data.destructive" (click)="confirm()"
              [disabled]="data.textPrompt && text.invalid">{{ data.confirmLabel }}</button>
    </mat-dialog-actions>
  `,
  styles: `.full-width { width: 100%; min-width: min(420px, 80vw); } .destructive { --mat-button-filled-container-color: var(--danger); }`,
})
export class ConfirmDialog {
  protected readonly data = inject<ConfirmDialogData>(MAT_DIALOG_DATA);
  private readonly ref = inject(MatDialogRef<ConfirmDialog, boolean | string>);
  protected readonly text = new FormControl(this.data.textPrompt?.initial ?? '', {
    nonNullable: true,
    validators: [Validators.required, (c) => (c.value?.trim() ? null : { required: true })],
  });

  confirm(): void {
    this.ref.close(this.data.textPrompt ? this.text.value.trim() : true);
  }
}
