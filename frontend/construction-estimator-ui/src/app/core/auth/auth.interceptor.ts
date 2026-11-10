import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { API_BASE_URL } from '../api/api-base';
import { AuthService } from './auth.service';

/** Attaches the bearer token to API calls only — never to third-party hosts. */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const base = inject(API_BASE_URL);
  const token = auth.token;
  if (!token || !req.url.startsWith(base)) return next(req);
  return next(req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }));
};
