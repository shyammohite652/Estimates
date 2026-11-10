import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

/**
 * Mirrors the domain's precision rules (R3) for immediate feedback. The server re-validates;
 * this is UX, not enforcement.
 */
export function maxDecimals(places: number): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const value = control.value;
    if (value === null || value === undefined || value === '') return null;
    const text = String(value);
    const decimals = text.includes('.') ? text.split('.')[1].replace(/0+$/, '').length : 0;
    return decimals > places ? { maxDecimals: { places } } : null;
  };
}

export function greaterThan(min: number): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const value = control.value;
    if (value === null || value === undefined || value === '') return null;
    return Number(value) > min ? null : { greaterThan: { min } };
  };
}
