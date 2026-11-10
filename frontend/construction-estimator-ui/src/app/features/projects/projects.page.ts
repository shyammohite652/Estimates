import { DecimalPipe, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { EstimatorApi } from '../../core/api/estimator-api.service';
import { AuthService } from '../../core/auth/auth.service';
import { ProjectSummary } from '../../core/models';
import { StatusChip } from '../../shared/status-chip';
import { NewEstimateDialog, NewEstimateResult } from './new-estimate.dialog';

@Component({
  selector: 'app-projects-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, DecimalPipe, DatePipe, MatCardModule, MatButtonModule, MatIconModule, MatProgressBarModule, StatusChip],
  template: `
    <section class="page">
      <header class="page-header">
        <div>
          <h1>My projects</h1>
          <p class="muted">Only projects you are assigned to are listed.</p>
        </div>
      </header>

      @if (loading()) { <mat-progress-bar mode="indeterminate" /> }
      @if (!loading() && projects().length === 0) {
        <p class="muted">You are not assigned to any projects.</p>
      }

      @for (project of projects(); track project.id) {
        <mat-card appearance="outlined" class="project">
          <div class="project-head">
            <div>
              <div class="code">{{ project.code }}</div>
              <h2>{{ project.name }}</h2>
              @if (project.client) { <div class="muted">{{ project.client }}</div> }
            </div>
            @if (canCreate()) {
              <button mat-stroked-button (click)="newEstimate(project)"><mat-icon>add</mat-icon> New estimate</button>
            }
          </div>

          @if (project.estimates.length === 0) {
            <p class="muted">No estimates yet.</p>
          } @else {
            <div class="table-scroll">
              <table class="estimates">
                <thead>
                  <tr><th>Number</th><th>Description</th><th>Pricing date</th><th>Status</th><th class="num">Lines</th><th class="num">Total</th></tr>
                </thead>
                <tbody>
                  @for (e of project.estimates; track e.id) {
                    <tr [routerLink]="['/estimates', e.id]" tabindex="0" (keydown.enter)="open(e.id)">
                      <td class="mono">{{ e.number }}</td>
                      <td>{{ e.description }}</td>
                      <td>{{ e.pricingDate | date: 'd MMM y' }}</td>
                      <td><app-status-chip [status]="e.status" /></td>
                      <td class="num">{{ e.lineCount }}</td>
                      <td class="num mono">{{ e.total | number: '1.2-2' }}</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          }
        </mat-card>
      }
    </section>
  `,
  styles: `
    .project { margin-bottom: 16px; padding: 16px; }
    .project-head { display: flex; justify-content: space-between; align-items: flex-start; gap: 12px; flex-wrap: wrap; margin-bottom: 8px; }
    .project h2 { margin: 2px 0; font-size: 20px; }
    .code { font-size: 12px; font-weight: 600; color: var(--accent); letter-spacing: .05em; }
    table.estimates { width: 100%; border-collapse: collapse; }
    .estimates th, .estimates td { text-align: left; padding: 10px 8px; border-bottom: 1px solid var(--border); white-space: nowrap; }
    .estimates td:nth-child(2) { white-space: normal; }
    .estimates th { font-size: 12px; color: var(--muted); font-weight: 600; }
    .estimates tbody tr { cursor: pointer; }
    .estimates tbody tr:hover, .estimates tbody tr:focus-visible { background: var(--hover); outline: none; }
  `,
})
export class ProjectsPage implements OnInit {
  private readonly api = inject(EstimatorApi);
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);

  protected readonly projects = signal<ProjectSummary[]>([]);
  protected readonly loading = signal(true);
  /** UI hint only; POST /projects/{id}/estimates is authorised server-side. */
  protected readonly canCreate = computed(() => this.auth.user()?.role === 'Estimator');

  ngOnInit(): void {
    this.api.myProjects().subscribe({
      next: (p) => { this.projects.set(p); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  open(id: string): void { void this.router.navigate(['/estimates', id]); }

  newEstimate(project: ProjectSummary): void {
    this.dialog.open<NewEstimateDialog, void, NewEstimateResult>(NewEstimateDialog).afterClosed().subscribe((result) => {
      if (!result) return;
      this.api.createEstimate(project.id, result).subscribe((created) => this.open(created.id));
    });
  }
}
