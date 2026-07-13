import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { switchMap } from 'rxjs/operators';
import { EquipmentService, Equipment } from '../../core/services/equipment.service';
import { AuthService } from '../../core/services/auth.service';

const STATUSES = ['Running', 'Idle', 'Maintenance', 'Fault', 'Offline'];

@Component({
  selector: 'app-equipment-detail',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule],
  template: `
    <div class="page">
      <div class="page-header">
        <h2>Equipment</h2>
        <a routerLink="/dashboard" class="btn-back">← Dashboard</a>
      </div>

      <div class="equipment-list">
        @for (eq of equipment(); track eq.id) {
          <div class="eq-row" [class.selected]="selected()?.id === eq.id" (click)="select(eq)">
            <span class="eq-row__code">{{ eq.code }}</span>
            <span class="eq-row__name">{{ eq.name }}</span>
            <span class="status-chip status-chip--{{ eq.status.toLowerCase() }}">{{ eq.status }}</span>
            <span class="eq-row__loc">{{ eq.location }}</span>
          </div>
        }
      </div>

      @if (selected()) {
        <div class="detail-panel">
          <div class="detail-panel__header">
            <div>
              <h3>{{ selected()!.name }}</h3>
              <span class="eq-code-label">{{ selected()!.code }}</span>
            </div>
            <a [href]="grafanaLink()" target="_blank" class="btn-grafana">
              View Metrics in Grafana ↗
            </a>
          </div>

          <dl class="detail-grid">
            <dt>Type</dt><dd>{{ selected()!.type }}</dd>
            <dt>Location</dt><dd>{{ selected()!.location }}</dd>
            <dt>Status</dt>
            <dd><span class="status-chip status-chip--{{ selected()!.status.toLowerCase() }}">{{ selected()!.status }}</span></dd>
            <dt>Last Maintenance</dt>
            <dd>{{ selected()!.lastMaintenanceDate ? (selected()!.lastMaintenanceDate | date:'mediumDate') : 'N/A' }}</dd>
          </dl>

          @if (auth.canAcknowledge()) {
            <div class="status-update">
              <h4>Update Status</h4>
              <div class="status-update__form">
                <select [(ngModel)]="newStatus">
                  @for (s of statuses; track s) {
                    <option [value]="s">{{ s }}</option>
                  }
                </select>
                <input [(ngModel)]="statusReason" placeholder="Reason (required)" />
                <button (click)="updateStatus()" [disabled]="!statusReason.trim() || updating()">
                  {{ updating() ? 'Updating…' : 'Apply' }}
                </button>
              </div>
              @if (updateMsg()) {
                <p class="update-msg" [class.update-msg--error]="updateError()">{{ updateMsg() }}</p>
              }
            </div>
          }
        </div>
      }
    </div>
  `,
  styles: [`
    .page { padding: 24px; max-width: 1000px; margin: 0 auto; }
    .page-header { display: flex; align-items: center; justify-content: space-between; margin-bottom: 20px; }
    .page-header h2 { margin: 0; font-size: 22px; color: #1a1a2e; }
    .btn-back { text-decoration: none; color: #666; font-size: 13px; }

    .equipment-list {
      background: #fff; border-radius: 8px;
      box-shadow: 0 1px 4px rgba(0,0,0,.08); margin-bottom: 20px; overflow: hidden;
    }
    .eq-row {
      display: flex; align-items: center; gap: 16px;
      padding: 14px 20px; border-bottom: 1px solid #f0f0f0;
      cursor: pointer; transition: background .15s;
    }
    .eq-row:last-child { border-bottom: none; }
    .eq-row:hover, .eq-row.selected { background: #e8f0fe; }
    .eq-row__code { font-weight: 700; font-size: 14px; width: 90px; flex-shrink: 0; }
    .eq-row__name { flex: 1; font-size: 14px; color: #333; }
    .eq-row__loc { font-size: 12px; color: #888; }

    .detail-panel {
      background: #fff; border-radius: 8px;
      box-shadow: 0 1px 4px rgba(0,0,0,.08); padding: 24px;
    }
    .detail-panel__header {
      display: flex; align-items: flex-start; justify-content: space-between; margin-bottom: 20px;
    }
    .detail-panel__header h3 { margin: 0 0 4px; font-size: 18px; color: #1a1a2e; }
    .eq-code-label { font-size: 13px; color: #888; }
    .btn-grafana {
      padding: 8px 14px; background: #f57c00; color: #fff;
      border-radius: 5px; text-decoration: none; font-size: 13px; white-space: nowrap;
    }

    .detail-grid {
      display: grid; grid-template-columns: 140px 1fr;
      gap: 10px 0; margin-bottom: 24px;
    }
    dt { font-size: 13px; font-weight: 600; color: #555; padding: 6px 0; }
    dd { font-size: 14px; color: #333; margin: 0; padding: 6px 0; border-bottom: 1px solid #f5f5f5; }

    .status-update { border-top: 1px solid #eee; padding-top: 20px; }
    .status-update h4 { margin: 0 0 12px; font-size: 14px; color: #333; }
    .status-update__form { display: flex; gap: 10px; align-items: center; }
    select, input {
      padding: 8px 12px; border: 1px solid #ddd; border-radius: 5px; font-size: 14px;
    }
    input { flex: 1; }
    button {
      padding: 8px 16px; background: #1a1a2e; color: #fff;
      border: none; border-radius: 5px; font-size: 14px; cursor: pointer;
    }
    button:disabled { opacity: .5; cursor: not-allowed; }
    .update-msg { font-size: 13px; color: #2e7d32; margin: 10px 0 0; }
    .update-msg--error { color: #c62828; }

    .status-chip {
      font-size: 11px; font-weight: 600; padding: 3px 10px;
      border-radius: 10px; text-transform: uppercase;
    }
    .status-chip--running     { background: #e8f5e9; color: #2e7d32; }
    .status-chip--idle        { background: #e3f2fd; color: #1565c0; }
    .status-chip--maintenance { background: #fff3e0; color: #e65100; }
    .status-chip--fault       { background: #ffebee; color: #c62828; }
    .status-chip--offline     { background: #f5f5f5; color: #616161; }
  `],
})
export class EquipmentDetailComponent implements OnInit {
  private equipmentSvc = inject(EquipmentService);
  private route = inject(ActivatedRoute);
  auth = inject(AuthService);

