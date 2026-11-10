import { Routes } from '@angular/router';
import { anonymousGuard, authGuard } from './core/auth/auth.guard';

export const routes: Routes = [
  { path: 'login', canActivate: [anonymousGuard], title: 'Sign in · Estimator',
    loadComponent: () => import('./features/login/login.page').then((m) => m.LoginPage) },
  { path: 'projects', canActivate: [authGuard], title: 'Projects · Estimator',
    loadComponent: () => import('./features/projects/projects.page').then((m) => m.ProjectsPage) },
  { path: 'estimates/:id', canActivate: [authGuard], title: 'Estimate · Estimator',
    loadComponent: () => import('./features/estimate/estimate.page').then((m) => m.EstimatePage) },
  { path: '', pathMatch: 'full', redirectTo: 'projects' },
  { path: '**', redirectTo: 'projects' },
];
