import { Component, OnInit, OnDestroy, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { interval, Subscription } from 'rxjs';
import { switchMap, startWith } from 'rxjs/operators';
import { EquipmentService, Equipment, StatusSummary } from '../../core/services/equipment.service';
import { AlertService, AlertSummary } from '../../core/services/alert.service';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <div class="page">
      <div class="page-header">
        <h2>Dashboard</h2>
        <div class="page-header__actions">
          <a [href]="grafanaServiceHealth" target="_blank" class="btn-grafana">
            Service Health in Grafana ↗
          </a>
          <a [href]="grafanaEquipment" target="_blank" class="btn-grafana">
            Equipment Metrics in Grafana ↗
          </a>
        </div>
      </div>

      <!-- Alert summary -->
      <section class="stat-row">
        <div class="stat-card stat-card--critical">
          <div class="stat-card__value">{{ alertSummary()?.criticalCount ?? '—' }}</div>
          <div class="stat-card__label">Critical Alerts</div>
        </div>
        <div class="stat-card stat-card--warning">
          <div class="stat-card__value">{{ alertSummary()?.warningCount ?? '—' }}</div>
          <div class="stat-card__label">Warnings</div>
        </div>
        <div class="stat-card stat-card--open">
          <div class="stat-card__value">{{ alertSummary()?.openCount ?? '—' }}</div>
          <div class="stat-card__label">Open Alerts</div>
        </div>
        <div class="stat-card stat-card--ack">
          <div class="stat-card__value">{{ alertSummary()?.acknowledgedCount ?? '—' }}</div>
          <div class="stat-card__label">Acknowledged</div>
        </div>
      </section>

      <!-- Equipment status row -->
      @if (statusSummary()) {
        <section class="stat-row">
          <div class="stat-card stat-card--running">
            <div class="stat-card__value">{{ statusSummary()!.running }}</div>
            <div class="stat-card__label">Running</div>
          </div>
          <div class="stat-card stat-card--idle">
            <div class="stat-card__value">{{ statusSummary()!.idle }}</div>
            <div class="stat-card__label">Idle</div>
          </div>
          <div class="stat-card stat-card--maint">
            <div class="stat-card__value">{{ statusSummary()!.maintenance }}</div>
            <div class="stat-card__label">Maintenance</div>
          </div>
          <div class="stat-card stat-card--fault">
            <div class="stat-card__value">{{ statusSummary()!.fault }}</div>
            <div class="stat-card__label">Fault</div>
          </div>
        </section>
      }

      <!-- Equipment cards grid -->
      <section>
        <h3>Equipment Fleet</h3>
        <div class="equipment-grid">
          @for (eq of equipment(); track eq.id) {
            <div class="eq-card" [class]="'eq-card--' + eq.status.toLowerCase()">
              <div class="eq-card__header">
                <span class="eq-card__code">{{ eq.code }}</span>
                <span class="status-chip status-chip--{{ eq.status.toLowerCase() }}">{{ eq.status }}</span>
              </div>
              <div class="eq-card__name">{{ eq.name }}</div>
              <div class="eq-card__location">{{ eq.location }}</div>
              <div class="eq-card__actions">
                <a routerLink="/equipment" [queryParams]="{ id: eq.id }" class="btn-sm">Manage</a>
                <a [href]="grafanaEquipmentLink(eq.code)" target="_blank" class="btn-sm btn-sm--grafana">
                  Metrics ↗
                </a>
              </div>
            </div>
          } @empty {
            <p class="empty">Loading equipment…</p>
          }
        </div>
      </section>
    </div>
  `,
  styles: [`
    .page { padding: 24px; max-width: 1200px; margin: 0 auto; }
    .page-header {
      display: flex; align-items: center; justify-content: space-between;
      margin-bottom: 20px;
    }
    .page-header h2 { margin: 0; font-size: 22px; color: #1a1a2e; }
    .page-header__actions { display: flex; gap: 10px; }
    .btn-grafana {
      padding: 7px 14px; background: #f57c00; color: #fff;
      border-radius: 5px; text-decoration: none; font-size: 13px;
    }
    .btn-grafana:hover { background: #e65100; }

    .stat-row {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(140px, 1fr));
      gap: 12px;
      margin-bottom: 20px;
    }
    .stat-card {
      background: #fff; border-radius: 8px; padding: 20px 16px;
      text-align: center; box-shadow: 0 1px 4px rgba(0,0,0,.08);
      border-top: 4px solid #ccc;
    }
    .stat-card__value { font-size: 32px; font-weight: 700; color: #1a1a2e; }
    .stat-card__label { font-size: 12px; color: #777; margin-top: 4px; }
    .stat-card--critical { border-color: #c62828; }
    .stat-card--warning  { border-color: #f57c00; }
    .stat-card--open     { border-color: #1976d2; }
    .stat-card--ack      { border-color: #616161; }
    .stat-card--running  { border-color: #2e7d32; }
    .stat-card--idle     { border-color: #1565c0; }
    .stat-card--maint    { border-color: #f57c00; }
    .stat-card--fault    { border-color: #c62828; }

    h3 { margin: 0 0 14px; font-size: 16px; color: #333; }
    .equipment-grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(220px, 1fr));
      gap: 14px;
    }
    .eq-card {
      background: #fff; border-radius: 8px; padding: 16px;
      box-shadow: 0 1px 4px rgba(0,0,0,.08);
      border-left: 4px solid #ccc;
    }
    .eq-card--running     { border-color: #2e7d32; }
    .eq-card--idle        { border-color: #1565c0; }
    .eq-card--maintenance { border-color: #f57c00; }
    .eq-card--fault       { border-color: #c62828; }
    .eq-card--offline     { border-color: #757575; }
    .eq-card__header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 6px; }
    .eq-card__code { font-weight: 700; font-size: 14px; }
    .eq-card__name { font-size: 13px; color: #333; margin-bottom: 2px; }
    .eq-card__location { font-size: 12px; color: #888; margin-bottom: 12px; }
    .eq-card__actions { display: flex; gap: 8px; }
    .btn-sm {
      padding: 4px 10px; border-radius: 4px; font-size: 12px;
      text-decoration: none; background: #eee; color: #333;
    }
    .btn-sm--grafana { background: #fff3e0; color: #e65100; }
    .btn-sm:hover { opacity: .8; }

    .status-chip {
      font-size: 10px; font-weight: 600; padding: 2px 8px;
      border-radius: 10px; text-transform: uppercase;
    }
    .status-chip--running     { background: #e8f5e9; color: #2e7d32; }
    .status-chip--idle        { background: #e3f2fd; color: #1565c0; }
    .status-chip--maintenance { background: #fff3e0; color: #e65100; }
    .status-chip--fault       { background: #ffebee; color: #c62828; }
    .status-chip--offline     { background: #f5f5f5; color: #616161; }
    .empty { color: #999; font-size: 14px; }
  `],
})
export class DashboardComponent implements OnInit, OnDestroy {
  private equipmentSvc = inject(EquipmentService);
  private alertSvc = inject(AlertService);

  equipment = signal<Equipment[]>([]);
  statusSummary = signal<StatusSummary | null>(null);
  alertSummary = signal<AlertSummary | null>(null);

  grafanaServiceHealth =
    environment.grafanaBase + environment.grafanaDashboards.serviceHealth;
  grafanaEquipment =
    environment.grafanaBase + environment.grafanaDashboards.equipmentMetrics;

  private sub = new Subscription();

  ngOnInit(): void {
    // Refresh every 10 s to match the simulator cadence
    const poll$ = interval(10_000).pipe(startWith(0));

    this.sub.add(
      poll$.pipe(switchMap(() => this.equipmentSvc.getAll())).subscribe(eq => {
        this.equipment.set(eq);
      }),
    );
    this.sub.add(
      poll$.pipe(switchMap(() => this.equipmentSvc.getStatusSummary())).subscribe(s => {
        this.statusSummary.set(s);
      }),
    );
    this.sub.add(
      poll$.pipe(switchMap(() => this.alertSvc.getSummary())).subscribe(s => {
        this.alertSummary.set(s);
      }),
    );
  }

  ngOnDestroy(): void {
    this.sub.unsubscribe();
  }

  grafanaEquipmentLink(code: string): string {
    return this.equipmentSvc.grafanaLink(code);
  }
}
