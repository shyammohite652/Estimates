import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';
import { API_BASE_URL } from '../api/api-base';
import { LoginResponse, User } from '../models';

const STORAGE_KEY = 'ce.session';

interface Session {
  token: string;
  expiresAt: string;
  user: User;
}

/**
 * Holds the signed-in session. Development trade-off (ADR-003): the token lives in sessionStorage,
 * so it is cleared when the tab closes. Production would use an httpOnly cookie or an IdP.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly base = inject(API_BASE_URL);
  private readonly session = signal<Session | null>(this.restore());

  readonly user = computed(() => this.session()?.user ?? null);
  readonly isAuthenticated = computed(() => this.session() !== null);

  get token(): string | null {
    return this.session()?.token ?? null;
  }

  listUsers(): Observable<User[]> {
    return this.http.get<User[]>(`${this.base}/auth/users`);
  }

  login(email: string): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(`${this.base}/auth/login`, { email }).pipe(
      tap((r) => this.store({ token: r.accessToken, expiresAt: r.expiresAt, user: r.user })),
    );
  }

  logout(): void {
    this.store(null);
    void this.router.navigate(['/login']);
  }

  private store(session: Session | null): void {
    this.session.set(session);
    try {
      if (session) sessionStorage.setItem(STORAGE_KEY, JSON.stringify(session));
      else sessionStorage.removeItem(STORAGE_KEY);
    } catch {
      /* storage unavailable: session lasts for this page only */
    }
  }

  private restore(): Session | null {
    try {
      const raw = sessionStorage.getItem(STORAGE_KEY);
      if (!raw) return null;
      const session = JSON.parse(raw) as Session;
      return new Date(session.expiresAt).getTime() > Date.now() ? session : null;
    } catch {
      return null;
    }
  }
}