  equipment = signal<Equipment[]>([]);
  selected = signal<Equipment | null>(null);
  newStatus = 'Running';
  statusReason = '';
  updating = signal(false);
  updateMsg = signal<string | null>(null);
  updateError = signal(false);
  statuses = STATUSES;

  ngOnInit(): void {
    this.equipmentSvc.getAll().subscribe(eq => {
      this.equipment.set(eq);

      // Auto-select if ?id= is in query params
      this.route.queryParamMap.pipe(
        switchMap(params => {
          const id = params.get('id');
          return id
            ? this.equipmentSvc.getById(id)
            : [];
        }),
      ).subscribe(e => {
        if (e) {
          this.selected.set(e as Equipment);
          this.newStatus = (e as Equipment).status;
        }
      });
    });
  }

  select(eq: Equipment): void {
    this.selected.set(eq);
    this.newStatus = eq.status;
    this.updateMsg.set(null);
    this.statusReason = '';
  }

  updateStatus(): void {
    const eq = this.selected();
    if (!eq || !this.statusReason.trim()) return;
    this.updating.set(true);
    this.updateMsg.set(null);

    this.equipmentSvc.updateStatus(eq.id, this.newStatus, this.statusReason).subscribe({
      next: () => {
        this.updateError.set(false);
        this.updateMsg.set(`Status updated to ${this.newStatus}.`);
        this.updating.set(false);
        this.statusReason = '';
        // Refresh list
        this.equipmentSvc.getAll().subscribe(list => this.equipment.set(list));
        this.equipmentSvc.getById(eq.id).subscribe(updated =>
          this.selected.set(updated),
        );
      },
      error: () => {
        this.updateError.set(true);
        this.updateMsg.set('Update failed. Check your permissions or service status.');
        this.updating.set(false);
      },
    });
  }

  grafanaLink(): string {
    const eq = this.selected();
    return eq ? this.equipmentSvc.grafanaLink(eq.code) : '#';
  }
}
