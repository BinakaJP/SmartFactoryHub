import { Component, OnInit, inject } from '@angular/core';
import { RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router';
import { CommonModule } from '@angular/common';
import { AuthService } from './core/services/auth.service';
import { NotificationService } from './core/services/notification.service';
import { ToastComponent } from './shared/components/toast/toast.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, CommonModule, ToastComponent],
  template: `
    @if (auth.isLoggedIn) {
      <header class="shell-header">
        <span class="shell-header__brand">SmartFactory Hub</span>
        <nav class="shell-header__nav">
          <a routerLink="/dashboard"  routerLinkActive="active">Dashboard</a>
          <a routerLink="/equipment"  routerLinkActive="active">Equipment</a>
          <a routerLink="/alerts"     routerLinkActive="active">Alerts</a>
          <a routerLink="/chat"       routerLinkActive="active">Chat</a>
          <a [href]="grafanaUrl" target="_blank" class="grafana-link">Grafana ↗</a>
        </nav>
        <div class="shell-header__user">
          <span class="role-chip role-chip--{{ auth.role()?.toLowerCase() }}">{{ auth.role() }}</span>
          <span class="user-email">{{ auth.email() }}</span>
          <button class="btn-logout" (click)="auth.logout()">Logout</button>
        </div>
      </header>
    }
    <main [class.has-header]="auth.isLoggedIn">
      <router-outlet />
    </main>
    <app-toast />
  `,
  styles: [`
    .shell-header {
      display: flex;
      align-items: center;
      gap: 24px;
      padding: 0 24px;
      height: 56px;
      background: #1a1a2e;
      color: #fff;
      position: sticky;
      top: 0;
      z-index: 100;
    }
    .shell-header__brand {
      font-weight: 700;
      font-size: 16px;
      white-space: nowrap;
      color: #64b5f6;
    }
    .shell-header__nav {
      display: flex;
      gap: 4px;
      flex: 1;
    }
    .shell-header__nav a {
      color: #ccc;
      text-decoration: none;
      padding: 6px 14px;
      border-radius: 4px;
      font-size: 14px;
      transition: background .15s;
    }
    .shell-header__nav a:hover,
    .shell-header__nav a.active { background: rgba(255,255,255,.12); color: #fff; }
    .grafana-link { font-size: 13px !important; opacity: .7; }
    .shell-header__user {
      display: flex;
      align-items: center;
      gap: 10px;
      font-size: 13px;
    }
    .user-email { color: #aaa; }
    .role-chip {
      padding: 2px 10px;
      border-radius: 12px;
      font-size: 11px;
      font-weight: 600;
      text-transform: uppercase;
    }
    .role-chip--admin    { background: #7b1fa2; color: #fff; }
    .role-chip--engineer { background: #1565c0; color: #fff; }
    .role-chip--operator { background: #2e7d32; color: #fff; }
    .role-chip--viewer   { background: #555;    color: #fff; }
    .btn-logout {
      background: rgba(255,255,255,.1);
      border: 1px solid rgba(255,255,255,.2);
      color: #fff;
      padding: 4px 12px;
      border-radius: 4px;
      cursor: pointer;
      font-size: 13px;
    }
    .btn-logout:hover { background: rgba(255,255,255,.2); }
    main { min-height: 100vh; background: #f5f5f5; }
    main.has-header { min-height: calc(100vh - 56px); }
  `],
})
export class AppComponent implements OnInit {
  auth = inject(AuthService);
  private notifications = inject(NotificationService);
  grafanaUrl = 'http://localhost:3000';

  ngOnInit(): void {
    if (this.auth.isLoggedIn) {
      this.notifications.connect();
    }
  }
}
