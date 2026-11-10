import { InjectionToken } from '@angular/core';

/** Relative so the dev-server proxy (proxy.conf.json) forwards to the API; override in providers for other hosts. */
export const API_BASE_URL = new InjectionToken<string>('API_BASE_URL', { factory: () => '/api' });
