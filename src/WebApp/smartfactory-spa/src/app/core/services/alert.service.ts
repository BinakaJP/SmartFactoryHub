import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';

export interface Alert {
  id: string;
  equipmentId: string;
  equipmentName: string;
  metricType: string;
  severity: 'Info' | 'Warning' | 'Critical';
  status: 'Open' | 'Acknowledged' | 'Resolved';
  message: string;
  triggeredAt: string;
  acknowledgedAt: string | null;
  acknowledgedBy: string | null;
}

export interface AlertSummary {
  openCount: number;
  acknowledgedCount: number;
  resolvedCount: number;
  criticalCount: number;
  warningCount: number;
  infoCount: number;
}

@Injectable({ providedIn: 'root' })
export class AlertService {
  private base = `${environment.apiBase}/api/alerts`;

  constructor(private http: HttpClient) {}

  getAll(status?: string, severity?: string) {
    const params: Record<string, string> = {};
    if (status) params['status'] = status;
    if (severity) params['severity'] = severity;
    return this.http.get<Alert[]>(this.base, { params });
  }

  getSummary() {
    return this.http.get<AlertSummary>(`${this.base}/summary`);
  }

  acknowledge(id: string, acknowledgedBy: string) {
    return this.http.patch(`${this.base}/${id}/acknowledge`, { acknowledgedBy });
  }

  resolve(id: string) {
    return this.http.patch(`${this.base}/${id}/resolve`, {});
  }
}
