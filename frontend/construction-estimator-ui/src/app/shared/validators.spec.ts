import { FormControl } from '@angular/forms';
import { greaterThan, maxDecimals } from './validators';

describe('line input validators (mirror R3/R6 for UX)', () => {
  it('rejects more decimal places than allowed but ignores trailing zeros', () => {
    expect(maxDecimals(3)(new FormControl(1.2345))).toEqual({ maxDecimals: { places: 3 } });
    expect(maxDecimals(3)(new FormControl('1.2000'))).toBeNull();
    expect(maxDecimals(2)(new FormControl(12.5))).toBeNull();
  });

  it('requires quantity strictly greater than zero', () => {
    expect(greaterThan(0)(new FormControl(0))).toEqual({ greaterThan: { min: 0 } });
    expect(greaterThan(0)(new FormControl(-1))).not.toBeNull();
    expect(greaterThan(0)(new FormControl(0.001))).toBeNull();
  });

  it('leaves empty values to Validators.required', () => {
    expect(maxDecimals(2)(new FormControl(null))).toBeNull();
    expect(greaterThan(0)(new FormControl(''))).toBeNull();
  });
});
