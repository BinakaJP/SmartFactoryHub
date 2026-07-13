import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { CommonModule } from '@angular/common';
import { AuthService } from '../../core/services/auth.service';
import { NotificationService } from '../../core/services/notification.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [FormsModule, CommonModule],
  template: `
    <div class="login-wrap">
      <div class="login-card">
        <div class="login-card__logo">⚙️</div>
        <h1>SmartFactory Hub</h1>
        <p class="login-card__sub">Sign in to continue</p>

        @if (error()) {
          <div class="login-error">{{ error() }}</div>
        }

        <form (ngSubmit)="submit()" #f="ngForm">
          <label>Email
            <input type="email" [(ngModel)]="email" name="email" required placeholder="admin@smartfactory.com" />
          </label>
          <label>Password
            <input type="password" [(ngModel)]="password" name="password" required placeholder="••••••••" />
          </label>
          <button type="submit" [disabled]="loading()">
            {{ loading() ? 'Signing in…' : 'Sign in' }}
          </button>
        </form>

        <div class="login-card__hint">
          <strong>Demo accounts:</strong><br>
          admin&#64;smartfactory.com / Admin123!<br>
          engineer&#64;smartfactory.com / Engineer123!
        </div>
      </div>
    </div>
  `,
  styles: [`
    .login-wrap {
      min-height: 100vh;
      display: flex;
      align-items: center;
      justify-content: center;
      background: linear-gradient(135deg, #1a1a2e 0%, #16213e 100%);
    }
    .login-card {
      background: #fff;
      border-radius: 12px;
      padding: 40px 36px;
      width: 100%;
      max-width: 380px;
      box-shadow: 0 20px 60px rgba(0,0,0,.4);
      text-align: center;
    }
    .login-card__logo { font-size: 40px; margin-bottom: 8px; }
    h1 { margin: 0 0 4px; font-size: 22px; color: #1a1a2e; }
    .login-card__sub { color: #777; margin: 0 0 24px; font-size: 14px; }
    form { display: flex; flex-direction: column; gap: 14px; text-align: left; }
    label { display: flex; flex-direction: column; gap: 5px; font-size: 13px; font-weight: 600; color: #444; }
    input {
      padding: 10px 12px;
      border: 1px solid #ddd;
      border-radius: 6px;
      font-size: 14px;
      outline: none;
    }
    input:focus { border-color: #1976d2; }
    button {
      margin-top: 4px;
      padding: 12px;
      background: #1a1a2e;
      color: #fff;
      border: none;
      border-radius: 6px;
      font-size: 15px;
      font-weight: 600;
      cursor: pointer;
    }
    button:disabled { opacity: .6; cursor: not-allowed; }
    .login-error {
      background: #ffebee;
      color: #c62828;
      border-radius: 6px;
      padding: 10px 14px;
      font-size: 13px;
      margin-bottom: 16px;
      text-align: left;
    }
    .login-card__hint {
      margin-top: 24px;
      font-size: 12px;
      color: #999;
      line-height: 1.7;
      text-align: left;
      background: #f9f9f9;
      border-radius: 6px;
      padding: 10px 12px;
    }
  `],
})
export class LoginComponent {
  private auth = inject(AuthService);
  private router = inject(Router);
  private notifications = inject(NotificationService);

  email = '';
  password = '';
  loading = signal(false);
  error = signal<string | null>(null);

  submit(): void {
    this.error.set(null);
    this.loading.set(true);

    this.auth.login(this.email, this.password).subscribe({
      next: () => {
        this.notifications.connect();
        this.router.navigate(['/dashboard']);
      },
      error: () => {
        this.error.set('Invalid email or password.');
        this.loading.set(false);
      },
    });
  }
}
