import { Injectable, signal } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { environment } from '../../../environments/environment';
import { AuthService } from './auth.service';

export interface Toast {
  id: number;
  message: string;
  type: 'info' | 'warning' | 'critical';
}

@Injectable({ providedIn: 'root' })
export class NotificationService {
  private hub: signalR.HubConnection | null = null;
  private toastId = 0;

  readonly toasts = signal<Toast[]>([]);

  constructor(private auth: AuthService) {}

  connect(): void {
    if (this.hub) return;

    this.hub = new signalR.HubConnectionBuilder()
      .withUrl(environment.signalRHub, {
        accessTokenFactory: () => this.auth.token() ?? '',
      })
      .withAutomaticReconnect()
      .build();

    this.hub.on('ReceiveAlert', (data: { severity: string; message: string }) => {
      this.push(data.message, this.severityToType(data.severity));
    });

    this.hub.on('ReceiveAnomaly', (data: { severity: string; equipmentId: string; metricType: string }) => {
      this.push(
        `Anomaly detected — ${data.equipmentId} ${data.metricType} (${data.severity})`,
        this.severityToType(data.severity),
      );
    });

    this.hub.on('ReceiveMaintenance', (data: { equipmentCode: string; estimatedDaysToMaintenance: number }) => {
      this.push(
        `Maintenance alert — ${data.equipmentCode}: ~${data.estimatedDaysToMaintenance} days remaining`,
        'warning',
      );
    });

    this.hub.on('ReceiveStatusChange', (data: { equipmentName: string; newStatus: string }) => {
      this.push(`${data.equipmentName} → ${data.newStatus}`, 'info');
    });

    this.hub.start().catch(err => console.warn('SignalR connect error:', err));
  }

  disconnect(): void {
    this.hub?.stop();
    this.hub = null;
  }

  dismiss(id: number): void {
    this.toasts.update(ts => ts.filter(t => t.id !== id));
  }

  private push(message: string, type: Toast['type']): void {
    const id = ++this.toastId;
    this.toasts.update(ts => [...ts, { id, message, type }]);
    setTimeout(() => this.dismiss(id), 6000);
  }

  private severityToType(severity: string): Toast['type'] {
    if (severity === 'Critical') return 'critical';
    if (severity === 'Warning' || severity === 'Anomalous') return 'warning';
    return 'info';
  }
}
