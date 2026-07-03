import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpContext, HttpContextToken } from '@angular/common/http';
import { BehaviorSubject, Observable, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { API_PATHS } from '../../shared/helpers/api-paths.constants';
import { AUTH_TOKEN_STORAGE_KEY } from '../../shared/helpers/auth.constants';
import {
  LoginRequest,
  LoginResponse,
  RegisterRequest,
  RegisterResponse,
} from '../models';

export const SKIP_AUTH_REFRESH = new HttpContextToken<boolean>(() => false);

export interface RefreshResponse {
  token: string;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly authStateSubject = new BehaviorSubject<boolean>(this.hasStoredToken());

  readonly isAuthenticated$ = this.authStateSubject.asObservable();

  register(request: RegisterRequest): Observable<RegisterResponse> {
    const url = `${environment.apiUrl}${API_PATHS.auth.register}`;
    return this.http.post<RegisterResponse>(url, request, { withCredentials: true });
  }

  login(request: LoginRequest): Observable<LoginResponse> {
    const url = `${environment.apiUrl}${API_PATHS.auth.login}`;
    return this.http
      .post<LoginResponse>(url, request, { withCredentials: true })
      .pipe(tap((response) => this.setSession(response.token)));
  }

  refreshAccessToken(): Observable<RefreshResponse> {
    const url = `${environment.apiUrl}${API_PATHS.auth.refresh}`;
    return this.http
      .post<RefreshResponse>(
        url,
        {},
        {
          withCredentials: true,
          context: new HttpContext().set(SKIP_AUTH_REFRESH, true),
        },
      )
      .pipe(tap((response) => this.setSession(response.token)));
  }

  logout(): Observable<void> {
    const url = `${environment.apiUrl}${API_PATHS.auth.logout}`;
    return this.http
      .post<void>(url, null, {
        withCredentials: true,
        context: new HttpContext().set(SKIP_AUTH_REFRESH, true),
      })
      .pipe(tap(() => this.clearSession()));
  }

  clearSession(): void {
    localStorage.removeItem(AUTH_TOKEN_STORAGE_KEY);
    this.authStateSubject.next(false);
  }

  getToken(): string | null {
    return localStorage.getItem(AUTH_TOKEN_STORAGE_KEY);
  }

  isAuthenticated(): boolean {
    return this.hasStoredToken();
  }

  private setSession(token: string): void {
    localStorage.setItem(AUTH_TOKEN_STORAGE_KEY, token);
    this.authStateSubject.next(true);
  }

  private hasStoredToken(): boolean {
    const token = localStorage.getItem(AUTH_TOKEN_STORAGE_KEY);
    return token !== null && token.length > 0;
  }
}
