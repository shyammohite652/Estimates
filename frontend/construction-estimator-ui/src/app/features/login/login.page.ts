import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { AuthService } from '../../core/auth/auth.service';
import { User } from '../../core/models';
import { ROLE_LABELS, ROLE_SUMMARIES } from '../../shared/role-label';

/** Development sign-in: pick a seeded user; the API issues a real signed JWT for them. */
@Component({
  selector: 'app-login-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatCardModule, MatIconModule, MatProgressBarModule],
  template: `
    <section class="login">
      <header>
        <h1>Sign in</h1>
        <p class="muted">Development build — choose a seeded user. No passwords; the API still issues and validates a signed token.</p>
      </header>

      @if (loading()) { <mat-progress-bar mode="indeterminate" /> }
      @if (loadError()) {
        <p class="error-text">Could not load users. Make sure the API is running on port 5080.</p>
      }

      <div class="user-grid">
        @for (user of users(); track user.id) {
          <button type="button" class="user-card" (click)="signIn(user)" [disabled]="signingIn() !== null"
                  [attr.aria-label]="'Sign in as ' + user.displayName + ', ' + roleLabel(user)">
            <span class="avatar" [class]="'role-' + user.role.toLowerCase()">{{ initials(user) }}</span>
            <span class="who">
              <strong>{{ user.displayName }}</strong>
              <span class="muted">{{ user.email }}</span>
              <span class="role">{{ roleLabel(user) }}</span>
              <span class="muted small">{{ roleSummary(user) }}</span>
            </span>
            @if (signingIn() === user.id) { <mat-icon class="spin">progress_activity</mat-icon> }
            @else { <mat-icon>arrow_forward</mat-icon> }
          </button>
        }
      </div>
    </section>
  `,
  styles: `
    .login { max-width: 880px; margin: 32px auto; padding: 0 16px; }
    h1 { margin: 0 0 4px; font-size: 28px; }
    header { margin-bottom: 20px; }
    .user-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(260px, 1fr)); gap: 12px; margin-top: 12px; }
    .user-card { display: flex; gap: 12px; align-items: center; text-align: left; padding: 14px; cursor: pointer;
      border: 1px solid var(--border); border-radius: 12px; background: var(--surface); color: inherit; font: inherit; }
    .user-card:hover:not(:disabled), .user-card:focus-visible { border-color: var(--accent); outline: none; box-shadow: 0 0 0 3px var(--accent-soft); }
    .user-card:disabled { opacity: .6; cursor: progress; }
    .who { display: flex; flex-direction: column; gap: 2px; flex: 1; min-width: 0; }
    .who .muted { overflow: hidden; text-overflow: ellipsis; }
    .role { font-size: 12px; font-weight: 600; color: var(--accent); text-transform: uppercase; letter-spacing: .04em; margin-top: 4px; }
    .small { font-size: 12px; }
    .avatar { width: 40px; height: 40px; border-radius: 50%; display: grid; place-items: center; font-weight: 700;
      color: #fff; flex-shrink: 0; background: var(--accent); }
    .role-reviewer { background: var(--status-submitted); }
    .role-viewer { background: var(--status-draft); }
    .role-projectmanager { background: var(--status-approved); }
    .spin { animation: spin 1s linear infinite; }
    @keyframes spin { to { transform: rotate(360deg); } }
  `,
})
export class LoginPage implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly users = signal<User[]>([]);
  protected readonly loading = signal(true);
  protected readonly loadError = signal(false);
  protected readonly signingIn = signal<string | null>(null);

  ngOnInit(): void {
    this.auth.listUsers().subscribe({
      next: (users) => { this.users.set(users); this.loading.set(false); },
      error: () => { this.loadError.set(true); this.loading.set(false); },
    });
  }

  signIn(user: User): void {
    this.signingIn.set(user.id);
    this.auth.login(user.email).subscribe({
      next: () => void this.router.navigate(['/projects']),
      error: () => this.signingIn.set(null),
    });
  }

  roleLabel(u: User): string { return ROLE_LABELS[u.role]; }
  roleSummary(u: User): string { return ROLE_SUMMARIES[u.role]; }
  initials(u: User): string {
    return u.displayName.split(' ').map((p) => p[0]).join('').slice(0, 2).toUpperCase();
  }
}
