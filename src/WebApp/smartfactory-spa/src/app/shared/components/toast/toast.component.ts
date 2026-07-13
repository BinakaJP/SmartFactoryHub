import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NotificationService } from '../../../core/services/notification.service';

@Component({
  selector: 'app-toast',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="toast-container">
      @for (t of notifications.toasts(); track t.id) {
        <div class="toast" [class]="'toast--' + t.type">
          <span>{{ t.message }}</span>
          <button class="toast__close" (click)="notifications.dismiss(t.id)">✕</button>
        </div>
      }
    </div>
  `,
  styles: [`
    .toast-container {
      position: fixed;
      bottom: 24px;
      right: 24px;
      z-index: 9999;
      display: flex;
      flex-direction: column;
      gap: 10px;
      max-width: 380px;
    }
    .toast {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 12px;
      padding: 12px 16px;
      border-radius: 6px;
      color: #fff;
      font-size: 14px;
      animation: slideIn .2s ease;
    }
    .toast--info     { background: #1976d2; }
    .toast--warning  { background: #f57c00; }
    .toast--critical { background: #c62828; }
    .toast__close {
      background: none;
      border: none;
      color: #fff;
      cursor: pointer;
      font-size: 14px;
      padding: 0;
      flex-shrink: 0;
    }
    @keyframes slideIn {
      from { transform: translateX(110%); opacity: 0; }
      to   { transform: translateX(0);    opacity: 1; }
    }
  `],
})
export class ToastComponent {
  notifications = inject(NotificationService);
}
