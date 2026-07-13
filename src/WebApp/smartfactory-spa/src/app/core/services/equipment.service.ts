import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';

export interface Equipment {
  id: string;
  code: string;
  name: string;
  type: string;
  location: string;
  status: 'Running' | 'Idle' | 'Maintenance' | 'Fault' | 'Offline';
  lastMaintenanceDate: string | null;
}

export interface StatusSummary {
  running: number;
  idle: number;
  maintenance: number;
  fault: number;
  offline: number;
}

@Injectable({ providedIn: 'root' })
export class EquipmentService {
  private base = `${environment.apiBase}/api/equipment`;

  constructor(private http: HttpClient) {}

  getAll() {
    return this.http.get<Equipment[]>(this.base);
  }

  getById(id: string) {
    return this.http.get<Equipment>(`${this.base}/${id}`);
  }

  getStatusSummary() {
    return this.http.get<StatusSummary>(`${this.base}/status/summary`);
  }

  updateStatus(id: string, status: string, reason: string) {
    return this.http.patch(`${this.base}/${id}/status`, { status, reason });
  }

  grafanaLink(equipmentCode: string): string {
    const base = environment.grafanaBase + environment.grafanaDashboards.equipmentMetrics;
    return `${base}?var-equipment=${encodeURIComponent(equipmentCode)}&kiosk=tv`;
  }
}
