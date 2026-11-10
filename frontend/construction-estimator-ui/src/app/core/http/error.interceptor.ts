import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { ProblemDetails } from '../models';
import { Notifier } from './notifier.service';

/**
 * The one place HTTP failures are turned into user-facing messages.
 * Components can still catch errors for local state, but never need to format them.
 */
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const notifier = inject(Notifier);
  const auth = inject(AuthService);

  return next(req).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse) {
        if (error.status === 401 && !req.url.endsWith('/auth/login')) {
          notifier.error('Your session has expired. Please sign in again.');
          auth.logout();
        } else {
          notifier.error(describeHttpError(error));
        }
      }
      return throwError(() => error);
    }),
  );
};

export function describeHttpError(error: HttpErrorResponse): string {
  if (error.status === 0) return 'Cannot reach the server. Is the API running?';
  const problem = (typeof error.error === 'object' ? error.error : null) as ProblemDetails | null;
  const fieldErrors = problem?.errors ? Object.values(problem.errors).flat().join(' ') : '';
  const message = fieldErrors || problem?.detail || problem?.title || error.statusText || 'Request failed';
  return error.status >= 500 && problem?.traceId ? `${message} (trace ${problem.traceId})` : message;
}
