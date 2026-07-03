import { HttpInterceptorFn, HttpErrorResponse, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import {
  catchError,
  finalize,
  shareReplay,
  switchMap,
  take,
  throwError,
} from 'rxjs';
import { AuthService, SKIP_AUTH_REFRESH } from '../services/auth.service';

let refreshInFlight: ReturnType<AuthService['refreshAccessToken']> | null = null;

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (request.context.get(SKIP_AUTH_REFRESH)) {
    return next(attachAccessToken(request, authService.getToken()));
  }

  const authorizedRequest = attachAccessToken(request, authService.getToken());

  return next(authorizedRequest).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse) || error.status !== 401) {
        return throwError(() => error);
      }

      if (isAuthEndpoint(request.url) || !authService.getToken()) {
        authService.clearSession();
        void router.navigate(['/login']);
        return throwError(() => error);
      }

      if (!refreshInFlight) {
        refreshInFlight = authService.refreshAccessToken().pipe(
          finalize(() => {
            refreshInFlight = null;
          }),
          shareReplay(1),
        );
      }

      return refreshInFlight.pipe(
        take(1),
        switchMap((response) =>
          next(attachAccessToken(request, response.token)),
        ),
        catchError((refreshError) => {
          authService.clearSession();
          void router.navigate(['/login']);
          return throwError(() => refreshError);
        }),
      );
    }),
  );
};

function attachAccessToken(
  request: HttpRequest<unknown>,
  token: string | null,
): HttpRequest<unknown> {
  if (!token) {
    return request;
  }

  return request.clone({
    setHeaders: {
      Authorization: `Bearer ${token}`,
    },
  });
}

function isAuthEndpoint(url: string): boolean {
  return (
    url.includes('/auth/login') ||
    url.includes('/auth/register') ||
    url.includes('/auth/refresh') ||
    url.includes('/auth/logout')
  );
}
