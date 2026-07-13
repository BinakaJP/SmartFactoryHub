import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AlertService, Alert } from '../../core/services/alert.service';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-alerts',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="page">
      <div class="page-header">
        <h2>Alerts</h2>
        <div class="filters">
          <select [(ngModel)]="filterStatus" (ngModelChange)="load()">
            <option value="">All Statuses</option>
            <option value="Open">Open</option>
            <option value="Acknowledged">Acknowledged</option>
            <option value="Resolved">Resolved</option>
          </select>
          <select [(ngModel)]="filterSeverity" (ngModelChange)="load()">
            <option value="">All Severities</option>
            <option value="Critical">Critical</option>
            <option value="Warning">Warning</option>
            <option value="Info">Info</option>
          </select>
          <button class="btn-refresh" (click)="load()">↻ Refresh</button>
        </div>
      </div>

      @if (alerts().length === 0) {
        <div class="empty-state">
          <p>No alerts match the current filter.</p>
        </div>
      } @else {
        <div class="alerts-table-wrap">
          <table class="alerts-table">
            <thead>
              <tr>
                <th>Severity</th>
                <th>Equipment</th>
                <th>Metric</th>
                <th>Message</th>
                <th>Status</th>
                <th>Triggered</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              @for (alert of alerts(); track alert.id) {
                <tr [class]="'row--' + alert.severity.toLowerCase()">
                  <td>
                    <span class="sev-chip sev-chip--{{ alert.severity.toLowerCase() }}">
                      {{ alert.severity }}
                    </span>
                  </td>
                  <td class="td-equip">{{ alert.equipmentName }}</td>
                  <td>{{ alert.metricType }}</td>
                  <td class="td-msg">{{ alert.message }}</td>
                  <td>
                    <span class="status-chip status-chip--{{ alert.status.toLowerCase() }}">
                      {{ alert.status }}
                    </span>
                  </td>
                  <td class="td-time">{{ alert.triggeredAt | date:'MMM d, HH:mm' }}</td>
                  <td class="td-actions">
                    @if (alert.status === 'Open' && auth.canAcknowledge()) {
                      <button class="btn-ack" (click)="acknowledge(alert)">Acknowledge</button>
                    }
                    @if (alert.status === 'Acknowledged' && auth.canAcknowledge()) {
                      <button class="btn-resolve" (click)="resolve(alert)">Resolve</button>
                    }
                    @if (alert.acknowledgedBy) {
                      <span class="ack-by">by {{ alert.acknowledgedBy }}</span>
                    }
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      }
    </div>
  `,
  styles: [`
    .page { padding: 24px; max-width: 1200px; margin: 0 auto; }
    .page-header {
      display: flex; align-items: center; justify-content: space-between; margin-bottom: 20px;
    }
    .page-header h2 { margin: 0; font-size: 22px; color: #1a1a2e; }
    .filters { display: flex; gap: 10px; align-items: center; }
    select {
      padding: 7px 10px; border: 1px solid #ddd; border-radius: 5px; font-size: 13px;
    }
    .btn-refresh {
      padding: 7px 12px; background: #fff; border: 1px solid #ddd;
      border-radius: 5px; cursor: pointer; font-size: 13px;
    }
    .btn-refresh:hover { background: #f5f5f5; }

    .alerts-table-wrap {
      background: #fff; border-radius: 8px;
      box-shadow: 0 1px 4px rgba(0,0,0,.08); overflow: hidden;
    }
    .alerts-table { width: 100%; border-collapse: collapse; font-size: 13px; }
    thead { background: #f5f5f5; }
    th { padding: 12px 16px; text-align: left; font-weight: 600; color: #555; white-space: nowrap; }
    td { padding: 12px 16px; border-bottom: 1px solid #f0f0f0; vertical-align: middle; }
    tbody tr:last-child td { border-bottom: none; }
    tbody tr:hover { background: #fafafa; }
    .row--critical { border-left: 3px solid #c62828; }
    .row--warning  { border-left: 3px solid #f57c00; }
    .row--info     { border-left: 3px solid #1976d2; }
    .td-equip { font-weight: 600; }
    .td-msg { max-width: 280px; color: #555; }
    .td-time { white-space: nowrap; color: #888; }
    .td-actions { white-space: nowrap; }

    .sev-chip {
      padding: 3px 10px; border-radius: 10px;
      font-size: 11px; font-weight: 700; text-transform: uppercase;
    }
    .sev-chip--critical { background: #ffebee; color: #c62828; }
    .sev-chip--warning  { background: #fff3e0; color: #e65100; }
    .sev-chip--info     { background: #e3f2fd; color: #1565c0; }

    .status-chip {
      padding: 2px 8px; border-radius: 8px;
      font-size: 11px; font-weight: 600; text-transform: uppercase;
    }
    .status-chip--open         { background: #e3f2fd; color: #1565c0; }
    .status-chip--acknowledged { background: #fff3e0; color: #e65100; }
    .status-chip--resolved     { background: #e8f5e9; color: #2e7d32; }

    .btn-ack, .btn-resolve {
      padding: 4px 10px; border: none; border-radius: 4px;
      font-size: 12px; cursor: pointer; font-weight: 600;
    }
    .btn-ack    { background: #fff3e0; color: #e65100; }
    .btn-resolve { background: #e8f5e9; color: #2e7d32; }
    .btn-ack:hover    { background: #ffe0b2; }
    .btn-resolve:hover { background: #c8e6c9; }
    .ack-by { font-size: 11px; color: #999; }

    .empty-state {
      background: #fff; border-radius: 8px; padding: 40px;
      text-align: center; color: #999; font-size: 14px;
      box-shadow: 0 1px 4px rgba(0,0,0,.08);
    }
  `],
})
export class AlertsComponent implements OnInit {
  private alertSvc = inject(AlertService);
  auth = inject(AuthService);

  alerts = signal<Alert[]>([]);
  filterStatus = '';
  filterSeverity = '';

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.alertSvc
      .getAll(this.filterStatus || undefined, this.filterSeverity || undefined)
      .subscribe(a => this.alerts.set(a));
  }

  acknowledge(alert: Alert): void {
    const by = this.auth.email() ?? 'engineer';
    this.alertSvc.acknowledge(alert.id, by).subscribe(() => this.load());
  }

  resolve(alert: Alert): void {
    this.alertSvc.resolve(alert.id).subscribe(() => this.load());
  }
}
