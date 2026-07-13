import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';

export interface ChatMessage {
  role: 'user' | 'assistant';
  content: string;
  timestamp: Date;
  toolsUsed?: string[];
}

export interface SendMessageResponse {
  sessionId: string;
  reply: string;
  toolsUsed: string[];
  timestamp: string;
}

@Injectable({ providedIn: 'root' })
export class ChatService {
  private base = `${environment.apiBase}/api/chat`;
  sessionId: string | null = null;

  constructor(private http: HttpClient) {}

  send(message: string) {
    return this.http.post<SendMessageResponse>(`${this.base}/message`, {
      message,
      sessionId: this.sessionId,
    });
  }

  clearSession() {
    if (this.sessionId) {
      this.http.delete(`${this.base}/session/${this.sessionId}`).subscribe();
    }
    this.sessionId = null;
  }
}
