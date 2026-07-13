import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { tap } from 'rxjs/operators';
import { environment } from '../../../environments/environment';

export interface TokenResponse {
  token: string;
  email: string;
  role: string;
  expiresAt: string;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private _token = signal<string | null>(null);
  private _role = signal<string | null>(null);
  private _email = signal<string | null>(null);

  readonly token = this._token.asReadonly();
  readonly role = this._role.asReadonly();
  readonly email = this._email.asReadonly();

  get isLoggedIn(): boolean {
    return !!this._token();
  }

  constructor(private http: HttpClient, private router: Router) {}

  login(email: string, password: string) {
    return this.http
      .post<TokenResponse>(`${environment.apiBase}/api/auth/login`, { email, password })
      .pipe(
        tap(res => {
          this._token.set(res.token);
          this._role.set(res.role);
          this._email.set(res.email);
        }),
      );
  }

  logout(): void {
    this._token.set(null);
    this._role.set(null);
    this._email.set(null);
    this.router.navigate(['/login']);
  }

  canAcknowledge(): boolean {
    const r = this._role();
    return r === 'Admin' || r === 'Engineer';
  }
}
