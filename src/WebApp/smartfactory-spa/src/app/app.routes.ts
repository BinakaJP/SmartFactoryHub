import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';

export const routes: Routes = [
  { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
  {
    path: 'login',
    loadComponent: () =>
      import('./pages/login/login.component').then(m => m.LoginComponent),
  },
  {
    path: 'dashboard',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./pages/dashboard/dashboard.component').then(m => m.DashboardComponent),
  },
  {
    path: 'equipment',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./pages/equipment-detail/equipment-detail.component').then(m => m.EquipmentDetailComponent),
  },
  {
    path: 'alerts',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./pages/alerts/alerts.component').then(m => m.AlertsComponent),
  },
  {
    path: 'chat',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./pages/chat/chat.component').then(m => m.ChatComponent),
  },
  { path: '**', redirectTo: 'dashboard' },
];
