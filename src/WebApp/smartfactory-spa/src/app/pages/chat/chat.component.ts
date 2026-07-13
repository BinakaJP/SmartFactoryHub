import { Component, inject, signal, ViewChild, ElementRef, AfterViewChecked } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ChatService, ChatMessage } from '../../core/services/chat.service';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-chat',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="chat-page">
      <div class="chat-header">
        <div class="chat-header__info">
          <span class="chat-header__title">AI Factory Assistant</span>
          <span class="chat-header__sub">Powered by Semantic Kernel · Ask anything about your factory</span>
        </div>
        <button class="btn-clear" (click)="clearSession()" title="Start a new conversation">
          ↺ New Chat
        </button>
      </div>

      <div class="chat-messages" #scrollContainer>
        @if (messages().length === 0) {
          <div class="chat-empty">
            <div class="chat-empty__icon">🏭</div>
            <div class="chat-empty__title">Factory Assistant ready</div>
            <div class="chat-empty__sub">Try asking:</div>
            <div class="chat-suggestions">
              @for (s of suggestions; track s) {
                <button class="suggestion" (click)="useSuggestion(s)">{{ s }}</button>
              }
            </div>
          </div>
        }
        @for (msg of messages(); track $index) {
          <div class="msg-row msg-row--{{ msg.role }}">
            <div class="msg-bubble msg-bubble--{{ msg.role }}">
              <div class="msg-content">{{ msg.content }}</div>
              @if (msg.toolsUsed?.length) {
                <div class="msg-tools">
                  🔧 {{ msg.toolsUsed!.join(' · ') }}
                </div>
              }
              <div class="msg-time">{{ msg.timestamp | date:'HH:mm' }}</div>
            </div>
          </div>
        }
        @if (loading()) {
          <div class="msg-row msg-row--assistant">
            <div class="msg-bubble msg-bubble--assistant msg-bubble--typing">
              <span class="dot"></span><span class="dot"></span><span class="dot"></span>
            </div>
          </div>
        }
      </div>

      <div class="chat-input-bar">
        <input
          #inputEl
          [(ngModel)]="inputText"
          (keydown.enter)="send()"
          [disabled]="loading()"
          placeholder="Ask about equipment, alerts, health scores, maintenance…"
          class="chat-input"
        />
        <button class="btn-send" (click)="send()" [disabled]="loading() || !inputText.trim()">
          Send
        </button>
      </div>
    </div>
  `,
  styles: [`
    .chat-page {
      display: flex; flex-direction: column;
      height: calc(100vh - 56px);
      max-width: 860px; margin: 0 auto;
      padding: 0; background: #fff;
    }
    .chat-header {
      display: flex; align-items: center; justify-content: space-between;
      padding: 16px 24px; border-bottom: 1px solid #eee; background: #fff;
    }
    .chat-header__title { font-weight: 700; font-size: 15px; color: #1a1a2e; display: block; }
    .chat-header__sub { font-size: 12px; color: #888; display: block; margin-top: 2px; }
    .btn-clear {
      padding: 6px 14px; background: #f5f5f5; border: 1px solid #ddd;
      border-radius: 5px; cursor: pointer; font-size: 13px;
    }
    .btn-clear:hover { background: #eee; }

    .chat-messages {
      flex: 1; overflow-y: auto; padding: 20px 24px;
      display: flex; flex-direction: column; gap: 12px;
      background: #f9f9fb;
    }
    .chat-empty {
      margin: auto; text-align: center; color: #888;
    }
    .chat-empty__icon { font-size: 48px; margin-bottom: 12px; }
    .chat-empty__title { font-size: 18px; font-weight: 600; color: #333; margin-bottom: 6px; }
    .chat-empty__sub { font-size: 13px; margin-bottom: 14px; }
    .chat-suggestions { display: flex; flex-direction: column; gap: 8px; max-width: 420px; margin: 0 auto; }
    .suggestion {
      padding: 10px 16px; background: #fff; border: 1px solid #e0e0e0;
      border-radius: 8px; cursor: pointer; font-size: 13px; text-align: left; color: #333;
    }
    .suggestion:hover { background: #e8f0fe; border-color: #1976d2; color: #1976d2; }

    .msg-row { display: flex; }
    .msg-row--user      { justify-content: flex-end; }
    .msg-row--assistant { justify-content: flex-start; }

    .msg-bubble {
      max-width: 72%; padding: 12px 16px; border-radius: 12px;
      font-size: 14px; line-height: 1.55; white-space: pre-wrap;
    }
    .msg-bubble--user {
      background: #1a1a2e; color: #fff;
      border-bottom-right-radius: 4px;
    }
    .msg-bubble--assistant {
      background: #fff; color: #222;
      border: 1px solid #e8e8e8;
      border-bottom-left-radius: 4px;
      box-shadow: 0 1px 3px rgba(0,0,0,.06);
    }
    .msg-content { margin-bottom: 4px; }
    .msg-tools {
      font-size: 11px; color: #999; margin-top: 6px;
      border-top: 1px solid #f0f0f0; padding-top: 4px;
    }
    .msg-time { font-size: 10px; color: #bbb; margin-top: 4px; text-align: right; }

    .msg-bubble--typing {
      display: flex; align-items: center; gap: 6px; padding: 14px 18px;
    }
    .dot {
      width: 8px; height: 8px; border-radius: 50%; background: #bbb;
      animation: bounce 1.2s infinite;
    }
    .dot:nth-child(2) { animation-delay: .2s; }
    .dot:nth-child(3) { animation-delay: .4s; }
    @keyframes bounce {
      0%, 60%, 100% { transform: translateY(0); }
      30% { transform: translateY(-6px); }
    }

    .chat-input-bar {
      display: flex; gap: 10px; padding: 16px 24px;
      border-top: 1px solid #eee; background: #fff;
    }
    .chat-input {
      flex: 1; padding: 11px 16px; border: 1px solid #ddd;
      border-radius: 8px; font-size: 14px; outline: none;
    }
    .chat-input:focus { border-color: #1976d2; }
    .btn-send {
      padding: 11px 22px; background: #1a1a2e; color: #fff;
      border: none; border-radius: 8px; font-size: 14px;
      font-weight: 600; cursor: pointer;
    }
    .btn-send:disabled { opacity: .5; cursor: not-allowed; }
    .btn-send:not(:disabled):hover { background: #2d2d5e; }
  `],
})
export class ChatComponent implements AfterViewChecked {
  private chatSvc = inject(ChatService);
  auth = inject(AuthService);

  @ViewChild('scrollContainer') private scrollContainer!: ElementRef<HTMLElement>;

  messages = signal<ChatMessage[]>([]);
  inputText = '';
  loading = signal(false);

  suggestions = [
    'What equipment is currently running?',
    'Are there any critical alerts right now?',
    'Which equipment needs maintenance soonest?',
    'Show me the health scores for all equipment.',
  ];

  ngAfterViewChecked(): void {
    this.scrollToBottom();
  }

  send(): void {
    const text = this.inputText.trim();
    if (!text || this.loading()) return;

    this.messages.update(ms => [
      ...ms,
      { role: 'user', content: text, timestamp: new Date() },
    ]);
    this.inputText = '';
    this.loading.set(true);

    this.chatSvc.send(text).subscribe({
      next: res => {
        this.chatSvc.sessionId = res.sessionId;
        this.messages.update(ms => [
          ...ms,
          {
            role: 'assistant',
            content: res.reply,
            timestamp: new Date(res.timestamp),
            toolsUsed: res.toolsUsed,
          },
        ]);
        this.loading.set(false);
      },
      error: () => {
        this.messages.update(ms => [
          ...ms,
          {
            role: 'assistant',
            content: 'I encountered an error. Please try again.',
            timestamp: new Date(),
          },
        ]);
        this.loading.set(false);
      },
    });
  }

  useSuggestion(text: string): void {
    this.inputText = text;
    this.send();
  }

  clearSession(): void {
    this.chatSvc.clearSession();
    this.messages.set([]);
  }

  private scrollToBottom(): void {
    try {
      const el = this.scrollContainer?.nativeElement;
      if (el) el.scrollTop = el.scrollHeight;
    } catch {}
  }
}
