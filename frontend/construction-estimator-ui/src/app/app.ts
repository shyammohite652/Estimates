import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink, RouterOutlet } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatToolbarModule } from '@angular/material/toolbar';
import { AuthService } from './core/auth/auth.service';
import { ROLE_LABELS } from './shared/role-label';

@Component({
  selector: 'app-root',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterOutlet, RouterLink, MatToolbarModule, MatButtonModule, MatIconModule],
  template: `
    <mat-toolbar class="topbar">
      <a routerLink="/projects" class="brand"><mat-icon>architecture</mat-icon> <span>Construction Estimator</span></a>
      <span class="spacer"></span>
      @if (auth.user(); as user) {
        <span class="who">
          <strong>{{ user.displayName }}</strong>
          <span class="role-pill">{{ roles[user.role] }}</span>
        </span>
        <button mat-button (click)="auth.logout()" aria-label="Switch user"><mat-icon>logout</mat-icon> <span class="label">Switch user</span></button>
      }
    </mat-toolbar>
    <main><router-outlet /></main>
  `,
  styles: `
    .topbar { position: sticky; top: 0; z-index: 10; background: var(--surface); border-bottom: 1px solid var(--border); gap: 12px; }
    .brand { display: flex; align-items: center; gap: 8px; color: inherit; text-decoration: none; font-weight: 600; font-size: 17px; }
    .spacer { flex: 1; }
    .who { display: flex; align-items: center; gap: 8px; font-size: 14px; }
    .role-pill { font-size: 11px; padding: 2px 8px; border-radius: 999px; background: var(--accent-soft); color: var(--accent); font-weight: 600; }
    @media (max-width: 600px) { .who strong, .brand span, .label { display: none; } }
  `,
})
export class App {
  protected readonly auth = inject(AuthService);
  protected readonly roles = ROLE_LABELS;
}
